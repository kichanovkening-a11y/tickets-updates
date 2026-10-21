# Run once on a Windows BUILD machine with installed SAP Crystal Reports runtime.
# PowerShell 5.1 / .NET Framework is required; this is not an application dependency.
[CmdletBinding()]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '../legacy-ticket-templates/Template'),
    [Parameter(Mandatory=$true)][string]$EngineAssembly,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Use Windows PowerShell 5.1, not pwsh/.NET Core.' }
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$enginePath = (Resolve-Path -LiteralPath $EngineAssembly).Path
$engine = [Reflection.Assembly]::LoadFrom($enginePath)
$reportType = $engine.GetType('CrystalDecisions.CrystalReports.Engine.ReportDocument', $true)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$destination = (Resolve-Path -LiteralPath $OutputDirectory).Path
# Captures scalar API properties without inventing values; getter failures are recorded.
function Read-Scalars($value) {
    $result = [ordered]@{}
    if ($null -eq $value) { return $result }
    foreach ($property in $value.GetType().GetProperties() | Sort-Object Name) {
        if (-not $property.CanRead -or $property.GetIndexParameters().Length -ne 0) { continue }
        $type = $property.PropertyType
        if (-not ($type.IsPrimitive -or $type.IsEnum -or $type -eq [string] -or $type -eq [decimal] -or $type.FullName -eq 'System.Drawing.Color')) { continue }
        try {
            $v = $property.GetValue($value, $null)
            if ($type.IsEnum) { $v = [string]$v }
            if ($type.FullName -eq 'System.Drawing.Color') { $v = @{ argb = $v.ToArgb(); name = $v.Name } }
            $result[$property.Name] = $v
        } catch { $result[$property.Name] = @{ getterError = $_.Exception.Message } }
    }
    return $result
}
# Runtime releases expose different optional metadata collections. Geometry is
# mandatory above; unavailable auxiliary metadata must not discard that layout.
function Get-OptionalValue($value, [string]$name) {
    if ($null -eq $value) { return $null }
    $property = $value.GetType().GetProperty($name)
    if ($null -eq $property) {
        $script:metadataWarnings += "Unavailable: $($value.GetType().FullName).$name"
        return $null
    }
    try { return $property.GetValue($value, $null) }
    catch {
        $script:metadataWarnings += "Getter failed: $($value.GetType().FullName).$name - $($_.Exception.Message)"
        return $null
    }
}
function Property-Scalars($value, [string]$name) {
    $nested = Get-OptionalValue $value $name
    if ($null -eq $nested) { return $null }
    return Read-Scalars $nested
}
$files = @(Get-ChildItem -LiteralPath $source -Filter '*.rpt' -File | Sort-Object Name)
if ($files.Count -ne 11) { throw "Expected 11 original RPTs, found $($files.Count)." }
$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $source '../manifest.json') | ConvertFrom-Json
$results = @()
foreach ($file in $files) {
    $expected = @($manifest.templates | Where-Object file -eq $file.Name)
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expected.Count -ne 1 -or $expected[0].sha256 -ne $hash) { throw "Original hash mismatch: $($file.Name)" }
    $report = [Activator]::CreateInstance($reportType)
    try {
        # OpenReportByTempCopy avoids modifying the source. Never call Refresh/VerifyDatabase.
        $methodType = $engine.GetReferencedAssemblies() | Where-Object Name -eq 'CrystalDecisions.Shared' | Select-Object -First 1
        $shared = [Reflection.Assembly]::Load($methodType)
        $openType = $shared.GetType('CrystalDecisions.Shared.OpenReportMethod', $true)
        $mode = [Enum]::Parse($openType, 'OpenReportByTempCopy')
        $report.Load($file.FullName, $mode)
        $script:metadataWarnings = @()
        $sections = @()
        foreach ($section in $report.ReportDefinition.Sections) {
            $objects = @()
            foreach ($object in $section.ReportObjects) {
                $objects += [ordered]@{
                    type = $object.GetType().FullName
                    properties = Read-Scalars $object
                    format = Property-Scalars $object 'ObjectFormat'
                    font = Property-Scalars $object 'Font'
                    dataSource = Property-Scalars $object 'DataSource'
                    border = Property-Scalars $object 'Border'
                    fieldFormat = Property-Scalars $object 'FieldFormat'
                }
            }
            $sections += [ordered]@{ properties = Read-Scalars $section; format = Property-Scalars $section 'SectionFormat'; objects = $objects }
        }
        $formulas = @()
        foreach ($formula in (Get-OptionalValue $report.DataDefinition 'FormulaFields')) { $formulas += Read-Scalars $formula }
        $databaseFields = @()
        $database = Get-OptionalValue $report 'Database'
        foreach ($table in (Get-OptionalValue $database 'Tables')) {
            foreach ($field in (Get-OptionalValue $table 'Fields')) {
                $databaseFields += [ordered]@{ table = Read-Scalars $table; field = Read-Scalars $field }
            }
        }
        $parameters = @()
        foreach ($parameter in (Get-OptionalValue $report.DataDefinition 'ParameterFields')) { $parameters += Read-Scalars $parameter }
        $record = [ordered]@{
            schemaVersion = 1; sourceFile = $file.Name; sourceSha256 = $hash
            engine = $engine.FullName; engineFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($enginePath).FileVersion
            status = 'extracted-unadapted'; coordinateUnits = 'twips (1/1440 inch)'
            printOptions = Read-Scalars $report.PrintOptions
            pageMargins = Property-Scalars $report.PrintOptions 'PageMargins'
            sections = $sections; formulas = $formulas; parameters = $parameters; databaseFields = $databaseFields
            metadataWarnings = $script:metadataWarnings
        }
        $output = Join-Path $destination ($file.BaseName + '.layout.json')
        $record | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $output -Encoding UTF8
        $results += @{ file = $file.Name; sha256 = $hash; sections = $sections.Count; objects = @($sections | ForEach-Object { $_.objects }).Count }
    } finally { $report.Close(); $report.Dispose() }
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'extraction-summary.json') -Encoding UTF8
Write-Output "Extracted $($results.Count) original reports. Layouts still require field/formula mapping and visual comparison before use."
