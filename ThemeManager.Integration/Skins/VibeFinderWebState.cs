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

    /// <summary>Raised whenever the VibeFinder embed produces new result or playback state.</summary>
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
            if (!root.TryGetProperty("type", out var typeEl))
            {
                _logger.LogDebug("VibeFinderWebState: message with no type field ignored: {Json}", Truncate(messageJson));
                return;
            }

            var messageType = typeEl.GetString();
            if (messageType == "VIBEFINDER_RESULTS")
            {
                HandleResults(root);
                return;
            }

            if (messageType == "VIBEFINDER_STATE")
            {
                HandlePlayerState(root);
                return;
            }

            _logger.LogTrace("VibeFinderWebState: unhandled message type {MessageType}", messageType);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: malformed message ignored: {Json}", Truncate(messageJson));
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";

    private static void HandleResults(JsonElement root)
    {
        if (!root.TryGetProperty("tracks", out var tracksEl) || tracksEl.ValueKind != JsonValueKind.Array)
            return;

        var newTracks = new List<TrackInfo>();
        foreach (var t in tracksEl.EnumerateArray())
        {
            newTracks.Add(new TrackInfo
            {
                Title = t.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "—" : "—",
                Artist = t.TryGetProperty("artist", out var aEl) ? aEl.GetString() ?? "—" : "—",
                CoverArt = t.TryGetProperty("cover_art", out var cEl) ? cEl.GetString() : null,
                PreviewUrl = t.TryGetProperty("preview_url", out var pEl) ? pEl.GetString() : null,
            });
        }

        if (newTracks.Count == 0) return;
        _tracks = newTracks;
        CurrentIndex = 0;
        IsActive = true;
        ApplyCurrentTrack();
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void HandlePlayerState(JsonElement root)
    {
        IsActive = true;
        IsPlayerActive = true;

        if (root.TryGetProperty("isPlaying", out var playEl)) IsPlaying = playEl.GetBoolean();
        if (root.TryGetProperty("title", out var titleEl)) Title = titleEl.GetString() ?? "—";
        if (root.TryGetProperty("artist", out var artistEl)) Artist = artistEl.GetString() ?? "—";
        if (root.TryGetProperty("coverArt", out var coverEl)) CoverArt = coverEl.GetString();
        if (root.TryGetProperty("previewUrl", out var previewEl)) PreviewUrl = previewEl.GetString();
        if (root.TryGetProperty("currentTime", out var curEl) && curEl.TryGetDouble(out var cTime)) CurrentTime = cTime;
        if (root.TryGetProperty("duration", out var durEl) && durEl.TryGetDouble(out var dur)) Duration = dur;

        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ApplyCurrentTrack()
    {
        if (_tracks.Count == 0) return;
        var track = _tracks[CurrentIndex];
        Title = track.Title;
        Artist = track.Artist;
        CoverArt = track.CoverArt;
        PreviewUrl = track.PreviewUrl;
        IsPlaying = false;
        IsPlayerActive = false;
        CurrentTime = 0;
        Duration = 0;
    }

    public static void SkipNext()
    {
        if (IsPlayerActive && SendCommand != null)
        {
            SendCommand("{\"command\":\"next\"}");
            return;
        }
        if (_tracks.Count == 0) return;
        CurrentIndex = (CurrentIndex + 1) % _tracks.Count;
        ApplyCurrentTrack();
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void SkipPrevious()
    {
        if (IsPlayerActive && SendCommand != null)
        {
            SendCommand("{\"command\":\"prev\"}");
            return;
        }
        if (_tracks.Count == 0) return;
        CurrentIndex--;
        if (CurrentIndex < 0) CurrentIndex = _tracks.Count - 1;
        ApplyCurrentTrack();
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Detach()
    {
        IsActive = false;
        IsPlayerActive = false;
        SendCommand = null;
        _tracks = new List<TrackInfo>();
        CurrentIndex = 0;
        StateChanged?.Invoke(null, EventArgs.Empty);
    }
}
