using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Integration.Skins;

/// <summary>VibeFinderAI-backed recommendation measure with active-theme-aware caching.</summary>
public sealed class VibeFinderMeasure : IMeasure
{
    private const string BaseUrl = "https://vibefinderai.onrender.com";
    private const string ActiveThemeSentinel = "$theme";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromMinutes(5);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly ConcurrentDictionary<string, TokenEntry> Tokens = new();
    private static readonly ConcurrentDictionary<string, ResultEntry> Cache = new();

    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "—";
    public string? ActionUrl { get; private set; }
    public string? SecondaryActionUrl { get; private set; }
    public string? ImageUrl { get; private set; }
    public string CurrentTrackTitle { get; private set; } = "—";
    public string CurrentTrackArtist { get; private set; } = "—";
    public string? CurrentVideoId { get; private set; }
    public string? CurrentPreviewUrl { get; private set; }

    private readonly MeasureType _type;
    private readonly string _target;
    private readonly ILogger _logger;
    private readonly IActiveThemeProvider? _activeThemeProvider;
    private string _lastLoggedText = "";

    private sealed class TokenEntry { public string? AccessToken; }

    private sealed class ResultEntry
    {
        public List<(string Title, string Artist, string Mood, string? SpotifyUrl, string? AppleUrl, string? CoverArtUrl, string? PreviewUrl, string? VideoId)> Tracks = new();
        public int CurrentIndex;
        public DateTime LastAttempt = DateTime.MinValue;
        public DateTime LastSuccess = DateTime.MinValue;
        public bool FetchInFlight;
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

    public void UpdateMeasure() { }

    public void Refresh()
    {
        if (VibeFinderWebState.IsActive)
        {
            CurrentTrackTitle = VibeFinderWebState.Title;
            CurrentTrackArtist = VibeFinderWebState.Artist;
            CurrentPreviewUrl = VibeFinderWebState.PreviewUrl;
            CurrentVideoId = null;
            Text = _type switch
            {
                MeasureType.VibeTrackTitle => VibeFinderWebState.Title,
                MeasureType.VibeTrackArtist => VibeFinderWebState.Artist,
                MeasureType.VibeMood => "—",
                MeasureType.VibePlaybackState => VibeFinderWebState.IsPlaying ? "PLAYING" : "PAUSED",
                _ => "—"
            };
            if (_type == MeasureType.VibeTrackProgress)
            {
                Value = VibeFinderWebState.Progress * 100;
                Text = $"{FormatClock(VibeFinderWebState.CurrentTime)} / {FormatClock(VibeFinderWebState.Duration)}";
            }
            else Value = 0;
            ActionUrl = VibeFinderWebState.PreviewUrl is { Length: > 0 } preview
                ? $"themed://vibefinder/preview?url={Uri.EscapeDataString(preview)}" : null;
            SecondaryActionUrl = null;
            ImageUrl = _type is MeasureType.VibePlaybackState or MeasureType.VibeTrackProgress ? null : VibeFinderWebState.CoverArt;
            LogTextTransitionIfChanged();
            return;
        }

        var parts = _target.Split('|', 3);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            Text = "Config Err";
            LogTextTransitionIfChanged();
            return;
        }

        string? vibeText = ResolveVibeText(parts[2]);
        string cacheKey = BuildCacheKey(parts[0], parts[1], vibeText ?? parts[2]);
        var entry = Cache.GetOrAdd(cacheKey, _ => new ResultEntry());

        bool shouldFetch = false;
        if (vibeText is not null)
        {
            lock (entry.Lock)
            {
                var now = DateTime.UtcNow;
                bool stale = now - entry.LastSuccess >= CacheLifetime;
                bool retryDue = now - entry.LastAttempt >= RetryBackoff;
                if (stale && retryDue && !entry.FetchInFlight)
                {
                    entry.FetchInFlight = true;
                    entry.LastAttempt = now;
                    shouldFetch = true;
                }
            }
        }

        if (shouldFetch)
        {
            _logger.LogDebug("VibeFinderMeasure[{MeasureName}]: cache stale, starting one background fetch for phrase \"{Phrase}\"", Name, vibeText);
            _ = Task.Run(() => FetchAsync(entry, parts[0], parts[1], vibeText!));
        }

