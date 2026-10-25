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

- The initial pass covered storage failure and retry. The follow-up below also
  exercised source-loss recovery through the live desktop manager using a
  temporary fault-injection build.
- Recovery is in-session. Autosave and recovery after a crash are not implemented
  or claimed by this acceptance pass.
- Native package file picker steps were user-assisted. The follow-up covered
  GUI package reimport, asset-browser image selection and transformed-group
  dragging. Native image-file import, multi-monitor/DPI behavior and backend
  startup/database/deployment remain outside this pass.
- Light/dark contrast was visually inspected; no numerical accessibility audit
  or exhaustive custom-palette matrix was performed.

## Follow-up desktop checks, 23 October

Package reimport used the unchanged published build from commit `5023da6` and
the original isolated profile. The user selected `rings-draft.themedwidget` in
the native Open dialog, which Computer Use could observe but not target as a
separate PickerHost process. The app reported a successful import and left the
widget off. Its editor showed the rings and all nine meters. Changing its name
to `QA Reimported Rings` and saving persisted the edit, three measures and nine
meters under a fresh imported identity.

Source-loss testing used an archived copy of `5023da6` under the separate
`qa-harness/source` artifact directory, with only a test button and its handler
added. This temporary build was not committed or substituted for the release
build. The button called the real `SkinManagerService.DeleteSkinAsync` on the
fixture named `QA Recovery Source`; it was guarded to require exactly the
dedicated `test-evidence/2026-10-23-followup/recovery-profile` data root. Normal
save, recovery, image selection and dragging code was unchanged. This setup was
needed because ordinary navigation cannot remove the source while retaining the
same single-window editor instance.

The disabled fixture contained two labels in a group with scale 1.25 and rotation
15 degrees. A pointer drag of 60 horizontal and 20 vertical screen pixels moved
the selected label from local (20,20) to (70,20), as expected with 10-DIP snapping
in transformed local coordinates. The other label and group transform stayed
unchanged. One Undo returned to (20,20), and Redo restored (70,20).

The asset browser displayed a generated checkerboard PNG from the isolated asset
library. Selecting it enabled Add image meter; applying it added a third meter
and rendered the image in the editor. This verified asset selection and decoding,
not the native image-import file picker.

After source removal, Save displayed **Keep edits as new widget** without losing
the group edit or image. Recovery disabled Delete, reset history and left a new
unsaved draft. Explicit Save created a fresh identity and enabled the host. A
second pass typed `QA Typed After Failure` after the failed save and immediately
clicked recovery with the pointer; the resulting `QA Typed After Failure remix`
retained that latest edit. The final saved JSON confirmed three meters, the
1.25/15-degree group transform, local X=70, valid remapped group references, a
readable image path and no recreated source identity. Reopening the first saved
recovery also retained the image and group edit.

An initial accessibility SetValue/Invoke attempt skipped the textbox's normal
focus-loss binding update and recovered the previous name. The real keyboard/
pointer reproduction above preserved the typed name; no production defect was
established and no code change was made for that automation artifact.

Follow-up evidence is stored outside the repository under
`test-evidence/2026-10-23-followup`: `reimported.json`, `fixture-before.json`,
`recovered-first-pass.json`, `recovered-final.json`, `source-loss-prompt.png`,
`recovered-draft.png`, and the fixture preparation script. No new production
source changed after the successful 748-test run and Windows publish; this
follow-up adds desktop evidence only.

## World runtime follow-up, 24 October

Computer Use recovered and controlled the combined build on the interactive
Windows desktop. The final self-contained build includes the atmosphere queue and
opacity fixes described below. It used only the process-scoped data root
`test-evidence/2026-10-23-worlds/final-profile`, with two static label fixtures and
no enabled VibeFinder widgets. QA Amber saves both labels at (80,120) and
(360,120); QA Blue saves the first at (80,340) and hides the second at (360,340).
Both fixtures use opacity 0.72 in the final regression pass.

- Studio started before any widget host without the earlier transparency warning.
- With Apply aesthetic on selection off, choosing QA Blue displayed Preview only
  and left Amber's enabled flags, positions and opacity unchanged.
- Explicit Apply to desktop made Blue active, moved the saved layout and closed
  label B. Applying Amber again restored B and both original positions. The GUI
  displayed the active world and label B disappearing/reappearing; persisted JSON
  verified both coordinates and flags, and host logs reported two open windows.
- Opacity stayed exactly 0.72 in skins and both scene placements through the round
  trip. Previously the runtime changed 1.0 to 0.92 and then 0.8464 while saving
  those presentation values back into the model.
