using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Holds the synchronized state from an active VibeFinder AI WebView2 embed.
/// The React app pushes its playback state via window.chrome.webview.postMessage,
/// which this class parses. When the web embed is active, VibeFinderMeasure will 
/// read from this state instead of polling the backend.
///
/// Two message types drive this state:
///   VIBEFINDER_RESULTS — posted by App.jsx when a new analysis finishes, carrying
///     the full track list so widgets can show the same results the embed displays
///     without waiting for the MusicPlayer to open.
///   VIBEFINDER_STATE — posted by MusicPlayer.jsx while the player is open, carrying
///     live playback state (isPlaying, currentTime, duration) for the currently
///     playing track.
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

    /// <summary>True while MusicPlayer.jsx is actively posting VIBEFINDER_STATE updates —
    /// i.e. the user has the full player open and a track loaded. False when only the
    /// result list is known (VIBEFINDER_RESULTS) but the player hasn't been opened.</summary>
    public static bool IsPlayerActive { get; private set; }

    // An action that routes a JSON command string to the active WebView2
    public static Action<string>? SendCommand { get; set; }

    // ── Track list from VIBEFINDER_RESULTS ──────────────────────────────────

    /// <summary>The full track list from the last VIBEFINDER_RESULTS message.</summary>
    public static IReadOnlyList<TrackInfo> Tracks => _tracks;
    private static List<TrackInfo> _tracks = new();

    /// <summary>Current index into <see cref="Tracks"/> — cycled by
    /// <see cref="SkipNext"/> / <see cref="SkipPrevious"/>.</summary>
    public static int CurrentIndex { get; private set; }

    public sealed class TrackInfo
    {
        public string Title { get; init; } = "—";
        public string Artist { get; init; } = "—";
        public string? CoverArt { get; init; }
        public string? PreviewUrl { get; init; }
    }

    /// <summary>Wires up real logging. Called once from VibeFinderAIPage/App startup — before
    /// that, this class silently no-ops on malformed messages via NullLogger, same as always.</summary>
    public static void Initialize(ILogger logger) => _logger = logger;

    public static void HandleMessage(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl))
            {
                _logger.LogDebug("VibeFinderWebState: message with no \"type\" field ignored: {Json}", Truncate(messageJson));
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

            // Not a bug by itself — VIBEFINDER_APP_READY and VIBEFINDER_LOGIN_RESULT are handled
            // upstream in VibeFinderAIPage/MainWindow before this method is even called — but
            // logging every other type at Trace means a genuinely new/renamed message type from
            // a frontend change shows up here instead of vanishing without a trace.
            _logger.LogTrace("VibeFinderWebState: unhandled message type {MessageType}", messageType);
        }
        catch (JsonException ex)
        {
            // Previously a bare `catch { }` "for security" — that reasoning still holds (we
            // should never let a malformed WebView2 message crash anything), but swallowing it
            // with zero trace is exactly the "can't analyse anything" gap: if the React app ever
            // posts a malformed payload mid-track-switch, this was the silent dead end.
            _logger.LogWarning(ex, "VibeFinderWebState: malformed message ignored: {Json}", Truncate(messageJson));
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";

    private static void HandleResults(JsonElement root)
    {
        if (!root.TryGetProperty("tracks", out var tracksEl) || tracksEl.ValueKind != JsonValueKind.Array)
        {
            _logger.LogDebug("VibeFinderWebState: VIBEFINDER_RESULTS had no tracks array");
            return;
        }

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

        if (newTracks.Count == 0)
        {
            _logger.LogDebug("VibeFinderWebState: VIBEFINDER_RESULTS carried an empty tracks array — ignoring");
            return;
        }

        _tracks = newTracks;
        CurrentIndex = 0;
        IsActive = true;
        _logger.LogInformation("VibeFinderWebState: received {Count} tracks from the embed; now showing track 0 ({Title})", newTracks.Count, newTracks[0].Title);

        // Populate the top-level fields from the first track so VibeFinderMeasure
        // can read them immediately without checking the list.
        ApplyCurrentTrack();
    }

    private static void HandlePlayerState(JsonElement root)
    {
        IsActive = true;
        IsPlayerActive = true;
        bool wasPlaying = IsPlaying;
        string previousTitle = Title;

        if (root.TryGetProperty("isPlaying", out var playEl)) IsPlaying = playEl.GetBoolean();
        if (root.TryGetProperty("title", out var titleEl)) Title = titleEl.GetString() ?? "—";
        if (root.TryGetProperty("artist", out var artistEl)) Artist = artistEl.GetString() ?? "—";
        if (root.TryGetProperty("coverArt", out var coverEl)) CoverArt = coverEl.GetString();
        if (root.TryGetProperty("previewUrl", out var previewEl)) PreviewUrl = previewEl.GetString();
        if (root.TryGetProperty("currentTime", out var curEl) && curEl.TryGetDouble(out var cTime)) CurrentTime = cTime;
        if (root.TryGetProperty("duration", out var durEl) && durEl.TryGetDouble(out var dur)) Duration = dur;

        if (wasPlaying != IsPlaying || !string.Equals(previousTitle, Title, StringComparison.Ordinal))
            _logger.LogDebug("VibeFinderWebState: player state -> {Title} / {Artist}, playing={IsPlaying}", Title, Artist, IsPlaying);
    }

    /// <summary>Applies the track at <see cref="CurrentIndex"/> to the top-level
    /// Title/Artist/CoverArt/PreviewUrl fields.</summary>
    private static void ApplyCurrentTrack()
    {
        if (_tracks.Count == 0) return;
        var track = _tracks[CurrentIndex];
        Title = track.Title;
        Artist = track.Artist;
        CoverArt = track.CoverArt;
        PreviewUrl = track.PreviewUrl;
        // Reset playback fields — we're browsing the result list, not playing
        IsPlaying = false;
        IsPlayerActive = false;
        CurrentTime = 0;
        Duration = 0;
        _logger.LogDebug("VibeFinderWebState: now browsing track {Index}/{Count} — {Title} (preview {HasPreview})",
            CurrentIndex, _tracks.Count, track.Title, track.PreviewUrl is null ? "MISSING" : "ok");
    }

    /// <summary>Advance to the next track in the result list. If the MusicPlayer is active
    /// in the embed, sends a "next" command to it instead of just cycling the list.</summary>
    public static void SkipNext()
    {
        if (IsPlayerActive && SendCommand != null)
        {
            _logger.LogDebug("VibeFinderWebState: SkipNext -> forwarding \"next\" to the embedded player");
            SendCommand("{\"command\":\"next\"}");
            return;
        }

        if (_tracks.Count == 0)
        {
            _logger.LogDebug("VibeFinderWebState: SkipNext ignored — no tracks loaded");
            return;
        }
        CurrentIndex = (CurrentIndex + 1) % _tracks.Count;
        ApplyCurrentTrack();
    }

    /// <summary>Go back to the previous track in the result list. If the MusicPlayer is active,
    /// sends a "prev" command instead.</summary>
    public static void SkipPrevious()
    {
        if (IsPlayerActive && SendCommand != null)
        {
            _logger.LogDebug("VibeFinderWebState: SkipPrevious -> forwarding \"prev\" to the embedded player");
            SendCommand("{\"command\":\"prev\"}");
            return;
        }

        if (_tracks.Count == 0)
        {
            _logger.LogDebug("VibeFinderWebState: SkipPrevious ignored — no tracks loaded");
            return;
        }
        CurrentIndex--;
        if (CurrentIndex < 0) CurrentIndex = _tracks.Count - 1;
        ApplyCurrentTrack();
    }

    public static void Detach()
    {
        _logger.LogDebug("VibeFinderWebState: Detach() — embed unloaded, state reset");
        IsActive = false;
        IsPlayerActive = false;
        SendCommand = null;
        _tracks = new List<TrackInfo>();
        CurrentIndex = 0;
    }
}
