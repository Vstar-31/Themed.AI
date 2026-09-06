using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using ThemeManager.WinUI.Services;
using System.Linq;
using System.Text.Json;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage : Page
{
    private readonly SkinManagerService _skinManager = App.SkinManager;
    private bool _isInitializing = true;

    /// <summary>Held so <see cref="Page_Unloaded"/> can detach it — App.ThemeService outlives
    /// this page, so an anonymous lambda subscribed without keeping a reference would leak a
    /// new handler every time this page is navigated to.</summary>
    private System.EventHandler<ThemeManager.Core.Models.CozyTheme>? _themeChangedHandler;

    /// <summary>Same sentinel VibeFinderMeasure.ResolveVibeText matches on the Target's third
    /// segment — kept in sync here so a Vibe Prompt of "$theme" (the PromptBox default) resolves
    /// the same way for the embed as it does for the native widgets.</summary>
    private const string ActiveThemeSentinel = "$theme";

    /// <summary>Track count pushed into the embed's own Tracks selector whenever the prompt is
    /// (re)synced — fixed at the top tier so the embed's own "Run Analysis" produces a full
    /// playlist rather than whatever the web app's default (5) happens to be.</summary>
    private const int AutoFillTrackLimit = 50;

    /// <summary>ContentDialog allows only one open per XamlRoot at a time — guards against a
    /// second issue arriving while one's already showing, which would otherwise throw.</summary>
    private bool _dialogOpen;

    public VibeFinderAIPage()
    {
        this.InitializeComponent();
        
        VibeFinderWebView.CoreWebView2Initialized += (s, e) =>
        {
            VibeFinderWebView.CoreWebView2.WebMessageReceived += async (sender, args) =>
            {
                var json = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(json)) return;

                if (VibeFinderAuth.TryParseLoginResult(json, out bool loginSuccess, out string? loginReason))
                {
                    await HandleLoginResultAsync(loginSuccess, loginReason);
                    return;
                }

                // The embedded React app posts this once it has mounted and registered its own
                // "message" listener (see the THEMED.AI BRIDGE block in the frontend's App.jsx).
                // It's a handshake rather than a blind push on our side because a command posted
                // before that listener exists is simply dropped by WebView2 — and
                // NavigationCompleted (below) firing isn't a reliable proxy for "React has
                // mounted," especially across the extra reload the auto-login script below can
                // trigger. Every mount (including that reload's remount) re-announces, so this
                // fires again automatically whenever the embed reloads.
                //
                // hasToken tells us whether the embed's React state already has a JWT. On the
                // very first mount (before auto-login has run), hasToken is false — we push the
                // prompt/trackLimit fields but skip runAnalysis (it would bail at `if (!token)`
                // anyway). The auto-login script stores the JWT and reloads, so the second mount
                // sends VIBEFINDER_APP_READY with hasToken:true, and this time we trigger the run.
                if (TryParseAppReady(json, out bool hasToken))
                {
                    PushVibePromptAndTrackLimit(triggerRun: hasToken);
                    return;
                }

                ThemeManager.Integration.Skins.VibeFinderWebState.HandleMessage(json);
            };
            
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand = (cmd) => 
            {
                try { VibeFinderWebView.CoreWebView2.PostWebMessageAsJson(cmd); }
                catch { }
            };

            // Auto-login + skip tutorial: after the VibeFinder web app page finishes loading,
            // inject JS that (a) marks the tutorial as seen so it never pops up, and (b) logs
            // in automatically using the credentials already saved on this page — the user
            // shouldn't have to re-enter them inside the web app every launch.
            VibeFinderWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        };

        _skinManager.EnsureVibeFinderSkinsExist();

        // Load current toggle states
        var skins = _skinManager.Skins;
        var primary = skins.FirstOrDefault(s => s.Name == "VibeFinder Primary");
        if (primary != null) TogglePrimary.IsOn = primary.Enabled;

        var minimal = skins.FirstOrDefault(s => s.Name == "VibeFinder Minimal");
        if (minimal != null) ToggleMinimal.IsOn = minimal.Enabled;

        var playlist = skins.FirstOrDefault(s => s.Name == "VibeFinder Playlist");
        if (playlist != null) TogglePlaylist.IsOn = playlist.Enabled;

        // Load saved credentials
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

        // Closes Phase 6's "follow active theme automatically" gap for the embed too — that
        // work (IActiveThemeProvider, ThemeVibeText, the "$theme" sentinel) already made the
        // native widgets react live to a theme switch; without this, a Vibe Prompt of "$theme"
        // would only pick up the new theme's text the next time the embed happens to reload or
        // the user hits Save & Apply, not the moment the theme actually changes.
        _themeChangedHandler = (_, _) =>
        {
            if (string.Equals(PromptBox.Text?.Trim(), ActiveThemeSentinel, System.StringComparison.OrdinalIgnoreCase))
            {
                PushVibePromptAndTrackLimit();
            }
        };
        App.ThemeService.ThemeChanged += _themeChangedHandler;
    }

    // ── Auto-login injection ─────────────────────────────────────────────────
    // Fired after the VibeFinder web app page finishes loading in the embedded WebView2.
    // Injects JS to:
    //   1. Skip the tutorial overlay (set vf_tutorial_seen in localStorage)
    //   2. If no token exists in localStorage, POST /auth/token with saved creds,
    //      store the JWT, and reload — the React app picks it up from localStorage
    //      on init and skips the login modal entirely.

    private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess) return;

        // Only inject on the VibeFinder app URL, not about:blank or other pages
        var uri = sender.Source;
        if (string.IsNullOrEmpty(uri) || !uri.Contains("vibefinderai")) return;

        string user = UsernameBox.Text?.Trim() ?? "";
        string pass = PasswordBox.Password ?? "";

        // Always skip the tutorial
        try
        {
            await sender.ExecuteScriptAsync(VibeFinderAuth.SkipTutorialScript);
        }
        catch { /* WebView2 may have been torn down */ }

        // Auto-login only if we have saved credentials and there's no existing token
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass)) return;

        try
        {
            await sender.ExecuteScriptAsync(VibeFinderAuth.BuildAutoLoginScript(user, pass));
        }
        catch { /* WebView2 may have been torn down during navigation */ }
    }

    private void WidgetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (sender is ToggleSwitch toggle && toggle.Tag is string skinName)
        {
            var skin = _skinManager.Skins.FirstOrDefault(s => s.Name == skinName);
            if (skin != null && skin.Enabled != toggle.IsOn)
            {
                _ = _skinManager.SetEnabledAsync(skin, toggle.IsOn);
            }
        }
    }

    private void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        string user = UsernameBox.Text;
        string pass = PasswordBox.Password;
        string prompt = PromptBox.Text;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(prompt))
        {
            StatusText.Text = "Please fill all fields.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        string target = $"{user}|{pass}|{prompt}";
        _skinManager.UpdateVibeFinderCredentials(target);

        // Also push the new credentials into the active WebView2 session so the user
        // doesn't have to navigate away and back for them to take effect.
        _ = TryInjectLoginAsync(user, pass);

        // The hidden prewarm browser (MainWindow) may have already given up on an earlier,
        // now-stale set of credentials — let it try again with these ones rather than staying
        // stuck until the app restarts.
        App.MainWindow?.ResetVibeFinderPrewarm();

        // And push the (possibly just-changed) Vibe Prompt + track count straight into the
        // embed's own input/selector, independent of whether the login refresh above reloads
        // the page — if it does reload, the embed's remount re-announces itself and this gets
        // sent again anyway (see WebMessageReceived), so there's no risk of the two racing.
        PushVibePromptAndTrackLimit();

        StatusText.Text = "Saved!";
        StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        StatusText.Visibility = Visibility.Visible;
    }

    /// <summary>Forces a fresh login in the embedded WebView2 with the given credentials,
    /// replacing whatever token (if any) was previously in localStorage.</summary>
    private async System.Threading.Tasks.Task TryInjectLoginAsync(string user, string pass)
    {
        try
        {
            if (VibeFinderWebView.CoreWebView2 is null) return;

            await VibeFinderWebView.CoreWebView2.ExecuteScriptAsync(VibeFinderAuth.BuildForceLoginScript(user, pass));
        }
        catch { }
    }

    /// <summary>Handles a VIBEFINDER_LOGIN_RESULT message from this page's own embedded browser
    /// (see <see cref="VibeFinderAuth.TryParseLoginResult"/>). A "network" reason just gets
    /// reflected in the existing StatusText — it's self-healing (MainWindow's hidden prewarm
    /// browser, sharing this same WebView2 profile, will supply a token the moment it succeeds
    /// there too) and not disruptive enough to warrant a modal. Anything else means retrying
    /// won't help on its own, so it gets an actual dialog instead.</summary>
    private async System.Threading.Tasks.Task HandleLoginResultAsync(bool success, string? reason)
    {
        if (success) return;

        if (reason == "network")
        {
            StatusText.Text = "Couldn't reach VibeFinder AI — check your connection.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        if (reason == "invalid_credentials")
        {
            await ShowIssueDialogAsync(
                "VibeFinder AI sign-in failed",
                "That username/password were rejected. Double-check them and hit Save & Apply again.");
        }
        else
        {
            await ShowIssueDialogAsync(
                "VibeFinder AI is unreachable",
                "VibeFinder AI's servers returned an error rather than signing in. This is usually temporary — try Save & Apply again in a bit.");
        }
    }

    private async System.Threading.Tasks.Task ShowIssueDialogAsync(string title, string message)
    {
        if (_dialogOpen) return;
        if (this.XamlRoot is null) return;

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
        catch { /* e.g. page navigated away mid-dialog */ }
        finally
        {
            _dialogOpen = false;
        }
    }

    /// <summary>Checks whether <paramref name="messageJson"/> is the embed's "mounted and listening"
    /// handshake (<c>{"type":"VIBEFINDER_APP_READY", "hasToken": bool}</c>). Returns false for
    /// any other message type. <paramref name="hasToken"/> indicates whether the React app already
    /// has a JWT in its state — false on the first mount before auto-login, true after.</summary>
    private static bool TryParseAppReady(string messageJson, out bool hasToken)
    {
        hasToken = false;
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl)
                || typeEl.GetString() != "VIBEFINDER_APP_READY")
                return false;

            if (doc.RootElement.TryGetProperty("hasToken", out var tokenEl)
                && tokenEl.ValueKind == JsonValueKind.True)
                hasToken = true;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Pushes the saved Vibe Prompt — resolved through the same "$theme" sentinel
    /// <c>VibeFinderMeasure.ResolveVibeText</c> honors for the native widgets — plus the fixed
    /// <see cref="AutoFillTrackLimit"/> into the embedded web app's own prompt textarea and
    /// Tracks selector, then triggers the actual search, via the THEMED.AI BRIDGE the frontend
    /// registers on mount (<c>window.chrome.webview</c> "message" listener in <c>App.jsx</c>).
    /// The run command carries its own text/trackLimit rather than relying on the two field-sync
    /// commands above it having already been applied — those land as two separate React state
    /// updates, and the frontend's own guard (<c>analyzeVibe</c>'s <c>overrideText</c>/
    /// <c>overrideTrackLimit</c>) exists precisely so the run doesn't have to wait on that.
    /// No-ops quietly if the embed isn't in a state to receive it (SendCommand not wired up, or
    /// an empty/blank resolved prompt) rather than sending commands the web app would just
    /// reject anyway.</summary>
    /// <summary>Pushes the saved Vibe Prompt and track limit into the embed. When
    /// <paramref name="triggerRun"/> is true, also sends a runAnalysis command to kick off
    /// the search. When false (first mount, before auto-login), only the field values are
    /// synced — the reload after login will call this again with triggerRun:true.</summary>
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
            string promptCmd = JsonSerializer.Serialize(new { command = "setPrompt", text = vibeText });
            string trackLimitCmd = JsonSerializer.Serialize(new { command = "setTrackLimit", value = AutoFillTrackLimit });

            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(promptCmd);
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(trackLimitCmd);

            if (triggerRun)
            {
                string runCmd = JsonSerializer.Serialize(new { command = "runAnalysis", text = vibeText, trackLimit = AutoFillTrackLimit });
                ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(runCmd);
            }
        }
        catch { /* WebView2 may have been torn down */ }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_themeChangedHandler is not null)
        {
            App.ThemeService.ThemeChanged -= _themeChangedHandler;
        }
        ThemeManager.Integration.Skins.VibeFinderWebState.Detach();
    }
}
