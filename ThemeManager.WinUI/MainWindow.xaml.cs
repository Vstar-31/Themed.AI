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
using Windows.Networking.Connectivity;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow : Window
{
    /// <summary>Guards against re-navigating an already-(attempted-)warm browser — see
    /// <see cref="EnsureVibeFinderPrewarm"/>.</summary>
    private bool _vibeFinderPrewarmStarted;

    /// <summary>True while waiting on <see cref="NetworkInformation.NetworkStatusChanged"/> to
    /// retry a prewarm that was skipped (or failed) for lack of connectivity — prevents piling up
    /// duplicate subscriptions if <see cref="EnsureVibeFinderPrewarm"/> is invoked again (e.g. a
    /// second widget activation) while still waiting on the first one.</summary>
    private bool _vibeFinderAwaitingNetwork;

    /// <summary>ContentDialog allows only one open per XamlRoot at a time — guards against a
    /// second issue arriving while one's already showing, which would otherwise throw.</summary>
    private bool _vibeFinderDialogOpen;

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
    /// Checks connectivity before attempting rather than after: there's no point spinning up the
    /// WebView2 for a login POST that can't succeed, and a clean "not online yet" skip is exactly
    /// what lets this retry itself the moment <see cref="NetworkInformation.NetworkStatusChanged"/>
    /// says otherwise (see <see cref="SubscribeVibeFinderNetworkRetry"/>) instead of needing to
    /// distinguish "haven't tried" from "tried and failed" after the fact.
    /// </summary>
    public async void EnsureVibeFinderPrewarm()
    {
        if (_vibeFinderPrewarmStarted) return;
        if (App.SkinManager is null) return;
        if (!App.SkinManager.Skins.Any(s => s.Name.StartsWith("VibeFinder") && s.Enabled)) return;

        var (user, pass) = VibeFinderAuth.TryReadCredentials(App.SkinManager);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass)) return;

        if (!NetworkStatus.IsInternetAvailable())
        {
            SubscribeVibeFinderNetworkRetry();
            return;
        }

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
            await ShowVibeFinderIssueDialogAsync(
                "VibeFinder AI widgets need the WebView2 Runtime",
                "Themed.AI couldn't start the embedded browser used to sign in and fetch playlists. Installing (or repairing) the Microsoft Edge WebView2 Runtime should fix this.");
            return;
        }

        VibeFinderPrewarmWebView.CoreWebView2.WebMessageReceived += async (sender, args) =>
        {
            var json = args.TryGetWebMessageAsString();
            if (!string.IsNullOrEmpty(json))
            {
                await HandleVibeFinderLoginResultAsync(json);
            }
        };

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

    /// <summary>Lets a fresh Save & Apply (new credentials) override a permanently-stuck failure
    /// state — e.g. the prewarm gave up after an invalid-credentials result, and simply won't
    /// retry on its own since retrying with the same bad password would just fail again. Called
    /// from VibeFinderAIPage.SaveCredentials_Click after credentials change.</summary>
    public void ResetVibeFinderPrewarm()
    {
        _vibeFinderPrewarmStarted = false;
        EnsureVibeFinderPrewarm();
    }

    /// <summary>Subscribes (at most once — re-entrant calls while already waiting are no-ops) to
    /// <see cref="NetworkInformation.NetworkStatusChanged"/>, unsubscribing itself and retrying
    /// <see cref="EnsureVibeFinderPrewarm"/> the moment that fires with connectivity restored.
    /// The event fires on a background thread, so the retry is marshalled back via
    /// <c>DispatcherQueue</c> before touching any UI-thread-affine WebView2/XAML state.</summary>
    private void SubscribeVibeFinderNetworkRetry()
    {
        if (_vibeFinderAwaitingNetwork) return;
        _vibeFinderAwaitingNetwork = true;

        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChangedForVibeFinderRetry;
    }

    private void OnNetworkStatusChangedForVibeFinderRetry(object sender)
    {
        if (!NetworkStatus.IsInternetAvailable()) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChangedForVibeFinderRetry;
            _vibeFinderAwaitingNetwork = false;
            EnsureVibeFinderPrewarm();
        });
    }

    /// <summary>Handles a <c>VIBEFINDER_LOGIN_RESULT</c> message from the hidden prewarm browser
    /// (see <see cref="VibeFinderAuth.TryParseLoginResult"/> for the message shape). A "network"
    /// reason is self-healing — no dialog, just fold back into the same reconnect-and-retry path
    /// a pre-check failure would have taken. Anything else means retrying blindly won't help, so
    /// it surfaces as a dialog instead and is left alone until the user does something about it
    /// (new credentials via Save & Apply, or toggling the widget off and back on).</summary>
    private async System.Threading.Tasks.Task HandleVibeFinderLoginResultAsync(string json)
    {
        if (!VibeFinderAuth.TryParseLoginResult(json, out bool success, out string? reason)) return;
        if (success) return;

        if (reason == "network")
        {
            _vibeFinderPrewarmStarted = false;
            SubscribeVibeFinderNetworkRetry();
            return;
        }

        if (reason == "invalid_credentials")
        {
            await ShowVibeFinderIssueDialogAsync(
                "VibeFinder AI sign-in failed",
                "The saved VibeFinder AI username/password were rejected. Update them from Widgets → VibeFinder AI, then hit Save & Apply.");
        }
        else
        {
            await ShowVibeFinderIssueDialogAsync(
                "VibeFinder AI is unreachable",
                "VibeFinder AI's servers returned an error rather than signing in. This is usually temporary — it'll be retried the next time a widget is toggled or the app restarts.");
        }
    }

    private async System.Threading.Tasks.Task ShowVibeFinderIssueDialogAsync(string title, string message)
    {
        if (_vibeFinderDialogOpen) return;
        if (Content?.XamlRoot is null) return;

        _vibeFinderDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { /* e.g. window closing mid-dialog */ }
        finally
        {
            _vibeFinderDialogOpen = false;
        }
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
