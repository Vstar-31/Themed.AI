using System.Linq;
using System.Text.Json;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Shared between VibeFinderAIPage's own embedded browser and MainWindow's hidden pre-warm
/// browser (see MainWindow.EnsureVibeFinderPrewarm) so both authenticate against the same
/// backend the same way, instead of the login script living only inline in one page's
/// code-behind. Lives in WinUI.Services rather than ThemeManager.Integration — it needs
/// SkinManagerService (to read saved credentials), and Integration only references Core, never
/// WinUI, so putting it there would create a circular project reference.
/// </summary>
public static class VibeFinderAuth
{
    private const string TokenEndpoint = "https://vibefinderai.onrender.com/auth/token";

    /// <summary>Always safe to run unconditionally, regardless of whether credentials exist —
    /// there's nothing to skip a tutorial *for* if the user never gets past the login screen,
    /// but setting the flag early means they never see it even for the split second before
    /// auto-login (or manual login) completes.</summary>
    public const string SkipTutorialScript =
        "try { localStorage.setItem('vf_tutorial_seen', '1'); } catch(e) {}";

    /// <summary>Idempotent and safe to call on every navigation: no-ops immediately (without
    /// reporting anything back — there's nothing to report, nothing was attempted) if a token
    /// already exists in localStorage, or if this session already tried and is mid-flight/failed
    /// once already (the sessionStorage flag guards against retry-looping on NavigationCompleted).
    /// Reports its outcome back to the host via <c>window.chrome.webview.postMessage</c> as
    /// <c>{"type":"VIBEFINDER_LOGIN_RESULT","success":bool,"reason":string|null}</c> — see
    /// <see cref="TryParseLoginResult"/>. Reloads the page on success so the React app's own
    /// <c>useState</c> init (which reads localStorage once, on mount) picks the fresh token up.
    /// </summary>
    public static string BuildAutoLoginScript(string user, string pass) => $@"
(async function() {{
    const postResult = (success, reason) => {{
        try {{ window.chrome.webview.postMessage(JSON.stringify({{ type: 'VIBEFINDER_LOGIN_RESULT', success: success, reason: reason || null }})); }} catch(e) {{}}
    }};
    try {{
        if (localStorage.getItem('vf_token')) return;
        if (sessionStorage.getItem('_themed_auto_login')) return;
        sessionStorage.setItem('_themed_auto_login', '1');
{BuildLoginAttemptBody(user, pass)}
    }} catch(e) {{
        postResult(false, 'network');
    }}
}})();";

    /// <summary>Always attempts, ignoring any existing token and the auto-login script's
    /// same-session guard — used right after the user saves new credentials (SaveCredentials_
    /// Click), when a stale token or a previously-failed auto-login attempt this session must
    /// not stand in the way of testing the fresh ones. Same result-reporting contract as
    /// <see cref="BuildAutoLoginScript"/>.</summary>
    public static string BuildForceLoginScript(string user, string pass) => $@"
(async function() {{
    const postResult = (success, reason) => {{
        try {{ window.chrome.webview.postMessage(JSON.stringify({{ type: 'VIBEFINDER_LOGIN_RESULT', success: success, reason: reason || null }})); }} catch(e) {{}}
    }};
    try {{
{BuildLoginAttemptBody(user, pass)}
    }} catch(e) {{
        postResult(false, 'network');
    }}
}})();";

    /// <summary>The actual fetch-and-store-token logic shared by both scripts above — assumes a
    /// <c>postResult(success, reason)</c> function is already in scope (both callers define one
    /// identically) and that it's running inside a try/catch that reports <c>'network'</c> on any
    /// thrown exception, so this only needs to cover the fetch *completing* one way or another.
    /// </summary>
    private static string BuildLoginAttemptBody(string user, string pass)
    {
        string safeUser = Escape(user);
        string safePass = Escape(pass);

        return $@"        const fd = new URLSearchParams();
        fd.append('username', '{safeUser}');
        fd.append('password', '{safePass}');

        const res = await fetch('{TokenEndpoint}', {{
            method: 'POST',
            headers: {{ 'Content-Type': 'application/x-www-form-urlencoded' }},
            body: fd,
        }});

        if (res.ok) {{
            const data = await res.json();
            if (data.access_token) {{
                localStorage.setItem('vf_token', data.access_token);
                postResult(true);
                location.reload();
                return;
            }}
            postResult(false, 'server_error');
            return;
        }}

        postResult(false, (res.status === 401 || res.status === 400) ? 'invalid_credentials' : 'server_error');";
    }

    /// <summary>Parses a <c>{"type":"VIBEFINDER_LOGIN_RESULT",...}</c> message posted by either
    /// script above. Returns false (leaving <paramref name="success"/>/<paramref name="reason"/>
    /// at their defaults) for any other message shape entirely — including malformed JSON — so
    /// callers can tell "not a login result" apart from "a login result that failed" and keep
    /// looking elsewhere (e.g. VIBEFINDER_APP_READY) rather than misreading one as the other.
    /// <paramref name="reason"/> is one of "invalid_credentials", "server_error", "network", or
    /// null (only meaningful when <paramref name="success"/> is false).</summary>
    public static bool TryParseLoginResult(string messageJson, out bool success, out string? reason)
    {
        success = false;
        reason = null;
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "VIBEFINDER_LOGIN_RESULT")
                return false;

            success = root.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.True;
            reason = root.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String
                ? reasonEl.GetString()
                : null;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Reads the saved username/password off the first VibeFinder-named skin's Target
    /// string (the same "user|pass|prompt" format VibeFinderAIPage's constructor parses into its
    /// own text boxes) — the one other place that string's shape gets decoded, aside from that
    /// page's own load path, which isn't a fit to share directly since it writes straight into
    /// UI TextBoxes rather than returning plain strings.</summary>
    public static (string User, string Pass) TryReadCredentials(SkinManagerService skinManager)
    {
        var vibeSkin = skinManager.Skins.FirstOrDefault(s => s.Name.StartsWith("VibeFinder"));
        if (vibeSkin is null) return ("", "");

        var measure = vibeSkin.Measures.FirstOrDefault(m =>
            m.Type == MeasureType.VibeTrackTitle ||
            m.Type == MeasureType.VibeTrackArtist ||
            m.Type == MeasureType.VibeMood);

        if (measure is null || string.IsNullOrWhiteSpace(measure.Target)) return ("", "");

        var targetStr = measure.Target;
        if (targetStr.StartsWith("|")) targetStr = targetStr.Substring(1);
        var parts = targetStr.Split('|', 3);

        return (parts.Length >= 1 ? parts[0] : "", parts.Length >= 2 ? parts[1] : "");
    }

    private static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");
}
