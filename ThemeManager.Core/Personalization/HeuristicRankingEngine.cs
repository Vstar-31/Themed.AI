using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Personalization;

public class ThemeCandidate
{
    public required CozyTheme Theme { get; set; }
    public string GenerationSource { get; set; } = string.Empty;
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
        
        // 1. Context Constraints
        // (Skipping MustBeDarkTheme check since CozyTheme doesn't expose BaseTheme directly yet)

        // 2. Profile History
        if (profile.LikedThemeIds.Contains(candidate.Theme.Id)) score += 10f;
        if (profile.DislikedThemeIds.Contains(candidate.Theme.Id)) score -= 20f;

        // 3. Color Preferences (Heuristic Example)
        // Check if the theme's primary color matches any highly preferred colors
        string primaryHex = candidate.Theme.AccentPrimary;
        if (profile.ColorPreferences.TryGetValue(primaryHex, out float weight))
        {
            score += weight * 5f;
        }

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
