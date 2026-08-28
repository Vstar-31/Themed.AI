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
        public (string Title, string Artist, string Mood, string? SpotifyUrl)? Data;
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

        if (entry.Data is { } data)
        {
            Text = _type switch
            {
                MeasureType.VibeTrackTitle  => data.Title,
                MeasureType.VibeTrackArtist => data.Artist,
                MeasureType.VibeMood        => data.Mood,
                _                           => "—",
            };
            ActionUrl = data.SpotifyUrl;
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
            string title = "No match", artist = "—";
            string? spotifyUrl = null;
            if (root.TryGetProperty("tracks", out var tracksEl)
                && tracksEl.ValueKind == JsonValueKind.Array && tracksEl.GetArrayLength() > 0)
            {
                var first = tracksEl[0];
                title = first.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "—" : "—";
                artist = first.TryGetProperty("artist", out var aEl) ? aEl.GetString() ?? "—" : "—";
                if (first.TryGetProperty("spotify_uri", out var uriEl))
                {
                    var uri = uriEl.GetString();
                    if (uri != null && uri.StartsWith("spotify:track:"))
                        spotifyUrl = $"https://open.spotify.com/track/{uri.Substring(14)}";
                    else if (uri != null && uri.StartsWith("spotify:search:"))
                        spotifyUrl = $"https://open.spotify.com/search/{uri.Substring(15)}";
                }
            }

            entry.Data = (title, artist, mood, spotifyUrl);
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
            Content = JsonContent.Create(new { text = vibeText, track_limit = 1 }),
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
}
