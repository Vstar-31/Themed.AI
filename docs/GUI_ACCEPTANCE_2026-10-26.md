# Isolated desktop acceptance — 2026-10-26

Build: Windows CI run 40239053222, artifact 12616115870, source fdca5bd3db489e831e26a72183c481bd58307c5f (PR #8 merge-test ref).

Data: test-evidence/2026-10-26-gui/profile via THEMEDAI_LOCAL_DATA_ROOT. Separate WebView2 folder. Fixture worlds have no widgets, effects or wallpaper and refer to a nonexistent system theme.

Verified through Computer Use:
- Studio opens and lists GUI Blue and GUI Amber.
- Selecting Amber in preview mode leaves Blue active.
- Vertical scrolling reaches the world action buttons.
- Clicking Activate this world changes Amber to ACTIVE and disables the button as Active world.
- Independent disk read confirms active-scene.json contains gui-amber.
- Personal skins.json, themes.json, scenes.json and active-scene.json SHA256 hashes match the pre-test baseline. See personal-after-activation.json.

Pending: orderly tray Exit and restart persistence; simulated save failure and retry. The tray is not returned by Computer Use window enumeration, so a manual Exit was requested. No shutdown or recovery pass is claimed yet.

Automation limitation: offscreen accessibility button had no cached bounds. Scrolling and targeting the main screenshot worked. Capture immediately after actions can show stale state; a subsequent observation confirmed activation. No product defect established in this pass.

## Tray Exit regression found

After the user chose tray Exit, the main window disappeared and the log recorded Application Shutting Down at 21:11:42. PID 23968 remained running with MainWindowHandle=0 on repeated checks. The saved active world remained gui-amber. This is a failed process-exit check, not a successful restart test.

Code review found MainWindow.Closed omitted WorldRuntime.Dispose(), leaving scene subscriptions and atmosphere timers alive. Atmosphere window disposal also hides windows rather than closing them. The candidate fix disposes WorldRuntime before SkinManager, then calls Application.Current.Exit after the successful scene flush and main-window cleanup. Microsoft documents this API as shutting down the application: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.exit

Local WinUI compilation is blocked by NU1301: api.nuget.org cannot be resolved. The corrected binary still requires CI compilation and a GUI retest. The old process has been left untouched; no forced termination is counted as a successful exit.

PR #9 CI run 40352586897: Debug and Release checks passed at head 48f0a9a8c3f485515f8d752f624e35f652813b2f, zero annotations. The previous orphaned test process (PID 23968) was stopped after verifying its exact isolated-build executable path; this cleanup is not counted as a successful tray exit. Amber remained saved before cleanup. Corrected build GUI retest pending.

## Corrected build restart

Downloaded PR #9 artifact 12665016632 from run 40352586897; SHA256 b00d460ba89594b0c65fa5a94bdd76bccc4e8d79d25493dc0525e366c841bbbb verified. source-commit.txt records merge-test ref e555f66705f9cf78129af45878a387aa76828c9f. Started PID 32336 from builds/2026-10-26-shutdown-fixed with the same isolated profile and WebView folder.

GUI verified after reopening: Studio shows GUI Amber ACTIVE, ACTIVE WORLD, and the disabled Active world button. Restart persistence passes. Requested user-assisted tray Exit to verify corrected process termination; result pending.

## Corrected tray Exit verified — 2026-10-27

After the user reported choosing Exit on the corrected build, PID 32336 was absent. The isolated log records Application Shutting Down at 21:29:41 on 2026-10-26. No forced termination was issued for the corrected process. The isolated active-scene.json remains gui-amber. The preceding GUI observation verified Amber restored as ACTIVE after launch. This establishes eventual process termination; exit latency was not measured.

Personal skins.json, themes.json, scenes.json and active-scene.json still match the original SHA256 baseline; see personal-after-exit.json. PR #9 was reviewed and merged as 93d14959ab5dec6ef7a8bee2833377a9d88ef5b1 after Debug and Release CI passed. Copilot review was unavailable due to quota and is not counted as completed.

Remaining: simulated save failure, error dialog and retry; post-fix authenticated playback and broader desktop acceptance. Computer Use and app launches are paused at the user's request; background repository work remains authorized.
