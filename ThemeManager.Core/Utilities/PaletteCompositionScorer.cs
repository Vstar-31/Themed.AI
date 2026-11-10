using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// A deterministic composition heuristic used to rank palettes after readability is repaired.
/// It does not claim to measure taste; it checks useful visual-design proxies: tonal hierarchy,
/// distinct semantic color roles, color-energy fit, and whether geometry/density follow the prompt.
/// Accessibility remains a hard gate in PaletteQualityValidator.
/// </summary>
public static class PaletteCompositionScorer
{
    public sealed record Report(float Score, IReadOnlyList<string> Signals);

    public static Report Evaluate(CozyTheme theme, string? prompt)
    {
        ArgumentNullException.ThrowIfNull(theme);

        float score = 50f;
        var signals = new List<string>();
        var normalizedPrompt = AestheticProfileCatalog.NormalizeForMatching(prompt);

        var contrast = ContrastChecker.Check(theme);
        if (contrast.Count > 0)
        {
            float passingRatio = (float)contrast.Count(result => result.PassesAA) / contrast.Count;
            score += passingRatio * 18f;
            if (passingRatio >= 1f)
                signals.Add("text contrast passes AA");
            else
            {
                score -= (1f - passingRatio) * 18f;
                signals.Add("contrast needs repair");
            }
        }

        var baseColor = ColorMath.HexToOklch(theme.BackgroundBase);
        var altColor = ColorMath.HexToOklch(theme.BackgroundAlt);
        var surfaceColor = ColorMath.HexToOklch(theme.Surface);
        score += ScoreTonalStep(Math.Abs(altColor.L - baseColor.L), "navigation surface depth", signals);
        score += ScoreTonalStep(Math.Abs(surfaceColor.L - altColor.L), "card surface depth", signals);

        var primaryAccent = ColorMath.HexToOklch(theme.AccentPrimary);
        var strongAccent = ColorMath.HexToOklch(theme.AccentStrong);
        float accentDistance = OklchDistance(primaryAccent, strongAccent);
        bool wantsMonochrome = HasAny(normalizedPrompt, "monochrome", "monochromatic", "tonal", "single hue", "grayscale", "greyscale");
        bool wantsHighEnergy = HasAny(normalizedPrompt, "neon", "dopamine", "maximalist", "hyperpop", "brat", "high camp", "electric", "vibrant");
        bool wantsMuted = HasAny(normalizedPrompt, "muted", "pastel", "quiet luxury", "soft focus", "matte", "desaturated", "clean girl");

        if (wantsMonochrome)
        {
            score += accentDistance < 0.13f ? 6f : -2f;
            signals.Add(accentDistance < 0.13f ? "tonal palette intent preserved" : "tonal palette is less cohesive than requested");
        }
        else if (wantsHighEnergy)
        {
            score += accentDistance >= 0.12f ? 7f : -5f;
            signals.Add(accentDistance >= 0.12f ? "high-energy accent roles are distinct" : "high-energy accents need more separation");
        }
        else if (accentDistance >= 0.08f)
        {
            score += 4f;
            signals.Add("accent roles are visually distinct");
        }

        float averageAccentChroma = (primaryAccent.C + strongAccent.C) / 2f;
        if (wantsHighEnergy)
        {
            score += averageAccentChroma >= 0.14f ? 6f : averageAccentChroma < 0.09f ? -4f : 1f;
            if (averageAccentChroma >= 0.14f) signals.Add("accent chroma matches expressive intent");
        }
        if (wantsMuted)
        {
            score += averageAccentChroma <= 0.16f ? 4f : averageAccentChroma > 0.22f ? -4f : 0f;
            if (averageAccentChroma <= 0.16f) signals.Add("accent intensity fits the restrained direction");
        }

        float accentAgainstBase = OklchDistance(primaryAccent, baseColor);
        float strongAgainstSurface = OklchDistance(strongAccent, surfaceColor);
        if (Math.Min(accentAgainstBase, strongAgainstSurface) >= 0.12f)
        {
            score += 5f;
            signals.Add("accent hierarchy reads against its surfaces");
        }
        else if (Math.Min(accentAgainstBase, strongAgainstSurface) < 0.065f)
        {
            score -= 3f;
            signals.Add("an accent may blend into its surface");
        }

        if (HasAny(normalizedPrompt, "glass", "glassmorphism", "liquid glass", "frutiger aero", "holographic", "glossy", "chrome"))
        {
            score += theme.CornerRadiusScale >= 1.18 ? 4f : -3f;
            score += theme.DensityScale >= 1.02 ? 2f : -1f;
        }
        if (HasAny(normalizedPrompt, "brutalist", "brutalism", "hard edges", "industrial", "terminal ui"))
            score += theme.CornerRadiusScale <= 0.72 ? 5f : -4f;
        if (HasAny(normalizedPrompt, "minimal", "minimalist", "clean girl", "quiet luxury", "focus desk", "less clutter", "spacious", "lots of whitespace"))
            score += theme.DensityScale >= 1.05 ? 5f : -4f;

        return new Report(Math.Clamp(score, 0f, 100f), signals);
    }

    private static float ScoreTonalStep(float gap, string name, ICollection<string> signals)
    {
        if (gap >= 0.05f && gap <= 0.22f)
        {
            signals.Add($"{name} is distinct");
            return 7f;
        }

        if (gap < 0.035f)
        {
            signals.Add($"{name} is too flat");
            return -5f;
        }

        if (gap > 0.30f)
        {
            signals.Add($"{name} has a large jump");
            return -2f;
        }

        signals.Add($"{name} is readable");
        return 3f;
    }

    private static float OklchDistance((float L, float C, float H) first, (float L, float C, float H) second)
    {
        float deltaL = first.L - second.L;
        float deltaC = first.C - second.C;
        float deltaH = Math.Abs(first.H - second.H);
        if (deltaH > 180f) deltaH = 360f - deltaH;
        float hueArc = 2f * MathF.Sqrt(Math.Max(0f, first.C * second.C)) *
                       MathF.Sin(deltaH * MathF.PI / 360f);
        return MathF.Sqrt(deltaL * deltaL + deltaC * deltaC + hueArc * hueArc);
    }

    private static bool HasAny(string text, params string[] phrases)
        => phrases.Any(phrase => AestheticProfileCatalog.ContainsPositiveCue(text, phrase));
}
