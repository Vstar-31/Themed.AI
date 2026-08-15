using System;
using System.Collections.Generic;
using System.Linq;
using ThemeManager.Core.NLP;

namespace ThemeManager.Core.Services.NLP
{
    public class EmotionProfile
    {
        public double Valence { get; set; }
        public double Arousal { get; set; }
        public double Intensity { get; set; }
        public List<string> MatchedTokens { get; set; } = new();
        public List<string> DetectedVibes { get; set; } = new();
        public List<string> ExtractedColors { get; set; } = new();
        public Dictionary<string, string> FuzzyCorrections { get; set; } = new();
        public List<string> BigramMatches { get; set; } = new();
        public bool HadEmojiInput { get; set; }

        public string GetDominantVibe()
        {
            if (DetectedVibes.Any())
            {
                return DetectedVibes.GroupBy(v => v)
                                    .OrderByDescending(g => g.Count())
                                    .First().Key;
            }

            // Fallback based on emotion quadrant
            if (Valence >= 0 && Arousal >= 0) return "energetic";
            if (Valence >= 0 && Arousal < 0) return "cozy";
            if (Valence < 0 && Arousal >= 0) return "cyberpunk"; // Intense/Negative could be aggressive/cyberpunk
            return "dark"; // Negative/Calm
        }
    }

    public static class EmotionAnalyzer
    {
        private static readonly HashSet<string> _negationWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "not", "no", "never", "without", "lack"
        };

        public static EmotionProfile AnalyzePrompt(string prompt)
        {
            var profile = new EmotionProfile();
            
            profile.HadEmojiInput = EmojiSignalMap.ContainsEmoji(prompt);
            prompt = EmojiSignalMap.Expand(prompt);
            
            var nGrams = NGramParser.ExtractNGrams(prompt, maxN: 3);
            
            // Sort by length descending so we match trigrams before unigrams
            nGrams = nGrams.OrderByDescending(g => g.Length).ToList();
            
            var matchedSpans = new HashSet<string>();
            double totalValence = 0;
            double totalArousal = 0;
            double totalIntensity = 0;
            int matchCount = 0;

            bool isNegated = false;

            // Simple pass: look for negations before evaluating tokens
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i].ToLowerInvariant();
                
                if (_negationWords.Contains(word))
                {
                    isNegated = true;
                    continue; // Skip the negation word itself
                }

                // Check for exact/fuzzy match in our massive dictionary
                var node = SemanticDictionary.LookupFuzzy(word);
                
                if (node != null)
                {
                    if (word != node.Word) profile.FuzzyCorrections[word] = node.Word;
                    profile.MatchedTokens.Add(node.Word);
                    if (!string.IsNullOrEmpty(node.TargetHex)) profile.ExtractedColors.Add(node.TargetHex);
                    if (!string.IsNullOrEmpty(node.TargetVibe)) profile.DetectedVibes.Add(node.TargetVibe);

                    double valence = node.Emotion.Valence;
                    double arousal = node.Emotion.Arousal;

                    if (isNegated)
                    {
                        valence = -valence; // "Not happy" -> Sad
                        arousal = -arousal; // "Not energetic" -> Calm
                        isNegated = false; // Reset negation after applying to the first matching concept
                    }

                    totalValence += valence;
                    totalArousal += arousal;
                    totalIntensity += node.Intensity;
                    matchCount++;
                }
            }

            // Also check compound n-grams (bigrams/trigrams) for things like "midnight blue"
            foreach (var ngram in nGrams.Where(n => n.Contains(' '))) // Only compounds
            {
                var node = SemanticDictionary.LookupFuzzy(ngram); // Stricter match for compounds handled internally
                if (node != null && !profile.MatchedTokens.Contains(node.Word))
                {
                    if (ngram != node.Word) profile.FuzzyCorrections[ngram] = node.Word;
                    profile.BigramMatches.Add(node.Word);
                    profile.MatchedTokens.Add(node.Word);
                    if (!string.IsNullOrEmpty(node.TargetHex)) profile.ExtractedColors.Add(node.TargetHex);
                    if (!string.IsNullOrEmpty(node.TargetVibe)) profile.DetectedVibes.Add(node.TargetVibe);
                    
                    totalValence += node.Emotion.Valence;
                    totalArousal += node.Emotion.Arousal;
                    totalIntensity += node.Intensity;
                    matchCount++;
                }
            }

            if (matchCount > 0)
            {
                profile.Valence = totalValence / matchCount;
                profile.Arousal = totalArousal / matchCount;
                profile.Intensity = totalIntensity / matchCount;
            }

            return profile;
        }
    }
}
