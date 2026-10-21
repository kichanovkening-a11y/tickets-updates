# Original ticket report adaptation

`Template/` preserves the eleven original Crystal Reports files byte-for-byte
from `Прога билеты/прога1.zip / прога1/Template`. `manifest.json` records their
SHA-256 and the extracted layout resource's SHA-256. These are the actual old
reports, not generic ticket layouts given old filenames.

All eleven layouts were extracted on disposable Windows CI using the official
SAP Crystal Reports SP33 x64 runtime, after a valid SAP Authenticode publisher
signature was checked. The extracted JSON is embedded under
`src/Tickets.Core/Resources/LegacyTickets/`; the portable application does not
load Crystal, install a runtime, or require the old program.

The conversion retains source sections, margins, coordinates (twips converted
using `value * 25.4 / 1440`), fixed text boxes, font family/size/style/alignment,
colours, suppression and 0/90/270-degree rotations. Detail sections repeat for
each ticket; report/page headers and footers retain their own placement rules.
The three narrow tear-off fields and Gift's vertical layout are preserved.
`podarok2` retains “Для детей от 3 до 8 лет”, “(не для продажи)” and “подарок”.
`250416` retains its invitation, three print-date fields and page number rather
than being replaced by a normal ticket.

Field values follow the recovered original
`Tickets.PrintForm.PrintForm_Load` XML construction: uppercase main title,
original mixed-case tear-off title, `серия № 000001`, `Ряд: …   Место: …`,
`Цена: …р.`, Russian long event date and all three exact tear-off combinations.
The venue address comes from the event's venue record. Reprints retain the
original ticket series, numbers, amounts and issuance information.

Original printer settings are recorded in JSON. `030879` and `Стандарт1 - копия`
save disassociated logical content areas of 700×1800 and 1800×1700 mm, while
explicitly selecting Letter and A4 landscape physical paper. Their portable
previews use that saved physical paper; the logical source areas remain in
metadata. At print time the chosen printer paper determines pagination for all
old layouts. Source field coordinates and font sizes are never scaled or shifted.
A field outside the printer's physical imageable area is rejected with a clear
message. The source layouts can be viewed and printed in the existing editor;
editing these layouts is deferred until the new editor is implemented.

Originals are seeded once per database. Deletion leaves a seed marker, so a
removed original does not reappear on reopening the template list.

## Verification and limits

Tests verify all original RPT hashes, eleven embedded layouts, key source
coordinates, the 113-twip left margin, Times New Roman font sizes/styles,
90/270-degree rotations, Gift/child labels, original field-value mappings,
detail-band pagination on source and chosen paper, and persistent deletion.
Native WPF text rendering can differ from Crystal in glyph metrics and line
breaking. Preview uses the source's measured paper or its explicit saved
standard paper; the printer dialog's chosen physical sheet can change the number
of pages, without scaling the fields. Actual printing, hardware margins and
paper alignment have not been physically verified; a printer comparison is still
needed before declaring pixel-identical output. The special report's print-date formatting uses the
Russian short date. No raster image or source logo was found among these eleven
reports' extracted objects (only field and text objects).

## Re-extraction

`Extract original ticket layouts` GitHub Actions workflow runs on `windows-2022`,
uses read-only repository permissions, and uploads `legacy-ticket-layouts` JSON
and installation/provenance logs. It also emits gzip/base64 JSON recovery lines
in the job log. Source hashes are checked before extraction, the reports open
as temporary copies, and no database refresh or source modification occurs.

The official installer route used by the workflow is:
`https://origin-az.softwaredownloads.sap.com/public/file/0020000001649962022`
(SAP's `CR13SP33MSI64_0-80007712.MSI`). This dependency exists only on the
build runner. The runtime is absent from the application and release package.

Primary references:

- [SAP Crystal runtime portal](https://pages.community.sap.com/topics/crystal-reports/visual-studio)
- [Microsoft Crystal ReportObject API](https://learn.microsoft.com/en-us/previous-versions/ms226132(v=vs.90))
- [SAP ReportDocument object model](https://help.sap.com/docs/SAP_CRYSTAL_REPORTS%2C_DEVELOPER_VERSION_FOR_MICROSOFT_VISUAL_STUDIO/0d6684e153174710b8b2eb114bb7f843/45aca98d6e041014910aba7db0e91070.html)
