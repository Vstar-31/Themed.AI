using System.Text.RegularExpressions;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Converts raw vibe text into clean stemmed tokens ready for lexicon lookup.
///
/// Pipeline — now with Phase 3 additions:
///   0. Emoji expansion   (NEW) — 🌊 → "ocean", 🌙 → "night"
///   1. Lowercase
///   2. Strip punctuation and non-alpha characters
///   3. Split on whitespace
///   4. Remove stopwords
///   5. Porter-stem each surviving token
///
/// Also produces bigram pairs for the BigramLexicon lookup in VibeAnalyzer.
/// </summary>
public static class VibeTokenizer
{
    private static readonly Regex NonAlpha   = new(@"[^a-zA-Z\s]", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new(@"\s+",          RegexOptions.Compiled);

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","this","that","these","those","my","your","our","their",
        "its","his","her","some","any","all","each","every","both","few","more",
        "most","other","such","no","nor","not","only","own","same","so","than",
        "too","very","just","but","a","an",
        "i","me","we","us","you","he","she","it","they","them","who","which",
        "what","whose",
        "in","on","at","to","for","of","with","by","from","up","about","into",
        "through","during","before","after","above","below","between","out",
        "off","over","under","again","further","then","once","and","or","if",
        "as","until","while","because","since","although","though","when","where",
        "is","are","was","were","be","been","being","have","has","had","do",
        "does","did","will","would","shall","should","may","might","must","can",
        "could","need","dare","ought","used",
        "like","make","want","feel","give","get","go","see","look","kind",
        "sort","bit","lot","way","thing","something","anything","nothing",
        "really","quite","rather","maybe","perhaps","please","want","would",
        "love","enjoy","prefer","think","imagine","create","generate","theme",
        "color","colour","palette","vibe","style","mood","feel","aesthetic",
        "show","shows","display","monitor",
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Full tokenization result: stemmed tokens + parallel raw tokens.
    /// Use this instead of calling Tokenize and TokenizeRaw separately.
    /// </summary>
    public static TokenizeResult TokenizeFull(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TokenizeResult(new(), new(), false);

        bool hadEmoji = EmojiSignalMap.ContainsEmoji(text);

        // Stage 0: expand emoji into prose
        text = EmojiSignalMap.Expand(text);

        // Stage 1–2: lowercase + strip non-alpha
        text = text.ToLowerInvariant();
        text = NonAlpha.Replace(text, " ");
        text = MultiSpace.Replace(text, " ").Trim();

        var stemmed = new List<string>();
        var raw     = new List<string>();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length < 2 || Stopwords.Contains(word)) continue;
            raw.Add(word);
            stemmed.Add(PorterStemmer.Stem(word));
        }

        return new TokenizeResult(stemmed, raw, hadEmoji);
    }

    /// <summary>Stemmed tokens only — kept for backward compat with VibeAnalyzer v1.</summary>
    public static List<string> Tokenize(string text) => TokenizeFull(text).Stemmed;

    /// <summary>Raw (unstemmed, lowercased) content words.</summary>
    public static List<string> TokenizeRaw(string text) => TokenizeFull(text).Raw;
}

/// <summary>The full output of a tokenization pass.</summary>
public sealed record TokenizeResult(
    List<string> Stemmed,
    List<string> Raw,
    bool         HadEmoji
);
