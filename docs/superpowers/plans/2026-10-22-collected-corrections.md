# Collected Tickets corrections implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development or executing-plans task by task.

**Goal:** Apply the corrections explicitly approved by the user's «делай правки все» after collecting them.
**Architecture:** Keep existing WPF/SQLite flows. Fix each persistence/input/document boundary without changing previously approved layout elsewhere. Ticketland is excluded.
**Tech Stack:** C# .NET 8, WPF, SQLite, OOXML.
**Spec:** User's collected requirements in this conversation, summarized below.

## Global constraints
- No old Russian Song scan; do not fabricate a new hall from it.
- Preserve compact approved controls, align text and numbers horizontally/vertically centrally; no unrelated visual redesign.
- Verify failures before claiming fixes; Windows Word opening is distinct from XML schema validity.

## Review focus
- Deleted last sender and historical deleted sender must not prevent creating a new sender/issuance.
- Actual mouse resizing, not programmatically setting column Width; per-event restart persistence.
- TTN exported after native text editing must open in Word; all generated files checked.
- Same prices with a new unique name means a new plan, but blank unchanged plan can reuse source.
- Occupied-only left clicks select; only right clicks open menu. Mixed selection warns and keeps free seats.

### Task 1: Documents
Files: ContractFields.cs, CustomIssueDocumentWindow.cs, OrganizationDetails.cs, ContractData.cs/source forms as needed; document regression tests.
- [ ] Diagnose TTN using supplied failure screenshot and source generation/edited form path. Fix proven cause; regenerate Word exports rather than reusing stale corrupt files.
- [ ] Remove Apply to set and recipient representative/position from document header. Persist contract number/date automatically; representative/position belong to recipient organization and propagate to all documents automatically.
- [ ] Scene/venue comes from event in every form.
- [ ] Test TTN package including edited form; avoid claiming Word COM check unless actually run.

### Task 2: Catalog, halls, selection
Files: CatalogWindows.cs, CatalogDirectories.cs, HallEditorWindow.cs, HallView.cs, core catalog/layout migration, tests.
- [ ] Empty new event fields; rows distinct from seats; retain existing pending changes.
- [ ] Halls open by single row click; remove redundant Open/Edit button. Remove Extreme parter without using old scan.
- [ ] Fix Refresh during AddNew/EditItem, compact row controls/live editor. Scan import trial only with actual recognition and explicit review, no misleading trace.
- [ ] Restore left select/right menu.
- [ ] Pricing one-line total count/sum/blocked. Block wording everywhere. Saving requires price for all unblocked seats. Allow new unique name with identical prices. Permit unblocking for event with assigned price.

### Task 3: UI/persistence/printing
Files: JournalActions.cs, RecipientsWindow.cs, InvoiceWindow.cs, app styles, printing routes, sender handling; tests.
- [ ] Diagnose and fix sender-deletion lockup without restoring hidden sender.
- [ ] Save actual dragged journal widths per event, reload including app restart.
- [ ] Restore compact aligned affected button rows; center controls' content without altering document paragraph layout.
- [ ] Route every print action through supported in-app preview; preserve selected tickets/blank and print confirmed pages.

### Task 4: Verification and release
- [ ] Run targeted and complete Core tests and WPF compile/UI checks.
- [ ] Review changes and regressions, update release notes, publish only verified artifact and report actual completed list/limits.
