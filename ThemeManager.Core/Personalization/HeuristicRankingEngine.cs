using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.Personalization;

public class ThemeCandidate
{
    public required CozyTheme Theme { get; set; }
    public string GenerationSource { get; set; } = string.Empty;

    /// <summary>Same reasoning as <see cref="WidgetCandidate.Analysis"/> — the theme insights
    /// panel (matched keywords, bigrams, fuzzy corrections, sentiment) needs this.</summary>
    public VibeAnalysisResult? Analysis { get; set; }
}

public class WidgetCandidate
{
    public required SkinDefinition Skin { get; set; }
    public string GenerationSource { get; set; } = string.Empty;

    /// <summary>Matched keywords, fuzzy corrections, etc. from the generation that produced
    /// <see cref="Skin"/> - the widget insights panel needs this. Populated by
    /// <see cref="CandidateEngine.GenerateWidgetCandidates"/>, which calls
    /// <c>GenerateAndExplain</c> rather than the bare <c>Generate</c> specifically so this
    /// survives the trip through ranking.</summary>
    public WidgetAnalysisResult? Analysis { get; set; }
}

public class HeuristicRankingEngine
{
    public List<ThemeCandidate> RankThemes(List<ThemeCandidate> candidates, UserProfile profile, GenerationContext context)
    {
        // Sort descending by score
        return candidates.OrderByDescending(c => ScoreTheme(c, profile, context)).ToList();
    }

    public List<WidgetCandidate> RankWidgets(List<WidgetCandidate> candidates, UserProfile profile, GenerationContext context)
    {
        return candidates.OrderByDescending(c => ScoreWidget(c, profile, context)).ToList();
    }

    private float ScoreTheme(ThemeCandidate candidate, UserProfile profile, GenerationContext context)
    {
        float score = 0f;
        var theme = candidate.Theme;
        var backgroundLuminance = ColorMath.LuminanceFromHex(theme.BackgroundBase);
        var isDark = backgroundLuminance < 0.18f;

        // Explicit light/dark constraints are hard product requirements, not descriptive hints.
        if (context.Constraints.MustBeDarkTheme)
            score += isDark ? 12f : -40f;
        if (context.Constraints.MustBeLightTheme)
            score += isDark ? -40f : 12f;

        // Aesthetic references get a strong semantic boost so a named look isn't beaten by a
        // generic synonym variant just because that variant happened to match another color word.
        var profileMatch = AestheticProfileCatalog.Find(context.Prompt);
        if (profileMatch is not null &&
            theme.Description.Contains(profileMatch.Description, StringComparison.OrdinalIgnoreCase))
            score += 18f;

        // Reward palettes that preserve the target color family while allowing art-directed
        // alternatives to vary in saturation/lightness.
        var targetSignal = VibeAnalyzer.Analyze(context.Prompt);
        if (targetSignal.HasSignal)
        {
            var actualHue = ColorMath.HexToHsl(theme.AccentPrimary).H;
            var hueDistance = Math.Abs(actualHue - targetSignal.Hue);
            hueDistance = Math.Min(hueDistance, 360f - hueDistance);
            score += Math.Max(-2f, 8f - hueDistance / 18f);
        }

        // Palette quality remains a first-class ranking signal even though all generated candidates
        // are repaired before ranking; this also protects future generation paths that add candidates.
        var contrast = ContrastChecker.Check(theme);
        int passing = contrast.Count(r => r.PassesAA);
        score += passing * 1.5f;
        score -= (contrast.Count - passing) * 8f;

        // Profile History and explicit color preferences.
        if (profile.LikedThemeIds.Contains(theme.Id)) score += 10f;
        if (profile.DislikedThemeIds.Contains(theme.Id)) score -= 20f;
        if (profile.ColorPreferences.TryGetValue(CozyTheme.NormalizeHex(theme.AccentPrimary), out float weight))
            score += weight * 5f;

        return score;
    }

    private float ScoreWidget(WidgetCandidate candidate, UserProfile profile, GenerationContext context)
    {
        float score = 0f;
        
        // 1. Constraints
        if (candidate.Skin.Measures.Count > context.Constraints.MaxWidgets) score -= 50f;

        // 2. Profile Preferences
        foreach (var measure in candidate.Skin.Measures)
        {
            if (profile.WidgetPreferences.TryGetValue(measure.Type, out float weight))
            {
                score += weight * 2f;
            }
        }

        return score;
    }
}
