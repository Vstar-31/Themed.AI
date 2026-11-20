using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>Chooses an eligible world for a VibeFinder mood without changing active state.</summary>
public static class WorldAutoSwitchSelector
{
    public static DesktopScene? FindCandidate(
        string mood,
        VibePersonalizationProfile? profile,
        DesktopScene active,
        IEnumerable<DesktopScene> scenes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mood);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(scenes);

        if (!active.Behavior.AutoSwitch || !active.Behavior.ReactToVibeFinder) return null;

        var desiredTag = TagForMood(profile is { Signals: >= 3 } && !string.IsNullOrWhiteSpace(profile.TopMood)
            ? profile.TopMood!
            : mood);
        desiredTag ??= TagForTheme(profile?.TopTheme);
        desiredTag ??= TagForAmbience(profile?.TopAmbience);

        if (desiredTag is null || HasTag(active, desiredTag)) return null;

        return scenes.FirstOrDefault(scene =>
            !scene.RequiresExplicitActivation &&
            scene.Behavior.ReactToVibeFinder &&
            HasTag(scene, desiredTag));
    }

    private static bool HasTag(DesktopScene scene, string tag) =>
        scene.Tags.Any(value => value.Equals(tag, StringComparison.OrdinalIgnoreCase));

    private static string? TagForMood(string mood) => mood.ToLowerInvariant() switch
    {
        "nocturnal" => "nocturnal",
        "cozy" => "cozy",
        "euphoric" or "intense" => "neon",
        "bright" => "minimal",
        "melancholic" or "heartbreak" => "nocturnal",
        "focus" or "calm" => "minimal",
        _ => null
    };

    private static string? TagForTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return null;
        var value = theme.ToLowerInvariant();
        if (value.Contains("neon")) return "neon";
        if (value.Contains("dark") || value.Contains("night")) return "nocturnal";
        if (value.Contains("cozy") || value.Contains("warm")) return "cozy";
        return value.Contains("minimal") || value.Contains("light") || value.Contains("bright")
            ? "minimal"
            : null;
    }

    private static string? TagForAmbience(string? ambience)
    {
        if (string.IsNullOrWhiteSpace(ambience)) return null;
        var value = ambience.ToLowerInvariant();
        if (value.Contains("neon") || value.Contains("dynamic")) return "neon";
        if (value.Contains("cozy") || value.Contains("calm")) return "cozy";
        return value.Contains("dark") || value.Contains("night") ? "nocturnal" : null;
    }
}
