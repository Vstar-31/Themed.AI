using System.Collections.Generic;

namespace ThemeManager.Core.Services.NLP
{
    public struct EmotionVector
    {
        /// <summary>
        /// -1.0 (Sad/Negative) to 1.0 (Happy/Positive)
        /// </summary>
        public double Valence { get; set; }

        /// <summary>
        /// -1.0 (Calm/Lethargic) to 1.0 (Energetic/Intense)
        /// </summary>
        public double Arousal { get; set; }

        public EmotionVector(double valence, double arousal)
        {
            Valence = valence;
            Arousal = arousal;
        }

        public static EmotionVector Neutral => new EmotionVector(0, 0);

        public double DistanceTo(EmotionVector other)
        {
            double dv = Valence - other.Valence;
            double da = Arousal - other.Arousal;
            return System.Math.Sqrt(dv * dv + da * da);
        }
    }

    public class SemanticNode
    {
        public string Word { get; set; } = string.Empty;
        public EmotionVector Emotion { get; set; } = EmotionVector.Neutral;
        public string[] RelatedConcepts { get; set; } = System.Array.Empty<string>();
        public string TargetHex { get; set; } = string.Empty; // Mapped color if applicable
        public string TargetVibe { get; set; } = string.Empty; // E.g., 'cyberpunk', 'cozy'
        public double Intensity { get; set; } = 1.0;
    }

    public static class SemanticDictionary
    {
        private static readonly Dictionary<string, SemanticNode> _lexicon = new();

        static SemanticDictionary()
        {
            Initialize();
        }

