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
    private static int _vibeFinderDialogOpen;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();
        ContentFrame.Navigate(typeof(ThemesPage));
        SetActiveNav(NavThemes);
    }

    public async void EnsureVibeFinderPrewarm()
    {
        if (_vibeFinderPrewarmStarted) { _logger.LogDebug("EnsureVibeFinderPrewarm: already started — ignored"); return; }
        if (App.SkinManager is null) return;
        if (!App.SkinManager.Skins.Any(s => s.Name.StartsWith("VibeFinder") && s.Enabled)) { _logger.LogDebug("EnsureVibeFinderPrewarm: no enabled VibeFinder widget — skipping"); return; }
        var (user, pass) = VibeFinderAuth.TryReadCredentials(App.SkinManager);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass)) return;
        if (!NetworkStatus.IsInternetAvailable()) { SubscribeVibeFinderNetworkRetry(); return; }
        _vibeFinderPrewarmStarted = true;
        try { await VibeFinderPrewarmWebView.EnsureCoreWebView2Async(); }
        catch (Exception ex) { _vibeFinderPrewarmStarted = false; _logger.LogWarning(ex, "VibeFinderAI pre-warm failed to initialize CoreWebView2"); await ShowVibeFinderIssueDialogAsync("VibeFinder AI widgets need the WebView2 Runtime", "Themed.AI couldn't start the embedded browser used to sign in and fetch playlists. Installing (or repairing) the Microsoft Edge WebView2 Runtime should fix this."); return; }
        VibeFinderPrewarmWebView.CoreWebView2.WebMessageReceived += async (sender, args) => { var json = args.TryGetWebMessageAsString(); if (!string.IsNullOrEmpty(json)) await HandleVibeFinderLoginResultAsync(json); };
        VibeFinderPrewarmWebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
        {
            if (!args.IsSuccess) return;
            var uri = sender.Source; if (string.IsNullOrEmpty(uri) || !uri.Contains("vibefinderai")) return;
            try { await sender.ExecuteScriptAsync(VibeFinderAuth.SkipTutorialScript); } catch (Exception ex) { _logger.LogDebug(ex, "VibeFinder prewarm tutorial skip failed"); }
            var (u, p) = VibeFinderAuth.TryReadCredentials(App.SkinManager); if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p)) return;
            try { await sender.ExecuteScriptAsync(VibeFinderAuth.BuildAutoLoginScript(u, p)); } catch (Exception ex) { _logger.LogWarning(ex, "VibeFinder prewarm auto-login failed for user {User}", u); }
        };
        VibeFinderPrewarmWebView.Source = new Uri("https://vibefinderai.netlify.app/app");
    }

    public void ResetVibeFinderPrewarm() { _vibeFinderPrewarmStarted = false; EnsureVibeFinderPrewarm(); }

    private void SubscribeVibeFinderNetworkRetry()
    {
        if (_vibeFinderAwaitingNetwork) return;
        _vibeFinderAwaitingNetwork = true;
        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChangedForVibeFinderRetry;
    }

    private void OnNetworkStatusChangedForVibeFinderRetry(object sender)
    {
        if (!NetworkStatus.IsInternetAvailable()) return;
        DispatcherQueue.TryEnqueue(() => { NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChangedForVibeFinderRetry; _vibeFinderAwaitingNetwork = false; EnsureVibeFinderPrewarm(); });
    }

    private async Task HandleVibeFinderLoginResultAsync(string json)
    {
        if (!VibeFinderAuth.TryParseLoginResult(json, out bool success, out string? reason)) return;
        if (success) return;
        if (reason == "network") { _vibeFinderPrewarmStarted = false; SubscribeVibeFinderNetworkRetry(); return; }
        if (reason == "invalid_credentials") await ShowVibeFinderIssueDialogAsync("VibeFinder AI sign-in failed", "The saved VibeFinder AI username/password were rejected. Update them from Widgets → VibeFinder AI, then hit Save & Apply.");
        else await ShowVibeFinderIssueDialogAsync("VibeFinder AI is unreachable", "VibeFinder AI's servers returned an error rather than signing in. This is usually temporary — it'll be retried the next time a widget is toggled or the app restarts.");
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
        Button[] all = [NavThemes, NavVibe, NavPreview, NavSystem, NavWidgets, NavWidgetVibe, NavGallery, NavVibeFinderAI, NavSettings];
        foreach (var btn in all)
        {
            btn.Style = btn == active ? (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemActiveStyle"] : (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemStyle"];
        }
    }
}