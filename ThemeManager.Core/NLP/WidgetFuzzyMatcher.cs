namespace ThemeManager.Core.NLP;

/// <summary>
/// Same edit-distance technique as <see cref="FuzzyMatcher"/> (same adaptive thresholds, same
/// early-exit DP), pointed at <see cref="WidgetLexicon"/> instead of <see cref="ColorLexicon"/>.
/// Duplicated rather than shared because the original is hard-typed to <see cref="ColorSignal"/>
/// — genericizing it would mean touching working, already-tested code for one extra caller.
/// </summary>
public static class WidgetFuzzyMatcher
{
    private static readonly Lazy<string[]> LexiconKeys = new(
        () => WidgetLexicon.Entries.Keys.OrderBy(k => k).ToArray());

    /// <summary>Looks up the closest widget-lexicon entry for <paramref name="token"/>, or null if nothing's close enough.</summary>
    public static WidgetSignal? FindClosest(string token, out string? matchedKey)
    {
        matchedKey = null;
        int len = token.Length;

        // Adaptive thresholds — now aligned with the proven FuzzyMatcher (color) values.
        // The previous widget-specific thresholds (1/3/3/4) were far too loose, causing
        // false matches like "space"→"date" (distance 3 on a 5-char word) and
        // "show"→"glow" (distance 2 on a 4-char word).
        int maxDist = len switch
        {
            <= 3 => 0,   // too short to risk a false match
            <= 5 => 1,
            <= 8 => 2,
            _    => 2,   // cap at 2 to avoid nonsense matches
        };

        if (maxDist == 0) return null;

        string? bestKey = null;
        int bestDist = maxDist + 1;

        foreach (var key in LexiconKeys.Value)
        {
            if (Math.Abs(key.Length - len) > maxDist) continue;

            int dist = EditDistance(token, key, maxDist);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestKey = key;
                if (dist == 0) break;
            }
        }

        if (bestKey is null) return null;

        // Similarity ratio gate: even within the distance budget, reject matches where
        // the edit distance is too large relative to the longer of the two words.
        // This catches cases like two 4-char words sharing only 2 characters.
        int maxLen = Math.Max(len, bestKey.Length);
        if (maxLen > 0 && (double)bestDist / maxLen > 0.4) return null;

        matchedKey = bestKey;
        return WidgetLexicon.Entries.TryGetValue(bestKey, out var sig) ? sig : null;
    }

    private static int EditDistance(string a, string b, int threshold)
    {
        int la = a.Length, lb = b.Length;

        if (la == 0) return lb;
        if (lb == 0) return la;
        if (Math.Abs(la - lb) > threshold) return threshold + 1;

        Span<int> prev = stackalloc int[lb + 1];
        Span<int> curr = stackalloc int[lb + 1];

        for (int j = 0; j <= lb; j++) prev[j] = j;

        for (int i = 1; i <= la; i++)
        {
            curr[0] = i;
            int rowMin = i;

            for (int j = 1; j <= lb; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
                if (curr[j] < rowMin) rowMin = curr[j];
            }

            if (rowMin > threshold) return threshold + 1;

            var tmp = prev; prev = curr; curr = tmp;
        }

        return prev[lb];
    }
}
