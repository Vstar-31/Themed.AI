using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Calls Vijay's own VibeFinderAI backend (vibefinderai.onrender.com) for a track recommendation
/// matching a vibe/mood phrase — the Phase 6 "VibeFinderAI Integration" bullet in phases.md.
/// Target is "Username|Password|Vibe text", e.g. "listener|hunter2|cozy rainy afternoon" — three
/// parts because, unlike <see cref="WeatherMeasure"/>'s "City|ApiKey", VibeFinderAI has no public
/// API-key mode (it's a per-account JWT login) *and* needs the free-text phrase to analyze.
///
/// The third part can also be the literal marker <see cref="ActiveThemeSentinel"/> ("$theme")
/// instead of a fixed typed phrase — the measure then follows whatever theme is active via
/// <see cref="IActiveThemeProvider"/>, re-resolved (see <see cref="ResolveVibeText"/>) on every
/// poll so switching themes is picked up on the next 5-minute cycle without re-typing anything.
/// This was the Phase 6 "follow the active theme automatically" gap: it needed the current
/// theme threaded into <see cref="IMeasure"/> construction, which nothing here had —
/// MeasureFactory built every measure from just a <see cref="MeasureDefinition"/>, with no
/// theme reference at all. <see cref="MeasureFactory"/> now takes an optional <see cref="IActiveThemeProvider"/>
/// and passes it straight through to this constructor; <c>SkinManagerService</c> is the one
/// place in <c>ThemeManager.WinUI</c> that supplies it (<c>App.ThemeService</c>, which already
/// implemented the interface's one member for free). A widget built without a provider wired up
/// (the parameter is optional and defaults to null) can still use a literal typed phrase exactly
/// as before — only "$theme" needs the provider, and its absence degrades to "Config Err"
/// rather than throwing.
///
/// Endpoint/schema confirmed by reading VibeFinderAI's own FastAPI source
/// (github.com/Vstar-31/vibefinderai, backend/main.py) rather than the live Render deployment,
/// which wasn't reachable from this environment to verify directly:
///   POST /auth/token        — OAuth2 password flow, form-encoded grant_type/username/password
///                              → {"access_token": "...", "token_type": "bearer"}
///   POST /api/vibe/analyze  — Bearer-authed, body {"text": "...", "track_limit": 1}
///                              → {"dominant_vibe": "...", "tracks": [{"title", "artist", ...}]}
/// Same caveat as OpenWeatherMapConditionProvider/WeatherMeasure: written without dotnet/NuGet
/// or a reachable VibeFinderAI instance in-session, so not build- or live-verified. If the free
/// tier has spun down, the first poll after a cold start can take 30-60s — the generous timeout
/// below is deliberate, not an oversight.
///
/// PLAYBACK (Phase 6's open item, closed here): VibeFinderAI's own player (<c>MusicPlayer.jsx</c>)
/// is a React component wired to a YouTube &lt;iframe&gt;+postMessage remote for full tracks, or
/// an HTML &lt;audio&gt; element for its own 30s-preview fallback — both are DOM/JS constructs
/// with nothing callable from a native process, so there is no "VibeFinderAI playback API" this
/// class could invoke even in principle. What <i>is</i> portable is the 30s iTunes preview clip
/// (<c>preview_url</c> — sourced from iTunes' search API server-side, nothing to do with Spotify)
/// that <c>/api/vibe/analyze</c> already returns on every track, in the same response this class
/// was already parsing for Title/Artist/Mood; it just wasn't being read. It's read now, and
/// <see cref="ActionUrl"/> plays it natively via <c>VibeFinderPreviewPlayer</c>
/// (<c>Windows.Media.Playback.MediaPlayer</c>, no browser/WebView2 involved) instead of opening
/// the Spotify link, falling back to that link only when iTunes has no match for the track.
/// <see cref="SecondaryActionUrl"/> (Apple Music, right-click) is unchanged. None of this needed
/// any change on the VibeFinderAI side — the data was already there.
///
/// LATER UPDATE, found while auditing a commit that never updated phases.md: the paragraph above
/// is no longer the live path. <c>MainWindow</c> now hosts a hidden WebView2 running the bare
/// YouTube iframe API directly (searched by "{title} {artist}", nothing VibeFinderAI-side), and
/// <c>SkinHostWindow</c> now dispatches <see cref="ActionUrl"/>'s <c>themed://vibefinder/preview</c>
/// scheme to that player instead of to <c>VibeFinderPreviewPlayer</c> — a third approach neither
/// this comment nor phases.md's "still genuinely open" framing anticipated. <see cref="MeasureType"/>
/// gained <c>VibePlaybackState</c>/<c>VibeTrackProgress</c> alongside it (handled below, reading
/// the new <c>YouTubePlaybackState</c> static) to drive a live play/pause icon and progress bar in
/// the default presets — both were defined but never reached <see cref="MeasureFactory"/>'s switch
/// until this session. <c>PreviewUrl</c> is still parsed above and <c>VibeFinderPreviewPlayer</c>
/// still exists; nothing calls it on this path any more.
/// </summary>
public sealed class VibeFinderMeasure : IMeasure
{
    private const string BaseUrl = "https://vibefinderai.onrender.com";

