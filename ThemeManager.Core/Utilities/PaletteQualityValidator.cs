using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// Repairs generated palettes before they are presented or applied. It keeps the art-directed hue
/// and saturation whenever possible, but refuses to ship ambiguous mid-luminance surfaces or
/// unreadable foreground colors. AA text contrast is calculated using WCAG relative luminance.
/// </summary>
public static class PaletteQualityValidator
{
    public const float MinimumTextContrast = 4.5f;
    public const float MinimumBorderContrast = 1.35f;

    public sealed record Report(
        int PassingPairs,
        int TotalPairs,
        float MinimumTextContrast,
        IReadOnlyList<string> Repairs)
    {
        public bool PassesAA => PassingPairs == TotalPairs;
    }

    public static Report ValidateAndRepair(CozyTheme theme, bool? preferDark = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var repairs = new List<string>();

        theme.BackgroundBase = Normalize(theme.BackgroundBase, CozyDefaults.Linen);
        theme.BackgroundAlt = Normalize(theme.BackgroundAlt, CozyDefaults.Khaki);
        theme.Surface = Normalize(theme.Surface, CozyDefaults.Camel);
        theme.AccentPrimary = Normalize(theme.AccentPrimary, CozyDefaults.Cocoa);
        theme.AccentStrong = Normalize(theme.AccentStrong, CozyDefaults.Espresso);
        theme.TextPrimary = Normalize(theme.TextPrimary, CozyDefaults.TextPrimary);
        theme.TextMuted = Normalize(theme.TextMuted, CozyDefaults.TextMuted);
        theme.BorderSubtle = Normalize(theme.BorderSubtle, CozyDefaults.BorderSubtle);

        var baseLuminance = ColorMath.LuminanceFromHex(theme.BackgroundBase);
        bool dark = preferDark ?? baseLuminance < 0.30f;

        // A background in the middle-luminance zone cannot support one consistent readable
        // foreground across a dark surface and a light surface. Move it toward a clear light/dark
        // direction while retaining its hue and saturation.
        var repairedBase = ConstrainLuminance(theme.BackgroundBase, dark ? 0f : 0.42f, dark ? 0.17f : 1f, dark);
        if (repairedBase != theme.BackgroundBase)
        {
            theme.BackgroundBase = repairedBase;
            repairs.Add("background mode");
        }

        // Keep all primary surfaces on the same luminance side. This preserves a coherent UI while
        // leaving hue, tint and fine tonal differences intact.
        var repairedAlt = ConstrainLuminance(theme.BackgroundAlt, dark ? 0f : 0.42f, dark ? 0.17f : 1f, dark);
        if (repairedAlt != theme.BackgroundAlt)
        {
            theme.BackgroundAlt = repairedAlt;
            repairs.Add("secondary background");
        }

        var repairedSurface = ConstrainLuminance(theme.Surface, dark ? 0f : 0.42f, dark ? 0.17f : 1f, dark);
        if (repairedSurface != theme.Surface)
        {
            theme.Surface = repairedSurface;
            repairs.Add("surface");
        }

        // Contrast alone is not enough for a polished palette: the base, navigation and card
        // surfaces need a perceptible tonal staircase. Enforce a small OKLCH-lightness gap while
        // preserving each token's own hue/chroma, so flat-looking generated palettes gain depth
        // without being recolored into a generic theme.
        var orderedAlt = EnsureSurfaceDepth(theme.BackgroundAlt, theme.BackgroundBase, dark);
        if (orderedAlt != theme.BackgroundAlt)
        {
            theme.BackgroundAlt = orderedAlt;
            repairs.Add("surface depth hierarchy");
        }

        var orderedSurface = EnsureSurfaceDepth(theme.Surface, theme.BackgroundAlt, dark);
        if (orderedSurface != theme.Surface)
        {
            theme.Surface = orderedSurface;
            repairs.Add("card surface depth");
        }

        var backgrounds = new[] { theme.BackgroundBase, theme.BackgroundAlt, theme.Surface };
        theme.TextPrimary = RepairForeground(theme.TextPrimary, backgrounds, dark, MinimumTextContrast, out var changedPrimary);
        if (changedPrimary) repairs.Add("primary text contrast");
        theme.TextMuted = RepairForeground(theme.TextMuted, backgrounds, dark, MinimumTextContrast, out var changedMuted);
        if (changedMuted) repairs.Add("muted text contrast");
        theme.AccentPrimary = RepairForeground(theme.AccentPrimary, backgrounds, dark, MinimumTextContrast, out var changedAccent);
        if (changedAccent) repairs.Add("primary accent contrast");
        theme.AccentStrong = RepairForeground(theme.AccentStrong, backgrounds, dark, MinimumTextContrast, out var changedStrong);
        if (changedStrong) repairs.Add("strong accent contrast");

        theme.BorderSubtle = RepairForeground(theme.BorderSubtle, [theme.BackgroundBase], dark,
            MinimumBorderContrast, out var changedBorder);
        if (changedBorder) repairs.Add("border visibility");

        var checks = ContrastChecker.Check(theme);
        var passing = checks.Count(r => r.PassesAA);
        var minimum = checks.Count == 0 ? 0f : checks.Min(r => r.Ratio);
        return new Report(passing, checks.Count, minimum, repairs);
    }

