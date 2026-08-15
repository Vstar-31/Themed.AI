Themed.AI — Phase Roadmap

What we shipped
Phase 1 — Foundation
WinUI 3 shell, MVVM architecture, Cozy Café design system, theme CRUD, JSON persistence, live ResourceDictionary hot-swap, system integration (wallpaper, DWM accent registry), settings page.
Phase 2 — Vibe Generator
Full in-house NLP pipeline: Porter stemmer, VADER-lite sentiment, 280-word color lexicon, circular-mean hue aggregation, HSL palette harmonizer, creative name generator. Zero external dependencies, runs offline.

Phase 3 — Intelligence & Polish
Goal: make the generator smarter and the editor feel professional
Smarter NLP

Bigram support — "midnight blue" and "rose gold" should be detected as compound signals, not two separate weak words. Currently "rose" + "gold" fire independently; bigrams would lock them together for a much stronger, more accurate signal.
Fuzzy matching — catch typos and near-misses ("oceanic" → ocean, "forestlike") using Levenshtein distance on the lexicon keys. Python equivalent of difflib.get_close_matches.
Emoji signals — 🌊 → ocean, 🌙 → midnight, 🍂 → autumn. Huge for mobile-style input.
Multilingual input — map common Spanish, French, Japanese vibe words to the same signals. No translation model needed, just extend the lexicon.

Palette editor upgrades

Harmony lock — when you drag one color, the other 7 adjust to maintain the computed HSL relationships. Like Figma's "link" icon but for semantic color roles.
Contrast checker — live WCAG AA/AAA pass/fail badges next to each text/background pair. Essential for accessibility.
Palette history — undo/redo stack for color edits, local to the editor session.

UX

Animated palette reveal on generation — swatches slide in one by one with a stagger animation instead of appearing all at once.
Keyboard shortcut Ctrl+G to generate from anywhere in the app.
Copy hex — click any swatch to copy its hex to clipboard.


Phase 4 — Theme Ecosystem
Goal: make themes shareable and discoverable
Local theme gallery

Browse community-submitted themes bundled as a local JSON pack (shipped with the app, updated via a simple GitHub raw file fetch — no backend needed).
One-click install into your local theme list.
"Remix this theme" button opens it in the Vibe editor pre-seeded.

Import/export v2

Export as a .cozy file (just renamed JSON, but with a file association so double-clicking it imports automatically).
Export as CSS custom properties — --bg-base: #F5F1EA; — for devs who want to use their palette in a web project.
Export as Figma Variables JSON — paste into Figma to theme a design file.
Export as Windows terminal settings.json color scheme.

Theme variants

Auto-generate a dark variant of any light theme (and vice versa) by inverting the HSL lightness curve while preserving hue and saturation relationships.
One click, instant preview, saveable as a linked pair.


Phase 5 — Scheduled & Adaptive Theming
Goal: the app does things automatically, not just on demand
Time-based scheduling

"Sunrise" theme 06:00–10:00, "Noon" 10:00–17:00, "Dusk" 17:00–20:00, "Midnight" 20:00–06:00.
Uses a background DispatcherTimer + Windows Task Scheduler registration for when the app isn't running.
Smooth crossfade transition between themes (interpolate hex values over 30 seconds).

Weather-reactive themes (optional, free API)

OpenWeatherMap has a free tier (1000 calls/day). Pull current conditions and auto-select a matching theme — rainy → dark teal, sunny → warm gold, snow → arctic white.
Fully optional, off by default, user opts in.

System-reactive

Follow Windows light/dark mode toggle automatically — maintain two linked theme variants and switch when Windows switches.
Battery saver mode → auto-switch to a low-saturation dark theme.


Phase 6 — Developer Mode
Goal: turn Themed.AI into a tool devs actually use in their workflow
Token export pipeline

Export active theme as:

CSS custom properties
Tailwind theme.extend.colors config
Style Dictionary JSON (feeds Adobe, Figma, iOS, Android)
WinUI ResourceDictionary XAML (so devs can drop the output straight into their own WinUI app)



