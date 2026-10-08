using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// One native preview player shared by VibeFinder widgets.
///
/// The WinRT MediaPlayer object is owned by the UI apartment. Widget measures are refreshed from
/// worker threads, so this class never queries MediaPlayer/MediaPlaybackSession directly from those
/// workers; it mirrors playback state through the session events into thread-safe scalar fields.
/// </summary>
public static class VibeFinderPreviewPlayer
{
    private static readonly MediaPlayer _player;
    private static ILogger _logger = NullLogger.Instance;

    private static string? _currentUrl;
    private static int _hasSource;
    private static int _isPlaying;
    private static long _positionTicks;
    private static long _durationTicks;

    public static bool IsPlaying => Volatile.Read(ref _isPlaying) != 0;
    public static double CurrentTime => TimeSpan.FromTicks(Math.Max(0, Volatile.Read(ref _positionTicks))).TotalSeconds;
    public static double Duration => TimeSpan.FromTicks(Math.Max(0, Volatile.Read(ref _durationTicks))).TotalSeconds;
    public static double Progress => Duration > 0 ? Math.Clamp(CurrentTime / Duration, 0, 1) : 0;

    static VibeFinderPreviewPlayer()
    {
        _player = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media
        };

        var session = _player.PlaybackSession;

        session.PlaybackStateChanged += (_, _) =>
        {
            try
            {
                var playing = session.PlaybackState == MediaPlaybackState.Playing;
                Volatile.Write(ref _isPlaying, playing ? 1 : 0);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "VibeFinderPreviewPlayer: failed to read PlaybackStateChanged state");
            }
        };

        session.PositionChanged += (sender, _) =>
        {
            try
            {
                Volatile.Write(ref _positionTicks, Math.Max(0, sender.Position.Ticks));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "VibeFinderPreviewPlayer: failed to read playback position");
            }
        };

        session.NaturalDurationChanged += (sender, _) =>
        {
            try
            {
                Volatile.Write(ref _durationTicks, Math.Max(0, sender.NaturalDuration.Ticks));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "VibeFinderPreviewPlayer: failed to read natural duration");
            }
        };

        _player.MediaFailed += (_, args) =>
        {
            _logger.LogWarning(
                "VibeFinderPreviewPlayer: MediaFailed for {Url} — {ErrorCode} {ErrorMessage}",
                _currentUrl,
                args.Error,
                args.ErrorMessage);

            Volatile.Write(ref _isPlaying, 0);
            Volatile.Write(ref _positionTicks, 0);
            Volatile.Write(ref _durationTicks, 0);
            Volatile.Write(ref _hasSource, 0);
            _currentUrl = null;

            try { _player.Source = null; }
            catch (Exception ex) { _logger.LogDebug(ex, "VibeFinderPreviewPlayer: failed to clear failed media source"); }
        };

        _player.MediaEnded += (_, _) =>
        {
            Volatile.Write(ref _isPlaying, 0);
            _logger.LogDebug("VibeFinderPreviewPlayer: preview clip ended ({Url})", _currentUrl);
        };
    }

    /// <summary>Wires up real logging. Called once during app startup before widgets are opened.</summary>
    public static void Initialize(ILogger logger) => _logger = logger;

    public static void Play(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogDebug("VibeFinderPreviewPlayer: Play() called with no URL — nothing to play");
            return;
        }

        try
        {
            _player.Source = MediaSource.CreateFromUri(new Uri(url));
            _currentUrl = url;
            Volatile.Write(ref _hasSource, 1);
            Volatile.Write(ref _isPlaying, 1);
            Volatile.Write(ref _positionTicks, 0);
            Volatile.Write(ref _durationTicks, 0);
            _player.Play();
            _logger.LogDebug("VibeFinderPreviewPlayer: playing {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderPreviewPlayer: Play() failed for {Url}", url);
            Volatile.Write(ref _isPlaying, 0);
            Volatile.Write(ref _hasSource, 0);
            Volatile.Write(ref _positionTicks, 0);
            Volatile.Write(ref _durationTicks, 0);
            _currentUrl = null;
            try { _player.Source = null; } catch { }
        }
    }

    public static void TogglePause(string? url)
    {
        if (IsPlaying)
        {
            _logger.LogDebug("VibeFinderPreviewPlayer: pausing {Url}", _currentUrl);
            try { _player.Pause(); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VibeFinderPreviewPlayer: Pause() failed");
            }
            Volatile.Write(ref _isPlaying, 0);
            return;
        }

        if (Volatile.Read(ref _hasSource) != 0)
        {
            _logger.LogDebug("VibeFinderPreviewPlayer: resuming {Url}", _currentUrl);
            try
            {
                _player.Play();
                Volatile.Write(ref _isPlaying, 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VibeFinderPreviewPlayer: resume failed");
                Volatile.Write(ref _isPlaying, 0);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            Play(url);
            return;
        }

        _logger.LogDebug("VibeFinderPreviewPlayer: TogglePause() called with no URL and nothing loaded");
    }

    public static void Stop()
    {
        _logger.LogDebug("VibeFinderPreviewPlayer: stop ({Url})", _currentUrl);
        try { _player.Pause(); } catch { }
        try { _player.Source = null; } catch { }

        _currentUrl = null;
        Volatile.Write(ref _hasSource, 0);
        Volatile.Write(ref _isPlaying, 0);
        Volatile.Write(ref _positionTicks, 0);
        Volatile.Write(ref _durationTicks, 0);
    }
}
