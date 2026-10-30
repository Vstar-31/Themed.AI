# World package desktop acceptance — 2026-10-30

Tested feature commit: af69f038fbc1cd52f16a7e829bb5d8c7f482ffd1. The status-message fix in this report was built and tested afterward.

The app ran with THEMEDAI_LOCAL_DATA_ROOT and WEBVIEW2_USER_DATA_FOLDER pointing to a dedicated test directory. Synthetic World GUI original contained one disabled text widget. No palette or wallpaper was bound, so the live apply check did not change Windows appearance.

## Verified desktop behavior

- Export opened its disclosure and native Save As picker. The user selected the destination because PickerHost was not targetable by Computer Use. The app displayed World exported. The resulting 1,356-byte ZIP contained world.json and widgets/000.themedwidget.
- Import opened the native Open picker; the user selected that package. The app displayed the imported-copy confirmation and retained the original active scene.
- The imported scene and widget received new IDs. The imported widget remained disabled and its placement retained X=160, Y=180. The original scene and widget remained present.
- Selecting the imported world in Studio displayed Preview only and left the original active identity unchanged.
- Apply to desktop created the imported widget window at (160,180), size 220x120, enabled that widget and persisted the imported active scene. The original widget remained disabled. Logs reported one resolved placement and zero missing definitions.
- After confirming persistence to disk, the exact isolated process was terminated and relaunched. Studio restored the imported world with its ACTIVE badge, and the widget reopened at the same coordinates. This verifies restart persistence, not graceful tray-exit flushing.
- Scrolling exposed the lower Studio controls. Export/import messages and active/selected labels were readable in the default light palette.
- Personal skins.json, themes.json, scenes.json and active-scene.json existence and SHA-256 hashes matched the pre-test baseline after acceptance.

## Fix and regression checks

An active-scene notification refreshed Studio during Apply and replaced the result with Static world / Ready to apply. Select now preserves the operation status while apply is in progress and otherwise reports active or preview state accurately. This also preserves the missing-widget result from that refresh.

The fixed build completed with zero warnings and zero errors. All 801 Release tests passed, with no skipped tests. The rebuilt GUI opened and preview selection worked. The user requested no further computer control before the final Apply-message recheck; that visual recheck remains unverified.

## Coverage limits and environment notes

This fixture tested a simple text-widget world. Bundled images, wallpaper and palette round trips are covered by automated tests; live Windows wallpaper/accent changes, multiple monitors, DPI variations, network playback and native-picker cancellation were not exercised here. Earlier widget editor acceptance belongs to separate reports.

One initial launch exited in CoreMessagingXP.dll (0xc000027b). The synthetic fixture enum representation and launch window style were corrected before retry. Subsequent launches succeeded; the initial cause was not conclusively isolated. A global hotkey registration warning was also logged.

Local evidence is under test-evidence/2026-10-30-world-gui in the artifact workspace: export-verification.json, applied scene/widget snapshots, personal-final-check.json, status-fix-build.log and tests.log. Native picker steps were manual; the remaining interactions used Computer Use.
