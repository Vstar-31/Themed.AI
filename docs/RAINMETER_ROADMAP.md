# Themed.AI desktop platform roadmap

The goal is a Rainmeter-class desktop customization platform with a visual editor, coherent
worlds, accessible themes and portable widgets. Success means people can build, use, recover
and share a desktop without hand-editing configuration files. Rainmeter compatibility is a
separate interoperability feature; current `.themedwidget` files are not `.rmskin` packages,
and existing Rainmeter DLL plugins do not run in Themed.AI.

## Baseline and evidence

Base: `feature/windows-app-theming` at `ebc5140`. The core suite ran successfully in the Linux
cloud environment: 415 tests, none skipped. Groups, formulas, plugin contracts and a plugin
loader already exist. These foundations need integration and lifecycle validation rather than
being rewritten. The WinUI UI requires a Windows build and real desktop checks.

The historical notes in `phases.md` mix completed implementation, old caveats and proposed work.
Use the milestones below for new delivery decisions. A milestone is complete only when its
acceptance checks pass; implemented code alone does not establish desktop behavior.

## Delivery sequence

| Milestone | Deliverable | Acceptance gate |
| --- | --- | --- |
| 1. Dependable desktop | Serialized world activation; cancellation of superseded activations; rollback after partial failure; persistence error reporting; clean shutdown/restart | Rapidly switch 20 worlds, restart, interrupt a save, and recover without duplicate windows or lost layouts. Verify VibeFinder navigation/playback recovery. Run Windows build/tests. |
| 2. Portable creation and sharing | Image meters; packaged assets; import/export controls; remix identities; gallery integration | Export an image-bearing grouped widget, remove its original image, import on another machine, edit and re-export. Invalid/oversized packages leave no partial install. No imported widget starts automatically. |
| 3. Responsive placement | Monitor identity, work-area anchors, offsets, DPI-aware positioning and topology-change recovery | Test 100/150/200% DPI, negative monitor coordinates, taskbar edges, monitor removal and reconnect. Widgets remain reachable and drag adjustments persist. |
| 4. Interactive runtime | Declarative hover/pressed/active/threshold states; explicit action contracts; bounded transitions; dependency-ordered formula updates | Editor and host agree on state changes. Cycles and invalid expressions produce useful diagnostics. Disabling a widget cancels its pending work. |
| 5. Authoring studio | Shared meter composition; layers; multi-select alignment/distribution; undo/redo; asset browser; live validation and template remix | Complete clock, launcher, system dashboard and music widgets entirely in the editor. Undo/redo restores both structure and properties. Keyboard-only authoring works. |
| 6. Extension ecosystem | Versioned extension API; capability declarations; load/error diagnostics; trusted plugin installation; documented samples | A sample measure, meter and action work through host and editor. Incompatible plugins are diagnosed. Distinguish code loaded in-process from any future isolated plugin host. |
| 7. Release and interoperability | Signed installation/update path; clean uninstall; recovery/export of user data; documented Rainmeter import subset | Clean Windows VM installation, upgrade and rollback preserve user data. A compatibility report lists unsupported Rainmeter directives instead of silently dropping them. |

Milestone 1 remains the release gate while the bounded sharing work in milestone 2 lands.
Avoid adding another rendering engine or promising arbitrary Rainmeter Lua/DLL compatibility
before the native runtime and lifecycle are stable.

## Current implementation slice: widget packages

Implemented on `feat/widget-package-workflow`:

- Image meters with local image selection and a shared preview/desktop image renderer.
- `.themedwidget` export bundles referenced raster images and deduplicates repeated references.
- Import validates the archive and definition, stages assets in a fresh directory, preserves
  groups and current schema data, assigns a new widget identity, and keeps it disabled.
- Export omits built-in network/plugin connection targets and writes atomically. Custom links,
  text and variables remain author-controlled; this is not a general-purpose data scrubber.
- Widgets page import/export controls, with explicit persistence failure reporting for imports.
- Regression coverage for portable images, groups, schema migration, unsafe paths, duplicate
  entries, null definitions, archive limits, cancellation and failed writes.
- Windows CI selects the SDK from `global.json` as well as installing the .NET 8 toolchain.

Remaining in milestone 2: gallery package browsing/remix, file associations, world-level
packages, reference-aware asset cleanup and Windows UI validation. See `WIDGET_PACKAGES.md`.

## Performance and quality checks

Measure startup, idle CPU, working set and tick latency on a named Windows machine with
1, 10 and 50 representative widgets before setting regression thresholds. Record warm and cold
runs separately and identify network-bound measures. Do not infer resource use from unit tests.

For theme quality, assess rendered desktops alongside palette tests: readability, distinctiveness,
widget hierarchy and legibility over light/dark wallpapers. Keep offline generation useful;
external services should enhance specific widgets without blocking startup.

A release candidate needs passing automated tests plus the Windows desktop matrix above.
This roadmap does not claim those manual checks or distribution work are complete.
