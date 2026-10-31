# Hall spacing implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, then one fresh whole-branch review.

**Goal:** Implement the collected 0.43 UI corrections and persistent sidebar width.
**Architecture:** Local compact text rendering measures visible glyph bounds; physical pixel resources account for DPI. MainWindow owns layout preference independently of events. Existing commands and seat state logic remain intact.
**Tech Stack:** C# / WPF / .NET 8, existing Windows UI test executable.
**Spec:** docs/superpowers/specs/2026-10-31-hall-spacing-design.md

## Global Constraints
- Physical pixel spacing, unchanged fonts, both hall modes.
- No Ticket PRO integration or user live database changes.
- Test only separate offscreen instances on Windows; interactive menus only in CI.
- Standing authorization covers release after successful checks.

## Review Focus
- Long text and multiple price rows wrap without clipping or leading reappearing.
- 100/125/150% scaling and monitor DPI changes preserve spacing.
- Narrow windows preserve usable hall and command access.
- Malformed/unwritable preference file cannot prevent startup or overwrite the last good preference.
- Sold, blocked, local issued and focused seats retain appearance/behavior except the requested occupied rendering.

## Task 1: Compact interface and seat rendering
**Files:** MainWindow.xaml, MainWindow.xaml.cs, MainPresentation.cs, MainHall030.cs, HallView.cs; new CompactText043.cs and MainLayout043.cs; HallLayout043UiChecks.cs and existing common layout checks.
**Interfaces:** CompactText043 exposes Text/Font properties and TextWrapping; physical spacing resources belong to MainWindow. Existing button names/handlers remain.
- [x] Add --layout043 UI regression and run against baseline; expect horizontal icon / physical spacing failure.
- [x] Implement physical spacing, tight text layout, horizontal icons and occupied circle rendering.
- [x] Run --layout043 at supported window sizes/scales, inspect captures and measured glyph spacing; expect pass.

## Task 2: Persist shared sidebar width
**Files:** MainLayout043.cs, HallLayout043UiChecks.cs.
**Interfaces:** Sidebar preference stored next to database as hall-sidebar-width.txt; read finite invariant numeric value, clamp 200..500 DIP; save only manual splitter changes. Adaptive display cap must not change saved preference.
- [x] Test manual resize, event switch, restart, narrow window, malformed preference. Baseline must fail persistence.
- [x] Implement atomic preference save and bounded layout restore.
- [x] Run --layout043 and existing --layout042 / --ticketpro-selection041; expect pass.

## Task 3: Release
- [x] Update version/notes, final fresh review and address important findings.
- [ ] Publish source; existing Windows CI plus --layout043 must pass.
- [ ] Verify normal manifest reports 0.43.0; no install/restart of user app.
