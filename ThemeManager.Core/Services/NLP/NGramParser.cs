using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ThemeManager.Core.Services.NLP
{
    public class NGramParser
    {
        public static List<string> ExtractNGrams(string prompt, int maxN = 3)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(prompt)) return results;

            // Remove punctuation and split into words
            prompt = prompt.ToLowerInvariant();
            prompt = Regex.Replace(prompt, @"[^\w\s]", "");
            var words = prompt.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

            // Extract unigrams
            results.AddRange(words);

            // Extract bigrams
            if (maxN >= 2)
            {
                for (int i = 0; i < words.Length - 1; i++)
                {
                    results.Add($"{words[i]} {words[i + 1]}");
                }
            }

            // Extract trigrams
            if (maxN >= 3)
            {
                for (int i = 0; i < words.Length - 2; i++)
                {
                    results.Add($"{words[i]} {words[i + 1]} {words[i + 2]}");
                }
            }

            return results;
        }
    }
}
