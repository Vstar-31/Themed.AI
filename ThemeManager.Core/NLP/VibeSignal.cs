namespace ThemeManager.Core.NLP;

/// <summary>
/// The combined color signal after all tokens have been processed.
/// Phase 3 additions: tracks bigram matches, fuzzy corrections, and emoji input separately
/// so the insights panel can explain exactly what happened.
/// </summary>
public sealed class VibeSignal
{
    public float Hue               { get; set; }
    public float Lightness         { get; set; }
    public float Saturation        { get; set; }
    public float Warmth            { get; set; }
    public float SentimentValence  { get; set; }

    /// <summary>Every matched surface-form keyword (unigrams + bigram display forms).</summary>
    public List<string> MatchedKeywords { get; set; } = new();

    /// <summary>keyword → category tag for name generation.</summary>
    public Dictionary<string, string> KeywordCategories { get; set; } = new();

    // ── Phase 3 diagnostics ───────────────────────────────────────────────────

    /// <summary>Bigram phrases that fired ("rose gold", "midnight ocean").</summary>
    public List<string> BigramMatches { get; set; } = new();

    /// <summary>
    /// Fuzzy corrections that fired: original token → matched lexicon key.
    /// e.g. "forrest" → "forest", "ocen" → "ocean".
    /// </summary>
    public Dictionary<string, string> FuzzyCorrections { get; set; } = new();

    /// <summary>Whether the original input contained emoji that were expanded.</summary>
    public bool HadEmojiInput { get; set; }

    // ── Derived ───────────────────────────────────────────────────────────────

    public bool HasSignal => MatchedKeywords.Count > 0;
    public bool IsDark    => Lightness < 0.42f;
}
