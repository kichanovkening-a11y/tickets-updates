# Approved corrections 0.27 Implementation Plan

**Goal:** Implement the corrections collected after 0.26 and explicitly authorized on 24 October 2026.
**Architecture:** Preserve WPF/SQLite/document architecture. Shared HallView handles common selection and captions; draft operations remain in hall editors. Existing issued data is preserved.
**Tech Stack:** C# 12, WPF .NET 8, SQLite, OpenXML.
**Spec:** User conversation through the command “займись этим правками, делай”.

## Global constraints
- Only the collected changes; no Ticketland integration, cloud backup or logging.
- Leave separate/modal window behavior unchanged (user explicitly chose to keep it).
- Keep existing fonts/heights unless explicitly requested. No duplicate buttons.
- Direction of seat numbering may change only while creating a hall, including Add sector, not while editing a saved hall.
- Separate hall editing: sector-name-only changes may update an in-use hall; any seat/geometry change requires a new hall name and new hall record.
- No repeated full suites: affected verification during work, one principal full run before release, repeat only after failures or new changes.

## Review focus
- Existing issued seat IDs/history survive sector renames; structural edits cannot overwrite an in-use scheme.
- Every row number (left, right, all aisles) targets the same row; horizontal shift cannot alter Y or numbering.
- IP sender signature changes must not affect recipient titles or ООО titles, including future IPs.
- Compact header aligns square commands with ordinary commands at 100/125/150% scaling and tabs beneath ordinary commands.
- Returned-ticket detail clicks expand inline and never invoke the issuance open action.

### Task 1: Documents and printing
- [x] Remove IP sender job titles in all generated forms while preserving the sole right bill signature, recipient and ООО titles. Enlarge upright stacked external flag to show only Выдача №N. Narrow sender field; enlarge/bold От кого. Remove dotted focus visuals. Print first-time/reprint titles according to print history.
- [x] Add meaningful signature/history regressions, run affected checks. Existing document tests whose explicitly changed expected output conflicts are updated.

### Task 2: Main interface, scheduling and return visibility
- [x] Remove home duplicate creation/find buttons; move search beside top brand. Align square Print/Issue tops with ordinary commands, lift tabs to ordinary-command lower edge.
- [x] Known performance is preselected when adding a show; clear date/time after Add date, remove inner border/watermark, match pricing field height to Create.
- [x] Add inline returned ticket details in journal and recipient history: sector, row, seat, ticket number, amount, return date; toggle via return count.
- [x] Verify affected layout/navigation/return behavior.

### Task 3: Hall creation and saved hall editing
- [x] Remove obsolete manual sector panel/instructions, retain metadata and the two creation methods (scan and legacy grid).
- [x] Edit sector name via right-click caption and seats during creation; separate saved-schema edit entry. Name-only may update in place; structural changes save only under distinct new hall name.
- [x] Aisle row captions retain original positions after deleting seats (additional user requirement during work); metadata persists through saves, undo and geometry transforms.
- [x] Numbering direction in creating/add-sector dialogs only. Add row context horizontal move, including every row label. Escape cancels, click commits; all row seats/labels remain tied.
- [x] Shared empty click clears selection in all hall modes. Sector headings 5 pixels above first row. Every aisle gets row numbers, including multiple aisles.
- [x] Verify draft transformations and persistence safeguards plus native hall interaction.

### Task 4: Integration and release
- [x] Inspect combined changes; compile Core/WPF, run principal suite once and Windows checks; inspect screenshots. Fresh reviewer checks requirements/risks; fix important findings with targeted evidence.
- [x] Publish version 0.27.0 through existing verified release workflow and confirm assets/Windows start. Report actual outcome only.

## Execution ledger
- 2026-10-24: Clean starting branch corrections-026 at 90bd431; new branch corrections-027. Existing checkout reused to avoid redundant setup. Authorization covers implementation/update, no further confirmation gate.

- Review finding: prevent clicks inside expanded journal returned-ticket table from opening its parent issuance; shared detail subtree marker and native interaction regression added.

## Completion — 24 October 2026
- Version 0.27.0 published at 08:56:42 UTC; release https://github.com/kichanovkening-a11y/tickets-updates/releases/tag/v0.27.0.
- Verified release source: 47ebb602aaea1307e3b2fb470da132406756f97d. Final workflow 40078387907 / Windows job 120828121504: every step succeeded.
- Core/accounting/updater: 197/197 passed. All native UI checks passed, including every aisle caption, deletion of the whole block on one side, row movement/Escape, 100/125/150% command alignment, and inline returned-ticket details.
- Actual packaged Windows executable opened with title Билеты · 0.27.0 and closed normally. Five release assets are present and latest release is v0.27.0.
- Review: fresh reviewer found the journal detail click-routing issue; marked detail table and interaction checks resolve it. No remaining critical or important findings.
- Verification findings fixed: anchor metadata uses immutable value equality; dense headings resolve collisions horizontally while retaining the requested 5px vertical gap; outer row captions reserve fixed aisle captions. Old tests were adapted only where the authorized UI requirements replaced their assumptions, and new native fixtures were isolated.
- Separate-window behavior remains as explicitly retained by the user. Ticketland, cloud backups and logging remain outside this update.
