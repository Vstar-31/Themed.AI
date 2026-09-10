using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;
using ThemeManager.WinUI.Services;
using System.Linq;
using System.Text.Json;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage : Page
{
    private readonly SkinManagerService _skinManager = App.SkinManager;
    private readonly ILogger _logger = App.LoggerFactory.CreateLogger<VibeFinderAIPage>();
    private bool _isInitializing = true;
    private System.EventHandler<ThemeManager.Core.Models.CozyTheme>? _themeChangedHandler;
    private const string ActiveThemeSentinel = "$theme";
    private const int AutoFillTrackLimit = 50;
    private bool _dialogOpen;

    public VibeFinderAIPage()
    {
        this.InitializeComponent();
        VibeFinderWebView.CoreWebView2Initialized += (s, e) =>
        {
            VibeFinderWebView.CoreWebView2.WebMessageReceived += async (sender, args) =>
            {
                var json = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("VibeFinderAIPage: WebMessageReceived fired with an empty/non-string payload — ignored");
                    return;
                }
                if (VibeFinderAuth.TryParseLoginResult(json, out bool loginSuccess, out string? loginReason))
                {
                    _logger.LogDebug("VibeFinderAIPage: VIBEFINDER_LOGIN_RESULT success={Success} reason={Reason}", loginSuccess, loginReason ?? "(none)");
                    await HandleLoginResultAsync(loginSuccess, loginReason);
                    return;
                }
                if (TryParseAppReady(json, out bool hasToken))
                {
                    _logger.LogInformation("VibeFinderAIPage: embed mounted (VIBEFINDER_APP_READY, hasToken={HasToken}) — {Action}", hasToken, hasToken ? "pushing prompt and triggering a run" : "pushing prompt only, waiting on auto-login");
                    PushVibePromptAndTrackLimit(triggerRun: hasToken);
                    return;
                }
                ThemeManager.Integration.Skins.VibeFinderWebState.HandleMessage(json);
            };
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand = (cmd) =>
            {
                try
                {
                    VibeFinderWebView.CoreWebView2.PostWebMessageAsJson(cmd);
                    _logger.LogTrace("VibeFinderAIPage: posted command to embed: {Command}", cmd);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VibeFinderAIPage: failed to post command to embed: {Command}", cmd);
                }
            };
            VibeFinderWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        };

        _skinManager.EnsureVibeFinderSkinsExist();
        var skins = _skinManager.Skins;
        var primary = skins.FirstOrDefault(s => s.Name == "VibeFinder Primary");
        if (primary != null) TogglePrimary.IsOn = primary.Enabled;
        var minimal = skins.FirstOrDefault(s => s.Name == "VibeFinder Minimal");
        if (minimal != null) ToggleMinimal.IsOn = minimal.Enabled;
        var playlist = skins.FirstOrDefault(s => s.Name == "VibeFinder Playlist");
        if (playlist != null) TogglePlaylist.IsOn = playlist.Enabled;

        var vibeSkin = skins.FirstOrDefault(s => s.Name.StartsWith("VibeFinder"));
        if (vibeSkin != null)
        {
            var measure = vibeSkin.Measures.FirstOrDefault(m =>
                m.Type == ThemeManager.Core.Skins.MeasureType.VibeTrackTitle ||
                m.Type == ThemeManager.Core.Skins.MeasureType.VibeTrackArtist ||
                m.Type == ThemeManager.Core.Skins.MeasureType.VibeMood);
            if (measure != null && !string.IsNullOrWhiteSpace(measure.Target))
            {
                var targetStr = measure.Target;
                if (targetStr.StartsWith("|")) targetStr = targetStr.Substring(1);
                var parts = targetStr.Split('|', 3);
                if (parts.Length >= 1) UsernameBox.Text = parts[0];
                if (parts.Length >= 2) PasswordBox.Password = parts[1];
                if (parts.Length >= 3) PromptBox.Text = parts[2];
            }
        }

        _isInitializing = false;
        _themeChangedHandler = (_, _) =>
        {
            if (string.Equals(PromptBox.Text?.Trim(), ActiveThemeSentinel, System.StringComparison.OrdinalIgnoreCase))
                PushVibePromptAndTrackLimit();
        };
        App.ThemeService.ThemeChanged += _themeChangedHandler;
    }

    private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            _logger.LogWarning("VibeFinderAIPage: WebView2 navigation to {Uri} failed (WebErrorStatus={Status})", sender.Source, args.WebErrorStatus);
            return;
        }
        var uri = sender.Source;
        if (string.IsNullOrEmpty(uri) || !uri.Contains("vibefinderai")) return;
        _logger.LogDebug("VibeFinderAIPage: navigation completed for {Uri}", uri);
        string user = UsernameBox.Text?.Trim() ?? "";
        string pass = PasswordBox.Password ?? "";
        try
        {
            await sender.ExecuteScriptAsync(VibeFinderAuth.SkipTutorialScript);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VibeFinderAIPage: SkipTutorialScript injection failed — WebView2 may have been torn down");
        }
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            _logger.LogDebug("VibeFinderAIPage: no saved credentials — skipping auto-login injection");
            return;
        }
        try
        {
            await sender.ExecuteScriptAsync(VibeFinderAuth.BuildAutoLoginScript(user, pass));
            _logger.LogDebug("VibeFinderAIPage: auto-login script injected for user \"{User}\"", user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderAIPage: auto-login script injection failed for user \"{User}\" — WebView2 may have been torn down during navigation", user);
        }
    }

    private void WidgetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is ToggleSwitch toggle && toggle.Tag is string skinName)
        {
            var skin = _skinManager.Skins.FirstOrDefault(s => s.Name == skinName);
            if (skin != null && skin.Enabled != toggle.IsOn)
                _ = _skinManager.SetEnabledAsync(skin, toggle.IsOn);
        }
    }

    private async void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        string user = UsernameBox.Text;
        string pass = PasswordBox.Password;
        string prompt = PromptBox.Text;
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(prompt))
        {
            _logger.LogDebug("VibeFinderAIPage: Save & Apply blocked — one or more fields empty");
            StatusText.Text = "Please fill all fields.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        _logger.LogInformation("VibeFinderAIPage: Save & Apply — updating credentials for user \"{User}\"", user);
        string target = $"{user}|{pass}|{prompt}";
        await ApplyVibeFinderCredentialsAsync(target);
        await TryInjectLoginAsync(user, pass);
        App.MainWindow?.ResetVibeFinderPrewarm();
        PushVibePromptAndTrackLimit();

        StatusText.Text = "Saved!";
        StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        StatusText.Visibility = Visibility.Visible;
    }

    private async System.Threading.Tasks.Task ApplyVibeFinderCredentialsAsync(string target)
    {
        var vibeSkins = _skinManager.Skins.Where(s => s.Name.StartsWith("VibeFinder")).ToList();
        bool changed = false;
        foreach (var skin in vibeSkins)
        {
            foreach (var measure in skin.Measures)
            {
                if (measure.Type != ThemeManager.Core.Skins.MeasureType.VibeTrackTitle &&
                    measure.Type != ThemeManager.Core.Skins.MeasureType.VibeTrackArtist &&
                    measure.Type != ThemeManager.Core.Skins.MeasureType.VibeMood &&
                    measure.Type != ThemeManager.Core.Skins.MeasureType.VibePlaybackState &&
                    measure.Type != ThemeManager.Core.Skins.MeasureType.VibeTrackProgress)
                    continue;
                if (!string.Equals(measure.Target, target, System.StringComparison.Ordinal))
                {
                    measure.Target = target;
                    changed = true;
                }
            }
        }
        if (!changed)
        {
            _logger.LogDebug("VibeFinderAIPage: Save & Apply — credentials unchanged; skipping skin rebuilds");
            return;
        }
        foreach (var skin in vibeSkins)
        {
            _logger.LogDebug("VibeFinderAIPage: persisting VibeFinder skin {SkinName} sequentially", skin.Name);
            await _skinManager.SaveSkinAsync(skin);
        }
        _logger.LogInformation("VibeFinderAIPage: VibeFinder credentials persisted for {SkinCount} skin(s) without concurrent rebuilds", vibeSkins.Count);
    }

    private async System.Threading.Tasks.Task TryInjectLoginAsync(string user, string pass)
    {
        try
        {
            if (VibeFinderWebView.CoreWebView2 is null)
            {
                _logger.LogDebug("VibeFinderAIPage: TryInjectLoginAsync skipped — CoreWebView2 not yet initialized");
                return;
            }
            await VibeFinderWebView.CoreWebView2.ExecuteScriptAsync(VibeFinderAuth.BuildForceLoginScript(user, pass));
            _logger.LogDebug("VibeFinderAIPage: force-login script injected for user \"{User}\" (credentials just saved)", user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderAIPage: force-login script injection failed for user \"{User}\"", user);
        }
    }

    private async System.Threading.Tasks.Task HandleLoginResultAsync(bool success, string? reason)
    {
        if (success)
        {
            _logger.LogInformation("VibeFinderAIPage: embed login succeeded");
            return;
        }
        _logger.LogWarning("VibeFinderAIPage: embed login failed, reason={Reason}", reason ?? "(unspecified)");
        if (reason == "network")
        {
            StatusText.Text = "Couldn't reach VibeFinder AI — check your connection.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            StatusText.Visibility = Visibility.Visible;
            return;
        }
        if (reason == "invalid_credentials")
        {
            await ShowIssueDialogAsync("VibeFinder AI sign-in failed", "That username/password were rejected. Double-check them and hit Save & Apply again.");
        }
        else
        {
            await ShowIssueDialogAsync("VibeFinder AI is unreachable", "VibeFinder AI's servers returned an error rather than signing in. This is usually temporary — try Save & Apply again in a bit.");
        }
    }

    private async System.Threading.Tasks.Task ShowIssueDialogAsync(string title, string message)
    {
        if (_dialogOpen || this.XamlRoot is null) return;
        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VibeFinderAIPage: dialog failed: {Title}", title);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private static bool TryParseAppReady(string messageJson, out bool hasToken)
    {
        hasToken = false;
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "VIBEFINDER_APP_READY")
                return false;
            if (doc.RootElement.TryGetProperty("hasToken", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.True)
                hasToken = true;
            return true;
        }
        catch { return false; }
    }

    private void PushVibePromptAndTrackLimit(bool triggerRun = true)
    {
        if (ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand is null) return;
        string rawPrompt = PromptBox.Text?.Trim() ?? "";
        string vibeText = string.Equals(rawPrompt, ActiveThemeSentinel, System.StringComparison.OrdinalIgnoreCase)
            ? ThemeManager.Core.NLP.ThemeVibeText.Describe(App.ThemeService.ActiveTheme)
            : rawPrompt;
        if (string.IsNullOrWhiteSpace(vibeText)) return;
        try
        {
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(JsonSerializer.Serialize(new { command = "setPrompt", text = vibeText }));
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(JsonSerializer.Serialize(new { command = "setTrackLimit", value = AutoFillTrackLimit }));
            if (triggerRun)
                ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(JsonSerializer.Serialize(new { command = "runAnalysis", text = vibeText, trackLimit = AutoFillTrackLimit }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VibeFinderAIPage: PushVibePromptAndTrackLimit failed");
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_themeChangedHandler is not null)
            App.ThemeService.ThemeChanged -= _themeChangedHandler;
        ThemeManager.Integration.Skins.VibeFinderWebState.Detach();
    }
}
