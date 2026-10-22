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


## Authoring slice — 2026-10-21

Visual state editing and anchor/responsive-rule editing are implemented in the widget editor.
Preview uses the same state controller and anchor resolver as the host, with explicitly simulated
inputs. State ordering, duplication, optional overrides and definition validation are exposed.
Windows build verification is recorded separately from the deferred automated/GUI tests.
This does not close milestones 3–5: mixed-DPI/topology recovery, full layers tooling,
and end-to-end authoring acceptance checks remain open.

### History and layout tools — 2026-10-21

Draft undo/redo, meter ordering within a parent, multi-select alignment, and equal-gap distribution
are implemented. Compound edits and drags form one history step. Preview stacking follows the host;
drag deltas use the parent canvas coordinates. See [editor tools](WIDGET_EDITOR_TOOLS.md) for controls
and limits. Windows build/publish verification is separate from deferred automated and GUI tests.
Asset browsing, template remix, and keyboard-only acceptance remain open; the following slice adds the layer tree.

### Layer tree and duplication — 2026-10-21

The editor now shows groups and meters in a front-to-back tree with synchronized selection, group/root
reordering, and independent duplication of meters or whole groups. These operations participate in
draft history. Group copies retain transforms, variables, and copied meter states, with fresh IDs and
remapped membership. Measures and asset references remain shared. Preview group stacking now uses the
host's maximum member ZIndex. Automated and GUI checks remain deferred; Windows build/publish is recorded
separately. Drag reparenting, visibility/lock controls, nested groups, and bulk tree selection are not included.

### Image asset browser — 2026-10-21

The editor now imports managed image copies, browses library/package images with searchable thumbnails,
shows missing draft references, and applies or adds image meters through undoable actions. Content-based
reuse, bounded copying/inventory, source-file independence, and input/error handling have automated coverage.
The full suite passes 660 tests and the separate editor logic harness passes 37; Windows build verification
does not close GUI acceptance. See [asset browser](WIDGET_ASSET_BROWSER.md) for limits. Reference-aware cleanup,
template remix, advanced tree manipulation, and keyboard-only acceptance remain open.

### Local template remix and editor regression integration — 2026-10-22

Bundled starter browsing/search and per-widget Remix now open independent unsaved drafts. First Save
creates a new persisted widget; discard leaves the source and widget inventory intact. IDs and group
membership are remapped, shared mutable models are cloned, and copied window flags start editable.
The existing export flow packages saved remixes. See [remix](WIDGET_REMIX.md).

Editor logic regressions now live in the main repository test project and run with the existing Windows
CI script. All 708 tests pass, including a save-concurrency regression: changes entered while a save is
pending remain unsaved and the editor stays open. Desktop GUI acceptance and real manager/UI integration
are not inferred from these tests. Remote gallery remix, visual template thumbnails, asset cleanup, and
advanced layer/keyboard workflows remain open.

### Blank drafts and draft export — 2026-10-22

Blank-widget creation now uses unsaved drafts and creates no stored widget on cancel/discard. The editor
can export an independent draft snapshot without saving/enabling it, while preserving dirty state and
undo history. Added six regressions; an invalid-data exception escaping the export handler was reproduced
and fixed, with the same catch correction applied to image imports. All 714 tests pass. GUI/native picker
verification remains outstanding; Windows publish verifies compilation only.

### Keyboard movement and source-loss recovery — 2026-10-22

Ctrl+D duplicates the selected layer. Ctrl+Alt+arrows move layers or compatible multi-selected meters
by 1 DIP (Shift: 10 DIP), with each move undoable. Shortcuts guard text/control focus and modal flows.
If an original widget disappears before Save, the editor can retain all current edits as a new unsaved
remix, requiring another explicit Save. This is session recovery, not persistent draft autosave.
Added 15 regression cases; all 729 tests and Windows x64 publish pass. GUI focus/routing, recovery
interaction, and full keyboard-only authoring acceptance remain unverified.

### Visual starter previews and discovery — 2026-10-22

The local starter picker now shows a scaled visual sample, description and result count, with
multi-word prefix search across names, descriptions, labels, tags, measures and meter kinds. Hidden
selections clear when filtering. Static text/bar/ring/graph visuals are shared with the editor;
graph samples now show an illustrative line and shaded area. Browsing starts no measures or hosts.
Added 19 catalog/search/sample cases. All 748 tests and Windows x64 publish pass. Preview appearance,
theme contrast, small-window scrolling, keyboard focus and selection routing still need GUI checks.
Complex/image/web starter previews, remote gallery package remix and asset cleanup remain open.
