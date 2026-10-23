# Approved corrections 0.26 implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Apply only the accumulated corrections authorized by the user.
**Architecture:** Keep existing WPF editors and persistence. Share document chrome and sender confirmation guards; retain all current hall operations.
**Tech Stack:** C#, WPF, SQLite, Word XML.
**Spec:** Conversation requirements captured below.

## Global Constraints
- No Ticketland integration or automatic/cloud backup.
- Preserve button heights, fonts unless enlargement was requested, document text and data.
- Default sender remains unchanged.
- All document menus: recipient in header, no duplicated recipient field, external attached vertical document/issue tab, one command row, 2px horizontal button padding; compact title/body margins and raise contract fields/document; date centered.

## Review Focus
- Rejecting a sender change must not re-enter through focus events.
- External tabs must be outside the border in embedded and separate modes without another window.
- Saved packages must follow embedded mode and preserve edits.
- Hall deletion/hiding/letters remain available after removing the legacy table.
- Compact main layout must preserve the two square action button coordinates.

### Task 1: Hall and confirmation behavior
Files: LegacyHallGridWindow.cs, HallLetterWindow025.cs, InvoiceWindow.cs, IssuanceActions.cs.
- [ ] Remove legacy upper table and use the existing on-scheme selection/actions; immediately uppercase typed/pasted letters.
- [ ] Guard sender confirmation against nested focus/selection callbacks and restore both selected item and text on rejection.
- [ ] Compile WPF and verify the changed native controls.

### Task 2: Document chrome and signatures
Files: DocumentFlag025.cs, EditorHost.cs, IssueDocumentNavigation.cs, DocumentActions.cs, document window classes, ContractFields.cs, SignatureBlocks.cs, RecipientDocumentForms.cs.
- [ ] Apply consistent recipient title and external vertical tab to all issue/package documents, single command line with 2px padding, compact margins and centered date.
- [ ] For IP bills retain the existing right sender signature only; preserve other signatures.
- [ ] Verify generated Word and native layouts.

### Task 3: Main interface and settings
Files: MainWindow.xaml/.cs, UiResources.xaml, EditorHost.cs, RecipientsWindow.cs, RecipientPackagesWindow.cs.
- [ ] Move venue beside event date and raise counters; retain square action positions and reduce gaps to 5px; show full price legend.
- [ ] Enlarge and center journal instruction/headings/cells without changing button heights.
- [ ] Remove specifications from Settings and move Update/Backup there; keep backup manual.
- [ ] Remove forced separate package dialogs.

### Task 4: Verification and release
- [ ] Run focused behavior checks, compile WPF and run one full core suite.
- [ ] Native Windows validation and fresh whole-branch review; resolve important findings.
- [ ] Publish the verified update using the existing release workflow and report actual results.
