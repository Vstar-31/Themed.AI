using System.Linq;
using System.Text.Json;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Services;

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
    public const string SkipTutorialScript =
        "try { localStorage.setItem('vf_tutorial_seen', '1'); } catch(e) {}";

    public static string BuildAutoLoginScript(string user, string pass) =>
        VibeFinderLoginScripts.Build(user, pass);

    public static string BuildForceLoginScript(string user, string pass) =>
        VibeFinderLoginScripts.Build(user, pass, force: true);

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

}
