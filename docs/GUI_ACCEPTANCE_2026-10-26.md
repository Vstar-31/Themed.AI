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
