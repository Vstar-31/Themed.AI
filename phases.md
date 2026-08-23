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
- [x] Prompt-to-widget generation (`WidgetVibeGenerator`), including conversational refinement (see Phase 5). Bug fix this session: `BuildSkin` was setting `Enabled = true` on every generated widget, contradicting `CreateNewSkinAsync`'s "starts disabled, refine before use" pattern (its own doc comment claims parity with that pattern) and the existing test suite's explicit expectation — a freshly-generated widget was one app restart away from silently showing on the desktop unreviewed. Reverted to `false`; added `Generate_NewWidget_StartsDisabled` to pin it down.
- [x] Grid snapping in the widget editor specifically — not confirmed either way this session; the desktop drag-to-move (Phase 3) does snap, unclear if the in-editor canvas does too. (Implemented 10px snap)
- [x] Widget visual polish pass (not an original Phase 4 bullet, done ad hoc on request). `SkinHostWindow`'s card was using `SurfaceBrush` (semantically "interactive controls" per `CozyTheme`'s own doc comment) with no border, instead of the `CardBackgroundBrush` + 1px `BorderSubtleBrush` pattern every other card in the app uses (`CardStyle` in `ThemeResources.xaml`) — fixed, and mirrored into `SkinEditorPage`'s `PreviewFrame` so the WYSIWYG preview shows the right color (its existing 2px border was left alone; that's there for editor click-target visibility, not aesthetic replication). Bar fill is now a gradient between the theme's two accent tokens instead of flat; Graph got a fading area fill under the line (restructured to live inside the background Border's own Child, so WinUI's automatic child-clipping keeps the fill's square corners from poking past the rounded plate — a Grid-of-siblings couldn't do that); Icon meters now sit on a softly-tinted rounded chip instead of floating bare (glyph shrunk from 0.8× to 0.55× of the bounding box accordingly, so the chip's padding reads as padding). All of it pulls from existing theme brushes (`PrimaryAccentBrush`/`StrongAccentBrush`/alpha-cut versions of them) rather than new hardcoded colors, so it stays correct under any palette, not just Cozy Café. Applied identically in both `SkinHostWindow` (live) and `SkinEditorPage` (WYSIWYG preview) — those are two separately-maintained code-behind renderers, not a shared code path, so both needed the same edit or the editor would start lying about what a widget actually looks like. Deliberately did **not** add a drop shadow for card elevation — `SkinHostWindow`'s own class remarks already flag its transparency mechanism as a from-the-docs, never-tested-on-real-Windows technique, and stacking another unverified Composition-adjacent effect on top of that felt like too much unverified surface area to add blind. Not build- or visually-verified, per the standing caveat — worth a real look on Windows before trusting it, especially the Graph area-fill clipping, which is the geometry-heaviest change here.

### Phase 5 — Intelligence & Polish (UX Angle)
*Goal: make the generator smarter and the editor feel professional*
- [x] Bigram support, fuzzy matching (Levenshtein), and emoji signals — all live in `Core/NLP/`, applied to both the theme and widget (~85 target, now 95) lexicons.
- [x] Harmony lock (`PaletteHarmonizer`), WCAG AA/AAA contrast checker (`Utilities/ContrastChecker.cs`, unit-tested), palette history undo/redo (`Utilities/PaletteHistory.cs`, unit-tested).
- [x] Ctrl+G generates from anywhere on the Vibe page; copy-hex-on-click is wired to the clipboard.
- [x] Conversational refinement for both themes (`VibeThemeGenerator.Refine`) and widgets (`WidgetVibeGenerator.Refine`) — "make the clock bigger" patches in place instead of regenerating.
- [x] Animated palette reveal on generation — not checked this session. (Verified as already implemented)
- [x] WidgetAnalysisResult insights panel visibility — not checked this session. (Added right-hand insights panel to WidgetGeneratorPage)