        List<(string Title, string Artist, string Mood, string? SpotifyUrl, string? AppleUrl, string? CoverArtUrl, string? PreviewUrl, string? VideoId)> tracks;
        int index;
        lock (entry.Lock)
        {
            tracks = entry.Tracks;
            index = entry.CurrentIndex;
        }

        if (tracks.Count > 0)
        {
            index = Math.Clamp(index, 0, tracks.Count - 1);
            var data = tracks[index];
            CurrentTrackTitle = data.Title;
            CurrentTrackArtist = data.Artist;
            CurrentVideoId = data.VideoId;
            CurrentPreviewUrl = data.PreviewUrl;
            Text = _type switch
            {
                MeasureType.VibeTrackTitle => data.Title,
                MeasureType.VibeTrackArtist => data.Artist,
                MeasureType.VibeMood => data.Mood,
                MeasureType.VibePlaybackState => VibeFinderPreviewPlayer.IsPlaying ? "PLAYING" : "PAUSED",
                _ => "—"
            };
            if (_type == MeasureType.VibeTrackProgress)
            {
                Value = VibeFinderPreviewPlayer.Progress * 100;
                Text = $"{FormatClock(VibeFinderPreviewPlayer.CurrentTime)} / {FormatClock(VibeFinderPreviewPlayer.Duration)}";
            }
            else Value = 0;
            ActionUrl = data.PreviewUrl is { Length: > 0 } preview
                ? $"themed://vibefinder/preview?url={Uri.EscapeDataString(preview)}" : data.SpotifyUrl;
            SecondaryActionUrl = data.AppleUrl;
            ImageUrl = _type is MeasureType.VibePlaybackState or MeasureType.VibeTrackProgress ? null : data.CoverArtUrl;
        }
        else if (vibeText is null)
        {
            Text = "Config Err";
        }
        LogTextTransitionIfChanged();
    }

    private static string BuildCacheKey(string username, string password, string vibeText)
        => $"{username}\u001f{password}\u001f{vibeText.Trim()}";

    private string? ResolveVibeText(string rawPhrase)
    {
        if (!string.Equals(rawPhrase.Trim(), ActiveThemeSentinel, StringComparison.OrdinalIgnoreCase))
            return rawPhrase.Trim();
        var theme = _activeThemeProvider?.ActiveTheme;
        return theme is null ? null : ThemeVibeText.Describe(theme);
    }

    private async Task FetchAsync(ResultEntry entry, string username, string password, string vibeText)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string? token = await GetTokenAsync(username, password);
            if (token is null)
            {
                _logger.LogWarning("VibeFinderAI login failed for user \"{User}\"; retrying after backoff", username);
                return;
            }

