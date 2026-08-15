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


## 🚀 Upcoming Phases (To Do)

### Phase 4 — Massive Custom Algorithm & Ultimate Customizability (New)
*Goal: Build an entirely in-house, highly robust personalized algorithm for deep semantic understanding, and offer unmatched widget customizability.*
- **Deep Semantic NLP Engine (In-House):**
  - Vastly expand the `ColorLexicon` and `WidgetLexicon` into a massive, multi-dimensional relational dictionary (2000+ terms, contextual clustering).
  - Emotion & Intent Detection: Implement algorithmic N-gram (bigram/trigram) mapping and semantic distance heuristics (e.g., custom Levenshtein/Jaro-Winkler hybrid logic) to identify user emotion without external models.
- **Ultimate Widget Customizability:**
  - Easy-to-use visual editor for widgets (adjusting layout, scaling, fonts, and data bindings without touching JSON).
  - QoL Improvements: Drag-and-drop widget arrangement, grid snapping, and WYSIWYG live previews.
- **Pioneering the Space:** Rainmeter has no AI skin generator; we will be the first to build a prompt-to-widget engine that generates layout and logic dynamically.

### Phase 5 — Intelligence & Polish (UX Angle)
*Goal: make the generator smarter and the editor feel professional*
- **Smarter NLP:**
  - Bigram support (e.g. "midnight blue", not "midnight" + "blue").
  - Fuzzy matching (Levenshtein distance) for typos.
  - Emoji signals (🌊, 🌙, 🍂).
  - Apply these same AI upgrades to the WidgetLexicon (~85 entries).
- **Palette editor upgrades:**
  - Harmony lock (drag one color, others adjust).
  - Contrast checker (WCAG AA/AAA).
  - Palette history (undo/redo).
- **UX & Conversational AI:**
  - Animated palette reveal on generation.
  - Keyboard shortcut Ctrl+G to generate from anywhere in the app.
  - Copy hex on click.
  - Conversational refinement for widgets — "make the clock bigger" patches the just-generated widget instead of starting over.
  - Make the existing WidgetAnalysisResult insights panel more visible.

### Phase 6 — Richer Widget Meters & External Data
*Goal: Make widgets vastly more capable and dynamic*
- **Richer meters:**
  - Conditional/threshold coloring (e.g. CPU bar turns red past 90%).
  - Per-core CPU, multi-drive disk.
  - Image/icon meter.
  - "Now playing" media meter via Windows' `GlobalSystemMediaTransportControlsSessionManager`.
  - **VibeFinderAI Integration**: Connect a specialized Music Player widget directly to the `vibefinderai.onrender.com` backend to automatically fetch and play AI-generated playlists that match the current desktop theme's vibe.
- **External data:**
  - One generic Web/JSON measure (point at a URL + a JSON path, poll on an interval).
  - Ship 2–3 presets (e.g. OpenWeatherMap integration) built on top of the generic Web/JSON measure.

### Phase 7 — Scheduled & Adaptive Theming
*Goal: the app does things automatically, not just on demand*
- **Time-based scheduling:** "Sunrise", "Noon", "Dusk", "Midnight" themes with smooth crossfade.
- **Weather-reactive themes:** Pull conditions and auto-select a matching theme.
- **System-reactive:** Follow Windows light/dark mode; battery saver mode triggers low-saturation dark theme.

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