namespace ThemeManager.Core.NLP;

/// <summary>
/// Lightweight port of the VADER (Valence Aware Dictionary and sEntiment Reasoner)
/// sentiment analysis algorithm — the same one used by NLTK's SentimentIntensityAnalyzer.
///
/// Python analogy:
///   from nltk.sentiment import SentimentIntensityAnalyzer
///   sia = SentimentIntensityAnalyzer()
///   score = sia.polarity_scores(text)['compound']
///
/// We use a subset of VADER's rules:
///   1. Lexicon lookup (positive/negative valence words)
///   2. Booster words (very, extremely, barely, …)
///   3. Negation detection (not, never, no within a 3-token window)
///   4. Compound normalisation to [−1, +1]
///
/// The compound score feeds into PaletteHarmonizer:
///   +1.0 → brighter, slightly more saturated
///    0.0 → neutral
///   −1.0 → darker, more desaturated
/// </summary>
public static class SentimentAnalyzer
{
    // ── Valence lexicon ───────────────────────────────────────────────────────
    // Positive (1.0–3.0) and negative (−1.0 – −3.0) scores, same scale as VADER.
    private static readonly Dictionary<string, float> Valence =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Strong positives
        ["amazing"]    =  3.0f, ["beautiful"]  =  2.8f, ["brilliant"]  =  2.8f,
        ["wonderful"]  =  2.8f, ["fantastic"]  =  2.7f, ["gorgeous"]   =  2.7f,
        ["magnificent"]=  2.7f, ["stunning"]   =  2.6f, ["splendid"]   =  2.6f,
        ["awesome"]    =  2.5f, ["excellent"]  =  2.5f, ["incredible"]  = 2.5f,
        ["perfect"]    =  2.4f, ["superb"]     =  2.4f, ["glorious"]   =  2.4f,
        ["majestic"]   =  2.4f, ["enchanting"] =  2.3f, ["captivating"]=  2.3f,
        ["radiant"]    =  2.3f, ["luminous"]   =  2.2f, ["vibrant"]    =  2.2f,

        // Moderate positives
        ["good"]       =  1.9f, ["great"]      =  2.0f, ["nice"]       =  1.8f,
        ["lovely"]     =  2.0f, ["pretty"]     =  1.9f, ["cozy"]       =  1.8f,
        ["warm"]       =  1.7f, ["pleasant"]   =  1.8f, ["peaceful"]   =  1.8f,
        ["calm"]       =  1.6f, ["serene"]     =  1.7f, ["elegant"]    =  1.9f,
        ["rich"]       =  1.8f, ["lush"]       =  1.9f, ["dreamy"]     =  1.8f,
        ["magical"]    =  2.0f, ["mystical"]   =  1.9f, ["ethereal"]   =  2.0f,
        ["fresh"]      =  1.7f, ["clean"]      =  1.5f, ["crisp"]      =  1.6f,
        ["soft"]       =  1.5f, ["gentle"]     =  1.6f, ["sweet"]      =  1.7f,
        ["joyful"]     =  2.0f, ["happy"]      =  1.9f, ["cheerful"]   =  1.9f,
        ["playful"]    =  1.8f, ["lively"]     =  1.8f, ["energetic"]  =  1.7f,
        ["bold"]       =  1.5f, ["strong"]     =  1.4f, ["powerful"]   =  1.6f,
        ["romantic"]   =  1.9f, ["intimate"]   =  1.7f, ["cosy"]       =  1.8f,

        // Mild positives
        ["interesting"]=  1.2f, ["cool"]       =  1.3f, ["unique"]     =  1.3f,
        ["simple"]     =  1.1f, ["minimal"]    =  1.0f, ["subtle"]     =  1.1f,
        ["quiet"]      =  1.0f, ["still"]      =  0.9f, ["mellow"]     =  1.2f,
        ["rustic"]     =  1.2f, ["vintage"]    =  1.3f, ["nostalgic"]  =  1.3f,