    private static string Normalize(string? value, string fallback)
    {
        try { return CozyTheme.NormalizeHex(string.IsNullOrWhiteSpace(value) ? fallback : value); }
        catch { return fallback; }
    }

    private static string ConstrainLuminance(string hex, float minLuminance, float maxLuminance, bool dark)
    {
        var luminance = ColorMath.LuminanceFromHex(hex);
        if (luminance >= minLuminance && luminance <= maxLuminance)
            return hex;

        var (h, s, l) = ColorMath.HexToHsl(hex);
        // Searching in small steps is deterministic and preserves the hue/chroma much better than
        // replacing a troublesome surface with a generic black/white token.
        int direction = dark ? -1 : 1;
        for (int step = 1; step <= 98; step++)
        {
            float nextLightness = Math.Clamp(l + direction * step * 0.01f, 0.02f, 0.98f);
            var candidate = ColorMath.HslToHex(h, s, nextLightness);
            var candidateLuminance = ColorMath.LuminanceFromHex(candidate);
            if (candidateLuminance >= minLuminance && candidateLuminance <= maxLuminance)
                return candidate;
        }

        return dark ? "#100D18" : "#F1EDF6";
    }

    private static string EnsureSurfaceDepth(string color, string adjacentSurface, bool dark)
    {
        const float minimumGap = 0.06f;
        var (colorL, chroma, hue) = ColorMath.HexToOklch(color);
        var (adjacentL, _, _) = ColorMath.HexToOklch(adjacentSurface);
        bool hierarchyAlreadyClear = dark
            ? colorL >= adjacentL + minimumGap
            : colorL <= adjacentL - minimumGap;
        if (hierarchyAlreadyClear) return color;

        float targetL = dark
            ? Math.Clamp(adjacentL + minimumGap + 0.012f, 0.04f, 0.96f)
            : Math.Clamp(adjacentL - minimumGap - 0.012f, 0.04f, 0.96f);
        var candidate = ColorMath.OklchToHex(targetL, chroma, hue);
        var candidateLuminance = ColorMath.LuminanceFromHex(candidate);

        // A surface staircase is a visual nicety, not permission to break the selected mode.
        // Keep the original role color when a large lightness jump would push a dark UI into the
        // mid-luminance zone (or make a light UI unexpectedly dark); contrast repair still runs.
        if (dark && candidateLuminance > 0.17f) return color;
        if (!dark && candidateLuminance < 0.42f) return color;
        return candidate;
    }

    private static string RepairForeground(
        string foreground,
        IReadOnlyList<string> backgrounds,
        bool darkBackground,
        float requiredRatio,
        out bool changed)
    {
        changed = false;
        if (MeetsContrast(foreground, backgrounds, requiredRatio))
            return foreground;

        var (h, s, l) = ColorMath.HexToHsl(foreground);
        int preferredDirection = darkBackground ? 1 : -1;

        // First preserve the role's expected direction (bright accents on dark surfaces, deep ink
        // on light surfaces); only the lightness shifts, so the accent's recognizable hue survives.
        for (int step = 1; step <= 98; step++)
        {
            var candidate = ColorMath.HslToHex(h, s, Math.Clamp(l + preferredDirection * step * 0.01f, 0.02f, 0.98f));
            if (MeetsContrast(candidate, backgrounds, requiredRatio))
            {
                changed = !string.Equals(candidate, foreground, StringComparison.OrdinalIgnoreCase);
                return candidate;
            }
        }

        // Fallback to a readable neutral in malformed or mixed-luminance input.
        string neutral = darkBackground ? "#F8F5FF" : "#17131D";
        changed = !string.Equals(neutral, foreground, StringComparison.OrdinalIgnoreCase);
        return neutral;
    }

    private static bool MeetsContrast(string foreground, IReadOnlyList<string> backgrounds, float ratio)
        => backgrounds.All(bg => ColorMath.ContrastRatio(foreground, bg) >= ratio);
}