- After terminating only the test process once persistence had completed and
  relaunching the same build/profile, Studio showed QA Amber active, both fixture
  hosts reopened at their saved positions, and opacity remained 0.72. This is a
  process-restart persistence check, not a crash-recovery or graceful-exit test.
- The final log contains no atmosphere transparency/DispatcherQueue error. Its
  existing global-hotkey registration warning remains; the hotkey was not tested.
- SHA-256 hashes for personal skins.json, themes.json and scenes.json match the
  baseline taken after the initial isolated launch and before GUI interactions.
  Explicit world application did apply Windows Light mode and accent #7B563F;
  data-folder isolation does not isolate OS appearance. No wallpaper was bound.

Two production defects were fixed. DesktopAtmosphereWindow now initializes and
retains a Windows.System dispatcher queue before creating its system compositor,
matching the existing requirement in the widget host. DesktopWorldRuntime now
applies atmosphere opacity to the open window only, without editing or persisting
SkinDefinition. Static worlds keep their saved opacity; reactive effects multiply
the current saved value, preserve zero opacity, and restore it when stopped.

The final Windows Release publish succeeded. All 748 Release tests passed with
zero failures and skips using isolated storage. These tests do not cover the native
queue or compositor; the startup and static-world persistence regressions above
provide that desktop evidence. Authenticated reactive playback, rapid 20-switch
stress, native image import, multi-monitor/DPI changes and exhaustive Windows
appearance synchronization remain unverified.

Evidence remains outside the repository in `test-evidence/2026-10-23-worlds`:
`opacity-before-fix.json`, `amber-fixed.json`, `blue-preview-fixed.json`,
`blue-fixed.json`, `amber-roundtrip-fixed.json`, `scenes-roundtrip-fixed.json`,
`after-restart-fixed.json`, personal hash comparisons, profile logs,
`publish-final.log` and `test-results/world-fixes.trx`. The test process was closed
after verification; no personal widget data was modified.

## Additional acceptance and apply-race fix, 25 October

The unchanged 56e2a5d build (same tree as merged parent 24f94a0) completed 20
alternating auto-applied QA Amber/QA Blue switches over 140 seconds. Every apply
resolved two placements with zero missing definitions; the maximum logged host
count was two. Final layout, visibility and opacity 0.72 were intact. This was
consecutive switching at human/tool pace, not sub-second concurrent-input stress.

Native image import passed with a user-assisted Windows picker. Image assets
reported a successful import, displayed the checkerboard preview, and Add image
meter plus Save persisted the image. Reopening rendered it again. The imported
asset hash matched the source, and personal skins/themes/scenes hashes were
unchanged. Evidence: external `test-evidence/2026-10-24-focused`.

The next isolated profile gave QA Blue the Midnight Glass palette. Selecting it
changed the app to dark colors, set both Windows AppsUseLightTheme and
SystemUsesLightTheme to 0, and applied accent #6EA8FF (DWM AccentColor FFFFA86E,
ColorizationColor C46EA8FF). Returning to QA Amber restored both mode values to 1
and accent #7B563F (FF3F567B/C47B563F). This covers the app-to-Windows light/dark
and accent path, not exhaustive OS settings synchronization or wallpaper behavior.

Source review of overlapping applies found that ApplySelectedWorldAsync reread
_selected after awaits. A later selection could therefore supply a different
world's theme/layout or activation. The method now retains its starting target,
disables the world selector until completion, and restores it in finally.
Selection callbacks also ignore stale completions after an outgoing-layout save.

A temporary standalone harness compiled the actual apply method extracted from
source, with explicit dependency doubles and controlled awaits. The original
method failed when selection changed while loading themes. The corrected method
passed selection changes during repository and Windows-theme awaits, rejection
of an overlapping apply, and recovery of the selector/error message after an
injected failure. This is logic validation with doubles, not a native overlap
stress reproduction, and it is not part of the 748-test suite.

The corrected Release publish succeeded and all 748 Release tests passed with
zero failures/skips. In the corrected GUI, ScenesList was observed disabled while
Applying QA Blue and enabled again when Blue became active. Switching back to
Amber also completed and restored the selector. No claim of sub-second input
stress is made. Personal data hashes and the initial Windows mode/accent values
were unchanged at the end; the isolated process was closed.

Evidence: external `test-evidence/2026-10-25-appearance`, including dark/light
registry results, final-widget JSON, personal hash comparisons, publish log,
apply-harness before/after logs and test-results/apply-guard.trx. Authenticated
playback/network recovery, live multi-monitor/DPI changes and broader Windows
appearance integration remain unverified.
