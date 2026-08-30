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
  - [x] **VibeFinderAI Integration** (preview playback added — full-length still open) — `VibeFinderMeasure` (`ThemeManager.Integration`) calls Vijay's own `vibefinderai.onrender.com` backend for a track recommendation matching a vibe phrase. The live Render deployment wasn't reachable from this environment to verify directly, so the API surface was confirmed by reading the backend's own FastAPI source instead (`github.com/Vstar-31/vibefinderai`, `backend/main.py`): `POST /auth/token` (OAuth2 password flow, form-encoded) → bearer JWT, then `POST /api/vibe/analyze` `{text, track_limit}` → `{dominant_vibe, tracks: [{title, artist, ...}]}`. Three new measure types — `VibeTrackTitle`/`VibeTrackArtist`/`VibeMood` — wired into `MeasureFactory` and the skin editor (auto-companion: requesting the title pulls in the artist, same as Time→Date); NLP trigger words "vibe"/"recommend"/"mood" added to `WidgetLexicon` (stems verified by porting `PorterStemmer.cs` to Python and running it, not guessed). Target format is `"username|password|vibe text"` — typed per-widget, no Settings-page credential storage added, same trade-off Weather/WebJson already accept for their own secrets. Not build- or live-verified — same caveat as the weather work above.
    - [x] Follow the *active theme's* vibe automatically instead of a fixed typed phrase. The third `Target` segment can now be the literal marker `"$theme"` instead of typed text; `VibeFinderMeasure.ResolveVibeText` re-resolves it on every poll from a new `IActiveThemeProvider` (`ThemeManager.Core/Services`) — a one-member interface (`CozyTheme ActiveTheme { get; }`) that `ThemeService` now implements for free, since the property already existed. `MeasureFactory.Create` takes an optional `IActiveThemeProvider?` and threads it through to `VibeFinderMeasure`'s constructor; `SkinHostViewModel` takes the same optional param and threads it further; `SkinManagerService.OpenWindowFor` is the one call site that actually supplies one, via `App.ThemeService` (same static-service-access pattern already used there for `App.MainWindow`). Everything upstream of the constructor is a plain optional parameter defaulting to `null`, so every existing call site and every widget with a literal typed phrase keeps working unchanged. What "the active theme's vibe" *is* — new `ThemeVibeText.Describe` (`ThemeManager.Core/NLP`, unit-tested) — combines `CozyTheme.Name` + `.Description` ("Mystic Forest. Cool and contemplative, evoking forest, mystic, night."), falling back to `Name` alone when `Description` is blank (a manually-created theme never run through `VibeThemeGenerator` has no description). Deliberately a pure function in Core rather than inline in the Integration-side measure — keeps it reusable and, concretely, keeps it testable from `ThemeManager.Tests` without that project needing a new reference to `ThemeManager.Integration` (which nothing in Tests depends on today, and extending that boundary felt like a bigger call than this one feature should make unilaterally). Cache key is unchanged (still the raw `Target` string) — every `"$theme"` widget on the same account correctly shares one cached answer, and a theme switch is picked up on the next scheduled poll rather than forcing an immediate refetch, the same lag the existing 5-minute per-target throttle already accepts for a literal phrase changing. Editor hint text updated so `"$theme"` is discoverable rather than a hidden magic value; no new UI control (e.g. a checkbox) was added for it — the Target field is already a plain hand-typed string for this measure type (creds included), so typing `$theme` is consistent with how the rest of the field already works, and a dedicated toggle would mean a new `MeterDefinition`/`MeasureDefinition` field for what a string literal already covers. Not build-verified, same standing caveat as the rest of this integration.
    - [x] Actual playback, scoped to the 30s preview clip — resolved by reading the room instead of adding new machinery: `/api/vibe/analyze` was already returning `preview_url` (iTunes' 30s MP3, unrelated to Spotify) on every track, in the same response this measure was already parsing for Title/Artist/Mood — the field just wasn't being read. Now it is, and it's played natively through a new `VibeFinderPreviewPlayer` (`ThemeManager.Integration/Skins`, `Windows.Media.Playback.MediaPlayer`) instead of the browser-link-vs-embedded-player-vs-Spotify-OAuth choice this bullet originally posed as the open question — none of those three ever needed deciding, because VibeFinderAI's own player (`MusicPlayer.jsx`) turned out to be a YouTube-iframe+postMessage / HTML-`<audio>` React component with no playback API to call into from a native process in the first place, and the one genuinely portable piece (the preview clip) was sitting unread in data Themed.AI already had. `ActionUrl` now points at a new `themed://vibefinder/preview?url=...` scheme (dispatched in `SkinHostWindow`, parallel to the existing `themed://media/` one) on primary click, falling back to the old Spotify link only when iTunes has no match for the track; `SecondaryActionUrl` (Apple, right-click) is untouched. Zero VibeFinderAI-side changes.
      - This bullet's own claim that `SkinHostWindow` had no per-meter click-handling is now stale — that infrastructure (the left/right `ActionUrl`/`SecondaryActionUrl` dispatch, `themed://media/` passthrough, reflection-based hover cursor) was built in the session that added Apple Music as the secondary click action, before this one started. Left uncorrected until now.
      - Still genuinely open: full-length playback. That's the piece that actually requires the browser-vs-native call this bullet originally posed — VibeFinderAI's full tracks only play through the YouTube iframe/postMessage path, which has no native equivalent short of embedding a real web surface (WebView2) somewhere in the app, a bigger architectural change (and a product one — it turns part of a lightweight always-on-top overlay into an embedded browser) than this session should make unilaterally. Two shapes worth Vijay picking between when that's wanted: (a) WebView2 panel pointed at VibeFinderAI's existing public `/playlist/:token` share page (would need one new, small, non-core backend call from `VibeFinderMeasure` to `POST /api/playlist/save` to mint that token — but note that page's own playback is *also* the iTunes preview, not YouTube, so this buys a nicer in-app surface, not full length); (b) open VibeFinderAI in a real browser tab/window and let it register with Windows' Media Session — `MediaMeasure`'s existing `themed://media/playpause`/`next` already control *whatever* app currently owns the OS "Now Playing" session, no VibeFinderAI-specific code needed on the Themed.AI side, but VibeFinderAI sets no `navigator.mediaSession` metadata today, so title/artist shown by Windows would be whatever Chrome/Edge infers from the raw YouTube iframe rather than VibeFinderAI's own clean strings — untested, and the one option that would want a (small, additive) VibeFinderAI-side change to be worth doing properly.
      - Not build- or live-verified — same standing caveat as the rest of this integration.
      - **This "still genuinely open" framing is now stale.** Full-length playback exists —
        landed in `e0d3a3b` ("implement MainWindow shell with navigation and YouTube playback..."),
        a commit that never updated this file, so it went undiscovered until this session's audit.
        `MainWindow` hosts a hidden `WebView2` (`HiddenYoutubePlayer`) running the bare YouTube
        iframe API (`loadPlaylist({listType:'search', list:"{title} {artist}"})`), polled every
        500ms into a new `YouTubePlaybackState` static (`Integration/Skins`: `IsPlaying`/`Progress`).
        That's a third shape neither of the two options above anticipated — not VibeFinderAI's own
        site via WebView2, not the OS Media Session, but a bare iframe embedded directly in
        Themed.AI. `SkinHostWindow` now dispatches the `themed://vibefinder/preview` click to
        `MainWindow.PlayYouTubeTrack(title, artist)` instead of `VibeFinderPreviewPlayer`, which
        still exists but is unused on that path (calling it again with the same title/artist toggles
        play/pause via a `togglePause()` JS function already defined alongside the iframe, which is
        also what `themed://media/playpause`/`next`/`prev` now call for a VibeFinder-bearing widget
        specifically, ahead of falling through to `MediaMeasure`'s OS-wide command for any other
        widget). Two new `MeasureType`s, `VibePlaybackState`/`VibeTrackProgress`, plus default
        Icon+Bar meters bound to them, were added to every `EnsureVibeFinderSkinsExist` preset to
        surface it as a play/pause icon and a progress bar.
      - **That machinery was fully wired except for one gap that neutered it completely**, found and
        fixed this session: `MeasureFactory.Create`'s switch had no cases for the two new
        `MeasureType`s, so both silently resolved to the defensive `UnknownMeasure` fallback (fixed
        `Value=0`, `Text="—"`) instead of a real `VibeFinderMeasure` — meaning the progress bar was
        hardwired to 0% and the play/pause icon's bound text could never actually read "PLAYING" or
        "PAUSED", regardless of what `YouTubePlaybackState` said. Added both arms, same pattern as
        the three existing `Vibe*` cases. Fixing that surfaced two more bugs that were harmless
        while the data was fake and became real the moment it wasn't:
        1. `VibeTrackProgress`'s `Value` was `YouTubePlaybackState.Progress` verbatim — a raw
           0.0–1.0 fraction — but `BarMeterViewModel`/`RingMeterViewModel` both normalize as
           `measure.Value / BarMax`, and `BarMax` defaults to 100 (matching `IMeasure.Value`'s own
           documented "0–100 percentage" convention, which every other measure follows). A
           half-played track (`0.5`) was rendering as a bar 0.5% full, not 50%. Now stored as
           `Progress * 100`, with `Text` reformatted as `{Value:F0}%` (matching `CpuMeasure`'s own
           formatting) instead of `ToString("P0")`, which assumes its input is still a fraction and
           would've printed "5000%" once `Value` was corrected.
        2. Even with correct data reaching it, `SkinHostWindow.BuildIconVisual` built its `FontIcon`
           from `vm.Glyph` once at construction and never subscribed to `Glyph` changes — unlike the
           `ImageUrl` and `IsThresholdCrossed` subscriptions a few lines below it in the same method,
           which do exactly that for their own properties. `IconMeterViewModel.Tick()`'s Play/Pause
           glyph swap (already present, just never fed real measure text until fix 1 above) was
           updating a ViewModel property nothing on screen was listening to. Added the missing
           subscription. Also corrected `IconMeterViewModel`'s class doc comment, which flatly
           stated "the glyph itself never changes at runtime" — true when that comment was written,
           left uncorrected once the Play/Pause logic below it made it false.
        - Not build- or live-verified — same standing caveat as the rest of this integration.
      - **Real-world testing (screenshot: Visual Studio + live floating widgets) surfaced three
        more problems the code-only audit above couldn't have caught, plus a fourth that's a real
        feature request, not a bug:**
        1. **No audio ever played, at all — root cause verified via search, not assumed:**
           `playTrackById`'s predecessor called `player.loadPlaylist({listType: 'search', list:
           query})`, and YouTube deprecated `listType: 'search'` on 15 Nov 2020 — every call since
           returns a 4xx and loads nothing
           (https://developers.google.com/youtube/iframe_api_reference). This has never worked,
           not once, regardless of anything fixed earlier in this integration; it's not a
           regression, it's dead on arrival from whenever this was first written. There's no
           supported client-side replacement for free-text search — resolving a query to a
           concrete `videoId` has to happen server-side now. VibeFinderAI's own backend already
           does exactly this for its own player (`core/youtube_cache.py` + `GET
           /api/services/youtube/search`, cached, quota-aware), so rather than duplicating that in
           Themed.AI, `/api/vibe/analyze` now resolves it too: added
           `youtube_cache.resolve_video_id(title, artist)` (cache-checked, same underlying Data API
           v3 search the existing route uses) and a `youtube_video_id` field on `TrackInfo`,
           populated per track via the same `asyncio.gather` pattern already used for iTunes
           previews a few lines above it. `VibeFinderMeasure` threads it through as a new
           `CurrentVideoId`, and `MainWindow.PlayYouTubeTrack` now takes a real video ID and calls
           `loadVideoById` (still fully supported) instead of the dead search call — a null/missing
           id (no server-side key configured, quota exceeded, or no match) means there's genuinely
           nothing to play, not a fallback this method can search its way out of.
        2. **Even with a real video ID, audio likely still wouldn't have been audible:** Chromium's
           default autoplay policy blocks unmuted audio/video unless playback is tied to a genuine
           user gesture on that frame, and every play here originates from a native C# call into
           `ExecuteScriptAsync` — never a real click inside the WebView2 itself — which Chromium
           doesn't count as a gesture. `playerVars: {autoplay:1}` alone was always at risk of being
           silently blocked, with zero visible error since this WebView2 is never shown to the
           user. Fixed with the standard, documented answer for exactly this "nothing ever clicks
           inside the page" case: a dedicated `CoreWebView2Environment` carrying
           `--autoplay-policy=no-user-gesture-required`. Given its own **separate user data
           folder** rather than the app's default one — `VibeFinderAIPage.VibeFinderWebView`
           (embedding the live site: see point 4 below) already uses the default environment for
           its own purposes, and WebView2 requires identical environment options for every control
           sharing a user data folder, so bolting this argument onto the default folder would throw
           the moment both controls exist in the same process. Separately (found while in this
           code, not reported): `_youtubeReady` was being set immediately after `NavigateToString`
           fired — which only means the WebView2 was told to start loading, not that the YouTube
           IFrame API script finished loading over the network, ran, and actually constructed a
           player object. A click landing in that window would have silently no-opped against the
           JS-side `player &&` guards. Now waits for the player's own `onReady` event, relayed back
           via `window.chrome.webview.postMessage('ready')`.
        3. **No progress bar, unresponsive-looking play button — different root cause, nothing to
           do with YouTube:** `EnsureVibeFinderSkinsExist()` gates purely on a skin's *name*
           already existing (`!_skins.Any(s => s.Name == "VibeFinder Primary")`), not on whether an
           existing same-named skin actually contains everything the current preset defines. A
           widget auto-created before `VibeState`/`VibeProgress` existed — like the one in the
           screenshot — never received them once they were added, even after every fix earlier in
           this integration made them fully functional in principle: there was simply no bar or
           reactive icon in that widget's saved definition to *be* functional. Restructured so each
           preset's "does the skin exist" check only gates creating the skin itself; separate
           `if (!skin.Measures.Any(...))` / `if (!skin.Meters.Any(...))` checks — mirroring the
           `Format` back-fill loop already at the bottom of this same method — now run
           unconditionally and heal an existing skin that's missing either. This runs every time
           `VibeFinderAIPage` loads (its constructor calls `EnsureVibeFinderSkinsExist()`), not just
           on first install, so existing widgets get healed on next visit to that page, not a
           rebuild.
        4. **"That right panel [VibeFinder Playlist, the 'Now Playing from Vibe' widget] should
           show actual VibeFinder controls — like an embed of the engine with all the controls
           from my site":** genuine feature request, not a bug, and a bigger one than the three
           above — flagged rather than rushed into this same pass. `VibeFinder Playlist` never had
           play/pause/next in *any* version (a pure display by original design), so it got the
           same two icons Primary has, added via the same healing mechanism as point 3 — its layout
           already packed content to the original bottom edge (a Bar at Y=290 in what was a
           300-tall widget), so the widget grows 40px downward to make room rather than overlapping
           anything already there. That's a real but small improvement, not what was actually
           asked for: an embedded live view of VibeFinderAI's own site, with its real interactive
           controls, inside the floating widget itself, not a native re-implementation
           approximating it via Icon/Bar meters. `VibeFinderAIPage` already proves the basic
           mechanism works elsewhere in this app (`VibeFinderWebView`, a plain `WebView2` XAML
           control navigated to `https://vibefinderai.onrender.com/`, wired up entirely
           declaratively) — but every floating widget goes through `SkinHostWindow`/
           `SkinEditorPage`, which build native XAML elements per meter in code (`BuildBarVisual`,
           `BuildIconVisual`, etc.), never a browser control. Bringing that into a floating widget
           needs a new `MeterKind` (e.g. `WebEmbed`) with its own `Build*Visual` in both renderers,
           each hosting a real `WebView2` sized to the meter's bounds — plus a decision on what URL
           to point it at (the live root site has full nav chrome, which may be too much for a
           small floating widget; a compact player-only route doesn't exist yet on the frontend and
           would need work there too, not just here) and on how a second real WebView2 environment
           interacts with the two already in this app (see point 2's user-data-folder note — a
           third environment is more of the same consideration, not a new problem). Worth doing,
           not done here.
        - Not build- or live-verified — same standing caveat as the rest of this integration.
    - **Session hygiene note:** auditing the commit that added the Ring/Icon NLP trigger words (`705f9ead`) surfaced a pattern worth naming — verifying what `PorterStemmer.Stem()` does to a handful of words had been re-implemented from scratch *seven times* across this repo's history (root `Program.cs`/`StemTest.cs`/`test.cs`, `StemTest/`, `TestStem/`, `TestStem2/`, and a `StemmingTests.PrintStems` xUnit test that printed but never asserted), including one (`TestStem2`) that doesn't compile — invalid escape sequences in a non-verbatim string literal writing to a hardcoded `G:\my projects\...` path — and one (`TestStem`) targeting `net10.0`, inconsistent with the rest of the repo's `net8.0`. All six scratch variants deleted; `StemmingTests.cs` rewritten as real `[Theory]`/`[Fact]` assertions (values checked against a faithful Python port of `PorterStemmer.cs`, not hand-traced) covering the VibeFinder and Ring/Icon trigger words. One genuinely interesting, verified-not-assumed finding from that exercise: `WidgetLexicon["play"]` (→ style boost, comment says `// "playful"`) is *not* a fixed point of its own stemmer — `Stem("play")` alone gives `"plai"` (step 1c's trailing-y rule fires on the bare word), but `Stem("playful")` correctly gives `"play"` (step 3's `-ful` rule fires first and consumes the suffix before step 1c would ever see the shortened word again). The entry is correct and reachable exactly as commented; a blanket "every lexicon key should equal `Stem(key)`" test — the first version written this session — would have flagged it as a false regression, so that blanket assertion was replaced with the narrower, empirically-checked one now in `StemmingTests.cs`. `TestVibe/` and `StressTestPrompts/` were left alone — both compile correctly (proper `ProjectReference` to `ThemeManager.Core`) and do something the new xUnit tests don't (ad-hoc single-prompt output inspection and bulk NLP accuracy sweeps, respectively).
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