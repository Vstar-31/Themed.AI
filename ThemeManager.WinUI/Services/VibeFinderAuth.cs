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

    public const string SkipTutorialScript =
        "try { localStorage.setItem('vf_tutorial_seen', '1'); } catch(e) {}";

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

    /// <summary>
    /// Reads the saved username/password from the VibeFinder skin that has the most useful
    /// persisted target. Credentialed presets are preferred over legacy placeholders so the
    /// hidden pre-warm browser and the visible VibeFinder widgets authenticate as the same user.
    /// </summary>
    public static (string User, string Pass) TryReadCredentials(SkinManagerService skinManager)
    {
        var skins = skinManager.Skins
            .Where(s => s.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var skin in skins)
        {
            foreach (var measure in skin.Measures)
            {
                if (measure.Type is not (MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood))
                    continue;

                var target = measure.Target?.TrimStart('|');
                var parts = target?.Split('|', 3);
                if (parts is { Length: >= 2 }
                    && !string.IsNullOrWhiteSpace(parts[0])
                    && !string.IsNullOrWhiteSpace(parts[1])
                    && !parts[0].Equals("listener", StringComparison.OrdinalIgnoreCase))
                {
                    return (parts[0], parts[1]);
                }
            }
        }

        return ("", "");
    }

    private static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");
}