    /// <summary>Target's third segment, matched case-insensitively (after trimming), means
    /// "use the active theme's vibe" instead of a literal typed phrase. See the class remarks.</summary>
    private const string ActiveThemeSentinel = "$theme";

    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "—";
    public string? ActionUrl { get; private set; }
    public string? SecondaryActionUrl { get; private set; }
    public string? ImageUrl { get; private set; }

    public string CurrentTrackTitle { get; private set; } = "—";
    public string CurrentTrackArtist { get; private set; } = "—";
    // Null when resolve_video_id couldn't find a match server-side (no API key configured,
    // quota exceeded, or no search results) — PlayYouTubeTrack treats null as "nothing to play"
    // rather than falling back to a text search, since that search path is what was broken.
    public string? CurrentVideoId { get; private set; }

    public void UpdateMeasure()
    { }

    private readonly MeasureType _type;
    private readonly string _target;
    private readonly ILogger _logger;
    private readonly IActiveThemeProvider? _activeThemeProvider;

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Keyed by "username|password" — one login/token shared by every widget on the same
    // VibeFinderAI account, regardless of what vibe phrase each one asks about.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TokenEntry> _tokens = new();

    // Keyed by the full target (creds+phrase) — same reasoning as WeatherMeasure's per-city
    // cache: two widgets asking about two different vibes must never share a result, but
    // Title/Artist/Mood meters pointed at the *same* target correctly share one API call.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ResultEntry> _cache = new();

    private sealed class TokenEntry
    {
        public string? AccessToken;
    }

    private sealed class ResultEntry
    {
        public System.Collections.Generic.List<(string Title, string Artist, string Mood, string? SpotifyUrl, string? AppleUrl, string? CoverArtUrl, string? PreviewUrl, string? VideoId)> Tracks = new();
        public int CurrentIndex = 0;
        public DateTime LastAttempt = DateTime.MinValue;
        public DateTime LastSuccess = DateTime.MinValue;
        public readonly object Lock = new();
    }

    public VibeFinderMeasure(string name, MeasureType type, string? target, ILogger? logger = null, IActiveThemeProvider? activeThemeProvider = null)
    {
        Name = name;
        _type = type;
        _target = target ?? "";
        _logger = logger ?? NullLogger.Instance;
        _activeThemeProvider = activeThemeProvider;
    }

    public void Refresh()
    {
        var parts = _target.Split('|', 3);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            Text = "Config Err";
            return;
        }

        var entry = _cache.GetOrAdd(_target, _ => new ResultEntry());

        // Resolved once and reused below for both the fetch and the no-provider fallback, rather
        // than re-checking the sentinel twice — see ResolveVibeText's own remarks for why a null
        // result specifically means "can't fetch," not "fetch an empty phrase."
        string? vibeText = ResolveVibeText(parts[2]);

