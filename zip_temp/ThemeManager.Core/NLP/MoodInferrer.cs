namespace ThemeManager.Core.NLP;

using ThemeManager.Core.Personalization;

/// <summary>
/// Infers a <see cref="Mood"/> from a <see cref="VibeAnalysisResult"/> using heuristic rules.
/// The <see cref="Mood"/> enum already exists in the personalization layer but was always
/// <see cref="Mood.Neutral"/> — this bridges the NLP output into the candidate generation
/// pipeline so the mood-injected variant actually gets a real mood.
/// </summary>
public static class MoodInferrer
{
    // Category tags from ColorLexicon that map to specific moods.
    // Keywords in the "mood" category are checked by their raw display form (the user's word),
    // not the stemmed form, since that's what MatchedKeywords contains.
    private static readonly HashSet<string> RelaxedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "calm", "serene", "peaceful", "tranquil", "zen", "gentle", "soft", "mellow"
    };
    private static readonly HashSet<string> CozyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "cozy", "cosy", "warm", "comfort", "snug", "comfortable"
    };
    private static readonly HashSet<string> PlayfulWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "playful", "fun", "vibrant", "neon", "electric", "party", "pop", "groovy"
    };
    private static readonly HashSet<string> MinimalWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "minimal", "minimalist", "clean", "simple", "sleek"
    };

    /// <summary>
    /// Maps NLP analysis output to the closest <see cref="Mood"/> value.
    /// Priority: explicit keyword matches > numeric signal heuristics > Neutral fallback.
    /// </summary>
    public static Mood InferMood(VibeAnalysisResult? analysis)
    {
        if (analysis is null) return Mood.Neutral;

        // ── 1. Keyword-based (highest confidence) ─────────────────────────
        foreach (var keyword in analysis.MatchedKeywords)
        {
            if (RelaxedWords.Contains(keyword))  return Mood.Relaxed;
            if (CozyWords.Contains(keyword))     return Mood.Cozy;
            if (PlayfulWords.Contains(keyword))  return Mood.Playful;
            if (MinimalWords.Contains(keyword))  return Mood.Minimal;
        }

        // ── 2. Numeric signal heuristics ──────────────────────────────────
        // High saturation + positive sentiment → Energetic
        if (analysis.ComputedSaturation > 0.7f && analysis.SentimentScore > 0.3f)
            return Mood.Energetic;

        // Low warmth + low saturation → Focused (cool, muted)
        if (analysis.WarmthBias < -0.2f && analysis.ComputedSaturation < 0.3f)
            return Mood.Focused;

        // Very dark with low saturation → Focused (dark, concentrated)
        if (analysis.IsDark && analysis.ComputedSaturation < 0.35f)
            return Mood.Focused;

        // Warm tones with moderate lightness → Cozy
        if (analysis.WarmthBias > 0.4f && analysis.ComputedLightness is > 0.35f and < 0.65f)
            return Mood.Cozy;

        // Negative sentiment → Relaxed is the closest fit (moody/melancholic palette)
        if (analysis.SentimentScore < -0.3f)
            return Mood.Relaxed;

        return Mood.Neutral;
    }
}
