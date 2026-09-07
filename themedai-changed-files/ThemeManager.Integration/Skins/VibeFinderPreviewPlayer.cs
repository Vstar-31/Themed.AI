using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// A simple native audio player for VibeFinder's 30s preview URLs.
/// Replaces the broken YouTube WebView2 approach, since the backend doesn't return youtube_video_id.
/// </summary>
public static class VibeFinderPreviewPlayer
{
    private static readonly MediaPlayer _player;
    private static ILogger _logger = NullLogger.Instance;

    // The URL currently loaded into _player.Source, kept alongside it because MediaPlayer
    // doesn't expose the source URI back out for logging once it's been set.
    private static string? _currentUrl;

    public static bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public static double CurrentTime => _player.PlaybackSession.Position.TotalSeconds;
    public static double Duration => _player.PlaybackSession.NaturalDuration.TotalSeconds;
    public static double Progress => Duration > 0 ? CurrentTime / Duration : 0;

    static VibeFinderPreviewPlayer()
    {
        _player = new MediaPlayer();
        _player.AudioCategory = MediaPlayerAudioCategory.Media;
        _player.MediaFailed += (sender, args) =>
        {
            // Previously an empty handler — "swallow media failures so they don't propagate to
            // the background thread and crash the process" was the right call, but silently
            // leaving _player.Source pointed at the URL that just failed was not: the next
            // TogglePause() call would see Source != null, assume playback was already loaded,
            // and call _player.Play() on a source that can never succeed — every subsequent
            // play/pause click on that widget would then silently no-op with nothing in the
            // logs. iTunes preview URLs do fail this way in practice (expired signed URL,
            // regional block, transient 404), so this was reachable, not theoretical — and is
            // very likely a real contributor to "themed.ai bugging after a few play/pause
            // cycles". Resetting Source here means the NEXT Play() call starts clean instead
            // of retrying a URL already known to be dead.
            _logger.LogWarning("VibeFinderPreviewPlayer: MediaFailed for {Url} — {ErrorCode} {ErrorMessage}",
                _currentUrl, args.Error, args.ErrorMessage);
            _player.Source = null;
            _currentUrl = null;
        };
        _player.MediaEnded += (sender, args) =>
            _logger.LogDebug("VibeFinderPreviewPlayer: preview clip ended ({Url})", _currentUrl);
    }

    /// <summary>Wires up real logging. Called once from App.xaml.cs during startup — before that,
    /// this class silently no-ops on failures via NullLogger, same as it always has.</summary>
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
            _player.Play();
            _logger.LogDebug("VibeFinderPreviewPlayer: playing {Url}", url);
        }
        catch (Exception ex)
        {
            // Previously an empty `catch (Exception) { }` — a malformed preview URL used to
            // fail completely silently here.
            _logger.LogWarning(ex, "VibeFinderPreviewPlayer: Play() failed for {Url}", url);
            _player.Source = null;
            _currentUrl = null;
        }
    }

    public static void TogglePause(string? url)
    {
        if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            _logger.LogDebug("VibeFinderPreviewPlayer: pausing {Url}", _currentUrl);
            _player.Pause();
        }
        else if (_player.Source == null && !string.IsNullOrWhiteSpace(url))
        {
            Play(url);
        }
        else if (_player.Source != null)
        {
            _logger.LogDebug("VibeFinderPreviewPlayer: resuming {Url}", _currentUrl);
            _player.Play();
        }
        else
        {
            // Source is null AND no url was supplied — e.g. the widget is showing a track with
            // no preview_url at all (VibeFinderMeasure's "No match" placeholder track has one).
            // Previously this fell into the resume branch below and called _player.Play() on an
            // empty player: a click that visibly did nothing, with nothing logged to explain why.
            _logger.LogDebug("VibeFinderPreviewPlayer: TogglePause() called with no URL and nothing loaded — nothing to play");
        }
    }

    public static void Stop()
    {
        _logger.LogDebug("VibeFinderPreviewPlayer: stop ({Url})", _currentUrl);
        _player.Pause();
        _player.Source = null;
        _currentUrl = null;
    }
}
