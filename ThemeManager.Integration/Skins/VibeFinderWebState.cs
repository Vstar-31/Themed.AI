using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Holds the synchronized state from an active VibeFinder AI WebView2 embed.
/// The React app pushes its playback state via window.chrome.webview.postMessage,
/// which this class parses. When the web embed is active, VibeFinderMeasure reads from this state.
/// </summary>
public static class VibeFinderWebState
{
    private static ILogger _logger = NullLogger.Instance;
    public static bool IsActive { get; private set; }
    public static bool IsPlaying { get; private set; }
    public static string Title { get; private set; } = "—";
    public static string Artist { get; private set; } = "—";
    public static string? CoverArt { get; private set; }
    public static string? PreviewUrl { get; private set; }
    public static double CurrentTime { get; private set; }
    public static double Duration { get; private set; }
    public static double Progress => Duration > 0 ? CurrentTime / Duration : 0;
    public static bool IsPlayerActive { get; private set; }
    public static event EventHandler? StateChanged;
    public static Action<string>? SendCommand { get; set; }
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
            if (typeEl.GetString() == "VIBEFINDER_RESULTS") { HandleResults(root); return; }
            if (typeEl.GetString() == "VIBEFINDER_STATE") { HandlePlayerState(root); return; }
        }
        catch (JsonException ex) { _logger.LogWarning(ex, "VibeFinderWebState: malformed message ignored: {Json}", Truncate(messageJson)); }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";

    private static void HandleResults(JsonElement root)
    {
        if (!root.TryGetProperty("tracks", out var tracksEl) || tracksEl.ValueKind != JsonValueKind.Array) return;
        var newTracks = new List<TrackInfo>();
        foreach (var t in tracksEl.EnumerateArray())
            newTracks.Add(new TrackInfo { Title = t.TryGetProperty("title", out var title) ? title.GetString() ?? "—" : "—", Artist = t.TryGetProperty("artist", out var artist) ? artist.GetString() ?? "—" : "—", CoverArt = t.TryGetProperty("cover_art", out var cover) ? cover.GetString() : null, PreviewUrl = t.TryGetProperty("preview_url", out var preview) ? preview.GetString() : null });
        if (newTracks.Count == 0) return;
        _tracks = newTracks; CurrentIndex = 0; IsActive = true; ApplyCurrentTrack(); StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void HandlePlayerState(JsonElement root)
    {
        IsActive = true; IsPlayerActive = true;
        if (root.TryGetProperty("isPlaying", out var play)) IsPlaying = play.GetBoolean();
        if (root.TryGetProperty("title", out var title)) Title = title.GetString() ?? "—";
        if (root.TryGetProperty("artist", out var artist)) Artist = artist.GetString() ?? "—";
        if (root.TryGetProperty("coverArt", out var cover)) CoverArt = cover.GetString();
        if (root.TryGetProperty("previewUrl", out var preview)) PreviewUrl = preview.GetString();
        if (root.TryGetProperty("currentTime", out var cur) && cur.TryGetDouble(out var c)) CurrentTime = c;
        if (root.TryGetProperty("duration", out var dur) && dur.TryGetDouble(out var d)) Duration = d;
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ApplyCurrentTrack()
    {
        if (_tracks.Count == 0) return;
        var track = _tracks[CurrentIndex]; Title = track.Title; Artist = track.Artist; CoverArt = track.CoverArt; PreviewUrl = track.PreviewUrl;
        IsPlaying = false; IsPlayerActive = false; CurrentTime = 0; Duration = 0;
    }

    public static void SkipNext()
    {
        if (IsPlayerActive && SendCommand != null) { SendCommand("{\"command\":\"next\"}"); return; }
        if (_tracks.Count == 0) return;
        CurrentIndex = (CurrentIndex + 1) % _tracks.Count; ApplyCurrentTrack(); StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void SkipPrevious()
    {
        if (IsPlayerActive && SendCommand != null) { SendCommand("{\"command\":\"prev\"}"); return; }
        if (_tracks.Count == 0) return;
        CurrentIndex = (CurrentIndex - 1 + _tracks.Count) % _tracks.Count; ApplyCurrentTrack(); StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Detach()
    {
        IsActive = false; IsPlayerActive = false; SendCommand = null; _tracks = new List<TrackInfo>(); CurrentIndex = 0; StateChanged?.Invoke(null, EventArgs.Empty);
    }
}
