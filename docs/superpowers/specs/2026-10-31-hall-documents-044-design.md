# Tickets 0.44 — agreed interface and document corrections

Authorized by «делай все правки» on 2026-10-31. Do not change Ticket PRO retrieval, authentication, matching or update logic. Do not operate the user's installed application.

- Restore hall refresh/expand buttons to 32 DIP, refresh glyph 24, expand graphic 24; keep 5 physical pixels between and below them in both hall modes.
- Default selection view to price summary; preserve explicit user choice across selection, refresh and event changes.
- Reduce title-to-statistics ink gap to 3 physical pixels. Increase the four hall tabs/actions by 10 physical pixels, preserving font and centered text.
- Price summary: same left edge, no ruble symbol, no spaces around ×, 4 physical pixels between rows, 10 before total, existing 5 pixel edge insets.
- Fix nested saved-package ownership. Offer Archive for past recipient issues/packages, retaining all stored documents. A package is archived only when every show represented in the document is archived/past; future dates keep it active.
- New show: remove space above form, center title across full blue header.
- Pricing: green expand icon on header, 5 pixels left of Close. Move colored prices to right panel in a vertical column. Remove visible Block/Undo/Assign selected/Assign all buttons, preserving keyboard/context commands. No change to plaque focus behavior.
- Pricing: totals 5 pixels below header, right-panel gaps and insets 5 pixels. Remove load button; selection immediately loads plan without confirmation, including initial plan supplied by caller.

Scope excludes unrelated visual changes, accounting, Ticket PRO processing and permanent-deletion workflows. Archive is a read-only display filter, not a migration or deletion.

Validation: isolated Windows build; failing-then-passing behavior checks; physical layout checks at multiple scales and modes; rendered screenshots; existing UI/core/document regressions; one fresh final review; release 0.44 via existing channel after checks.
