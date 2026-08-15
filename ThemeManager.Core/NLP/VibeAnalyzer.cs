using ThemeManager.Core.Services.NLP;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Aggregates color signals from all matched tokens into a single VibeSignal.
///
/// Phase 3 lookup priority per token position:
///   1. Bigram (token[i] + token[i+1])  — highest specificity, skips both tokens if matched
///   2. Unigram exact/stem match         — standard lexicon lookup
///   3. Fuzzy match                      — Levenshtein fallback for typos
///
/// The circular mean hue algorithm is unchanged from Phase 2.
/// </summary>
public static class VibeAnalyzer
{
    public static VibeSignal Analyze(string rawText)
    {
        var signal = new VibeSignal();
        if (string.IsNullOrWhiteSpace(rawText)) return signal;

        // ── 1. Leverage the Phase 4 Massive Custom NLP Engine ─────────────────
        var emotionProfile = EmotionAnalyzer.AnalyzePrompt(rawText);
        
        signal.SentimentValence = (float)emotionProfile.Valence;
        
        foreach (var token in emotionProfile.MatchedTokens)
        {
            signal.MatchedKeywords.Add(token);
        }
        
        signal.FuzzyCorrections = emotionProfile.FuzzyCorrections;
        signal.BigramMatches = emotionProfile.BigramMatches;
        signal.HadEmojiInput = emotionProfile.HadEmojiInput;

        // If no tokens matched, we have no signal
        if (emotionProfile.MatchedTokens.Count == 0) return signal;

        // ── 2. Derive Color Math from Emotion Matrix ──────────────────────────
        // Valence maps to Lightness (Happy = Lighter, Sad = Darker)
        // Arousal maps to Saturation (Energetic = Saturated, Calm = Muted)
        
        double baseLightness = 0.50 + (emotionProfile.Valence * 0.40); // Range 0.1 to 0.9
        double baseSaturation = 0.50 + (emotionProfile.Arousal * 0.50); // Range 0.0 to 1.0

        // Add a small random jitter so clicking "Refresh" actually changes the vibe slightly
        var rnd = new Random();
        double jitterL = (rnd.NextDouble() - 0.5) * 0.1;
        double jitterS = (rnd.NextDouble() - 0.5) * 0.1;

        signal.Lightness = (float)Math.Clamp(baseLightness + jitterL, 0.05, 0.95);
        signal.Saturation = (float)Math.Clamp(baseSaturation + jitterS, 0.02, 1.0);
        signal.Warmth = (float)emotionProfile.Arousal; // Rough heuristic

        // ── 3. Hue Selection ──────────────────────────────────────────────────
        // If the dictionary extracted explicit colors, average them
        if (emotionProfile.ExtractedColors.Count > 0)
        {
            double sinSum = 0, cosSum = 0;
            foreach (var hex in emotionProfile.ExtractedColors)
            {
                var (r, g, b) = ThemeManager.Core.Utilities.ColorMath.HexToRgb(hex);
                var hsl = ThemeManager.Core.Utilities.ColorMath.RgbToHsl(r, g, b);
                double hueRad = hsl.H * Math.PI / 180.0;
                sinSum += Math.Sin(hueRad);
                cosSum += Math.Cos(hueRad);
            }
            double hueResult = Math.Atan2(sinSum, cosSum) * 180.0 / Math.PI;
            if (hueResult < 0) hueResult += 360.0;
            signal.Hue = (float)hueResult;
        }
        else
        {
            // Otherwise, dynamically generate Hue based on Emotion quadrant
            if (emotionProfile.Valence >= 0 && emotionProfile.Arousal >= 0) signal.Hue = 15f; // Warm Orange (was 45, which drifted to green)
            else if (emotionProfile.Valence >= 0 && emotionProfile.Arousal < 0) signal.Hue = 195f; // Calm Cyan/Blue
            else if (emotionProfile.Valence < 0 && emotionProfile.Arousal >= 0) signal.Hue = 330f; // Intense Pink/Crimson
            else signal.Hue = 230f; // Melancholic Blue
        }

        // Add random hue jitter (±15 degrees) so refresh works
        signal.Hue = (float)((signal.Hue + (rnd.NextDouble() - 0.5) * 30.0 + 360.0) % 360.0);

        return signal;
    }
}
