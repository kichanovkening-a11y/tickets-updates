# Original ticket report conversion

`Template/` contains the eleven original, byte-for-byte Crystal Reports files from
`Прога билеты/прога1.zip / прога1/Template`. `manifest.json` records every SHA-256.
These are preserved source documents, **not yet printable templates in the new app**.
No replacement geometry has been guessed from filenames.

## Read-only investigation

All eleven files are OLE compound documents. Their `Contents` streams have the
binary prefix `FC00FFFF070000000018FFFEFEFFFFFE` and contain encoded report data;
plain string extraction does not provide positions, fonts or field bindings.
The archive's engine assembly is `10.5.3700.0`, file version `10.5.0.1943`
(Crystal Reports Basic for Visual Studio 2008). It references `crpe32.dll` and
`CrystalDecisions.ReportAppServer.*` dependencies absent from the archive.
The managed DLLs in the archive are therefore insufficient for extraction.

## One-time extraction on a Windows build machine

The new application must remain independent of Crystal Reports. Install an
appropriate official SAP Crystal runtime **only on a disposable Windows build
machine**, then run Windows PowerShell 5.1 with the matching runtime bitness:

```powershell
powershell.exe -NoProfile -File scripts/extract_legacy_ticket_layouts.ps1 `
  -EngineAssembly 'C:\path\to\installed\CrystalDecisions.CrystalReports.Engine.dll' `
  -OutputDirectory 'C:\temp\legacy-ticket-layouts'
```

Use an installed runtime's engine, not the incomplete old archive's DLLs.
The script opens temporary report copies, never refreshes data and never saves
or modifies the originals. It checks all eleven original hashes, and exports
report sections, object coordinates/types, fonts, data source bindings, formulas,
parameters, object/section formatting and printer options as reviewable JSON.
A load failure is an extraction failure; no fallback layout is generated.
The script has not yet been executed against an installed Crystal runtime.
The isolated `Extract original ticket layouts` GitHub Actions workflow runs this
on `windows-2022`, downloads the SAP SP33 x64 installer, logs its SHA-256, requires
a valid SAP Authenticode publisher signature and installs only on the disposable
runner. It uploads `legacy-ticket-layouts` JSON and diagnostic logs, including on
failure. It has read-only repository permissions and does not publish or modify
the portable application. A successful extraction still requires adaptation.

Crystal API coordinates are twips: convert to millimetres using `value * 25.4 / 1440`.
Section-relative `Top` also needs the section's placement, repeat/suppression
rules, page margins and any column settings; do not flatten these without
inspection. Rotated text, repeated fields, static labels, formulas, lines and
images must be preserved or explicitly reported unsupported before adapting.
Extracted JSON is marked `extracted-unadapted`; it is not automatically trusted
as a printable new application blank.

## Remaining acceptance checks

1. Obtain an official runtime installer and verify its SAP publisher signature.
   Record exact installer version and SHA-256 in the isolated extraction job.
2. Run extraction for all eleven reports; retain JSON and log artifacts.
3. Map actual field/formula semantics to the new application's ticket data.
   Extend the portable blank model where source evidence requires it.
4. Compare rendered samples to the old program, including paper, margins,
   duplicate/torn-off portions, orientation, alignment and fonts.
5. Only then embed and register verified converted templates in the application.

No Crystal runtime installer is included or downloaded by the portable app.
SAP-hosted installer routes discovered during investigation (binary download,
signature and execution still unverified in this environment):

- SP33 x64: `https://origin-az.softwaredownloads.sap.com/public/file/0020000001649962022`
  redirects to SAP's `akall.softwaredownloads.sap.com` with filename
  `CR13SP33MSI64_0-80007712.MSI`.
- SP31 x64: `https://origin.softwaredownloads.sap.com/public/file/0020000001250222021`
  redirects to the same SAP download host with filename
  `CR13SP31MSI64_0-10010309.MSI`.

Try these only on the disposable build machine, validate Authenticode's SAP
publisher and record the downloaded SHA-256 before installation. Availability
and compatibility with the eleven old reports still need to be checked.
Retired BusinessObjects ClickOnce URLs must not be used as though available.

Primary references:

- [SAP Crystal runtime download and licensing portal](https://pages.community.sap.com/topics/crystal-reports/visual-studio)
- [SAP public runtime download portal](https://origin.softwaredownloads.sap.com/public/site/index.html)
- [SAP explanation of retired download URLs](https://community.sap.com/t5/technology-q-a/crruntime-64bit-13-0-26-msi-file-not-found-trying-to-download-using-url/qaq-p/12077361)
- [Microsoft Crystal ReportObject API](https://learn.microsoft.com/en-us/previous-versions/ms226132(v=vs.90))
- [SAP ReportDocument object model](https://help.sap.com/docs/SAP_CRYSTAL_REPORTS%2C_DEVELOPER_VERSION_FOR_MICROSOFT_VISUAL_STUDIO/0d6684e153174710b8b2eb114bb7f843/45aca98d6e041014910aba7db0e91070.html)
