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
    // Keep clause punctuation while tokenizing so negation does not leak across commas,
    // sentence boundaries, or contrastive words such as "but" and "however".
    private static readonly Regex TokenOrBoundary = new(@"[a-zA-Z]+|[,;.!?:\r\n]", RegexOptions.Compiled);

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
        "color","colour","palette","style","feel","aesthetic",
        "show","shows","display","monitor","use","uses","using","keep","keeps","keeping",
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

        // Stage 1–2: lowercase, normalize contractions and tokenize content words alongside
        // clause punctuation. Keep the public raw/stemmed lists compatible, but carry a parallel
        // negation flag so VibeAnalyzer can ignore rejected color words before hue accumulation.
        text = text.ToLowerInvariant().Replace("’", "'", StringComparison.Ordinal)
            .Replace("don't", "dont", StringComparison.Ordinal)
            .Replace("doesn't", "doesnt", StringComparison.Ordinal)
            .Replace("can't", "cant", StringComparison.Ordinal)
            .Replace("isn't", "isnt", StringComparison.Ordinal)
            .Replace("won't", "wont", StringComparison.Ordinal)
            .Replace("cannot", "cannot", StringComparison.Ordinal);

        var stemmed = new List<string>();
        var raw = new List<string>();
        var negatedTokens = new List<bool>();
        var matches = TokenOrBoundary.Matches(text);
        int negationBudget = 0;

        bool IsNegationOperator(string word)
            => word is "no" or "nor" or "not" or "never" or "without" or "avoid" or "skip" or "exclude"
                or "dont" or "doesnt" or "cant" or "isnt" or "wont" or "cannot";

        bool IsContrastConnector(string word)
            => word is "but" or "however" or "instead" or "except" or "yet" or "though" or "although";

        for (int i = 0; i < matches.Count; i++)
        {
            var word = matches[i].Value;

            if (word.Length == 1 && word[0] is ',' or ';' or '.' or '!' or '?' or ':')
            {
                negationBudget = 0;
                continue;
            }

            if (IsContrastConnector(word))
            {
                negationBudget = 0;
                continue;
            }

            // "not only neon" is emphatic, not a rejection of neon.
            if (word == "not" && i + 1 < matches.Count &&
                string.Equals(matches[i + 1].Value, "only", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsNegationOperator(word))
            {
                negationBudget = 2;
                continue;
            }

            if (word.Length < 2 || Stopwords.Contains(word)) continue;

            raw.Add(word);
            stemmed.Add(PorterStemmer.Stem(word));
            negatedTokens.Add(negationBudget > 0);
            if (negationBudget > 0) negationBudget--;
        }

        return new TokenizeResult(stemmed, raw, hadEmoji, negatedTokens);
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
    bool HadEmoji,
    List<bool>? NegatedTokens = null
);