### Phase 7 — Scheduled & Adaptive Theming
*Goal: the app does things automatically, not just on demand*
- [x] Time-based scheduling — Sunrise/Noon/Dusk/Midnight, each mapped to any existing theme, with a smooth crossfade (`ThemeInterpolator` + `App.CrossfadeToThemeAsync`). Built this session; see `ThemeAutomationService` and Settings → Theme Automation.
- [x] System-reactive: follow Windows light/dark mode (`ISystemThemeIntegrator.GetCurrentSystemThemeAsync`, already existed) — now drives an automatic theme switch. Built this session.
- [x] System-reactive: battery saver mode triggers a designated theme (`PowerManager.EnergySaverStatus`) — takes priority over the other two rules. Built this session. Not implemented as "auto low-saturation" — the user picks any existing theme to switch to, rather than the app generating a desaturated variant on the fly.
- [x] Weather-reactive themes — pull conditions and auto-select a matching theme. Built this session: `IWeatherConditionProvider`/`WeatherCondition` (`ThemeManager.Core`) is a new, independent abstraction — deliberately not sharing `WeatherMeasure`'s cache, just its OpenWeatherMap endpoint pattern — implemented by `OpenWeatherMapConditionProvider` (`ThemeManager.Integration`), which maps OpenWeatherMap's ~15 condition codes onto 6 buckets (Clear/Clouds/Rain/Thunderstorm/Snow/Fog). Wired into `ThemeAutomationService` at priority #2, after Battery Saver but before Light/Dark and time-of-day — a judgment call about relative priority, not a spec requirement, worth revisiting if it doesn't feel right in practice. Settings → Theme Automation → "Weather-reactive". Plus dynamic ("use my current location") geolocation via `Windows.Devices.Geolocation.Geolocator`, added in the widget-generator commit. Not build-verified — written on Linux with no `dotnet`/NuGet access in-session; needs a real build on Windows before it's trusted. *(Note: the most recent commit before this session — "WidgetGenerator NLP pipeline..." — contains changes that look like fixes for real Windows build errors: `PlatformTarget` added to every `.csproj`, an `x:Bind Mode=OneWay` compile error fixed in `SkinsPage.xaml` (x:Bind's default is OneTime; OneWay/TwoWay requires the bound type to support change notification, which the list-item wrapper apparently doesn't for `Name`/`Enabled`/`Opacity`/`ClickThrough`/`Locked` — those bindings now read once and rely on the existing `Toggled`/`ValueChanged` handlers to persist edits instead of live-updating), `MainWindow` min-size enforcement. That's inferred from the diff, not confirmed — if it really was a green Windows build, this line is stale and Phase 7 is more trustworthy than it says.)*


## 🚀 Upcoming Phases (To Do)

