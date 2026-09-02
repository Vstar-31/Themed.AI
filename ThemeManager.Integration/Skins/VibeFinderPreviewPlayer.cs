using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// A simple native audio player for VibeFinder's 30s preview URLs.
/// Replaces the broken YouTube WebView2 approach, since the backend doesn't return youtube_video_id.
/// </summary>
public static class VibeFinderPreviewPlayer
{
    private static readonly MediaPlayer _player;
    
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
            // Swallow media failures (e.g. 404s, network drops) so they don't propagate
            // to the background thread and crash the process with STATUS_STOWED_EXCEPTION.
        };
    }

    public static void Play(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            _player.Source = MediaSource.CreateFromUri(new Uri(url));
            _player.Play();
        }
        catch (Exception)
        {
            // Ignore bad URLs
        }
    }

    public static void TogglePause(string? url)
    {
        if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            _player.Pause();
        }
        else if (_player.Source == null && !string.IsNullOrWhiteSpace(url))
        {
            Play(url);
        }
        else
        {
            _player.Play();
        }
    }

    public static void Stop()
    {
        _player.Pause();
        _player.Source = null;
    }
}
