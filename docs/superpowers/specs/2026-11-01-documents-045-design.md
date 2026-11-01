# Documents 0.45

Approved scope: user's 1 November instruction to apply all agreed document changes, preserving Ticket PRO reports. Earlier interface work was released in 0.44 and is not part of this change.

## Required behavior

- Enter creates paragraphs; Shift+Enter creates line breaks. Blank lines and multiline paste survive saving, reopening and undo. Automatic fields remain protected and table structure/formatting survives ordinary editing.
- Filled documents use Microsoft Word's fixed pages for viewing and printing when Word is available. Templates retain automatic fields separately. A clearly identified portable fallback remains usable without Word.
- Contract signatures/requisites stay with preceding substantive text. Do not shrink typography or force a multipage contract onto two pages.
- Edited recipient forms remain scoped to that recipient. Existing issue/package snapshots, including archived dates, remain independent and accessible.
- TTN for all recipients follows the supplied approved XLS: `№ 28 ТТН Павлюков(1).xls`, SHA256 `327ea193b894edcdc7e653dcb8f2bf62cd3c57ceab9e1e046c74be387067fe82`. Preserve its two landscape A4 sides, margins (L16/R10/T9/B7 mm), cells, merged ranges, labels, fonts and sizes. Do not append the program's event heading or generic signatures/total outside the source form. Event details belong in the source goods-description field.
- Print preview removes Cancel and Refresh; Print occupies Refresh's place in the settings row. Changes refresh automatically. Close remains. Preserve the separate ticket-printing controls and behavior.
- Exercise single issues, recipient forms, saved combined packages and archived documents.

## Implementation boundaries

Work in an isolated checkout and isolated Windows test data. Never close, replace or restart the user's running application. Do not alter Ticket PRO integration, report import, authentication, parsing, matching, target selection or queue implementation.

Use OOXML-aware editor changes rather than flattening paragraphs into strings. Office conversion uses an owned invisible Office instance and temporary filled copies; preview and printing consume the same fixed pages. Keep original templates and recipient/issue snapshots intact. Verify the approved TTN against native Excel rendering before claiming fidelity; choose the representation based on that comparison.

## Verification

Meaningful regression cases cover editor paragraph/line-break round trips, protected fields and undo, recipient isolation and issue snapshots, landscape/page-margin propagation, fixed-page reuse, contract tail pagination and TTN layout/data. Native Office checks are isolated and export files only. Run all existing suites and Ticket PRO regressions, inspect the complete branch, then publish through the normal update channel under standing authorization.
