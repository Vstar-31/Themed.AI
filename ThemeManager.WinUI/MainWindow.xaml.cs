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
    private readonly ILogger _logger = App.LoggerFactory.CreateLogger<MainWindow>();
    private bool _vibeFinderPrewarmStarted;
    private bool _vibeFinderAwaitingNetwork;
    private bool _vibeFinderPrewarmHandlersAttached;
    private const int VibeTrackLimit = 50;
    private const string ActiveThemeSentinel = "$theme";
    private static int _vibeFinderDialogOpen;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);

        // WinUI 3 Window has no XAML Loaded event. Start the provisioning pipeline from the
        // constructor; the pipeline itself waits asynchronously for SkinManagerService.
        _ = InitializeVibeProvisioningAsync();
    }

    public bool IsVibeFinderPrewarmActive =>
        _vibeFinderPrewarmStarted && VibeFinderPrewarmWebView.CoreWebView2 is not null;

    public async void EnsureVibeFinderPrewarm()
    {
        if (_vibeFinderPrewarmStarted)
        {
            RebindVibeFinderPrewarmBridge();
            _logger.LogDebug("EnsureVibeFinderPrewarm: already started — bridge rebound/ignored");
            return;
        }
        if (App.SkinManager is null) return;
        if (!App.SkinManager.Skins.Any(s => s.Name.StartsWith("VibeFinder") && s.Enabled))
        {
            _logger.LogDebug("EnsureVibeFinderPrewarm: no enabled VibeFinder widget — skipping");
            return;
        }

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
            _vibeFinderPrewarmStarted = false;
            _logger.LogWarning(ex, "VibeFinderAI pre-warm failed to initialize CoreWebView2");
            await ShowVibeFinderIssueDialogAsync(
                "VibeFinder AI widgets need the WebView2 Runtime",
                "Themed.AI couldn't start the embedded browser used to sign in and control VibeFinder AI. Installing (or repairing) the Microsoft Edge WebView2 Runtime should fix this.");
            return;
        }

        if (!_vibeFinderPrewarmHandlersAttached)
        {
            _vibeFinderPrewarmHandlersAttached = true;
            VibeFinderPrewarmWebView.CoreWebView2.WebMessageReceived += async (sender, args) =>
            {
                var json = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(json)) return;

                if (VibeFinderAuth.TryParseLoginResult(json, out bool success, out string? reason))
                {
                    await HandleVibeFinderLoginResultAsync(json);
                    return;
                }

                if (TryParseAppReady(json, out bool hasToken))
                {
                    _logger.LogInformation("VibeFinder prewarm: embed ready (hasToken={HasToken})", hasToken);
                    if (hasToken)
                        PushVibePromptAndTrackLimit();
                    return;
                }

                VibeFinderWebState.HandleMessage(json);
            };

            VibeFinderPrewarmWebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
            {
                if (!args.IsSuccess) return;
                var uri = sender.Source;
                if (string.IsNullOrEmpty(uri) || !uri.Contains("vibefinderai")) return;

                try { await sender.ExecuteScriptAsync(VibeFinderAuth.SkipTutorialScript); }
                catch (Exception ex) { _logger.LogDebug(ex, "VibeFinder prewarm tutorial skip failed"); }

                var (u, p) = VibeFinderAuth.TryReadCredentials(App.SkinManager);
                if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p)) return;
                try
                {
                    await sender.ExecuteScriptAsync(VibeFinderAuth.BuildAutoLoginScript(u, p));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VibeFinder prewarm auto-login failed for user {User}", u);
                }
            };
        }

        RebindVibeFinderPrewarmBridge();
        VibeFinderPrewarmWebView.Source = new Uri("https://vibefinderai.netlify.app/app");
    }

    public void ResetVibeFinderPrewarm()
    {
        _vibeFinderPrewarmStarted = false;
        EnsureVibeFinderPrewarm();
    }

    public void RebindVibeFinderPrewarmBridge()
    {
        var dispatch = DispatcherQueue;
        VibeFinderWebState.SendCommand = commandJson =>
        {
            dispatch.TryEnqueue(() =>
            {
                try
                {
                    var core = VibeFinderPrewarmWebView.CoreWebView2;
                    if (core is null) return;
                    core.PostWebMessageAsJson(commandJson);
                    _logger.LogTrace("VibeFinder prewarm bridge: posted command {Command}", commandJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VibeFinder prewarm bridge: failed to post command {Command}", commandJson);
                }
            });
        };
    }

    private void PushVibePromptAndTrackLimit()
    {
        if (VibeFinderPrewarmWebView.CoreWebView2 is null) return;

        string rawPrompt = "";
        var (user, pass) = VibeFinderAuth.TryReadCredentials(App.SkinManager);
        if (App.SkinManager is not null)
        {
            var vibeSkin = App.SkinManager.Skins.FirstOrDefault(s => s.Name.StartsWith("VibeFinder"));
            var measure = vibeSkin?.Measures.FirstOrDefault(m =>
                m.Type == MeasureType.VibeTrackTitle ||
                m.Type == MeasureType.VibeTrackArtist ||
                m.Type == MeasureType.VibeMood);
            if (!string.IsNullOrWhiteSpace(measure?.Target))
            {
                var target = measure.Target!;
                if (target.StartsWith("|")) target = target[1..];
                var parts = target.Split('|', 3);
                if (parts.Length >= 3) rawPrompt = parts[2];
            }
        }

        string vibeText = string.Equals(rawPrompt.Trim(), ActiveThemeSentinel, StringComparison.OrdinalIgnoreCase)
            ? ThemeManager.Core.NLP.ThemeVibeText.Describe(App.ThemeService.ActiveTheme)
            : rawPrompt.Trim();

        if (string.IsNullOrWhiteSpace(vibeText)) return;

        try
        {
            var core = VibeFinderPrewarmWebView.CoreWebView2;
            core.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { command = "setPrompt", text = vibeText }));
            core.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { command = "setTrackLimit", value = VibeTrackLimit }));
            core.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { command = "runAnalysis", text = vibeText, trackLimit = VibeTrackLimit }));
            _logger.LogDebug("VibeFinder prewarm: pushed prompt and triggered analysis for {User}", user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinder prewarm: failed to push prompt/run analysis");
        }
    }

    private static bool TryParseAppReady(string messageJson, out bool hasToken)
    {
        hasToken = false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(messageJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "VIBEFINDER_APP_READY")
                return false;
            if (doc.RootElement.TryGetProperty("hasToken", out var tokenEl) && tokenEl.ValueKind == System.Text.Json.JsonValueKind.True)
                hasToken = true;
            return true;
        }
        catch { return false; }
    }

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

    private async Task HandleVibeFinderLoginResultAsync(string json)
    {
        if (!VibeFinderAuth.TryParseLoginResult(json, out bool success, out string? reason)) return;
        if (success) return;

        if (reason == "network")
        {
            _vibeFinderPrewarmStarted = false;
            SubscribeVibeFinderNetworkRetry();
            _logger.LogWarning("VibeFinder prewarm login reported a network failure; scheduled retry without user-facing error");
            return;
        }
        if (reason == "invalid_credentials")
        {
            _logger.LogWarning("VibeFinder prewarm login rejected saved credentials");
            return;
        }
        _logger.LogWarning("VibeFinder prewarm login failed: {Reason}; leaving the visible embed authoritative", reason ?? "unknown");
    }

    private async Task ShowVibeFinderIssueDialogAsync(string title, string message)
    {
        if (Interlocked.CompareExchange(ref _vibeFinderDialogOpen, 1, 0) != 0) return;
        try
        {
            var root = Content?.XamlRoot;
            if (root is null) return;
            var dialog = new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = root };
            await dialog.ShowAsync();
        }
        catch (Exception ex) { _logger.LogDebug(ex, "VibeFinder issue dialog failed"); }
        finally { Interlocked.Exchange(ref _vibeFinderDialogOpen, 0); }
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);
        var appWindow = AppWindow;
        if (AppWindowTitleBar.IsCustomizationSupported() && appWindow is not null)
        {
            var tb = appWindow.TitleBar;
            tb.ExtendsContentIntoTitleBar = true;
            tb.ButtonBackgroundColor = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0x20, 0, 0, 0);
            tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0x30, 0, 0, 0);
            tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x2A, 0x20);
        }
        AppWindow?.Resize(new SizeInt32(1100, 720));
        if (appWindow is not null)
        {
            appWindow.Changed += (s, e) =>
            {
                if (!e.DidSizeChange) return;
                const int minW = 880, minH = 560;
                var size = s.Size;
                if (size.Width < minW || size.Height < minH) s.Resize(new SizeInt32(Math.Max(size.Width, minW), Math.Max(size.Height, minH)));
            };
        }
    }

    private void NavThemes_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(ThemesPage)); SetActiveNav(NavThemes); }
    private void NavVibe_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(VibePage)); SetActiveNav(NavVibe); }
    private void NavPreview_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(ThemeEditorPage)); SetActiveNav(NavPreview); }
    private void NavSystem_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(SystemIntegrationPage)); SetActiveNav(NavSystem); }
    private void NavSettings_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(SettingsPage)); SetActiveNav(NavSettings); }
    private void NavWidgets_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(SkinsPage)); SetActiveNav(NavWidgets); }
    private void NavWidgetVibe_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(WidgetGeneratorPage)); SetActiveNav(NavWidgetVibe); }
    private void NavGallery_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(GalleryPage)); SetActiveNav(NavGallery); }
    private void NavVibeFinderAI_Click(object sender, RoutedEventArgs e) { ContentFrame.Navigate(typeof(VibeFinderAIPage)); SetActiveNav(NavVibeFinderAI); }

    public void NavigateToSkinEditor(SkinDefinition skin)
    {
        AppWindow.Show();
        Activate();
        ContentFrame.Navigate(typeof(SkinEditorPage), skin);
        SetActiveNav(NavWidgets);
    }

    private void SetActiveNav(Button active)
    {
        Button[] all = [NavStudio, NavWorld, NavThemes, NavVibe, NavPreview, NavSystem, NavWidgets, NavWidgetVibe, NavGallery, NavVibeFinderAI, NavSettings];
        foreach (var btn in all)
        {
            btn.Style = btn == active ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemActiveStyle"] : (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemStyle"];
        }
    }
}
