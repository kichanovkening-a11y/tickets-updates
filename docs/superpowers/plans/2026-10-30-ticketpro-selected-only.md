# Ticket PRO selected-only refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox syntax for tracking.

**Goal:** Refresh only the open showing and switch promptly when the user selects another.

**Architecture:** A serial latest-selection coordinator cancels obsolete requests and coalesces duplicates. MainWindow routes all refresh entry points through it; the session resolves only a missing selected binding. Existing strict parsing and transactional application remain unchanged.

**Tech Stack:** C# / .NET 8, WPF, WebView2, SQLite.

**Spec:** `docs/superpowers/specs/2026-10-30-ticketpro-selected-only-design.md`

## Global Constraints

- No automatic application restart or visible test windows on the user's PC.
- No writes to the original database or Ticket PRO inventory during diagnostics.
- Preserve layout, hall expansion controls, strict snapshot validation and last successful data.
- Standing permission covers release publication through the existing updater.

## Review Focus

- Startup before any selection must not initiate an all-show read.
- Rapid A → B → C selection must not queue B or overlap WebView navigations.
- Leaving for Home or closing must cancel pending reads without replacing the new screen's status.
- A failed/timed-out request must release the gate and allow a retry.
- Unknown/ambiguous source identity must preserve data and never pick an unrelated showing.

### Task 1: Serial latest-selection queue

**Files:** create `src/Tickets.App/TicketProSelection041.cs`, `tests/Tickets.UiChecks/TicketPro041QueueChecks.cs`; modify UI check entry point.

**Interfaces:** constructor `(Func<long,CancellationToken,Task>, CancellationToken)`; `Task RequestAsync(long? eventId)`.

- [x] Write cases for idle home, selected only, duplicate coalescing, A/B/C cancellation and serialization, home cancellation, lifetime cancellation, retry after failure.
- [x] Run `Tickets.UiChecks.exe --ticketpro-queue041`; expect missing coordinator failure before implementation.
- [x] Implement coordinator; run same command, expect all cases PASS.

### Task 2: Wire the actual screen and targeted discovery

**Files:** modify `TicketProConnection037.cs`, `MainWindow.xaml.cs`, `TicketProDiscovery038.cs`, `TicketPro037.cs`, source-specific tooltip code; create `TicketPro041UiChecks.cs`.

**Interfaces:** retain `RefreshTicketPro037()` entry point; add `TicketProFetch041` test seam analogous to existing report fetch seam; add optional selected event ID to discovery/title relevance without changing existing full-discovery callers.

- [x] Add off-screen MainWindow cases: no fetch at home, selecting A calls only A, manual retry calls only A, switching to B cancels A and applies only B, home cancels, failure preserves saved data, source-specific tooltip.
- [x] Observe failing integration cases against old routing, then replace the batch path with selected request routing.
- [x] Run queue/UI/catalog/scheme fixtures; expect PASS. Run targeted native live read on an isolated copy if saved sign-in is available, without showing login or disturbing the running app.

### Task 3: Review and publish

**Files:** version, release notes and workflow fixture commands.

- [x] Obtain fresh final code review, address blocking findings with focused red/green tests.
- [ ] Publish 0.41.0 source through the connector; keep full release checks.
- [ ] Verify final logs, package version and normal update manifest. Do not install or restart the app.

## Verification before publication

- Native Windows: queue 6, target completeness 5, read-only scheme 3, catalog/session 6 and actual-screen 35 assertions passed.
- Fresh final review found one incomplete-discovery ambiguity blocker. A focused check reproduced it, then passed after rejecting partial discovery before any scheme read.
- Isolated live Ticket PRO: known selected binding 8.928 seconds, targeted first discovery 17.248 seconds. Both read one scheme; the other 17 showings remained unchanged.
- Normal and expanded native captures verified: green icon above Refresh, requested three buttons absent, navigation hides and returns.
- Full release workflow and normal updater manifest verification follow publication.
