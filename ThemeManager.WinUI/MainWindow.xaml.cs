using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Views;
using ThemeManager.WinUI.Services;
using Windows.Graphics;
using Microsoft.Extensions.Logging;
using ThemeManager.Integration.Skins;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow : Window
{
    /// <summary>Guards against re-navigating an already-(attempted-)warm browser — see
    /// <see cref="EnsureVibeFinderPrewarm"/>.</summary>
    private bool _vibeFinderPrewarmStarted;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();

        // Navigate to Themes list on startup.
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);
    }

    /// <summary>Logs the hidden <c>VibeFinderPrewarmWebView</c> into vibefinderai.netlify.app
    /// ahead of time, so a session already exists in this WebView2 profile's shared localStorage
    /// by the time the user opens the VibeFinderAI tab or a widget needs a fresh playlist —
    /// rather than only starting that process once <see cref="Views.VibeFinderAIPage"/> itself is
    /// navigated to. Called once right after <c>App.SkinManager.InitializeAsync()</c> (covers "a
    /// widget is already active at launch") and again on every <c>SkinManager.SkinsChanged</c>
    /// (covers "a widget just got activated") — safe to call from either as often as needed,
    /// since it no-ops immediately once a warm-up is already underway or nothing needs warming.
    ///
    /// Known simplification: once a warm-up is kicked off, this won't retry even if that
    /// particular attempt's login never actually succeeds (e.g. no internet at that instant) —
    /// VibeFinderAIPage's own visible browser still logs in normally when opened either way, so
    /// this only ever affects how *warm* that first open is, never whether it works at all.
    /// </summary>
    public async void EnsureVibeFinderPrewarm()
    {
        if (_vibeFinderPrewarmStarted) return;
        if (App.SkinManager is null) return;
        if (!App.SkinManager.Skins.Any(s => s.Name.StartsWith("VibeFinder") && s.Enabled)) return;

        var (user, pass) = VibeFinderAuth.TryReadCredentials(App.SkinManager);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass)) return;

        _vibeFinderPrewarmStarted = true;

        try
        {
            await VibeFinderPrewarmWebView.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            _vibeFinderPrewarmStarted = false; // WebView2 Runtime hiccup — let a later trigger retry
            App.LoggerFactory?.CreateLogger<MainWindow>()
                .LogWarning(ex, "VibeFinderAI pre-warm failed to initialize CoreWebView2");
            return;
        }

        VibeFinderPrewarmWebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
        {
            if (!args.IsSuccess) return;

            var uri = sender.Source;
            if (string.IsNullOrEmpty(uri) || !uri.Contains("vibefinderai")) return;

            try { await sender.ExecuteScriptAsync(VibeFinderAuth.SkipTutorialScript); }
            catch { /* WebView2 may have been torn down */ }

            var (u, p) = VibeFinderAuth.TryReadCredentials(App.SkinManager);
            if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p)) return;

            try { await sender.ExecuteScriptAsync(VibeFinderAuth.BuildAutoLoginScript(u, p)); }
            catch { /* WebView2 may have been torn down */ }
        };

        VibeFinderPrewarmWebView.Source = new Uri("https://vibefinderai.netlify.app/app");
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

    private void NavGallery_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(GalleryPage));
        SetActiveNav(NavGallery);
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
        Button[] all = [NavThemes, NavVibe, NavPreview, NavSystem, NavWidgets, NavWidgetVibe, NavGallery, NavVibeFinderAI, NavSettings];
        foreach (var btn in all)
        {
            btn.Style = btn == active
                ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemActiveStyle"]
                : (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemStyle"];
        }
    }

}
