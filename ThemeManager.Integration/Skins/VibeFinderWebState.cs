using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThemeManager.Core.Models;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Holds synchronized state from the active VibeFinder AI embed and provides a resilient command
/// bridge for desktop widget controls.
/// </summary>
public static class VibeFinderWebState
{
    private static ILogger _logger = NullLogger.Instance;
    private static Action<string>? _sendCommand;
    private static long _stateVersion;
    private static CancellationTokenSource? _nativeFallbackCts;

    public static bool IsActive { get; private set; }
    public static bool IsPlaying { get; private set; }
    public static string Title { get; private set; } = "—";
    public static string Artist { get; private set; } = "—";
    public static string? CoverArt { get; private set; }
    public static string? PreviewUrl { get; private set; }
    public static string? DominantVibe { get; private set; }
    public static double CurrentTime { get; private set; }
    public static double Duration { get; private set; }
    public static double Progress => Duration > 0 ? Math.Clamp(CurrentTime / Duration, 0, 1) : 0;
    public static bool IsPlayerActive { get; private set; }
    public static VibePersonalizationProfile Profile { get; private set; } = VibePersonalizationProfile.Empty;
    public static event EventHandler? StateChanged;

    public static Action<string>? SendCommand
    {
        get => _sendCommand is null ? null : DispatchCommand;
        set => _sendCommand = value;
    }

    public static IReadOnlyList<TrackInfo> Tracks => _tracks;
    private static List<TrackInfo> _tracks = new();
    public static int CurrentIndex { get; private set; }

    public sealed class TrackInfo
    {
        public string Title { get; init; } = "—";
        public string Artist { get; init; } = "—";
        public string? CoverArt { get; init; }
        public string? PreviewUrl { get; init; }
    }

    public static void Initialize(ILogger logger) => _logger = logger;

    public static void HandleMessage(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();
            if (type == "VIBEFINDER_RESULTS") { HandleResults(root); return; }
            if (type == "VIBEFINDER_STATE") { HandlePlayerState(root); return; }
            if (type == "VIBEFINDER_ANALYSIS_COMPLETE") { HandleAnalysisComplete(root); return; }
            if (type == "VIBEFINDER_PLAYBACK_RESET") { HandlePlaybackReset(); return; }
            if (type == "VIBEFINDER_PROFILE") { HandleProfile(root); return; }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: malformed message ignored: {Json}", Truncate(messageJson));
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";

    private static void HandleProfile(JsonElement root)
    {
        if (!root.TryGetProperty("profile", out var profileEl) || profileEl.ValueKind != JsonValueKind.Object) return;
        Profile = new VibePersonalizationProfile(
            ReadDouble(profileEl, "signals"),
            ReadScoreMap(profileEl, "topMoods"),
            ReadScoreMap(profileEl, "topArtists"),
            ReadScoreMap(profileEl, "topGenres"),
            ReadInterfaceMap(profileEl, "interface", "themes"),
            ReadInterfaceMap(profileEl, "interface", "ambience"),
            ReadStringList(profileEl, "recentContexts"));
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static double ReadDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetDouble(out var d) ? d : 0;

    private static Dictionary<string, double> ReadScoreMap(JsonElement root, string name)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in array.EnumerateArray())
        {
            if (!item.TryGetProperty("value", out var value) || !item.TryGetProperty("score", out var score)) continue;
            var key = value.GetString();
            if (!string.IsNullOrWhiteSpace(key) && score.TryGetDouble(out var numeric)) result[key] = numeric;
        }
        return result;
    }

    private static Dictionary<string, double> ReadInterfaceMap(JsonElement root, string containerName, string fieldName)
    {
        if (!root.TryGetProperty(containerName, out var container) || container.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        return container.TryGetProperty(fieldName, out _) ? ReadScoreMap(container, fieldName) : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement root, string name)
    {
        var result = new List<string>();
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in array.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : item.TryGetProperty("value", out var nested) ? nested.GetString() : null;
            if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
        }
        return result;
    }

