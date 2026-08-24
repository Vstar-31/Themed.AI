using ThemeManager.Core.Models;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Turns a <see cref="CozyTheme"/>'s generated name/description back into a single free-text
/// "vibe phrase" — the input format <see cref="VibeThemeGenerator.Generate"/>'s own analysis
/// pipeline (and anything else built around a plain vibe string, like VibeFinderAI's own
/// <c>/api/vibe/analyze</c>) expects. Kept here in Core, next to <see cref="ThemeNameGenerator"/>
/// (which does the same job in the opposite direction: vibe text → name/description), rather
/// than inside <c>ThemeManager.Integration.Skins.VibeFinderMeasure</c> where it's actually
/// consumed — it's a small pure function with no network/OS dependency, so it belongs with the
/// rest of the NLP text-generation code, stays reusable by any future integration that wants
/// "describe the active theme in words," and — the immediate reason — stays unit-testable from
/// <c>ThemeManager.Tests</c> without that project needing a new reference to
/// <c>ThemeManager.Integration</c> (which nothing in Tests depends on today).
/// </summary>
public static class ThemeVibeText
{
    /// <summary>
    /// Combines <paramref name="theme"/>'s <see cref="CozyTheme.Name"/> and
    /// <see cref="CozyTheme.Description"/> into one descriptive phrase, e.g.
    /// <c>"Mystic Forest. Cool and contemplative, evoking forest, mystic, night."</c>
    /// Description is usually the richer signal for a downstream analyzer — a full sentence
    /// built from every matched mood/category, versus Name's punchy 2–3 words — but isn't
    /// guaranteed non-empty: a theme created via <see cref="ThemeService.CreateThemeAsync"/>
    /// (blank/manually-named, never run through <see cref="VibeThemeGenerator"/>) has an empty
    /// Description, so this falls back to Name alone rather than producing a phrase with a
    /// stray leading/trailing period.
    /// </summary>
    public static string Describe(CozyTheme theme) =>
        string.IsNullOrWhiteSpace(theme.Description) ? theme.Name : $"{theme.Name}. {theme.Description}";
}
