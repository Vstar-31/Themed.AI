using System;

namespace ThemeManager.Core.Services.NLP
{
    public static class FuzzyMatcher
    {
        /// <summary>
        /// Computes the Levenshtein distance between two strings, ignoring case.
        /// Useful for catching typos (e.g. "cybrpunk" vs "cyberpunk").
        /// </summary>
        public static int ComputeLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
            {
                if (string.IsNullOrEmpty(target)) return 0;
                return target.Length;
            }

            if (string.IsNullOrEmpty(target)) return source.Length;

            source = source.ToLowerInvariant();
            target = target.ToLowerInvariant();

            int n = source.Length;
            int m = target.Length;
            int[,] d = new int[n + 1, m + 1];

            // Initialize the first row and column
            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// Returns a similarity score between 0.0 and 1.0.
        /// 1.0 means exact match.
        /// </summary>
        public static double GetSimilarity(string source, string target)
        {
            int distance = ComputeLevenshteinDistance(source, target);
            int maxLength = Math.Max(source?.Length ?? 0, target?.Length ?? 0);

            if (maxLength == 0) return 1.0;

            return 1.0 - ((double)distance / maxLength);
        }
    }
}