### Phase 6 — Richer Widget Meters & External Data
*Goal: Make widgets vastly more capable and dynamic*
- **Richer meters:**
  - [x] Conditional/threshold coloring (e.g. CPU bar turns red past 90%) — applies to Bar, Graph, and now Icon meters; String meters get it too via "apply to text" toggle.
  - [x] Per-core CPU (`CpuCoreMeasure`, targets a core index), multi-drive disk (`DiskFree`/`DiskUsed` target a drive path).
  - [x] Image/icon meter — `MeterKind.Icon` + `IconGlyph`, built this session. Renders a Segoe Fluent Icons glyph, recolors on threshold cross like Bar/Graph. Glyph entry is free-text (paste from Character Map) rather than a picker — no hardcoded glyph-codepoint table, since that couldn't be visually verified without a Windows box.
  - [x] Circular/ring gauge meter — `MeterKind.Ring` (not an original Phase 6 bullet; added on request after researching what makes Rainmeter's most-imitated skins recognizable — the circular percentage gauge, especially in "Jarvis"/HUD-style setups, was the single biggest visual gap versus Bar/Graph/Icon/String). Same fill-fraction-from-BarMax data as Bar (`RingMeterViewModel` mirrors `BarMeterViewModel` almost exactly); only the shape differs. Rendered via `Path` + `ArcSegment` (the standard WinUI technique for a partial ring) rather than the `Ellipse`+`StrokeDashArray` trick — dash-array units are relative to `StrokeThickness` in WinUI/UWP, and getting that unit conversion subtly wrong blind felt riskier than the explicit trigonometry `BuildRingVisual` uses instead. Wired end-to-end: `MeterKind` enum, `RingMeterViewModel`, `SkinHostViewModel`'s construction switch, live rendering in `SkinHostWindow`, matching WYSIWYG rendering in `SkinEditorPage` (static, computed once from `PreviewFraction` rather than redrawn from a ticking measure), the "+ Ring" button (both copies — editor has a duplicate button row), and `UsesBarMax`/default-size wiring so the editor's BarMax field and default 60×60 size both make sense for it.
    - Also added `MeterDefinition.CenterText` (a plain bool, same pattern as `Bold`) — String meters were left-aligned only, no way to center, which meant a percentage label couldn't cleanly sit over a Ring gauge. Small, reusable, threaded through the same places as `Bold` (model → `StringMeterViewModel` → both renderers → editor checkbox), not a one-off hack scoped to just the Ring showcase below.
    - New built-in preset, **System Rings** (`builtin-system-rings`) — three Ring gauges (CPU/RAM/Disk) with a `CenterText` percentage over each and a caption below, demonstrating the intended "compose from independent meters" usage rather than a special-cased "ring with a built-in label" kind. Uses `DiskUsed`, not `DiskFree` (which `CreateSystemMonitor` above uses) — `DiskFree`'s value is *free* space, so an 85% threshold on it would fire backwards for a gauge that's supposed to read as "how full is this"; `DiskUsed` is the one where all three rings' threshold means the same thing. Test added (`CreateSystemRings_UsesDiskUsedNotDiskFree`) specifically to pin that down, since it's exactly the kind of thing that's easy to get backwards silently.
    - Not build- or visually-verified, per the standing caveat — this one especially: the arc geometry (start/end point trig, `IsLargeArc` at the 180° boundary, the 99.9%-clamp sidestepping ArcSegment's degenerate full-circle case) is the most math-heavy rendering code in the project so far and the one I'd most want a real Windows box to confirm before trusting.
  - [x] "Now playing" media meter (`MediaMeasure`) via `GlobalSystemMediaTransportControlsSessionManager`.
  - [x] **VibeFinderAI Integration** (partial) — `VibeFinderMeasure` (`ThemeManager.Integration`) calls Vijay's own `vibefinderai.onrender.com` backend for a track recommendation matching a typed vibe phrase. The live Render deployment wasn't reachable from this environment to verify directly, so the API surface was confirmed by reading the backend's own FastAPI source instead (`github.com/Vstar-31/vibefinderai`, `backend/main.py`): `POST /auth/token` (OAuth2 password flow, form-encoded) → bearer JWT, then `POST /api/vibe/analyze` `{text, track_limit}` → `{dominant_vibe, tracks: [{title, artist, ...}]}`. Three new measure types — `VibeTrackTitle`/`VibeTrackArtist`/`VibeMood` — wired into `MeasureFactory` and the skin editor (auto-companion: requesting the title pulls in the artist, same as Time→Date); NLP trigger words "vibe"/"recommend"/"mood" added to `WidgetLexicon` (stems verified by porting `PorterStemmer.cs` to Python and running it, not guessed). Target format is `"username|password|vibe text"` — typed per-widget, no Settings-page credential storage added, same trade-off Weather/WebJson already accept for their own secrets. Not build- or live-verified — same caveat as the weather work above.
    - [ ] Follow the *active theme's* vibe automatically instead of a fixed typed phrase — needs theme context threaded into `IMeasure` construction, which nothing has today (`MeasureFactory.Create` only ever sees a `MeasureDefinition`).
    - [ ] Actual playback ("auto-play playlists") — needs a playback-mechanism decision (open the track's own link in a browser vs. an embedded preview-URL player vs. driving Spotify directly via VibeFinderAI's own `spotify_routes.py` OAuth flow) that's a product call for Vijay, not a technical blocker.
- **External data:**
  - [x] Generic Web/JSON measure (`WebJsonMeasure`) — URL + JSON path, polled on an interval.
  - [x] 2–3 shipped presets on top of it — `WebJsonPresets` (`ThemeManager.Core`) plus a presets `ComboBox` in the skin editor, shown only for WebJson measures. Three presets, each URL/path checked against current API docs rather than guessed: this repo's own GitHub star count, Bitcoin price (CoinGecko's key-free demo tier), and a random-advice API. All plain unauthenticated GETs, so `WebJsonMeasure` itself needed no changes.

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