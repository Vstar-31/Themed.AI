# Theme-library save integrity — 2026-10-30

ThemeService previously added or removed items in its public list before ThemeRepository saved the file. A failed save therefore left the live library different from disk. Deleting the active theme also broadcast a fallback palette before persistence succeeded. Saving a different object with an existing ID appended a duplicate and left ActiveTheme pointing at the old instance.

The service now serializes library mutations, stages membership changes, saves the candidate list, then publishes it. Failed saves retain membership and active selection and do not emit success notifications. Saves replace by ID and update the active instance only after persistence. Deletes resolve the stored identity and its built-in protection. Failed saves restore the service-assigned LastModified timestamp. ThemeRepository removes only the temporary file belonging to that save attempt after failure or success; cleanup remains best-effort when access itself is denied.

Eight new isolated regression cases exercise failed create/duplicate/import/new-save plus retry, failed active deletion plus retry and notifications, detached save replacement and active-instance consistency, concurrent additions, and detached deletion/built-in protection. A read-only open file denies replacement of the temporary test library on Windows. Before the fix, seven cases failed on membership or identity assertions; after the fix, all eight pass.

Validation: 809 Release tests passed with zero failures/skips. WinUI Release build passed with zero warnings/errors using a separate output directory. No GUI interactions, app restarts, tray operations, or Windows appearance changes were performed for this work. Native error-display behavior is not covered by these service tests.

Limits: callers can still edit fields on shared mutable theme objects before saving, and these edits are not rolled back. A subscriber that throws after persistence does not roll back the committed file. The write gate coordinates one ThemeService instance; it is not a cross-process lock. This change does not add active-theme persistence across restarts or change the default startup palette.