CLI companion (themed command)

themed generate "cozy autumn" → prints hex palette to stdout
themed apply --theme "Midnight Ocean" → sets wallpaper + accent, no UI needed
themed export --format css > theme.css
Built as a separate .NET console app in the same solution, sharing ThemeManager.Core.

VS Code extension (stretch)

Sidebar panel showing the active Themed.AI palette.
Autocomplete for token names in CSS/XAML files.
Preview swatch on hover for hex values.


Phase 7 — Packaging & Distribution
Goal: make it something you can actually hand to someone

MSIX packaging — proper Store-ready package with publisher certificate.
winget package — submit to the Windows Package Manager index so winget install ThemedAI just works.
Auto-updater — check a GitHub releases JSON on launch, prompt to download if a newer version is available. No Squirrel or Sparkle needed — just HttpClient + Process.Start.
Crash reporting — write unhandled exceptions to a local log file with a "Send report" button that opens a pre-filled GitHub issue. No telemetry, no cloud.
Installer wizard — simple WiX Toolset installer as an alternative to MSIX for users who can't sideload.


Suggested sequencing
Phase 3  ←── highest ROI, builds directly on what exists
Phase 4  ←── community angle, makes the project feel alive
Phase 5  ←── the "wow" feature for regular users
Phase 6  ←── opens a completely different audience (devs)
Phase 7  ←── do this last, when the product is stable



Phase W0 — Make it real (do this before anything else)

Fix the build (bug #1)
Fix DPI scaling and the hardcoded 1920×1080 assumption (bugs #2, #4)
Actually run it on a Windows machine once — confirm transparency, WorkerW attach, and click-through behave as the code comments assume, since none of it has been tested live yet
Clean the repo root

Phase W1 — Widget interactivity

Right-click menu directly on a desktop widget (Edit / Disable / Reset position) — Rainmeter's most-used gesture; currently only reachable from the in-app Widgets page
Multi-monitor–aware placement — "top right" should mean the monitor under the cursor, not a hardcoded resolution
Snap-to-edge/grid while dragging
Global hotkey to toggle a widget even when the app isn't focused

Phase W2 — Richer meters

Conditional/threshold coloring (CPU bar turns red past 90%) — small addition to MeterDefinition + a check in Tick()
Per-core CPU, multi-drive disk (currently one aggregate CPU number, one hardcoded C:\)
Image/icon meter
"Now playing" media meter via Windows' GlobalSystemMediaTransportControlsSessionManager

Phase W3 — External data

One generic Web/JSON measure (point at a URL + a JSON path, poll on an interval) — this single building block gets you weather, stock/crypto price, RSS headlines, GitHub stars, anything, instead of a bespoke integration per source
Ship 2–3 presets on top of it — reuse the OpenWeatherMap integration already scoped in phases.md Phase 5 for an actual weather widget, not just theme-switching

Phase W4 — Double down on the AI angle

WidgetLexicon is ~85 entries vs. your 280-word ColorLexicon — same infrastructure (bigrams, fuzzy match, emoji already exist for widgets too), it just needs vocabulary
One prompt → matching color theme and widget layout together, generated as a pair
Conversational refinement — "make the clock bigger" patches the just-generated widget instead of starting over
The insights panel (WidgetAnalysisResult — matched keywords, fuzzy corrections) is already built; make sure it's actually visible, it's easy to miss

Phase W5 — Ecosystem

Extend the theme-gallery mechanism already planned in phases.md Phase 4 to widgets too — one JSON-pack mechanism, two content types
Single-file widget export (.aiwidget) with file association, same pattern as the planned .cozy file
"Remix this widget," mirroring the planned "Remix this theme"

Phase W6 — Packaging

Rides on your existing Phase 7 (MSIX, winget, auto-update). One thing to test early: SetParent-based WorkerW reparenting may behave differently under a sandboxed MSIX package — worth confirming before the whole desktop-layer feature depends on it.