        // Strong negatives
        ["terrible"]   = -3.0f, ["horrible"]   = -2.9f, ["awful"]      = -2.8f,
        ["dreadful"]   = -2.8f, ["hideous"]    = -2.7f, ["disgusting"] = -2.7f,
        ["ugly"]       = -2.5f, ["ghastly"]    = -2.5f, ["repulsive"]  = -2.5f,
        ["revolting"]  = -2.4f, ["vile"]       = -2.4f, ["wretched"]   = -2.3f,

        // Moderate negatives
        ["bad"]        = -1.9f, ["dark"]       = -0.5f, ["gloomy"]     = -1.8f,
        ["bleak"]      = -1.7f, ["dreary"]     = -1.6f, ["dull"]       = -1.5f,
        ["boring"]     = -1.5f, ["drab"]       = -1.6f, ["harsh"]      = -1.7f,
        ["cold"]       = -0.7f, ["stark"]      = -1.4f, ["grim"]       = -1.7f,
        ["somber"]     = -1.5f, ["sad"]        = -1.8f, ["melancholy"] = -1.6f,
        ["depressing"] = -2.0f, ["dismal"]     = -1.9f, ["forlorn"]    = -1.8f,
        ["eerie"]      = -1.2f, ["creepy"]     = -1.5f, ["ominous"]    = -1.4f,
        ["shadowy"]    = -0.8f, ["murky"]      = -1.2f, ["hazy"]       = -0.5f,
        ["chaotic"]    = -1.3f, ["rough"]      = -1.0f, ["harsh"]      = -1.5f,
    };

    // ── Booster words (intensifiers / diminishers) ────────────────────────────
    private static readonly Dictionary<string, float> Boosters =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["absolutely"]=  0.8f, ["extremely"]  =  0.7f, ["incredibly"]  =  0.7f,
        ["very"]      =  0.5f, ["really"]     =  0.4f, ["quite"]       =  0.3f,
        ["fairly"]    =  0.2f, ["rather"]     =  0.2f, ["pretty"]      =  0.2f,
        ["so"]        =  0.3f, ["deeply"]     =  0.5f, ["truly"]       =  0.4f,
        ["utterly"]   =  0.7f, ["supremely"]  =  0.7f, ["especially"]  =  0.4f,
        ["barely"]    = -0.4f, ["slightly"]   = -0.3f, ["somewhat"]    = -0.2f,
        ["kind"]      = -0.2f, ["sort"]       = -0.2f, ["little"]      = -0.3f,
    };

    // Negation window: any valence word within 3 tokens after these is flipped.
    private static readonly HashSet<string> Negations = new(StringComparer.OrdinalIgnoreCase)
    {
        "not","never","no","neither","nor","hardly","barely","scarcely","without",
        "don't","doesn't","didn't","won't","wouldn't","isn't","aren't","wasn't",
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a compound sentiment score in [−1, +1].
    /// Positive → brighter/warmer palette adjustments.
    /// Negative → darker/cooler palette adjustments.
    /// </summary>
    public static float Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0f;

        var words = text.ToLowerInvariant()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        float sum = 0f;
        int   count = 0;

        for (int i = 0; i < words.Length; i++)
        {
            var word = StripPunctuation(words[i]);
            if (string.IsNullOrEmpty(word)) continue;

            if (!Valence.TryGetValue(word, out float val)) continue;

            // Check for negation in the preceding 3-token window.
            bool negated = false;
            for (int j = Math.Max(0, i - 3); j < i; j++)
            {
                if (Negations.Contains(StripPunctuation(words[j])))
                { negated = true; break; }
            }

            // Apply booster from the immediately preceding word.
            float boost = 0f;
            if (i > 0 && Boosters.TryGetValue(StripPunctuation(words[i - 1]), out float b))
                boost = Math.Sign(val) * b;

            float effective = val + boost;
            if (negated) effective *= -0.74f; // VADER's empirical negation dampening

            sum += effective;
            count++;
        }

        if (count == 0) return 0f;

        // VADER compound normalisation: tanh-like squash to [−1, +1].
        float raw = sum / (Math.Abs(sum) + 15f);
        return Math.Clamp(raw, -1f, 1f);
    }

    private static string StripPunctuation(string w)
        => new(w.Where(char.IsLetter).ToArray());
}
