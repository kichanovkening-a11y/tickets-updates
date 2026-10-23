# Approved corrections 0.23 Implementation Plan

> For agentic workers: use subagent-driven-development or executing-plans, with tests and review.

Goal: implement only requests discussed after v0.22.0, preserving approved UI.
Architecture: extend existing WPF/SQLite app, existing document generation and hall editor; no replacement database and no Ticketland. Latest explicit command authorizes implementation of previously collected requirements.

## Global Constraints
- Do not change approved presentation or behavior outside these requests.
- No Ticketland; master/copy synchronization remains discussion only (mechanism not selected).
- Do not use previous Russian Song scan.
- No production database is present: cleanup must run explicitly against user's existing working database after backup, never erase seed blindly or restore old data.
- Hall editing must never be available in issuance.

## Tasks
- [ ] 1. Recipient shared document package: check issues of one recipient across shows only inside Recipients. Common contract/bill/act/specification/TTN/invoice package; aggregate totals, per-show grouped places; act date latest selected show. Preserve all original issue IDs, documents and journal behavior. Reject mixed senders/recipients or returned-only selections. Larger issue text under date, no date background. Signatory block two blank lines below preceding content and line one blank line below signatory, aligned sides.
- [ ] 2. Hall editor: right click seat/group offers make aisle or delete, preserve remaining labels. Common canvas sectors added through popup name/rows/seats saved explicitly, then edited on canvas. Sector mouse move/rotation in editor only, persisted in seat coordinates; remove Shift tab. Legacy grid zoom instead of excessive scrolling. Row captions both sides. Estrada missing numbered seat placeholders visible inactive, not issuance/print/pricing inventory; verify exact omitted numbers from geometry and reference.
- [ ] 3. Cleanup: explicit permanently purge deleted catalog records and linked shows/issues/docs, preserve venues and all hall geometry/plans. Past entries can be permanently deleted by confirmation, backup and SQLite compaction. No silent purging of active/past entries.
- [ ] 4. Red launch shortcut named ! Билеты, preserve name and target existing copy, ensure updater can refresh icon. Version/release notes; tests core/windows; release and verify assets.

## Review Focus
Mixed recipients/senders and returned seats; overlapping original/merged invoices; preserving all actual seat labels while inserting aisles; no rotation/move in issuance; interrupted cleanup never loses original data.
