using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Holds synchronized state from the active VibeFinder AI embed and provides a resilient command
/// bridge for desktop widget controls. The web player remains the preferred playback engine; when
/// a command is sent successfully but no corresponding state update arrives, the bridge falls back
/// to the native 30-second preview so widgets never become permanently dead controls.
/// </summary>
public static class VibeFinderWebState
{
    private static ILogger _logger = NullLogger.Instance;
    private static Action<string>? _sendCommand;
    private static long _stateVersion;
    private static int _commandGeneration;
    private static CancellationTokenSource? _nativeFallbackCts;

    public static bool IsActive { get; private set; }
    public static bool IsPlaying { get; private set; }
    public static string Title { get; private set; } = "—";
    public static string Artist { get; private set; } = "—";
    public static string? CoverArt { get; private set; }
    public static string? PreviewUrl { get; private set; }
    public static double CurrentTime { get; private set; }
    public static double Duration { get; private set; }
    public static double Progress => Duration > 0 ? Math.Clamp(CurrentTime / Duration, 0, 1) : 0;
    public static bool IsPlayerActive { get; private set; }
    public static event EventHandler? StateChanged;

    /// <summary>
    /// Host-to-web command channel. Non-playback messages pass through immediately. Playback
    /// commands are wrapped so a stalled WebView/player can fall back to native preview playback.
    /// </summary>
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
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: malformed message ignored: {Json}", Truncate(messageJson));
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";

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
        CancelNativeFallback();
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

        if (root.TryGetProperty("isPlaying", out var play) && play.ValueKind is JsonValueKind.True or JsonValueKind.False)
            IsPlaying = play.GetBoolean();
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
            if (string.Equals(_tracks[i].Title, Title, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_tracks[i].Artist, Artist, StringComparison.OrdinalIgnoreCase))
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
            if (doc.RootElement.TryGetProperty("command", out var commandEl))
                command = commandEl.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: command JSON malformed; passing through unchanged");
        }

        if (command is not ("playpause" or "next" or "prev"))
        {
            sender(commandJson);
            return;
        }

        var beforeVersion = Volatile.Read(ref _stateVersion);
        var beforePlaying = IsPlaying;
        var beforeTitle = Title;
        var generation = Interlocked.Increment(ref _commandGeneration);

        try
        {
            sender(commandJson);
            _logger.LogDebug("VibeFinderWebState: dispatched playback command {Command} (generation {Generation})", command, generation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderWebState: web playback command {Command} threw; using native fallback", command);
            StartNativeFallback(command, beforePlaying, beforeTitle);
            return;
        }

        _ = MonitorPlaybackCommandAsync(command!, beforeVersion, beforePlaying, beforeTitle, generation);
    }

    private static async Task MonitorPlaybackCommandAsync(string command, long beforeVersion, bool beforePlaying, string beforeTitle, int generation)
    {
        try
        {
            await Task.Delay(900).ConfigureAwait(false);
            if (generation != Volatile.Read(ref _commandGeneration)) return;

            var changed = Volatile.Read(ref _stateVersion) != beforeVersion;
            var playingChanged = IsPlaying != beforePlaying;
            var trackChanged = !string.Equals(Title, beforeTitle, StringComparison.OrdinalIgnoreCase);

            if (changed && (command == "playpause" ? playingChanged : trackChanged)) return;

            if (command == "playpause" && beforePlaying)
            {
                // The requested action was pause. Retrying is safer than starting a second audio
                // source when the external player is still playing but failed to report state.
                _logger.LogDebug("VibeFinderWebState: pause command produced no state transition; retrying once");
                _sendCommand?.Invoke("{\"command\":\"playpause\"}");
                await Task.Delay(600).ConfigureAwait(false);
                return;
            }

            StartNativeFallback(command, beforePlaying, beforeTitle);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VibeFinderWebState: playback command monitor aborted");
        }
    }

    private static void StartNativeFallback(string command, bool beforePlaying, string beforeTitle)
    {
        if (_tracks.Count == 0) return;

        CancelNativeFallback();
        var generation = Volatile.Read(ref _commandGeneration);

        if (command == "next")
            CurrentIndex = (CurrentIndex + 1) % _tracks.Count;
        else if (command == "prev")
            CurrentIndex = (CurrentIndex - 1 + _tracks.Count) % _tracks.Count;

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
            // A failed pause cannot safely be mirrored by a second audio engine; retain the web
            // player's state and only log the bridge failure.
            _logger.LogWarning("VibeFinderWebState: web player did not acknowledge pause; leaving external playback untouched");
            return;
        }

        VibeFinderPreviewPlayer.Play(track.PreviewUrl);
        IsPlaying = VibeFinderPreviewPlayer.IsPlaying;
        Duration = VibeFinderPreviewPlayer.Duration;
        _logger.LogWarning("VibeFinderWebState: web player command {Command} timed out; native preview fallback active for \"{Title}\"", command, track.Title);
        StateChanged?.Invoke(null, EventArgs.Empty);

        var cts = new CancellationTokenSource();
        _nativeFallbackCts = cts;
        _ = MonitorNativeFallbackAsync(generation, cts.Token);
    }

    private static async Task MonitorNativeFallbackAsync(int generation, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && generation == Volatile.Read(ref _commandGeneration))
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
        if (SendCommand != null && IsPlayerActive)
        {
            SendCommand("{\"command\":\"next\"}");
            return;
        }
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
        if (SendCommand != null && IsPlayerActive)
        {
            SendCommand("{\"command\":\"prev\"}");
            return;
        }
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
        IsActive = false;
        IsPlayerActive = false;
        _sendCommand = null;
        _tracks = new List<TrackInfo>();
        CurrentIndex = 0;
        Interlocked.Increment(ref _commandGeneration);
        Interlocked.Increment(ref _stateVersion);
        StateChanged?.Invoke(null, EventArgs.Empty);
    }
}
