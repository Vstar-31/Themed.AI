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

    public VibeFinderAIPage()
    {
        this.InitializeComponent();
        
        VibeFinderWebView.CoreWebView2Initialized += (s, e) =>
        {
            VibeFinderWebView.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                var json = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(json)) return;

                // The embedded React app posts this once it has mounted and registered its own
                // "message" listener (see the THEMED.AI BRIDGE block in the frontend's App.jsx).
                // It's a handshake rather than a blind push on our side because a command posted
                // before that listener exists is simply dropped by WebView2 — and
                // NavigationCompleted (below) firing isn't a reliable proxy for "React has
                // mounted," especially across the extra reload the auto-login script below can
                // trigger. Every mount (including that reload's remount) re-announces, so this
                // fires again automatically whenever the embed reloads.
                if (IsAppReadySignal(json))
                {
                    PushVibePromptAndTrackLimit();
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
            await sender.ExecuteScriptAsync("try { localStorage.setItem('vf_tutorial_seen', '1'); } catch(e) {}");
        }
        catch { /* WebView2 may have been torn down */ }

        // Auto-login only if we have saved credentials and there's no existing token
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass)) return;

        // Escape the credentials for safe JS string embedding (handle quotes/backslashes)
        string safeUser = user.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");
        string safePass = pass.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");

        // The injected script:
        // - Checks localStorage for an existing token — if found, the user is already logged in
        // - If no token, calls the same /auth/token endpoint the web app's own submitAuth uses
        // - Stores the result and reloads so React picks it up on useState init
        // - Uses a sessionStorage flag to prevent infinite reload loops if auth fails
        string autoLoginScript = $@"
(async function() {{
    try {{
        if (localStorage.getItem('vf_token')) return;          // already logged in
        if (sessionStorage.getItem('_themed_auto_login')) return; // already tried this session
        sessionStorage.setItem('_themed_auto_login', '1');

        const fd = new URLSearchParams();
        fd.append('username', '{safeUser}');
        fd.append('password', '{safePass}');

        const res = await fetch('https://vibefinderai.onrender.com/auth/token', {{
            method: 'POST',
            headers: {{ 'Content-Type': 'application/x-www-form-urlencoded' }},
            body: fd,
        }});

        if (res.ok) {{
            const data = await res.json();
            if (data.access_token) {{
                localStorage.setItem('vf_token', data.access_token);
                location.reload();
            }}
        }}
    }} catch(e) {{}}
}})();";

        try
        {
            await sender.ExecuteScriptAsync(autoLoginScript);
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

            string safeUser = user.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");
            string safePass = pass.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");

            string script = $@"
(async function() {{
    try {{
        const fd = new URLSearchParams();
        fd.append('username', '{safeUser}');
        fd.append('password', '{safePass}');
        const res = await fetch('https://vibefinderai.onrender.com/auth/token', {{
            method: 'POST',
            headers: {{ 'Content-Type': 'application/x-www-form-urlencoded' }},
            body: fd,
        }});
        if (res.ok) {{
            const data = await res.json();
            if (data.access_token) {{
                localStorage.setItem('vf_token', data.access_token);
                location.reload();
            }}
        }}
    }} catch(e) {{}}
}})();";

            await VibeFinderWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }

    /// <summary>True if the embed's React app just posted its "mounted and listening" ping
    /// (<c>{"type":"VIBEFINDER_APP_READY"}</c>), as opposed to a player-state update destined
    /// for <see cref="ThemeManager.Integration.Skins.VibeFinderWebState"/>.</summary>
    private static bool IsAppReadySignal(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            return doc.RootElement.TryGetProperty("type", out var typeEl)
                && typeEl.GetString() == "VIBEFINDER_APP_READY";
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
    private void PushVibePromptAndTrackLimit()
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
            string runCmd = JsonSerializer.Serialize(new { command = "runAnalysis", text = vibeText, trackLimit = AutoFillTrackLimit });

            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(promptCmd);
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(trackLimitCmd);
            ThemeManager.Integration.Skins.VibeFinderWebState.SendCommand(runCmd);
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
