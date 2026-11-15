# Desktop World activation on Windows, 16 November 2026

Tested desktop commit `4bbac5f` in a Windows x64 Debug build. The local suite passed 835 tests. The test used a separate app-data directory and WebView profile, two one-widget worlds, no wallpaper and no matching Windows palette.

| Scenario | Observed result |
| --- | --- |
| Repair an old mismatched active ID | The initial card said QA Cozy Clock while a live CPU/RAM/disk monitor was visible. Reapply active world replaced it with the clock. |
| Switch worlds from More tools → Desktop World | Alternated System Monitor and Cozy Clock 20 times. On each click, the GUI showed the expected live widget, the requested world reported one applied widget, and the persisted active ID matched the only enabled widget. Both fixtures used the same position (20, 25). |
| Active ID write fails | Placed an empty directory at the isolated active ID temporary-file path. Switching Clock → Monitor reported a write error. The clock was visible again, and both the active ID and saved enabled widget remained Clock. |
| Retry and restart | Removed the test obstruction and retried. The live monitor appeared. Terminated only the identified QA process and relaunched the same executable and profile. The monitor was visible again and Desktop World marked QA System Monitor active. |

The switch test used one display and two native widget types. Appearance changes, streamed playback and long-session rendering are separate acceptance checks. The observed rollback covers the widget layout and active world identity.
