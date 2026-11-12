using System.Text.Json;

namespace ThemeManager.Core.Services;

public static class VibeFinderLoginScripts
{
    // This is only a renewal hint. The backend still verifies the JWT signature and claims.
    private const string CurrentTokenFunction = """
        const hasCurrentToken = () => {
            try {
                const token = localStorage.getItem('vf_token');
                const parts = token?.split('.');
                if (parts?.length !== 3) return false;
                const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
                const claims = JSON.parse(atob(payload.padEnd(Math.ceil(payload.length / 4) * 4, '=')));
                return typeof claims.exp === 'number' && Number.isFinite(claims.exp)
                    && claims.exp * 1000 > Date.now() + 30000;
            } catch { return false; }
        };
        """;

    public static string SessionReadyScript => $$"""
        (() => {
            if (location.origin !== {{JsonSerializer.Serialize(VibeFinderSite.Origin)}}) return false;
            window._themed_session_renewal_supported = true;
            {{CurrentTokenFunction}}
            return !window._themed_login_in_flight && hasCurrentToken();
        })();
        """;

    public static string BuildRenewal(string user, string pass, string requestId) => $$"""
        (async () => {
            if (location.origin !== {{JsonSerializer.Serialize(VibeFinderSite.Origin)}}) return;
            const id = {{JsonSerializer.Serialize(requestId)}};
            if (window._themed_session_renewal !== id || window._themed_renewal_in_flight) return;
            {{CurrentTokenFunction}}
            window._themed_renewal_in_flight = true;
            const controller = new AbortController();
            const timeout = setTimeout(() => controller.abort(), 15000);
            let success = false;
            try {
                const fd = new URLSearchParams();
                fd.append('username', {{JsonSerializer.Serialize(user)}});
                fd.append('password', {{JsonSerializer.Serialize(pass)}});
                const res = await fetch('https://vibefinderai.onrender.com/auth/token', {
                    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: fd, signal: controller.signal,
                });
                if (res.ok) {
                    const data = await res.json();
                    // Sign Out, navigation, or the SPA timeout may cancel this request.
                    // Never restore a session after that cancellation.
                    if (window._themed_session_renewal === id && typeof data.access_token === 'string') {
                        localStorage.setItem('vf_token', data.access_token);
                        success = hasCurrentToken();
                        if (!success) localStorage.removeItem('vf_token');
                    }
                }
            } catch { /* The SPA keeps the prompt and offers an explicit retry. */ }
            finally {
                clearTimeout(timeout);
                window._themed_renewal_in_flight = false;
                window.dispatchEvent(new CustomEvent('themed:session-renewed', { detail: { id, success } }));
            }
        })();
        """;

    public static string Build(string user, string pass, bool force = false)
    {
        // Check in JavaScript too: navigation can change the document after the native check
        // and before ExecuteScriptAsync runs. Serialize credentials without altering passwords.
        var guard = force ? "window._themed_session_renewal = null;" : """
            if (hasCurrentToken()) return;
            if (window._themed_auto_login) return;
            window._themed_auto_login = true;
            """;
        return $$"""
            (async function() {
                if (location.origin !== {{JsonSerializer.Serialize(VibeFinderSite.Origin)}}) return;
                window._themed_session_renewal_supported = true;
                {{CurrentTokenFunction}}
                const postResult = (success, reason) => {
                    try { window.chrome.webview.postMessage(JSON.stringify({ type: 'VIBEFINDER_LOGIN_RESULT', success, reason: reason || null })); } catch(e) {}
                };
                try {
                    if (window._themed_login_in_flight) return;
                    {{guard}}
                    window._themed_login_in_flight = true;
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
                            if (hasCurrentToken()) {
                                postResult(true);
                                // Keep the old SPA from running with its stale React token.
                                // The reload creates a new document and clears the in-flight flag.
                                location.reload();
                                return;
                            }
                            localStorage.removeItem('vf_token');
                        }
                        window._themed_login_in_flight = false;
                        postResult(false, 'server_error');
                        return;
                    }
                    window._themed_login_in_flight = false;
                    postResult(false, (res.status === 401 || res.status === 400) ? 'invalid_credentials' : 'server_error');
                } catch(e) {
                    window._themed_login_in_flight = false;
                    postResult(false, 'network');
                }
            })();
            """;
    }
}
