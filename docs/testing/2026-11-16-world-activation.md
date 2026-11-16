# Desktop World activation on Windows, 16 November 2026

Tested desktop commit `4bbac5f` in a Windows x64 Debug build. The local suite passed 835 tests. The test used a separate app-data directory and WebView profile, two one-widget worlds, no wallpaper and no matching Windows palette.

| Scenario | Observed result |
| --- | --- |
| Repair an old mismatched active ID | The initial card said QA Cozy Clock while a live CPU/RAM/disk monitor was visible. Reapply active world replaced it with the clock. |
| Switch worlds from More tools → Desktop World | Alternated System Monitor and Cozy Clock 20 times. On each click, the GUI showed the expected live widget, the requested world reported one applied widget, and the persisted active ID matched the only enabled widget. Both fixtures used the same position (20, 25). |
| Active ID write fails | Placed an empty directory at the isolated active ID temporary-file path. Switching Clock → Monitor reported a write error. The clock was visible again, and both the active ID and saved enabled widget remained Clock. |
| Retry and restart | Removed the test obstruction and retried. The live monitor appeared. Terminated only the identified QA process and relaunched the same executable and profile. The monitor was visible again and Desktop World marked QA System Monitor active. |

The switch test used one display and two native widget types. Appearance changes, streamed playback and long-session rendering are separate acceptance checks. The observed rollback covers the widget layout and active world identity.

## Headless follow-up on world themes

The Release x64 build passed 841 tests. Six cases ran the same activation code used by Studio and Desktop World, with real JSON repositories in temporary folders, an in-memory widget manager and a recording Windows settings adapter.

- Blocking the active-ID write restored the old widget selection, kept the app palette, and produced zero Windows settings calls. Retrying applied the target palette, mode, accent and world wallpaper.
- When Windows rejected the requested settings, the new world and layout stayed active. The result reported both the theme and wallpaper failures. A separate test forced a theme listener to throw after the save and verified that the new layout and active-world notification survived.
- Linked themes respected both sync toggles independently. An explicit world wallpaper took precedence, with the opted-in theme wallpaper used when the world file was missing.

Actual Windows colors and wallpaper still need a visual check in an isolated desktop session.
