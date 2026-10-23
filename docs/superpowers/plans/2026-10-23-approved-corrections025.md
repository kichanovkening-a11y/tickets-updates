# Approved corrections 0.25

Goal: implement exactly the corrections collected after release 0.24, authorized by «делай все правки. будь внимательным, не делай отсебятину».
Spec: user messages and screenshots in this session. Requirements below are binding; no extra features or cosmetic changes.

## Global Constraints
- Preserve existing app, working database, document content and update mechanism. No Ticketland integration in this batch.
- Do NOT change default sender behavior (user cancelled this request). Do require confirmation before changing an already chosen sender from any menu; reject restores previous value.
- No duplicate buttons, no unrequested colors or functionality. Existing source documents and issue identities must remain safe.
- Work in corrections-025 in existing isolated task checkout. No requests for further approval for these already specified changes. No subagents from implementer.

## Review Focus
- Hall labels follow logical rows through rotations, including 90/180 degrees, and do not drift to unrelated rows.
- Convert selected whole row to aisle: remove its seats, leave geometry of others fixed, reduce actual row count, renumber remaining numeric rows sequentially. Never regenerate missing seats to match old count. Keep ordinary selected-seat aisle behavior for partial rows; show center row captions when aisle created.
- Assign uppercase letter in ANY position for row/place using ONE context menu command; other numeric labels sequential, letters not counted. Hidden-seat original numbering behavior remains intact.
- Confirm sender changes at all UI entry points without modifying default sender, silently modifying other issued documents, or confirming merely opening a menu.
- Printer warning suppression per blank persists, does not leak to other blanks; no issuance before explicit print/confirmation. Seat print order by sector/logical row/place, natural numeric not ticket serial or selection order.

### Task 1: Exact collected application corrections
Implement all requirements below, adding meaningful behavior regression tests first (RED then GREEN) for geometry, numbering, persistence, ordering and sender confirmation. Simple style/removal changes need UI inspection/checks, not implementation-mirroring tests. Run local Core suite and WPF/UI compilation. Commit changes and report evidence. No version/release changes in this task (root handles).

Hall/editor:
1. In performance tree from screenshot131900 (event selection/catalog tree), selected performance blue background white text; enlarge show dates there too. Retain no red/black date style anywhere: user rejects red/black introduced in recipients; unselected dates ordinary colors, enlarged.
2. Zone captions must have clear gap of TWO standard seat spacings from zone's upper edge/first seats, anchored to own zone, no overlap other zone seats. User says lower than current wrongly floating label: correct stale sector.Y placement relative to current seats, no manual repositioning unrelated zones. Keep horizontal text.
3. Rotation rotates entire zone seat geometry; seat numbers and zone captions stay upright. Row captions remain anchored to real row endpoints instead of minX endpoints that swap on rotation or collision displacement away from row.
4. Whole-row selection + «Сделать проход» converts that row to empty passage without shifting other seats, reduces actual row count and numeric labels. Example18→17 is illustrative only. Current HallEditing023.MakeAisle inserts horizontal gap per row; do not apply that to whole-row selection. Keep explicit «Проход после ряда» insertion.
5. Remove visible tab «Места — редактирование» entirely. Remove «Настройки рядов» panel in legacy-grid mode reliably including opening/copying legacy halls; no visible confusing duplicate table. Remove «Обновить схему» button.
6. ONE context item «Назначить букву» opens input, uppercases. Right click seat edits place label; right click row caption edits row. Allow А, Б, 1, 2… and letters anywhere (1, А, 2…). Numeric sequence skips letters; coordinates unchanged, no duplicate row/place identifiers. Respect real row/seat ordering and hidden semantics.
7. Row numbers displayed three times: left endpoint, center of aisle, right endpoint. Center added automatically when creating aisle. Tie all three to own row through rotation, upright text. Persist enough aisle context after save/reopen if required.

Ticket printing:
8. Remove «Обновить просмотр» and «Отмена» from ticket printing window; keep existing «Закрыть». Other document print preview can keep relevant functionality, don't break margins controls.
9. Narrow printer/paper/orientation combos WITHOUT changing heights/font sizes. Closed printer caption may be shortened to HP LaserJet (appropriate short name for other models); dropdown FULL names visible.
10. All print fields/buttons same height. Labels «Принтер», «Бумага», «Копий» centered ABOVE respective fields within existing top gap, no extra top spacing. Shorten blank label to «Бланк», move blank combo onto SAME row. Print button labeled «Печать», at former refresh position after settings, GREEN and SQUARE matching issuance green button. One row fits current app window.
11. Compact printable-area warning: remove redundant Cancel vs Close, keep explicit print-anyway action. Checkbox «Больше не показывать для этого бланка» persists independently PER BLANK; other blanks still warn when necessary. User confirms actual output uncut; diagnose excessive warning bounds if possible, do not silently scale/move original blank content.
12. Print tickets by row and seat ascending, regardless selection order or ticket serials; natural numeric (2 before10), mixed letter/numeric logical hall order preserved. All existing print/reprint/standard/custom blank paths.

Documents/recipient/journal:
13. Fix clipped reserve/block/return button labels in right panel, remove inappropriate price-required tooltip from block button only (block action shouldn't require price); preserve layout as far as possible.
14. Contract date text centered horizontally, larger bold WITHOUT resizing date field. Contract number field narrower, same text size/weight as date, same height. Apply shared document metadata controls where used.
15. TTN opening slower: find actual cause and improve relevant processing, validate content unchanged; no blind unrelated caching.
16. Document nav buttons + «Открыть в Word», «Сохранить», «Печать» in ONE row, same order; applies every document EXCEPT standalone bill view. Also common package window nav/actions ONE row (including its bill mode as shared package toolbar). Do not shrink approved font text merely to fit.
17. Replace window header «Счёт — выдача №…» with recipient. Put «Счёт — выдача №…» on left vertical flag, reading top-to-bottom. User asks bill specifically; preserve other docs titles unless same requested recipient header applicable.
18. Rename «Общий комплект отмеченных выдач» to «Объединить выдачи»; actual data remain separate (package behavior unchanged).
19. Enlarge whole journal summary «Всего мест · Свободно с ценой · Бронь · Без печати · Напечатано».
20. Recipient issue tree: include sender immediately AFTER issue date+TIME: «№52 · 23.10.2026 17:25 · ООО …». Preserve recipient, stats, checkbox behavior.
21. Confirm change of sender anywhere AFTER initial chosen value. Covers invoice sender editor, settings, organization menus changing sender selection; user explicitly retains existing default configuration. Other documents follow their own issue snapshots.

### Task 2: Verification and release
Root reviews Task1 diff with fresh reviewer; correct important findings. Build/check Windows via existing CI (core, UI, native launch), release0.25 using same update mechanism. Update release notes with only actual implemented changes; do not claim Ticketland implemented. Verify published release/assets before reporting success.