            var resp = await PostAnalyzeAsync(token, vibeText);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                token = await GetTokenAsync(username, password, true);
                if (token is null) return;
                resp = await PostAnalyzeAsync(token, vibeText);
            }
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("VibeFinderAI /api/vibe/analyze returned {Status} after {ElapsedMs}ms", resp.StatusCode, sw.ElapsedMilliseconds);
                return;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            string mood = root.TryGetProperty("dominant_vibe", out var moodEl) ? moodEl.GetString() ?? "—" : "—";
            var tracks = new List<(string, string, string, string?, string?, string?, string?, string?)>();
            if (root.TryGetProperty("tracks", out var tracksEl) && tracksEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var track in tracksEl.EnumerateArray())
                {
                    string title = track.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "—" : "—";
                    string artist = track.TryGetProperty("artist", out var artistEl) ? artistEl.GetString() ?? "—" : "—";
                    string? spotify = null, apple = null, cover = null, preview = null, video = null;
                    if (track.TryGetProperty("spotify_uri", out var spotifyEl))
                    {
                        var uri = spotifyEl.GetString();
                        if (uri?.StartsWith("spotify:track:", StringComparison.Ordinal) == true) spotify = $"https://open.spotify.com/track/{uri.Substring(14)}";
                        else if (uri?.StartsWith("spotify:search:", StringComparison.Ordinal) == true) spotify = $"https://open.spotify.com/search/{uri.Substring(15)}";
                    }
                    if (track.TryGetProperty("apple_uri", out var appleEl)) apple = appleEl.GetString();
                    if (track.TryGetProperty("cover_art", out var coverEl)) cover = coverEl.GetString()?.Replace("100x100bb", "512x512bb");
                    if (track.TryGetProperty("preview_url", out var previewEl)) preview = previewEl.GetString();
                    if (track.TryGetProperty("youtube_video_id", out var videoEl)) video = videoEl.GetString();
                    if (!string.IsNullOrEmpty(cover)) tracks.Add((title, artist, mood, spotify, apple, cover, preview, video));
                }
            }
            if (tracks.Count == 0) tracks.Add(("No match", "—", mood, null, null, null, null, null));

            lock (entry.Lock)
            {
                entry.Tracks = tracks;
                entry.CurrentIndex = 0;
                entry.LastSuccess = DateTime.UtcNow;
            }
            _logger.LogInformation("VibeFinderMeasure[{MeasureName}]: fetched {Count} track(s) in {ElapsedMs}ms for \"{Phrase}\"", Name, tracks.Count, sw.ElapsedMilliseconds, vibeText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch VibeFinderAI recommendation after {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        finally
        {
            lock (entry.Lock) entry.FetchInFlight = false;
        }
    }

    private static Task<HttpResponseMessage> PostAnalyzeAsync(string token, string vibeText)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/vibe/analyze")
        {
            Content = JsonContent.Create(new { text = vibeText, track_limit = 20 })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return Http.SendAsync(req);
    }

    private async Task<string?> GetTokenAsync(string username, string password, bool forceRefresh = false)
    {
        string key = $"{username}|{password}";
        var entry = Tokens.GetOrAdd(key, _ => new TokenEntry());
        if (!forceRefresh && entry.AccessToken is not null) return entry.AccessToken;
        try
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password
            });
            var response = await Http.PostAsync($"{BaseUrl}/auth/token", form);
            if (!response.IsSuccessStatusCode)
            {
                entry.AccessToken = null;
                _logger.LogWarning("VibeFinderAI /auth/token returned {Status} for user \"{User}\"", response.StatusCode, username);
                return null;
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            entry.AccessToken = doc.RootElement.TryGetProperty("access_token", out var tokenEl) ? tokenEl.GetString() : null;
            return entry.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderAI login request failed for user \"{User}\"", username);
            return null;
        }
    }

    public void SkipNext()
    {
        if (VibeFinderWebState.IsActive) { VibeFinderWebState.SkipNext(); Refresh(); return; }
        var key = GetCurrentCacheKey();
        if (key is null) return;
        var entry = Cache.GetOrAdd(key, _ => new ResultEntry());
        lock (entry.Lock) if (entry.Tracks.Count > 0) entry.CurrentIndex = (entry.CurrentIndex + 1) % entry.Tracks.Count;
        Refresh();
    }

    public void SkipPrevious()
    {
        if (VibeFinderWebState.IsActive) { VibeFinderWebState.SkipPrevious(); Refresh(); return; }
        var key = GetCurrentCacheKey();
        if (key is null) return;
        var entry = Cache.GetOrAdd(key, _ => new ResultEntry());
        lock (entry.Lock) if (entry.Tracks.Count > 0) entry.CurrentIndex = entry.CurrentIndex <= 0 ? entry.Tracks.Count - 1 : entry.CurrentIndex - 1;
        Refresh();
    }

    private string? GetCurrentCacheKey()
    {
        var parts = _target.Split('|', 3);
        if (parts.Length < 3) return null;
        var vibe = ResolveVibeText(parts[2]);
        return BuildCacheKey(parts[0], parts[1], vibe ?? parts[2]);
    }

    private void LogTextTransitionIfChanged()
    {
        if (string.Equals(_lastLoggedText, Text, StringComparison.Ordinal)) return;
        _logger.LogDebug("VibeFinderMeasure[{MeasureName}] ({Type}): {Previous} -> {Current}", Name, _type, _lastLoggedText, Text);
        _lastLoggedText = Text;
    }

    private static string FormatClock(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}" : $"{span.Minutes}:{span.Seconds:D2}";
    }
}
