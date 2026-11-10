using System.Text.Json;

namespace ThemeManager.Core.Services;

public static class VibeFinderLoginScripts
{
    public static string Build(string user, string pass, bool force = false)
    {
        // Check in JavaScript too: navigation can change the document after the native check
        // and before ExecuteScriptAsync runs. Serialize credentials without altering passwords.
        var guard = force ? "" : """
            if (localStorage.getItem('vf_token')) return;
            if (sessionStorage.getItem('_themed_auto_login')) return;
            sessionStorage.setItem('_themed_auto_login', '1');
            """;
        return $$"""
            (async function() {
                if (location.origin !== {{JsonSerializer.Serialize(VibeFinderSite.Origin)}}) return;
                const postResult = (success, reason) => {
                    try { window.chrome.webview.postMessage(JSON.stringify({ type: 'VIBEFINDER_LOGIN_RESULT', success, reason: reason || null })); } catch(e) {}
                };
                try {
                    {{guard}}
                    const fd = new URLSearchParams();
                    fd.append('username', {{JsonSerializer.Serialize(user)}});
                    fd.append('password', {{JsonSerializer.Serialize(pass)}});
                    const res = await fetch('https://vibefinderai.onrender.com/auth/token', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: fd,
                    });
                    if (res.ok) {
                        const data = await res.json();
                        if (data.access_token) {
                            localStorage.setItem('vf_token', data.access_token);
                            postResult(true);
                            location.reload();
                            return;
                        }
                        postResult(false, 'server_error');
                        return;
                    }
                    postResult(false, (res.status === 401 || res.status === 400) ? 'invalid_credentials' : 'server_error');
                } catch(e) { postResult(false, 'network'); }
            })();
            """;
    }
}
