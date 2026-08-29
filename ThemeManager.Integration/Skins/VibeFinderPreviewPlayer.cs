using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Plays the 30-second iTunes preview clip VibeFinderAI already returns on every
/// <c>/api/vibe/analyze</c> track (see <see cref="VibeFinderMeasure"/>'s <c>PreviewUrl</c>
/// parsing) natively and in-process, with no browser or WebView2 involved.
///
/// This exists because VibeFinderAI's own playback (<c>frontend/src/MusicPlayer.jsx</c>) is a
/// React component built around a YouTube &lt;iframe&gt;+postMessage remote control for full
/// tracks, or an HTML &lt;audio&gt; element for its own 30s-preview fallback — both are DOM/JS
/// constructs with no equivalent to "call this function to play a song" from outside a live web
/// page. There is no playback API to invoke from a WinUI3 process; the component expects to be
/// mounted inside VibeFinderAI's own authenticated React app (it takes <c>token</c>,
/// <c>buildApiUrl</c>, <c>servicesConnected</c> etc. as props), not embedded standalone. The
/// preview URL itself, though, is just a static MP3 (iTunes' <c>previewUrl</c>, nothing to do
/// with Spotify) — genuinely portable — and <see cref="VibeFinderMeasure"/> was already
/// receiving it in the very JSON payload it fetches for Title/Artist/Mood, just not reading
/// that field until now.
///
/// One shared <see cref="MediaPlayer"/> for the whole app, the same "single shared instance"
/// shape as <c>MediaMeasure</c>'s static <c>GlobalSystemMediaTransportControlsSessionManager</c>
/// — there is exactly one desktop, so at most one VibeFinder preview should ever play at once
/// no matter how many VibeFinder widgets/meters are on screen.
///
/// Not build- or live-verified — same standing caveat as every other Windows-Runtime-facing
/// class in this project (see <c>MediaMeasure</c>, <c>WeatherMeasure</c>): written without
/// dotnet/NuGet or a Windows box in this environment. <c>Windows.Media.Playback</c> ships in the
/// same <c>net8.0-windows10.0.19041.0</c> SDK surface as the already-used
/// <c>Windows.Media.Control</c> (see <c>ThemeManager.Integration.csproj</c> — neither has a
/// separate package reference), so it resolves the same way; the actual runtime behavior of
/// swapping <see cref="MediaPlayer.Source"/> mid-playback still wants a real Windows 11 machine
/// to confirm before trusting.
/// </summary>
public static class VibeFinderPreviewPlayer
{
    private static readonly MediaPlayer _player = new() { AutoPlay = false };
    private static string? _currentUrl;
    private static ILogger _logger = NullLogger.Instance;

    static VibeFinderPreviewPlayer()
    {
        // Ended fires at the natural end of a preview clip (~30s, sometimes shorter depending on
        // what iTunes enriched) — clear _currentUrl so the next click on the same meter is read
        // as "start over," not "resume something that already finished."
        _player.MediaEnded += (_, _) => _currentUrl = null;
        _player.MediaFailed += (_, args) =>
        {
            _logger.LogWarning("VibeFinder preview playback failed: {Error} {ErrorMessage}", args.Error, args.ErrorMessage);
            _currentUrl = null;
        };
    }

    /// <summary>The preview URL currently loaded (playing or paused), or null when nothing is
    /// loaded. Not read anywhere in the UI yet — exposed for a future "playing this preview"
    /// visual state on the meter itself, which is a separate, bigger change (every meter kind's
    /// visual builder in SkinHostWindow would need to react to it) than this one is scoping.</summary>
    public static string? CurrentlyPlayingUrl => _currentUrl;

    /// <summary>
    /// Toggles playback of <paramref name="previewUrl"/>: starts it if nothing (or a different
    /// URL) is loaded, pauses it if it's already the one playing, resumes it if it's the one
    /// loaded but paused. Mirrors the exact tri-state toggle VibeFinderAI's own
    /// <c>SharedPlaylist.jsx</c> (<c>togglePlay</c>) already uses for this same preview-URL
    /// field, just against a native <see cref="MediaPlayer"/> instead of an HTML
    /// <c>&lt;audio&gt;</c> element.
    ///
    /// Best-effort, like <c>MediaMeasure.TrySendCommandAsync</c>: a bad or dead URL logs a
    /// warning instead of throwing back into the widget's pointer-pressed handler, since a
    /// meter's click action failing should never be able to crash the app.
    /// </summary>
    /// <param name="previewUrl">The iTunes preview MP3 URL, already percent-decoded by the caller.</param>
    /// <param name="logger">Optional logger; defaults to the one from the previous call, or a
    /// no-op if this is the first call. SkinHostWindow passes its own App.LoggerFactory logger.</param>
    public static void TogglePreview(string previewUrl, ILogger? logger = null)
    {
        if (logger is not null) _logger = logger;

        if (string.IsNullOrWhiteSpace(previewUrl)) return;
        if (!Uri.TryCreate(previewUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("VibeFinder preview URL was not a valid absolute URI: {Url}", previewUrl);
            return;
        }

        try
        {
            bool isSameTrack = string.Equals(_currentUrl, previewUrl, StringComparison.Ordinal);
            bool isPlaying = _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

            if (isSameTrack && isPlaying)
            {
                _player.Pause();
                return;
            }

            if (isSameTrack && !isPlaying)
            {
                _player.Play();
                return;
            }

            // A different track (or nothing) was loaded — switch source and start fresh, the
            // same thing loadTrackIntoIframe/togglePlay do on the VibeFinderAI side when the
            // queue index changes to a track whose video/preview isn't the one already loaded.
            _currentUrl = previewUrl;
            _player.Source = MediaSource.CreateFromUri(uri);
            _player.Play();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle VibeFinder preview playback for {Url}", previewUrl);
        }
    }

    /// <summary>Stops playback and releases the loaded source — e.g. if a widget is deleted or
    /// disabled while its preview is mid-playback. Not called from anywhere yet: SkinHostWindow
    /// doesn't currently tear down per-meter state on removal for any meter kind, VibeFinder
    /// included, so this is here for whenever that gap gets closed rather than left unaddressable.</summary>
    public static void Stop()
    {
        try
        {
            _player.Pause();
            _player.Source = null;
        }
        catch
        {
            // Best-effort, same as TogglePreview's own catch above.
        }
        _currentUrl = null;
    }
}