        private static void Initialize()
        {
            // Initial Seed for Phase 4 Massive Custom Lexicon
            // In a full production environment, this could be loaded from an embedded compressed JSON
            // to support 2000+ entries easily.

            // Basic Emotions
            Add(new SemanticNode { Word = "happy", Emotion = new EmotionVector(0.8, 0.5), RelatedConcepts = new[] { "joy", "cheerful", "sunny" } });
            Add(new SemanticNode { Word = "sad", Emotion = new EmotionVector(-0.8, -0.5), RelatedConcepts = new[] { "gloomy", "depressed", "melancholy" } });
            Add(new SemanticNode { Word = "calm", Emotion = new EmotionVector(0.4, -0.8), RelatedConcepts = new[] { "peaceful", "quiet", "serene" } });
            Add(new SemanticNode { Word = "energetic", Emotion = new EmotionVector(0.6, 0.9), RelatedConcepts = new[] { "active", "dynamic", "vibrant" } });
            
            // Intense/Aggressive
            Add(new SemanticNode { Word = "furious", Emotion = new EmotionVector(-0.9, 0.9), RelatedConcepts = new[] { "angry", "mad" } });
            Add(new SemanticNode { Word = "chaotic", Emotion = new EmotionVector(-0.5, 1.0), RelatedConcepts = new[] { "wild", "crazy" } });
            Add(new SemanticNode { Word = "burning", Emotion = new EmotionVector(-0.6, 0.8), RelatedConcepts = new[] { "fire", "hot" } });
            Add(new SemanticNode { Word = "aggressive", Emotion = new EmotionVector(-0.8, 0.9), RelatedConcepts = new[] { "hostile", "loud" } });
            Add(new SemanticNode { Word = "loud", Emotion = new EmotionVector(0.0, 0.9), RelatedConcepts = new[] { "noisy", "intense" } });

            // Cozy/Calm
            Add(new SemanticNode { Word = "peaceful", Emotion = new EmotionVector(0.8, -0.9), RelatedConcepts = new[] { "calm", "quiet" } });
            Add(new SemanticNode { Word = "morning", Emotion = new EmotionVector(0.6, -0.3), RelatedConcepts = new[] { "dawn", "sunrise" } });
            Add(new SemanticNode { Word = "tea", Emotion = new EmotionVector(0.7, -0.8), RelatedConcepts = new[] { "warm", "cozy" } });
            Add(new SemanticNode { Word = "fireplace", Emotion = new EmotionVector(0.8, -0.6), RelatedConcepts = new[] { "fire", "warm" } });
            Add(new SemanticNode { Word = "snow", Emotion = new EmotionVector(0.5, -0.7), RelatedConcepts = new[] { "cold", "white" } });
            Add(new SemanticNode { Word = "quiet", Emotion = new EmotionVector(0.4, -0.9), RelatedConcepts = new[] { "silent", "calm" } });

            // Melancholic/Sad
            Add(new SemanticNode { Word = "gloomy", Emotion = new EmotionVector(-0.7, -0.6), RelatedConcepts = new[] { "dark", "sad" } });
            Add(new SemanticNode { Word = "depressing", Emotion = new EmotionVector(-0.9, -0.8), RelatedConcepts = new[] { "sad", "bleak" } });
            Add(new SemanticNode { Word = "gray", Emotion = new EmotionVector(-0.3, -0.5), TargetHex = "#808080", RelatedConcepts = new[] { "dull", "neutral" } }); // Keep gray as a color
            Add(new SemanticNode { Word = "isolation", Emotion = new EmotionVector(-0.8, -0.9), RelatedConcepts = new[] { "alone", "lonely" } });

            // Euphoric/Energetic
            Add(new SemanticNode { Word = "ecstatic", Emotion = new EmotionVector(1.0, 1.0), RelatedConcepts = new[] { "thrilled", "happy" } });
            Add(new SemanticNode { Word = "bouncing", Emotion = new EmotionVector(0.8, 0.9), RelatedConcepts = new[] { "active", "energetic" } });
            Add(new SemanticNode { Word = "electric", Emotion = new EmotionVector(0.7, 1.0), RelatedConcepts = new[] { "shock", "neon" } });
            Add(new SemanticNode { Word = "summer", Emotion = new EmotionVector(0.8, 0.6), RelatedConcepts = new[] { "hot", "sun" } });

            // Contradiction Test Words
            Add(new SemanticNode { Word = "nightmare", Emotion = new EmotionVector(-1.0, 0.8), RelatedConcepts = new[] { "scary", "dark" } });
            Add(new SemanticNode { Word = "terrifyingly", Emotion = new EmotionVector(-0.9, 0.9), RelatedConcepts = new[] { "scary", "fear" } });
            Add(new SemanticNode { Word = "joyful", Emotion = new EmotionVector(0.9, 0.7), RelatedConcepts = new[] { "happy", "glad" } });
            Add(new SemanticNode { Word = "pink", Emotion = new EmotionVector(0.6, 0.5), TargetHex = "#FFC0CB", RelatedConcepts = new[] { "color", "bright" } }); // Keep pink!
            
            // Compound words (Bigrams/Trigrams)
            Add(new SemanticNode { Word = "midnight blue", Emotion = new EmotionVector(0.1, -0.6), TargetHex = "#191970", TargetVibe = "dark", RelatedConcepts = new[] { "night", "deep", "space" } });
            Add(new SemanticNode { Word = "rose gold", Emotion = new EmotionVector(0.6, 0.2), TargetHex = "#B76E79", TargetVibe = "elegant", RelatedConcepts = new[] { "luxury", "soft", "metal" } });
            Add(new SemanticNode { Word = "cyberpunk", Emotion = new EmotionVector(0.1, 0.8), TargetVibe = "cyberpunk", RelatedConcepts = new[] { "neon", "future", "hacker" } });
            Add(new SemanticNode { Word = "cozy", Emotion = new EmotionVector(0.7, -0.7), TargetVibe = "cozy", RelatedConcepts = new[] { "warm", "coffee", "autumn" } });
            Add(new SemanticNode { Word = "rainy", Emotion = new EmotionVector(-0.3, -0.6), TargetVibe = "gloomy", RelatedConcepts = new[] { "wet", "cloudy", "storm" } });
            Add(new SemanticNode { Word = "neon", Emotion = new EmotionVector(0.5, 0.9), TargetVibe = "cyberpunk", RelatedConcepts = new[] { "bright", "glow" } });
        }

        private static void Add(SemanticNode node)
        {
            _lexicon[node.Word] = node;
        }

        public static IReadOnlyDictionary<string, SemanticNode> Lexicon => _lexicon;

        public static SemanticNode? LookupFuzzy(string token)
        {
            if (_lexicon.TryGetValue(token, out var exactMatch))
            {
                return exactMatch;
            }

            int len = token.Length;
            int maxDist = len switch
            {
                <= 3 => 0,
                <= 5 => 1,
                <= 8 => 2,
                _ => 3
            };

            if (maxDist == 0) return null;

            SemanticNode? bestMatch = null;
            int bestDist = maxDist + 1;

            foreach (var kvp in _lexicon)
            {
                if (Math.Abs(kvp.Key.Length - len) > maxDist) continue;

                int dist = FuzzyMatcher.ComputeLevenshteinDistance(token, kvp.Key);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestMatch = kvp.Value;
                    if (dist == 0) break;
                }
            }

            return bestMatch;
        }
    }
}
