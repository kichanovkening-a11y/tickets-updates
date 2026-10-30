# Ticket PRO: update only the open showing

The user reports that 0.40.0 still runs a slow 18-show batch. Startup begins that batch before a showing is selected; manual refresh and the five-minute timer use the same batch. Prioritizing one showing did not solve the requested interaction.

The chosen correction is to refresh only the current showing on selection, manual refresh, timer, and connection. At home there is no scheme refresh. Switching shows cancels the previous request and retains only the newest pending selection. Requests for the same in-flight showing coalesce. Existing bindings bypass catalog discovery; a missing binding searches only the selected performance's catalog rows and applies only the selected showing. Keep strict identity and complete-seat validation, read-only remote access, existing hall controls and local data.

Approaches considered: retaining a prioritized full batch leaves the reported problem; concurrent full scans add server load and navigation races; selected-only requests remove the unnecessary work. This is the implementation decision under the user's standing instruction to fix the program without further approvals.

The user also identified a stale Ticketland tooltip in Ticket PRO mode and explicitly requested the correction everywhere. Make provider names follow actual showing/seat data in status tooltips, legend, totals, selected-seat status, conflict window and printing/reservation error messages. Keep Ticketland labels for actual legacy report data and its connection/import screens.

Do not close or reopen the user's running application. Use off-screen fixtures and isolated database/profile copies. Publish through the normal update channel after validation.

Additional explicit UI requests: remove the main hall toolbar buttons «Весь зал», «−», «+». Replace the text expansion button with the supplied green four-arrow icon, outside the scheme at the upper right above «Обновить». Preserve expansion/restoration and accessible tooltip names; reserve header space so the icon cannot cover tabs.
