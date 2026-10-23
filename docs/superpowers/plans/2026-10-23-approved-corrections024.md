# Approved corrections 0.24 Implementation Plan

**Goal:** Apply only the corrections collected after 0.23, authorized by «Делай правки».
**Architecture:** Preserve current WPF/Core/SQLite application. Use shared document signatures and print settings. Draft operations keep coordinates stable except explicitly inserted aisles and whole-sector transforms.
**Tech Stack:** .NET 8, WPF, SQLite, OOXML.
**Spec:** User instructions in this session, including signature screenshot image(20261023-110100).png.

## Global Constraints
- No unrelated layout or behavior changes. Keep user data, update existing working copy.
- Delete draft seats: sequential remaining labels, fixed coordinates. Hide: retain original labels and coordinates, exclude hidden seats from display/issuance.
- Individual seat movement forbidden. Whole-sector movement stays available and responsive.
- One ticket print window with printer, media, orientation, copies, blank and preview; issuance only on explicit print.
- All signature blocks: separate position and unbroken surname/initials, aligned columns and spacing from reference.

## Review Focus
- Hidden seats persist on save and reopen; forged IDs cannot issue hidden seats.
- Reverse and letter numbering retain direction after deletion.
- Rotated sectors and row aisles move geometrically, other sectors stay fixed.
- Cancel printing never issues tickets; failures/reprints preserve IDs.
- Existing saved/custom documents and general packages use consistent signature rendering.

### Task 1: Hall draft editing
- [x] Add failing regressions for delete geometry/labels, hide persistence, row aisle insertion.
- [x] Implement in Catalog.cs, Store.cs, HallEditing024.cs, HallEditorWindow.cs; hide row settings in legacy grid mode, remove toolbar delete button, coordinates read-only.
- [x] Cache drawing during sector gesture in HallView.cs; transform drawing rather than rebuild each mouse move.
- [x] Run Core tests and Windows UI checks.

### Task 2: Print consolidation
- [x] Embed UnifiedPrintPreview settings in TicketPrintWindow; preserve submit safety and all callers.
- [x] Verify UI settings are present before first print and cancel does not issue.

### Task 3: Recipients and selection
- [x] Replace package kind dropdown with existing document navigation buttons.
- [x] Saved package window; remove inline requisites and redundant open issue button.
- [x] Selected performance blue/white, larger dates; retain red/black date style.
- [x] Check UI behavior and opening saved packages.

### Task 4: Signatures
- [x] Normalize shared generated/source/custom OOXML and WPF signatures without changing other document content.
- [x] Regression check separate identity lines, no wrap, two columns, Word structure.

### Task 5: Verification and release
- [x] Build Core/App/UI checks; inspect diff and independent review.
- [ ] Version 0.24.0, release notes, publish through existing CI after checks pass.
- [ ] Verify published assets and report update instructions.

## Review and verification
Independent review found three issues: custom seat suffixes, mixed/rotated row order, and modal package child ownership. All were corrected with regressions. HallDraft retains insertion order by seat ID so row progression survives rotations and reopening. Unknown arbitrary seat labels fall back to a sequential numeric series on deletion; coordinates stay unchanged. Signature legal prose and organization snapshots retain full names; signature lines use surname/initials.

Targeted hall regressions 5/5; signature checks 20/20. Full Core and Windows verification recorded below when complete.