        // Same 5-minute-per-target throttle as WeatherMeasure/WebJsonMeasure — a fixed vibe
        // phrase's recommendation doesn't change fast enough to poll harder than that, and this
        // is someone else's (Vijay's own, but still free-tier) backend to be a good citizen of.
        // Cache key stays the raw target (creds + "$theme" or creds + literal phrase) rather than
        // the resolved text — every "$theme" widget on the same account correctly shares one
        // cached answer for "what does the currently active theme's vibe recommend," the same
        // way same-phrase widgets already share one answer for a literal phrase. Switching themes
        // mid-cycle doesn't force an immediate refetch (same trade-off this file already makes
        // for a literal phrase changing), just picked up on the next scheduled poll.
        if (vibeText is not null && (DateTime.UtcNow - entry.LastSuccess).TotalMinutes > 5)
            Task.Run(() => FetchAsync(entry, parts[0], parts[1], vibeText));

        if (entry.Tracks.Count > 0)
        {
            var data = entry.Tracks[entry.CurrentIndex];
            CurrentTrackTitle = data.Title;
            CurrentTrackArtist = data.Artist;
            CurrentVideoId = data.VideoId;

            Text = _type switch
            {
                MeasureType.VibeTrackTitle  => data.Title,
                MeasureType.VibeTrackArtist => data.Artist,
                MeasureType.VibeMood        => data.Mood,
                MeasureType.VibePlaybackState => YouTubePlaybackState.IsPlaying ? "PLAYING" : "PAUSED",
                _                           => "—",
            };

            if (_type == MeasureType.VibeTrackProgress)
            {
                // 0-100 here, not YouTubePlaybackState.Progress's raw 0.0-1.0 fraction, to match
                // every other measure's "Value is a percentage" convention (see IMeasure.Value's
                // own doc comment: "For CPU/Memory/Disk measures this is a 0-100 percentage").
                // BarMeterViewModel/RingMeterViewModel both normalize as measure.Value / BarMax,
                // and BarMax defaults to 100 — so a 0.0-1.0 Value here was rendering a
                // half-played track as a 0.5%-full bar, not a 50%-full one. Text is reformatted
                // to match; ToString("P0") assumes its input is a fraction and would have shown
                // "5000%" once Value was corrected to already be 0-100.
                Value = YouTubePlaybackState.Progress * 100;
                // "1:23 / 3:45" instead of a bare percentage — a percentage isn't what anyone
                // reads a media player's clock as. Reads "0:00 / 0:00" before playback starts
                // (Duration is 0 until the first successful poll after a track loads), same as
                // most native players show before the first tick rather than blank space.
                Text = $"{FormatClock(YouTubePlaybackState.CurrentTime)} / {FormatClock(YouTubePlaybackState.Duration)}";
            }
            else
            {
                Value = 0;
            }
            // Primary click plays the 30s iTunes preview natively (VibeFinderPreviewPlayer)
            // instead of opening Spotify — the Phase 6 "actual playback" decision. preview_url
            // was already present on every /api/vibe/analyze track (see the parsing below);
            // this measure just wasn't reading it before. Falls back to the old Spotify deep
            // link when iTunes has no match for this track (PreviewUrl null), so a left-click
            // never goes dead. SecondaryActionUrl (Apple, right-click) is untouched.
            ActionUrl = data.PreviewUrl is { Length: > 0 } previewUrl
                ? $"themed://vibefinder/preview?url={Uri.EscapeDataString(previewUrl)}"
                : data.SpotifyUrl;
            SecondaryActionUrl = data.AppleUrl;

            // Only a display-type measure (Title/Artist/Mood) should carry cover art onto an
            // Icon meter — VibePlaybackState and VibeTrackProgress are control/status measures,
            // not display ones. IconMeterViewModel.Tick() copies ImageUrl straight through to a
            // meter, and BuildIconVisual layers that image ON TOP of the FontIcon glyph inside
            // the same chip (so a cover-art thumbnail can sit over a play/pause icon, or a
            // progress readout, and fully obscure it once the image loads). This used to be set
            // unconditionally here, which meant the "VibeState" measure — bound to the
            // play/pause Icon meter in every preset — silently inherited the current track's
            // cover art too: clicking play *did* flip the glyph from Play to Pause underneath,
            // but the album art thumbnail, already loaded and visible, never let that repaint
            // show through, so the button looked dead and just displayed the cover instead.
            ImageUrl = _type is MeasureType.VibePlaybackState or MeasureType.VibeTrackProgress
                ? null
                : data.CoverArtUrl;
        }
        else if (vibeText is null)
        {
            // Targets "$theme" but nothing ever wired up an IActiveThemeProvider for this
            // measure, and there's no cached result from before that gap existed to fall back
            // on — surface that distinctly rather than silently sitting on the "—" placeholder
            // forever, which would look like a slow first poll instead of a real config problem.
            Text = "Config Err";
        }
    }

    /// <summary>
    /// Turns the raw third <c>Target</c> segment into the text actually sent to
    /// <c>/api/vibe/analyze</c>. Returns <paramref name="rawPhrase"/> unchanged unless it's the
    /// <see cref="ActiveThemeSentinel"/> marker, in which case it derives text from
    /// <see cref="_activeThemeProvider"/>'s active theme via <see cref="ThemeVibeText.Describe"/>
    /// — or null if no provider is wired up, so the caller can tell "nothing to fetch" apart
    /// from "fetch this literal (even if unusual) phrase."
    /// </summary>
    private string? ResolveVibeText(string rawPhrase)
    {
        if (!string.Equals(rawPhrase.Trim(), ActiveThemeSentinel, StringComparison.OrdinalIgnoreCase))
            return rawPhrase;

        var theme = _activeThemeProvider?.ActiveTheme;
        return theme is null ? null : ThemeVibeText.Describe(theme);
    }

    private async Task FetchAsync(ResultEntry entry, string username, string password, string vibeText)
    {
        lock (entry.Lock)
        {
            if ((DateTime.UtcNow - entry.LastAttempt).TotalMinutes < 5) return;
            entry.LastAttempt = DateTime.UtcNow;
        }

        try
        {
            string? token = await GetTokenAsync(username, password);
            if (token is null)
            {
                _logger.LogWarning("VibeFinderAI login failed for user \"{User}\"", username);
                return;
            }

            var resp = await PostAnalyzeAsync(token, vibeText);

            // A cached token can expire between polls (5-minute cache, but the app can be open
            // for hours) — one retry with a fresh login rather than surfacing "Config Err" until
            // the widget happens to poll again.
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                token = await GetTokenAsync(username, password, forceRefresh: true);
                if (token is null) return;
                resp = await PostAnalyzeAsync(token, vibeText);
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("VibeFinderAI /api/vibe/analyze returned {Status}", resp.StatusCode);
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string mood = root.TryGetProperty("dominant_vibe", out var vEl) ? vEl.GetString() ?? "—" : "—";
            var newTracks = new System.Collections.Generic.List<(string, string, string, string?, string?, string?, string?, string?)>();
            if (root.TryGetProperty("tracks", out var tracksEl) && tracksEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var track in tracksEl.EnumerateArray())
                {
                    string tTitle = track.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "—" : "—";
                    string tArtist = track.TryGetProperty("artist", out var aEl) ? aEl.GetString() ?? "—" : "—";
                    string? tSpotify = null, tApple = null, tCover = null, tPreview = null, tVideoId = null;
                    
                    if (track.TryGetProperty("spotify_uri", out var uriEl))
                    {
                        var uri = uriEl.GetString();
                        if (uri != null && uri.StartsWith("spotify:track:")) tSpotify = $"https://open.spotify.com/track/{uri.Substring(14)}";
                        else if (uri != null && uri.StartsWith("spotify:search:")) tSpotify = $"https://open.spotify.com/search/{uri.Substring(15)}";
                    }
                    if (track.TryGetProperty("apple_uri", out var appleEl)) tApple = appleEl.GetString();
                    if (track.TryGetProperty("cover_art", out var artEl))
                    {
                        var art = artEl.GetString();
                        if (art != null) tCover = art.Replace("100x100bb", "512x512bb");
                    }
                    if (track.TryGetProperty("preview_url", out var previewEl)) tPreview = previewEl.GetString();
                    // Added alongside the fields above — /api/vibe/analyze now resolves this
                    // server-side (core/youtube_cache.resolve_video_id) the same way our own
                    // frontend does, instead of Themed.AI trying to search YouTube itself: the
                    // IFrame Player API's loadPlaylist({listType:'search'}) that PlayYouTubeTrack
                    // used to rely on was deprecated by YouTube in Nov 2020 and has returned a 4xx
                    // on every call since — see MainWindow.PlayYouTubeTrack. Null here (no key
                    // configured server-side, quota exceeded, or no results) means this track has
                    // nothing to actually play; that's handled where CurrentVideoId is read, not
                    // here.
                    if (track.TryGetProperty("youtube_video_id", out var videoIdEl)) tVideoId = videoIdEl.GetString();
                    
                    // We only require cover art now, since YouTube handles playback instead of iTunes preview URL
                    if (!string.IsNullOrEmpty(tCover))
                    {
                        newTracks.Add((tTitle, tArtist, mood, tSpotify, tApple, tCover, tPreview, tVideoId));
                    }
                }
            }
            if (newTracks.Count == 0)
                newTracks.Add(("No match", "—", mood, null, null, null, null, null));

            lock (entry.Lock)
            {
                entry.Tracks = newTracks;
                entry.CurrentIndex = 0;
            }
            entry.LastSuccess = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // Same reasoning as WeatherMeasure/WebJsonMeasure: entry.Data is left as whatever it
            // was before, so a transient failure (or a Render cold-start timeout) doesn't blank
            // out a widget that was showing a real recommendation a moment ago.
            _logger.LogWarning(ex, "Failed to fetch VibeFinderAI recommendation");
        }
    }

    private static Task<HttpResponseMessage> PostAnalyzeAsync(string token, string vibeText)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/vibe/analyze")
        {
            // track_limit: 1 — the widget only ever shows the top match, so there's no reason to
            // make VibeFinderAI's own analysis pipeline do more work than that per poll.
            Content = JsonContent.Create(new { text = vibeText, track_limit = 20 }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(req);
    }

    private async Task<string?> GetTokenAsync(string username, string password, bool forceRefresh = false)
    {
        string key = $"{username}|{password}";
        var tokenEntry = _tokens.GetOrAdd(key, _ => new TokenEntry());

        if (!forceRefresh && tokenEntry.AccessToken is not null)
            return tokenEntry.AccessToken;

        try
        {
            // FastAPI's OAuth2PasswordRequestForm expects application/x-www-form-urlencoded,
            // not JSON — this is the one call in this file that isn't a JSON body.
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
            });
            var resp = await _http.PostAsync($"{BaseUrl}/auth/token", form);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            string? token = doc.RootElement.TryGetProperty("access_token", out var el) ? el.GetString() : null;

            tokenEntry.AccessToken = token;
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderAI login request failed");
            return null;
        }
    }

    public void SkipNext()
    {
        var entry = _cache.GetOrAdd(_target, _ => new ResultEntry());
        lock (entry.Lock)
        {
            if (entry.Tracks.Count > 0)
                entry.CurrentIndex = (entry.CurrentIndex + 1) % entry.Tracks.Count;
        }
    }

    public void SkipPrevious()
    {
        var entry = _cache.GetOrAdd(_target, _ => new ResultEntry());
        lock (entry.Lock)
        {
            if (entry.Tracks.Count > 0)
            {
                entry.CurrentIndex--;
                if (entry.CurrentIndex < 0) entry.CurrentIndex = entry.Tracks.Count - 1;
            }
        }
    }

    /// <summary>Formats a seconds count as a player clock face — "1:23", "12:04", "1:02:03" past
    /// an hour. Negative/NaN input (shouldn't happen, but a mid-poll YouTube API hiccup is cheap
    /// insurance against) clamps to "0:00" rather than showing a garbage string.</summary>
    private static string FormatClock(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes}:{span.Seconds:D2}";
    }
}
