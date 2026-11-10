# Agreed corrections Implementation Plan

Goal: five approved corrections; reuse .NET8/WPF printing, hall geometry and transactional updater.
Spec: ../specs/2026-11-10-agreed-corrections.md

- [x] Add regression checks, see expected failures, implement per-window print visibility without changing accounting.
- [x] Keep one manual creation path, remove fit button, use18×18 geometry and safe one-time existing-hall compaction with identities/backups intact.
- [x] Add byte/file progress and explicit WPF dispatcher; retain installation rollback.
- [x] Independently review changes and reproduce/fix important findings with tests.
- [ ] Complete full core, WPF, updater and real installation checks in a temporary Windows copy.
- [ ] Save tested branch and reviewable PR without publishing or changing main.
