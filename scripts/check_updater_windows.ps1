param([Parameter(Mandatory=$true)][string]$Payload,[Parameter(Mandatory=$true)][string]$Seed,[string]$Legacy,[switch]$Interrupt,[switch]$RejectStartup)
$ErrorActionPreference='Stop'
$root=Join-Path $env:TEMP ('Tickets046-integration-'+[guid]::NewGuid().ToString('N'))
$target=Join-Path $root 'app';$stage=Join-Path $root 'payload'
New-Item -ItemType Directory -Force $target,$stage | Out-Null
# PowerShell expands an 8.3 TEMP alias in FileInfo.FullName. Use the same
# canonical directory spelling before taking relative file names.
$stage=(Get-Item -LiteralPath $stage).FullName
$target=(Get-Item -LiteralPath $target).FullName
if($Legacy){Copy-Item "$Legacy\*" $target -Recurse -Force}else{Set-Content "$target\Tickets.dll" "old binary fixture"}
New-Item -ItemType Directory -Force "$target\Data" | Out-Null
Copy-Item $Seed "$target\Data\tickets.db"
Copy-Item $Seed "$target\Data\seed.db"
Set-Content "$target\Data\preserve-me.txt" 'user settings'
Copy-Item "$Payload\*" $stage -Recurse -Force
$files=@(Get-ChildItem $stage -File -Recurse | ForEach-Object {
 $relative=$_.FullName.Substring($stage.Length+1).Replace('\','/')
 if($relative -ne '! Tickets.exe' -and ($relative -match '^[^/]+\.(dll|exe|deps\.json|runtimeconfig\.json)$' -or $relative -match '^[a-z]{2,3}(-[A-Za-z]{2,8})?/[^/]+\.resources\.dll$')){
  @{Path=$relative;Sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
 }
})
foreach($required in @('Tickets.exe','Tickets.dll','Tickets.runtimeconfig.json','Tickets.deps.json','hostfxr.dll','hostpolicy.dll')){
 if($required -notin $files.Path){throw "Incomplete test payload: missing $required ($($files.Count) files)"}
}
Write-Host "Verified complete integration payload: $($files.Count) files"
$plan=Join-Path $root 'plan.json'
@{Target=$target;Stage=$stage;Files=$files}|ConvertTo-Json -Depth 8|Set-Content $plan -Encoding UTF8
$oldBinaryHash=(Get-FileHash "$target\Tickets.dll").Hash
$oldDataHash=(Get-FileHash "$target\Data\tickets.db").Hash
$updater=Start-Process "$stage\Tickets.Updater.exe" -ArgumentList @('--temporary','--target',"`"$target`"",'--plan',"`"$plan`"") -PassThru
try {
 if($RejectStartup){
  $deadline=(Get-Date).AddMinutes(2);$blocked=$false
  do {
   $j=Get-ChildItem "$target\.updates\transaction-*\journal.json" -ErrorAction SilentlyContinue | Select-Object -First 1
   if($j){New-Item -ItemType Directory -Path (Join-Path $j.DirectoryName 'health.json') | Out-Null;$blocked=$true;break}
   Start-Sleep -Milliseconds 10
  }while((Get-Date) -lt $deadline)
  if(-not $blocked){throw 'Did not reach startup failure boundary'}
 }
 if($Interrupt){
  $deadline=(Get-Date).AddMinutes(2);$cut=$false
  do {
   $j=Get-ChildItem "$target\.updates\transaction-*\journal.json" -ErrorAction SilentlyContinue | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1
   if($j){
    try{$phase=(Get-Content $j.FullName -Raw | ConvertFrom-Json).Phase}catch{$phase=''}
    if($phase -in 'installing','installed'){$updater.Kill();$updater.WaitForExit();$cut=$true;Write-Host "Interrupted actual updater during $phase";break}
   }
   Start-Sleep -Milliseconds 10
  }while((Get-Date) -lt $deadline)
  if(-not $cut){throw 'Did not reach interruption boundary'}
  Start-Sleep -Seconds 1
  Get-Process | Where-Object {$_.Path -eq "$target\! Tickets.exe"} | ForEach-Object {$null=$_.CloseMainWindow();if(-not $_.WaitForExit(5000)){$_.Kill();$_.WaitForExit()}}
  $updater=Start-Process "$stage\Tickets.Updater.exe" -ArgumentList @('--temporary','--target',"`"$target`"",'--plan',"`"$plan`"") -PassThru
 }
 $deadline=(Get-Date).AddMinutes(4);$confirmed=$false
 do {
  Start-Sleep -Milliseconds 500
  $journals=@(Get-ChildItem "$target\.updates\transaction-*\journal.json" -ErrorAction SilentlyContinue | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1)
  foreach($j in $journals){if((Get-Content $j.FullName -Raw|ConvertFrom-Json).Phase -eq $(if($RejectStartup){'restored'}else{'confirmed'})){$confirmed=$true}}
  if($confirmed){break}
  $updater.Refresh();if($updater.HasExited){throw 'Updater exited before confirmation'}
 } while((Get-Date) -lt $deadline)
 if(-not $confirmed){throw "No healthy-start confirmation: $root"}
 if(Test-Path "$target\.updates\pending.json"){throw 'Pending transaction remained'}
 if((Get-Content "$target\Data\preserve-me.txt").Trim() -ne 'user settings'){throw 'User settings changed'}
 if($RejectStartup){
  if((Get-FileHash "$target\Tickets.dll").Hash -ne $oldBinaryHash){throw 'Failed startup did not restore original binary'}
  if((Get-FileHash "$target\Data\tickets.db").Hash -ne $oldDataHash){throw 'Failed startup did not restore original database bytes'}
  Write-Host "PASS failed startup restores exact old binary and database: $root"
  return
 }
 if((Get-FileHash "$target\! Tickets.exe").Hash -ne (Get-FileHash "$target\Tickets.exe").Hash){throw 'Launchers differ'}
 if(-not (Get-Process | Where-Object {$_.Path -eq "$target\! Tickets.exe"})){throw 'New app did not stay running'}
 Write-Host "PASS independent updater installs, validates database, preserves settings, confirms healthy leading launcher: $root"
} finally {
 Get-Process | Where-Object {$_.Path -eq "$target\! Tickets.exe" -or $_.Id -eq $updater.Id} | ForEach-Object {
  $null=$_.CloseMainWindow();if(-not $_.WaitForExit(5000)){$_.Kill();$_.WaitForExit()}
 }
}
