# Portable updater 0.46

User authorized implementation of the discussed independent updater and subsequently requested the launch executable sort first: `! Tickets.exe`. Work inline on fix/updater-046; retain Tickets.exe for old updater compatibility. No live app installation, database replacement, or Ticket PRO changes.

## Approach
A separate bundled Windows updater relaunches itself from a temporary directory, downloads verified packages independently of the installed version, and installs through a recoverable transaction. Existing in-app updates hand off to that updater; standalone operation selects a folder. Reusing the running app as installer couples installation to its old runtime; manual data copying was rejected by the user.

## Contract
Preflight paths, hashes, available space and writable targets before replacement. Retain a durable journal and original files; recover incomplete transactions before another update. Back up Data while holding the app session lock. Preserve user files. Validate the upgraded database on a copy using the new app before replacing live binaries. Installation failure restores binaries; startup validation failure also restores database. Never claim arbitrary historical versions are tested; verify archived 0.36 and available legacy seed. Store full error details. Two same-directory launches cannot update concurrently. Unknown/corrupt transaction state fails closed.

## Packaging
Ship ! Tickets.exe and compatibility Tickets.exe with identical apphost bytes. Update paths allow the explicit launcher name only, not arbitrary unsafe paths. Portable build includes updater and its runtime. Keep manifest format 1 readable by historical clients. Existing copies with broken old updater still require full-folder transfer (user elected this).

## Limits
No software can promise automatic recovery while removable media is absent or physically damaged; retain on-disk backups and explain recoverable failure. Explorer controls sorting; ! name leads files only under name ascending. Actual second-PC rollback failure is not yet reproduced: do not claim its cause.
