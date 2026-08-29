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

    private string? _currentYouTubeQuery;
    private bool _youtubeReady;
    private DispatcherQueueTimer? _youtubePollTimer;

    private async void InitializeYouTubePlayer()
    {
        try
        {
            await HiddenYoutubePlayer.EnsureCoreWebView2Async();
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
                  });
                }
                function playTrack(query) {
                  if (player && player.loadPlaylist) {
                      player.loadPlaylist({ listType: 'search', list: query });
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
            _youtubeReady = true;

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

    public async void PlayYouTubeTrack(string title, string artist)
    {
        if (!_youtubeReady) return;
        var query = $"{title} {artist}".Trim();
        if (string.IsNullOrEmpty(query) || query == "—") return;

        if (_currentYouTubeQuery == query)
        {
            // Toggle play/pause if it's the exact same query
            await HiddenYoutubePlayer.ExecuteScriptAsync("togglePause();");
        }
        else
        {
            // Load and play new search query
            _currentYouTubeQuery = query;
            var script = $"playTrack('{query.Replace("'", "\\'")}');";
            await HiddenYoutubePlayer.ExecuteScriptAsync(script);
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
