# Approved corrections 0.22 implementation plan

Goal: apply only corrections explicitly collected after 0.21.0 and authorized by the user.
Architecture: retain WPF/SQLite/DOCX pipelines; fix their existing behavior without redesigning approved screens.
Constraints: AGENTS.md; preserve all unrequested UI geometry, actions and data. Ticketland excluded. Legacy hall workflow is an opt-in grid mode preserving the current editor; user paused collection then authorized all, without cancelling this request. No database switching or cleanup feature. Do not use forbidden old Russian Song scan. Changes to runtime data only via confirmed user actions; never delete actual user data during development.

## Task 1 — Documents
- Restore invoice seat-column row boundaries/cells (and other explicitly missing invoice borders). Keep grouped seats.
- Bills: exactly one service row for total count and sum, no per-price/place splitting, across built-in and recipient forms.
- All documents: automatic event venue AND address, remove fixed Estrada literals when filling forms, including already saved prior standard forms.
- Act: user supplied exact five-clause service acceptance act, Appendix №2, top-right contract number/date, title АКТ / СДАЧИ-ПРИЕМКИ УСЛУГ; Moscow and event date; buyer full organization/representative/authority and sender/representative; event/venue/address, scheduled and actual event date/time; defects not found; amount words; fulfilled quality/timing; signatures in two aligned columns. Contract number/date manually editable; act date always event date. No hardcoded Sber identities.
- TTN supplied failing file upload/ТТН-35-документ-6376cf308c674c27b43d1577bd019edd.docx has grid95, schema validator0 errors. Word limit63: preserve appearance/content while making table Word-compatible. Do not claim Word-open success without actual Word.
- Add meaningful tests for saved old forms and multiple organizations/events, bill totals, all rows border, TTN grids<=63 and unchanged visible content. Compile/run relevant tests.

## Task 2 — Catalog deletion
- DeletePerformance must fully remove selected performance, its events, tickets/issuances/returns/documents and event-specific state transactionally, preserve shared halls/plans/recipients/senders and unrelated performance.
- Confirmation explicitly lists related counts. Keep global invoice sequence monotonic. Must remain deleted after reopening and seed/bootstrap processing. Backups before destructive operation.
- Update obsolete tests requiring archived preservation for this specific action only. Test issue+return+doc+multiple shows and untouched unrelated event.

## Task 3 — Hall and requested UI
- Reproduce/fix app exit while creating first-row places. Guard unsafe refresh paths; do not claim an unobserved crash is reproduced.
- Study old recovered editor code and add its selectable rectangular draft grid, typed numeric/letter seats and row markers, arbitrary passages, explicit compile/accept; preserve existing editor. Never port old destructive reset.
- Screenshot 200531: Estrada parter has spurious blue isolated edge squares (row numbers rendered as seats likely); inspect actual layout source. First row label then actual seats, correct rows/numbering. Don't arbitrarily remove genuine seats or use old Russian Song scheme.
- Journal header only №. Restore visible row/cell separators (scope journal only), preserve widths per event and approved arrangement.
- Pricing totals single line larger/bold, numeric values red only; recipients field/dropdown names larger/bold, field/row bounds unchanged.
- Tickets app icon red; retain existing motif if any, otherwise simple red ticket icon. No other branding.
- UI and regression tests verifying behavior and bounded layout.

## Task 4 — Verify and publish
- Independent code review with scope and regression checks; fix findings. Full Core tests, WPF compilation and Windows UI checks. Native Word if available, otherwise clearly state limitation.
- Version 0.22.0, release through existing GitHub workflow, keep latest+previous policy. Do not replace user's working database. Report completed corrections and remaining verified limitations only.
