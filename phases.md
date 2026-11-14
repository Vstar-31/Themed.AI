# Themed.AI project report and phases

**14 November 2026 · Windows desktop · baseline `master` at `71872f6`**

Themed.AI can generate a palette offline, build and recover a desktop widget, package a world, restore it on restart, and play music through the embedded VibeFinder app. These workflows have Windows evidence. The current deliverable is an unpackaged Windows x64 build. The largest uncertainties before a public release are long-running rendering, real display changes, endpoint hardening, and installation on a clean machine.

**Planning estimate:** 2–4 working weeks to a limited tester beta and 6–10 working weeks total to a signed public v1, assuming one full-time developer, a frozen feature set, test hardware and signing access. The beta date depends especially on reproducing the rendering problem. See the [future action plan](docs/FUTURE_ACTION_PLAN.md) for scope, sequencing, and the longer platform estimate.

## What is on the stable branches

| Product | Baseline reviewed | What belongs here |
| --- | --- | --- |
| Themed.AI desktop | [`master` at 71872f6](https://github.com/Vstar-31/Themed.AI/commit/71872f676cb0710e07601734d78529a8d78bbe78) | WinUI shell, runtime, Studio, editor, native playback and session bridge. Use this branch to build the desktop. |
| VibeFinder desktop module | [`Themed.AI` at fb2868b](https://github.com/Vstar-31/vibefinderai/pull/17) | Embedded web UI. Netlify published this revision. The last change improved genre labels and the filter hint. |
| Shared recommendation backend | [VibeFinderAI PR 14](https://github.com/Vstar-31/vibefinderai/pull/14) | The shared service runs from VibeFinderAI `main`. Ambiguous artist detection and sentiment confidence were fixed for both clients. Standalone frontend development is a separate project. |

## Progress by phase

The original plan used phase 10 twice. **10A** below means the widget runtime and **10B** means release engineering. Worlds and the music integration span several phases and have their own entries.

| Phase | Delivered | Remaining or qualifying evidence |
| --- | --- | --- |
| 1. Theme foundation | WinUI shell, themed controls, theme CRUD and JSON storage, live palette changes, built-in palettes, wallpaper and accent integration. Theme-library writes now publish changes after persistence succeeds. | Verify actual Windows accent/wallpaper restore and failure messages on release hardware. [Save integrity](docs/THEME_SAVE_INTEGRITY_2026-10-30.md) |
| 2. Offline vibe generation | Prompt-to-palette pipeline with stemming, sentiment, color lexicon, hue aggregation, harmonization and theme naming. | Keep the offline path in the clean-install smoke test. |
| 3. Desktop widgets | Desktop hosts, placement, click-through, context actions, edge/grid snapping, visibility shortcut and per-widget settings. | Mixed DPI, monitor removal/reconnect and hotkey conflicts need a repeatable Windows pass. |
| 4–5. Generation and editing | Prompt-to-widget generation and refinement, fuzzy/bigram/emoji matching, palette history and contrast tools. The editor supports layer selection and ordering, group duplication, alignment, distribution, placement preview and switching compatible meter styles. | Complete four representative widgets using the keyboard and verify focus, validation, transformed groups and host/editor agreement. [Editor tools](docs/WIDGET_EDITOR_TOOLS.md) |
| 6. Data and meters | Text, bars, graphs, icons, rings and images. Sources include CPU, memory, disk, network, now-playing, web JSON and music recommendations. | Check live behavior when a provider, device or network becomes unavailable. |
| 7. Adaptive themes | Scheduled themes, Windows appearance and battery rules, weather rules, optional location and cached weather. A key-free city lookup and live Jaipur weather widget worked. | Exercise physical battery and location transitions, offline fallback and stale readings. Revisit provider terms/capacity if distribution changes. [Weather setup](docs/WEATHER-PROVIDER.md) |
| 8. Sharing and remix | Bundled local gallery, five visual widget starters, search, editable copies, managed image assets and portable widget/world packages. Imported copies get new identities. | Cross-machine portability of complex packages, remote gallery, associations and reference-aware asset cleanup remain open. [Widget packages](docs/WIDGET_PACKAGES.md) · [World packages](docs/WORLD_PACKAGES_2026-10-29.md) |
| 9. Developer tools | The theme and widget models already underpin the app and package formats. | Token exporters, CLI and optional VS Code tooling remain roadmap work. |
| 10A. Runtime and authoring | Per-widget scheduling, persisted schema migrations, validation, groups/transforms, formulas, visual states, anchors and responsive rules. Editor has image browsing, local checkpoints, undo/redo and draft export. Initial plugin contracts and loader exist. | Measure resource usage, verify real monitor topology, and finish extension compatibility, permissions, diagnostics and sample integrations. [States and anchors](docs/WIDGET_STATES_AND_ANCHORS.md) |
| Worlds and Studio | World composition, selection vs. application, active-state persistence, placement restoration, atmosphere support, serialized activation, duplicate-widget repair and tray shutdown fixes. | Long-session rendering and rapid switching under combined playback/display changes need further testing. |
| Embedded VibeFinder | Saved-account startup, prompt input preservation, authenticated recommendations, playlist/history loading, native/web playback coordination, compact player and narrow-window layout. Expired analysis sessions renew in place and retry once. | Recheck final genre styling in the desktop; verify failure/reconnect for library and service calls independently of analysis renewal. |
| 10B. Distribution and support | Windows Debug/Release CI, downloadable x64 artifact, local logs, help, privacy and terms documents. | Signed installer, update/rollback, uninstall, third-party notices, final privacy/retention review and clean-machine acceptance remain open. [Windows testing](docs/WINDOWS_TESTING.md) |

## What actually passed

| Workflow | Evidence and limits |
| --- | --- |
| Build and tests | 833 desktop tests passed, with Windows Debug and Release CI. The clean Release rebuild reported zero warnings/errors. The module passed 22 frontend tests and a production build. The sentiment fix passed 28 backend tests on each reviewed branch. These are separate suites. [Desktop CI](https://github.com/Vstar-31/Themed.AI/actions/runs/43278411795) · [Module CI](https://github.com/Vstar-31/vibefinderai/actions/runs/43553782270) |
| Widget creation | On Windows, all five starter previews, search and empty state, scrolling, light/dark contrast, duplication, keyboard movement and undo/redo were exercised. A saved widget survived restart. Failed save preserved the draft and retry succeeded. Draft export and native image import used assisted Windows pickers. [Editor acceptance](docs/GUI_ACCEPTANCE_2026-10-23.md) |
| Recovery | An interrupted isolated process left a recoverable draft. Reopening created an independent copy. A checkpoint write failure preserved the earlier checkpoint, retry worked, and explicit discard removed the checkpoint while preserving the original widget. Multi-selection snap, undo and redo were also observed. [Recovery checks](docs/EDITOR_RECOVERY_2026-10-27.md) |
| Worlds | Twenty alternating world selections passed on an October build at human pace. A simple exported world reimported with fresh IDs, applied at its saved coordinates and survived restart. The corrected tray Exit terminated the process. These runs used isolated profiles. [World round trip](docs/WORLD_PACKAGE_GUI_2026-10-30.md) · [Tray exit](docs/GUI_ACCEPTANCE_2026-10-26.md) |
| Music | Real GUI typing and correction preserved a 62-character prompt. Play All, two pause/resume cycles, next, collapse/expand and native minimize/restore passed. Applying two worlds during playback preserved the queue. The rainy-night prompt returned chill at 62% after the backend fix. [Playback and renewal](https://github.com/Vstar-31/Themed.AI/pull/16) · [Recommendation fix](https://github.com/Vstar-31/vibefinderai/pull/15) |
| Session expiry | An isolated build returned one synthetic 401 to the visible analysis POST. Real authentication returned 200, the retry returned 200, and the same page showed results. The temporary hook was removed. The scenario covers the analysis flow. [Module recovery](https://github.com/Vstar-31/vibefinderai/pull/16) |

## Open findings that affect readiness

- **Long-running rendering.** On 1 November, the user confirmed missing glyphs and dark blocks on the physical display after the app had been left running. Restart restored readability. A shorter November 14 run did not reproduce it. The cause and a durable fix remain open.
- **Final visual check.** The last module change made genre labels opaque and raised the hint from 8 to 11 px. Production deployment and CI passed. The signed-in native visual recheck remains pending.
- **Security.** Policies were approved and endpoint changes prepared. Desktop PR 13 covers the backend snapshot. VibeFinderAI PRs 2 and 3 cover the live backend branch and module mirror. All three were open at review, and GitHub reported conflicts on both VibeFinderAI PRs. An audit of every deployed route is still outstanding. [Desktop PR 13](https://github.com/Vstar-31/Themed.AI/pull/13) · [Backend PR 2](https://github.com/Vstar-31/vibefinderai/pull/2) · [Module PR 3](https://github.com/Vstar-31/vibefinderai/pull/3)
- **Branch housekeeping.** Desktop PRs 2 and 12 are still open against historical feature branches. Their heads were already ancestors of the reviewed desktop master. Reconcile the PRs and their descriptions before treating them as remaining implementation. [Desktop PR 2](https://github.com/Vstar-31/Themed.AI/pull/2) · [Desktop PR 12](https://github.com/Vstar-31/Themed.AI/pull/12)
- **Public-release evidence.** A clean-machine installation, upgrade preserving real-world data shapes, rollback, multi-monitor matrix and long-duration performance baseline remain unverified. The help and legal files exist; retention, deletion, provider disclosures and third-party notices need a factual review before public distribution.

For earlier implementation detail, see the [original phase journal at the reviewed commit](https://github.com/Vstar-31/Themed.AI/blob/71872f676cb0710e07601734d78529a8d78bbe78/phases.md) and the [desktop platform acceptance criteria](docs/RAINMETER_ROADMAP.md).
