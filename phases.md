# Themed.AI — Unified Phase Roadmap

## ✅ Completed Phases

### Phase 1 — Theme Foundation
WinUI 3 shell, MVVM architecture, Cozy Café design system, theme CRUD, JSON persistence, live ResourceDictionary hot-swap, system integration (wallpaper, DWM accent registry), settings page.

### Phase 2 — Vibe Generator
Full in-house NLP pipeline: Porter stemmer, VADER-lite sentiment, 280-word color lexicon, circular-mean hue aggregation, HSL palette harmonizer, creative name generator. Zero external dependencies, runs offline.

### Phase 3 — Widget Foundation & Interactivity (Formerly W0 & W1)
- [x] Fix the build (bug #1)
- [x] Fix DPI scaling and the hardcoded 1920×1080 assumption
- [x] Actually run it on a Windows machine once — confirm transparency, WorkerW attach, and click-through behave as expected
- [x] Clean the repo root
- [x] Right-click menu directly on a desktop widget (Edit / Disable / Reset position)
- [x] Multi-monitor–aware placement
- [x] Snap-to-edge/grid while dragging
- [x] Global hotkey (Win+Shift+W) to toggle widgets globally

### Phase 4 — Massive Custom Algorithm & Ultimate Customizability
*Goal: Build an entirely in-house, highly robust personalized algorithm for deep semantic understanding, and offer unmatched widget customizability.*
- [x] Expanded `ColorLexicon` to 313 entries and `WidgetLexicon` to 95 entries — short of the original "2000+" aspiration, but a deliberately curated set rather than a padded one; can still grow.
- [x] Emotion & intent detection: the `Services/NLP/EmotionAnalyzer` N-gram/semantic-distance attempt referenced in this bullet turned out to be dead code (zero references anywhere) and has been removed. The live pipeline (`Core/NLP/`: `BigramLexicon`, `FuzzyMatcher`, `EmojiSignalMap`, `MoodInferrer`) covers the same goal via a different, actually-wired-up route.
- [x] Visual widget editor (`SkinEditorPage`) — drag-to-reposition, live WYSIWYG preview canvas, full per-meter property panel. No raw JSON editing required.
- [x] Prompt-to-widget generation (`WidgetVibeGenerator`), including conversational refinement (see Phase 5).
- [x] Grid snapping in the widget editor specifically — not confirmed either way this session; the desktop drag-to-move (Phase 3) does snap, unclear if the in-editor canvas does too. (Implemented 10px snap)

### Phase 5 — Intelligence & Polish (UX Angle)
*Goal: make the generator smarter and the editor feel professional*
- [x] Bigram support, fuzzy matching (Levenshtein), and emoji signals — all live in `Core/NLP/`, applied to both the theme and widget (~85 target, now 95) lexicons.
- [x] Harmony lock (`PaletteHarmonizer`), WCAG AA/AAA contrast checker (`Utilities/ContrastChecker.cs`, unit-tested), palette history undo/redo (`Utilities/PaletteHistory.cs`, unit-tested).
- [x] Ctrl+G generates from anywhere on the Vibe page; copy-hex-on-click is wired to the clipboard.
- [x] Conversational refinement for both themes (`VibeThemeGenerator.Refine`) and widgets (`WidgetVibeGenerator.Refine`) — "make the clock bigger" patches in place instead of regenerating.
- [x] Animated palette reveal on generation — not checked this session. (Verified as already implemented)
- [x] WidgetAnalysisResult insights panel visibility — not checked this session. (Added right-hand insights panel to WidgetGeneratorPage)


## 🚀 Upcoming Phases (To Do)

### Phase 6 — Richer Widget Meters & External Data
*Goal: Make widgets vastly more capable and dynamic*
- **Richer meters:**
  - [x] Conditional/threshold coloring (e.g. CPU bar turns red past 90%) — applies to Bar, Graph, and now Icon meters; String meters get it too via "apply to text" toggle.
  - [x] Per-core CPU (`CpuCoreMeasure`, targets a core index), multi-drive disk (`DiskFree`/`DiskUsed` target a drive path).
  - [x] Image/icon meter — `MeterKind.Icon` + `IconGlyph`, built this session. Renders a Segoe Fluent Icons glyph, recolors on threshold cross like Bar/Graph. Glyph entry is free-text (paste from Character Map) rather than a picker — no hardcoded glyph-codepoint table, since that couldn't be visually verified without a Windows box.
  - [x] "Now playing" media meter (`MediaMeasure`) via `GlobalSystemMediaTransportControlsSessionManager`.
  - [ ] **VibeFinderAI Integration** — connecting a Music Player widget to `vibefinderai.onrender.com` to auto-play playlists matching the current theme's vibe. Not started; needs the actual API surface (auth, endpoints) confirmed against the live backend before it's worth building against.
- **External data:**
  - [x] Generic Web/JSON measure (`WebJsonMeasure`) — URL + JSON path, polled on an interval.
  - [ ] 2–3 shipped presets on top of it (Weather already exists as its own dedicated measure type rather than a WebJson preset, which arguably covers the spirit of this bullet — but no presets built explicitly on the generic WebJson path yet).

### Phase 7 — Scheduled & Adaptive Theming
*Goal: the app does things automatically, not just on demand*
- [x] Time-based scheduling — Sunrise/Noon/Dusk/Midnight, each mapped to any existing theme, with a smooth crossfade (`ThemeInterpolator` + `App.CrossfadeToThemeAsync`). Built this session; see `ThemeAutomationService` and Settings → Theme Automation.
- [x] System-reactive: follow Windows light/dark mode (`ISystemThemeIntegrator.GetCurrentSystemThemeAsync`, already existed) — now drives an automatic theme switch. Built this session.
- [x] System-reactive: battery saver mode triggers a designated theme (`PowerManager.EnergySaverStatus`) — takes priority over the other two rules. Built this session. Not implemented as "auto low-saturation" — the user picks any existing theme to switch to, rather than the app generating a desaturated variant on the fly.
- [ ] Weather-reactive themes — pull conditions and auto-select a matching theme. Not started; `WeatherMeasure` already has the OpenWeatherMap fetch/API-key plumbing a theme-level version could reuse.

### Phase 8 — Ecosystem & Sharing
*Goal: make themes and widgets shareable and discoverable*
- **Local Gallery:** Browse community-submitted themes/widgets bundled as a local JSON pack.
- **Remixing:** "Remix this theme" or "Remix this widget" button opens it in the editor pre-seeded.
- **Import/export v2:**
  - Export themes as `.cozy` and widgets as `.aiwidget` with file associations.
  - Export themes as CSS custom properties, Figma Variables JSON, and Windows Terminal settings.

### Phase 9 — Developer Mode
*Goal: turn Themed.AI into a tool devs actually use in their workflow*
- **Token export pipeline:** Export active theme as CSS custom properties, Tailwind config, Style Dictionary JSON, WinUI ResourceDictionary XAML.
- **CLI companion:** `themed generate "cozy autumn"`, `themed apply`, `themed export`.
- **VS Code extension (stretch):** Sidebar panel, autocomplete, preview swatches.

### Phase 10 — Packaging & Distribution
*Goal: make it something you can actually hand to someone*
- **Test WorkerW reparenting early:** Verify how `SetParent`-based reparenting works under a sandboxed MSIX package.
- **MSIX packaging:** Proper Store-ready package with publisher certificate.
- **winget package:** Submit to the Windows Package Manager index.
- **Auto-updater:** Check a GitHub releases JSON on launch.
- **Crash reporting:** Unhandled exception logging with a "Send report" button.
- **Installer wizard:** Simple WiX Toolset installer as an alternative to MSIX.