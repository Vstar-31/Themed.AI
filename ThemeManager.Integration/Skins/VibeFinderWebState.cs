using System;
using System.Collections.Generic;
using System.Text.Json;

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

    public static void HandleMessage(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;

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
        }
        catch
        {
            // Ignore malformed messages for security
        }
    }

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

        // Populate the top-level fields from the first track so VibeFinderMeasure
        // can read them immediately without checking the list.
        ApplyCurrentTrack();
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
    }

    /// <summary>Advance to the next track in the result list. If the MusicPlayer is active
    /// in the embed, sends a "next" command to it instead of just cycling the list.</summary>
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
    }

    /// <summary>Go back to the previous track in the result list. If the MusicPlayer is active,
    /// sends a "prev" command instead.</summary>
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
    }

    public static void Detach()
    {
        IsActive = false;
        IsPlayerActive = false;
        SendCommand = null;
        _tracks = new List<TrackInfo>();
        CurrentIndex = 0;
    }
}
