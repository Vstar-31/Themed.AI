namespace ThemeManager.Core.NLP;

/// <summary>
/// Finds the closest lexicon key to an unrecognised token using Levenshtein
/// edit distance — the same algorithm as Python's difflib.get_close_matches
/// and RapidFuzz.
///
/// Python analogy:
///   difflib.get_close_matches(word, ColorLexicon.Entries.Keys, n=1, cutoff=0.75)
///
/// Only activates when exact and stem lookups both fail, so it's called rarely
/// and the O(n·m) cost is fine.
///
/// Adaptive threshold:
///   word length 1–3  → max distance 0  (too short to risk a false match)
///   word length 4–5  → max distance 1
///   word length 6–8  → max distance 2
///   word length 9+   → max distance 2  (cap at 2 to avoid nonsense matches)
/// </summary>
public static class FuzzyMatcher
{
    // Pre-materialised sorted key list — built once at first call.
    private static readonly Lazy<string[]> LexiconKeys = new(
        () => ColorLexicon.Entries.Keys.OrderBy(k => k).ToArray());

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up the closest lexicon entry for <paramref name="token"/>.
    /// Returns null if no match is within the adaptive edit-distance threshold.
    ///
    /// <paramref name="matchedKey"/> receives the actual lexicon key that matched,
    /// useful for displaying in the insights panel ("forrest → forest").
    /// </summary>
    public static ColorSignal? FindClosest(string token, out string? matchedKey)
    {
        matchedKey = null;
        int len = token.Length;

        int maxDist = len switch
        {
            <= 3 => 0,
            <= 5 => 1,
            _    => 2,
        };

        if (maxDist == 0) return null;

        string? bestKey  = null;
        int     bestDist = maxDist + 1;

        foreach (var key in LexiconKeys.Value)
        {
            // Quick length pre-filter to skip impossible candidates fast.
            if (Math.Abs(key.Length - len) > maxDist) continue;

            int dist = EditDistance(token, key, maxDist);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestKey  = key;
                if (dist == 0) break; // exact match found
            }
        }

        if (bestKey is null) return null;

        matchedKey = bestKey;
        return ColorLexicon.Entries.TryGetValue(bestKey, out var sig) ? sig : null;
    }

    // ── Levenshtein distance (early-exit DP) ──────────────────────────────────

    /// <summary>
    /// Standard DP Levenshtein with an early exit when the running minimum
    /// exceeds <paramref name="threshold"/> — same optimisation as RapidFuzz.
    /// Returns threshold+1 when cost exceeds threshold (not the true distance).
    /// </summary>
    private static int EditDistance(string a, string b, int threshold)
    {
        int la = a.Length, lb = b.Length;

        // Base cases.
        if (la == 0) return lb;
        if (lb == 0) return la;
        if (Math.Abs(la - lb) > threshold) return threshold + 1;

        // Single-row DP — allocate on the stack for short strings.
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

            // Early exit: if the minimum in this row already exceeds the threshold,
            // no path can reach a distance ≤ threshold.
            if (rowMin > threshold) return threshold + 1;

            // Swap rows.
            var tmp = prev; prev = curr; curr = tmp;
        }

        return prev[lb];
    }
}
