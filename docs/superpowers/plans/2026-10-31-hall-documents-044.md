# Tickets 0.44 implementation plan

1. Add focused regression checks for default summary, immediate pricing selection, package owner and archive boundaries. Observe baseline failure.
2. Apply hall layout/summary changes with physical pixel resources; keep sidebar persistence and Ticket PRO code intact.
3. Add read-only document archive classification and filters; fix nested package ownership and verify stored document bytes.
4. Compact new-show and pricing forms; replace/move controls, retain commands and automatic plan loading.
5. Update affected UI expectations, run isolated Windows tests and inspect captures. Run one fresh final review, resolve important findings, then repeat affected checks.
6. Update version/release notes, publish source through GitHub, wait for normal release checks, and verify the downloadable manifest without updating the user's running app.
