using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Views;
using Windows.Graphics;
using Microsoft.Extensions.Logging;
using ThemeManager.Integration.Skins;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();

        InitializeYouTubePlayer();

        // Navigate to Themes list on startup.
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);
    }

    // ── Title bar – extend content into title bar for a macOS-like look ──────
    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null); // Let the whole window be draggable; sidebar acts as drag area.

        var appWindow = AppWindow;
        if (AppWindowTitleBar.IsCustomizationSupported() && appWindow is not null)
        {
            var tb = appWindow.TitleBar;
            tb.ExtendsContentIntoTitleBar = true;
            tb.ButtonBackgroundColor         = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor  = Colors.Transparent;
            tb.ButtonHoverBackgroundColor     = Windows.UI.Color.FromArgb(0x20, 0, 0, 0);
            tb.ButtonPressedBackgroundColor   = Windows.UI.Color.FromArgb(0x30, 0, 0, 0);
            tb.ButtonForegroundColor          = Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x2A, 0x20);
        }

        // Resize to a comfortable default.
        AppWindow?.Resize(new SizeInt32(1100, 720));

        // Enforce a minimum window size so content never overflows/clips.
        if (appWindow is not null)
        {
            appWindow.Changed += (s, e) =>
            {
                if (!e.DidSizeChange) return;
                const int minW = 880, minH = 560;
                var size = s.Size;
                if (size.Width < minW || size.Height < minH)
                {
                    s.Resize(new SizeInt32(
                        Math.Max(size.Width, minW),
                        Math.Max(size.Height, minH)));
                }
            };
        }
    }

    // ── Navigation ─────────────────────────────────────────────────────────────

    private void NavThemes_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);
    }

    private void NavVibe_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(VibePage));
        SetActiveNav(NavVibe);
    }

    private void NavPreview_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ThemeEditorPage));
        SetActiveNav(NavPreview);
    }

    private void NavSystem_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SystemIntegrationPage));
        SetActiveNav(NavSystem);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        SetActiveNav(NavSettings);
    }

    private void NavWidgets_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SkinsPage));
        SetActiveNav(NavWidgets);
    }

    private void NavWidgetVibe_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(WidgetGeneratorPage));
        SetActiveNav(NavWidgetVibe);
    }

    private void NavVibeFinderAI_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(VibeFinderAIPage));
        SetActiveNav(NavVibeFinderAI);
    }

    /// <summary>Brings the window to the foreground (it may be hidden to the tray) and navigates
    /// straight to the editor for a specific widget. Used by the right-click "Edit" item on a
    /// floating widget itself, which lives in its own Window and has no Frame of its own.</summary>
    public void NavigateToSkinEditor(SkinDefinition skin)
    {
        AppWindow.Show();
        Activate();
        ContentFrame.Navigate(typeof(SkinEditorPage), skin);
        SetActiveNav(NavWidgets);
    }

    /// <summary>Swaps the visual state of sidebar buttons.</summary>
    private void SetActiveNav(Button active)
    {
        Button[] all = [NavThemes, NavVibe, NavPreview, NavSystem, NavWidgets, NavWidgetVibe, NavSettings];
        foreach (var btn in all)
        {
            btn.Style = btn == active
                ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemActiveStyle"]
                : (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemStyle"];
        }
    }

    // ── YouTube Player ─────────────────────────────────────────────────────────

    private string? _currentYouTubeVideoId;
    private bool _youtubeReady;
    private DispatcherQueueTimer? _youtubePollTimer;

    private async void InitializeYouTubePlayer()
    {
        try
        {
            // A dedicated environment + user data folder, not the app's default one.
            // VibeFinderAIPage's own WebView2 (VibeFinderWebView, embedding the live site) already
            // uses the default environment, and WebView2 requires every control sharing a user data
            // folder to be created from *identical* environment options — mixing a custom
            // AdditionalBrowserArguments into the default folder would throw as soon as both
            // controls exist in the same process. This one gets its own folder specifically so it
            // can carry an argument the other one has no reason to.
            //
            // The argument itself: Chromium's default autoplay policy blocks unmuted audio/video
            // unless playback is tied to a genuine user gesture on that frame. Every "play" here
            // originates from a native C# call into ExecuteScriptAsync — Chromium never counts that
            // as a gesture — so playerVars: {autoplay:1} alone was reliably getting silently
            // blocked with no visible error (this WebView2 is never shown). This is the standard,
            // documented fix for exactly that "nothing ever clicks inside the page itself" case.
            var envOptions = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required" };
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThemeManager.WinUI", "YouTubePlayerWebView2");
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, envOptions);
            await HiddenYoutubePlayer.EnsureCoreWebView2Async(environment);

            // _youtubeReady used to be set immediately after NavigateToString below, which only
            // means "we asked the WebView2 to start loading" — not that the YouTube iframe API
            // finished loading over the network, ran, and actually constructed a player object.
            // A click landing in that window would silently no-op (the JS-side `player &&` guards
            // below see `player` as still undefined). This waits for the player's own onReady
            // event instead, which is the real signal.
            HiddenYoutubePlayer.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.TryGetWebMessageAsString() == "ready") _youtubeReady = true;
            };

            var html = @"<!DOCTYPE html>
            <html>
            <body>
              <div id='player'></div>
              <script>
                var player;
                function onYouTubeIframeAPIReady() {
                  player = new YT.Player('player', {
                    height: '0',
                    width: '0',
                    playerVars: { 'autoplay': 1, 'controls': 0 },
                    events: {
                      'onReady': function() { window.chrome.webview.postMessage('ready'); }
                    },
                  });
                }
                function playTrackById(videoId) {
                  if (player && player.loadVideoById) {
                      player.loadVideoById(videoId);
                  }
                }
                function togglePause() {
                    if (player && player.getPlayerState) {
                        var state = player.getPlayerState();
                        if (state === 1) { // PLAYING
                            player.pauseVideo();
                        } else {
                            player.playVideo();
                        }
                    }
                }
              </script>
              <script src='https://www.youtube.com/iframe_api'></script>
            </body>
            </html>";
            HiddenYoutubePlayer.NavigateToString(html);

            _youtubePollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _youtubePollTimer.Interval = TimeSpan.FromMilliseconds(500);
            _youtubePollTimer.Tick += YoutubePollTimer_Tick;
            _youtubePollTimer.Start();
        }
        catch (System.Exception ex)
        {
            App.LoggerFactory.CreateLogger<MainWindow>().LogWarning(ex, "Failed to initialize hidden YouTube WebView2");
        }
    }

    private static readonly System.Text.RegularExpressions.Regex YouTubeVideoIdPattern =
        new(@"^[A-Za-z0-9_-]{11}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Loads and plays a track in the hidden YouTube player, or toggles play/pause if it's already
    /// the currently-loaded video.
    /// </summary>
    /// <param name="videoId">
    /// A resolved YouTube video ID (from VibeFinderMeasure.CurrentVideoId, ultimately
    /// /api/vibe/analyze's youtube_video_id) — NOT a free-text "{title} {artist}" query. This used
    /// to take (title, artist) and hand the query straight to the IFrame Player API's
    /// loadPlaylist({listType: 'search', list: query}), which YouTube deprecated on 15 Nov 2020;
    /// every call since has returned a 4xx and never loaded anything
    /// (https://developers.google.com/youtube/iframe_api_reference). There's no supported
    /// client-side replacement for "search by free text" any more — resolution has to happen
    /// server-side (see core/youtube_cache.resolve_video_id in the VibeFinderAI backend) before a
    /// video is nameable at all, so a null/missing id here means there's genuinely nothing to play,
    /// not a step this method can fall back to doing itself.
    /// </param>
    public async void PlayYouTubeTrack(string? videoId)
    {
        if (!_youtubeReady || string.IsNullOrEmpty(videoId) || !YouTubeVideoIdPattern.IsMatch(videoId))
            return;

        if (_currentYouTubeVideoId == videoId)
        {
            // Toggle play/pause if it's the exact same video already loaded
            await HiddenYoutubePlayer.ExecuteScriptAsync("togglePause();");
        }
        else
        {
            // Real YouTube video IDs are always exactly 11 chars of [A-Za-z0-9_-] (validated
            // above), so this can't break out of the single-quoted JS string literal the way the
            // old free-text query needed a Replace("'", "\\'") escape for.
            _currentYouTubeVideoId = videoId;
            await HiddenYoutubePlayer.ExecuteScriptAsync($"playTrackById('{videoId}');");
        }
    }

    private async void YoutubePollTimer_Tick(object? sender, object e)
    {
        if (!_youtubeReady) return;
        try
        {
            var stateStr = await HiddenYoutubePlayer.ExecuteScriptAsync("player && player.getPlayerState ? player.getPlayerState() : -1");
            var timeStr = await HiddenYoutubePlayer.ExecuteScriptAsync("player && player.getCurrentTime ? player.getCurrentTime() : 0");
            var durStr = await HiddenYoutubePlayer.ExecuteScriptAsync("player && player.getDuration ? player.getDuration() : 0");

            if (int.TryParse(stateStr, out int state))
            {
                YouTubePlaybackState.IsPlaying = (state == 1); // 1 = PLAYING
            }

            if (double.TryParse(timeStr, out double time) && double.TryParse(durStr, out double duration) && duration > 0)
            {
                YouTubePlaybackState.Progress = time / duration;
            }
            else
            {
                YouTubePlaybackState.Progress = 0;
            }
        }
        catch { }
    }
}