    private static void HandleAnalysisComplete(JsonElement root)
    {
        if (root.TryGetProperty("vibe", out var vibeEl) && vibeEl.ValueKind == JsonValueKind.String)
            DominantVibe = vibeEl.GetString();

        Interlocked.Increment(ref _stateVersion);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void HandlePlaybackReset()
    {
        CancelNativeFallback();
        VibeFinderPreviewPlayer.Stop();
        IsPlaying = false;
        IsPlayerActive = false;
        CurrentTime = 0;
        Duration = 0;
        Interlocked.Increment(ref _stateVersion);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void HandleResults(JsonElement root)
    {
        if (!root.TryGetProperty("tracks", out var tracksEl) || tracksEl.ValueKind != JsonValueKind.Array) return;
        var newTracks = new List<TrackInfo>();
        foreach (var t in tracksEl.EnumerateArray())
        {
            newTracks.Add(new TrackInfo
            {
                Title = t.TryGetProperty("title", out var title) ? title.GetString() ?? "—" : "—",
                Artist = t.TryGetProperty("artist", out var artist) ? artist.GetString() ?? "—" : "—",
                CoverArt = t.TryGetProperty("cover_art", out var cover) ? cover.GetString() : null,
                PreviewUrl = t.TryGetProperty("preview_url", out var preview) ? preview.GetString() : null,
            });
        }
        if (newTracks.Count == 0) return;
        // Never let an old native preview survive a fresh web result set.
        CancelNativeFallback();
        VibeFinderPreviewPlayer.Stop();
        _tracks = newTracks;
        CurrentIndex = 0;
        IsActive = true;
        ApplyCurrentTrack();
        PublishState();
    }

    private static void HandlePlayerState(JsonElement root)
    {
        CancelNativeFallback();
        IsActive = true;
        IsPlayerActive = true;
        if (root.TryGetProperty("isPlaying", out var play) && play.ValueKind is JsonValueKind.True or JsonValueKind.False) IsPlaying = play.GetBoolean();
        if (root.TryGetProperty("title", out var title)) Title = title.GetString() ?? "—";
        if (root.TryGetProperty("artist", out var artist)) Artist = artist.GetString() ?? "—";
        if (root.TryGetProperty("coverArt", out var cover)) CoverArt = cover.GetString();
        if (root.TryGetProperty("previewUrl", out var preview)) PreviewUrl = preview.GetString();
        if (root.TryGetProperty("currentTime", out var cur) && cur.TryGetDouble(out var c)) CurrentTime = Math.Max(0, c);
        if (root.TryGetProperty("duration", out var dur) && dur.TryGetDouble(out var d)) Duration = Math.Max(0, d);
        SyncCurrentIndexToPlayerTrack();
        Interlocked.Increment(ref _stateVersion);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void SyncCurrentIndexToPlayerTrack()
    {
        if (_tracks.Count == 0 || string.IsNullOrWhiteSpace(Title)) return;
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (string.Equals(_tracks[i].Title, Title, StringComparison.OrdinalIgnoreCase) && string.Equals(_tracks[i].Artist, Artist, StringComparison.OrdinalIgnoreCase))
            {
                CurrentIndex = i;
                return;
            }
        }
    }

    private static void ApplyCurrentTrack()
    {
        if (_tracks.Count == 0) return;
        CurrentIndex = Math.Clamp(CurrentIndex, 0, _tracks.Count - 1);
        var track = _tracks[CurrentIndex];
        Title = track.Title;
        Artist = track.Artist;
        CoverArt = track.CoverArt;
        PreviewUrl = track.PreviewUrl;
        IsPlaying = false;
        IsPlayerActive = false;
        CurrentTime = 0;
        Duration = 0;
        Interlocked.Increment(ref _stateVersion);
    }

    private static void PublishState()
    {
        Interlocked.Increment(ref _stateVersion);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void DispatchCommand(string commandJson)
    {
        var sender = _sendCommand;
        if (sender is null) return;

        string? command = null;
        try
        {
            using var doc = JsonDocument.Parse(commandJson);
            if (doc.RootElement.TryGetProperty("command", out var commandEl)) command = commandEl.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: command JSON malformed; passing through unchanged");
        }

        try
        {
            // When a real WebView2 bridge exists, it is the single playback authority.
            // Do not launch a native fallback merely because the browser state update is delayed.
            sender(commandJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: web playback command {Command} threw; using native fallback", command ?? "unknown");
            if (command is "playpause" or "next" or "prev")
                StartNativeFallback(command, IsPlaying);
        }
    }

    private static void StartNativeFallback(string command, bool beforePlaying)
    {
        if (_tracks.Count == 0) return;
        CancelNativeFallback();
        if (command == "next") CurrentIndex = (CurrentIndex + 1) % _tracks.Count;
        else if (command == "prev") CurrentIndex = (CurrentIndex - 1 + _tracks.Count) % _tracks.Count;
        var track = _tracks[Math.Clamp(CurrentIndex, 0, _tracks.Count - 1)];
        Title = track.Title;
        Artist = track.Artist;
        CoverArt = track.CoverArt;
        PreviewUrl = track.PreviewUrl;
        CurrentTime = 0;
        Duration = 0;
        IsActive = true;
        IsPlayerActive = true;
        if (command == "playpause" && beforePlaying)
        {
            VibeFinderPreviewPlayer.Stop();
            IsPlaying = false;
            StateChanged?.Invoke(null, EventArgs.Empty);
            return;
        }
        VibeFinderPreviewPlayer.Play(track.PreviewUrl);
        IsPlaying = VibeFinderPreviewPlayer.IsPlaying;
        Duration = VibeFinderPreviewPlayer.Duration;
        StateChanged?.Invoke(null, EventArgs.Empty);
        var cts = new CancellationTokenSource();
        _nativeFallbackCts = cts;
        _ = MonitorNativeFallbackAsync(cts.Token);
    }

    private static async Task MonitorNativeFallbackAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(500, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;
                CurrentTime = VibeFinderPreviewPlayer.CurrentTime;
                Duration = VibeFinderPreviewPlayer.Duration;
                IsPlaying = VibeFinderPreviewPlayer.IsPlaying;
                Interlocked.Increment(ref _stateVersion);
                StateChanged?.Invoke(null, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static void CancelNativeFallback()
    {
        var cts = Interlocked.Exchange(ref _nativeFallbackCts, null);
        if (cts is null) return;
        try { cts.Cancel(); } finally { cts.Dispose(); }
    }

    public static void SkipNext()
    {
        if (_tracks.Count == 0) return;
        if (SendCommand != null && IsPlayerActive) { SendCommand("{\"command\":\"next\"}"); return; }
        CurrentIndex = (CurrentIndex + 1) % _tracks.Count;
        ApplyCurrentTrack();
        VibeFinderPreviewPlayer.Play(PreviewUrl);
        IsActive = true;
        IsPlayerActive = true;
        IsPlaying = VibeFinderPreviewPlayer.IsPlaying;
        PublishState();
    }

    public static void SkipPrevious()
    {
        if (_tracks.Count == 0) return;
        if (SendCommand != null && IsPlayerActive) { SendCommand("{\"command\":\"prev\"}"); return; }
        CurrentIndex = (CurrentIndex - 1 + _tracks.Count) % _tracks.Count;
        ApplyCurrentTrack();
        VibeFinderPreviewPlayer.Play(PreviewUrl);
        IsActive = true;
        IsPlayerActive = true;
        IsPlaying = VibeFinderPreviewPlayer.IsPlaying;
        PublishState();
    }

    public static void Detach()
    {
        CancelNativeFallback();
        VibeFinderPreviewPlayer.Stop();
        IsActive = false;
        IsPlayerActive = false;
        _sendCommand = null;
        _tracks = new List<TrackInfo>();
        CurrentIndex = 0;
        Profile = VibePersonalizationProfile.Empty;
        Interlocked.Increment(ref _stateVersion);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }
}
