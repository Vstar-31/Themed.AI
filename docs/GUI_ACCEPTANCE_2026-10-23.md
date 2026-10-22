# Windows GUI acceptance, 22–23 October 2026

Tested the accumulated `feat/widget-package-workflow` changes and the resulting
Release win-x64 build using Computer Use on Windows. This is a bounded desktop
acceptance pass, not certification of every editor or backend feature.

## Isolation and artifacts

The test process used `THEMEDAI_LOCAL_DATA_ROOT` pointing to
`D:\Local_Profile\Documents\ChatGPT\themed.ai\test-evidence\2026-10-22-gui\profile`.
Save/retry fixtures and repository inspection targeted that folder. Personal
widget repositories were not used for these tests. No before/after hash baseline
of personal storage was captured, so no hash-based unchanged claim is made.
Theme selection can also apply Windows appearance preferences according to the
app settings; data isolation does not isolate those appearance integrations.

Artifacts are outside the source repository under
`D:\Local_Profile\Documents\ChatGPT\themed.ai`:

- Fixed build: `builds/2026-10-22-gui-fixed-win-x64/ThemeManager.WinUI.exe`.
- Test results: `test-evidence/2026-10-22-gui/tests-gui-fixed.trx`.
- Screenshots: `dark-starter-fixed.png` and `light-starter-fixed.png` in that evidence folder.
- Exported package: `rings-draft.themedwidget` in that evidence folder.

## Observed results

| Area | Desktop observation |
| --- | --- |
| Starter previews | Inspected all five bundled starters: clock, monitor bars, rings, network graph and uptime. Static samples were visible. |
| Search | `CPU bar`, `ring`, `network graph` and `uptime` returned the expected starters. `ring` did not match String meters. An unmatched query showed guidance and disabled Open. Filtering out a selection cleared it. |
| Scrolling and layout | Picker details and actions remained reachable in the normal window; widget-library scrolling reached the final card. Also checked maximized layout. |
| Contrast | Found and fixed light dialog surfaces with white text after switching to Midnight Glass, stale beige button hover colors, and dark caption icons. Retested the dark picker/editor and light picker. |
| Duplication | Ctrl+D duplicated the selected uptime label, taking the draft from two to three meters with an offset copy. It did not duplicate while the name textbox had focus. |
| Keyboard movement | Ctrl+Alt+arrows moved the selected meter by 1 DIP; adding Shift moved it by 10 DIP. One early Down attempt scrolled instead, but repeated focused attempts succeeded; no reproducible defect was established. |
| Undo/redo | Ctrl+Z reversed the last move and Ctrl+Y restored it, with matching coordinate/history changes. |
| Save and reopen | First Save created/enabled the test widget. Reopening retained its three meters. A later full process restart retained the saved name `GUI QA Recovered`, three meters and a clean history. |
| Failed-save recovery | Made only the isolated `skins.json` read-only, edited the name and pressed Save. The app displayed a storage error and retained the draft; the repository hash stayed unchanged. Restored writability and retried Save successfully. |
| Export | Exported an unsaved System Rings remix. The user completed the native Save As dialog because PickerHost was not targetable through Computer Use. The resulting ZIP contained valid `skin.json`: disabled widget, three measures and nine meters. Export did not add the remix to isolated `skins.json`. |

## Fix and validation

`App.ApplyThemeToResources` now updates the stock-control brush aliases and hover/
pressed color resources when the palette changes. `MainWindow.ApplyWindowTheme`
selects the appropriate WinUI light/dark variant and updates caption colors.

Release self-contained Windows publish succeeded. The main test project passed
**748 tests, zero failed and zero skipped** after the fix. `git diff --check`
passed. Reviewed the accumulated editor, persistence, runtime, startup, theme
and backend changes before committing; this was not a deployment validation.

## Coverage limits

- The source-loss/identity-conflict **Keep edits as new widget** button path was
  covered by editor logic tests, not exercised through the live desktop manager.
  The GUI recovery result above concerns storage failure and retry.
- Recovery is in-session. Autosave and recovery after a crash are not implemented
  or claimed by this acceptance pass.
- The native file picker step was user-assisted. GUI package reimport, image
  selection, transformed-group dragging, multi-monitor/DPI behavior and backend
  startup/database/deployment were not validated in this pass.
- Light/dark contrast was visually inspected; no numerical accessibility audit
  or exhaustive custom-palette matrix was performed.
