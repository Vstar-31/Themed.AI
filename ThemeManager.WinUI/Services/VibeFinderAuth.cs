using System.Linq;
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

    /// <summary>Idempotent and safe to call on every navigation: no-ops immediately if a token
    /// already exists in localStorage, and guards itself against retry-looping on a failed
    /// attempt within the same browser session via sessionStorage. Reloads the page on success
    /// so the React app's own <c>useState</c> init (which reads localStorage once, on mount)
    /// picks the fresh token up.</summary>
    public static string BuildAutoLoginScript(string user, string pass)
    {
        string safeUser = Escape(user);
        string safePass = Escape(pass);

        return $@"
(async function() {{
    try {{
        if (localStorage.getItem('vf_token')) return;
        if (sessionStorage.getItem('_themed_auto_login')) return;
        sessionStorage.setItem('_themed_auto_login', '1');

        const fd = new URLSearchParams();
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
                location.reload();
            }}
        }}
    }} catch(e) {{}}
}})();";
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
