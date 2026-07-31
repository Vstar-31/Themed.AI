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

        // ── 1. Sentiment ──────────────────────────────────────────────────────
        signal.SentimentValence = SentimentAnalyzer.Analyze(rawText);

        // ── 2. Tokenize (includes emoji expansion in Stage 0) ─────────────────
        var result  = VibeTokenizer.TokenizeFull(rawText);
        var stemmed = result.Stemmed;
        var raw     = result.Raw;
        signal.HadEmojiInput = result.HadEmoji;

        if (stemmed.Count == 0) return signal;

        // ── 3. Signal accumulation ────────────────────────────────────────────
        double sinSum = 0, cosSum = 0;
        double lightnessSum = 0, lightnessWeightSum = 0;
        double saturationSum = 0, saturationWeightSum = 0;
        double warmthSum = 0, warmthWeightSum = 0;

        // Track which indices have already been consumed by a bigram
        // so we don't double-count them as unigrams.
        var consumed = new HashSet<int>();

        void Accumulate(ColorSignal s, float freqBoost, string displayWord, string category)
        {
            double hueRad = s.HueDeg * Math.PI / 180.0;
            double hw = s.HueWeight * freqBoost;
            sinSum += Math.Sin(hueRad) * hw;
            cosSum += Math.Cos(hueRad) * hw;

            lightnessSum       += s.Lightness  * s.LightnessWeight  * freqBoost;
            lightnessWeightSum += s.LightnessWeight  * freqBoost;

            saturationSum       += s.Saturation * s.SaturationWeight * freqBoost;
            saturationWeightSum += s.SaturationWeight * freqBoost;

            warmthSum       += s.Warmth * hw;
            warmthWeightSum += hw;

            if (!signal.MatchedKeywords.Contains(displayWord))
            {
                signal.MatchedKeywords.Add(displayWord);
                signal.KeywordCategories[displayWord] = category;
            }
        }

        // ── Pass A: bigram scan (highest priority) ────────────────────────────
        for (int i = 0; i < stemmed.Count - 1; i++)
        {
            var bsig = BigramLexicon.Lookup(stemmed[i], stemmed[i + 1]);
            if (bsig is null) continue;

            string bigramDisplay = raw.Count > i + 1
                ? $"{raw[i]} {raw[i + 1]}"
                : $"{stemmed[i]} {stemmed[i + 1]}";

            // Bigrams get a 1.4× frequency boost — they're more specific than unigrams.
            Accumulate(bsig, 1.4f, bigramDisplay, bsig.Category);
            signal.BigramMatches.Add(bigramDisplay);
            consumed.Add(i);
            consumed.Add(i + 1);
            i++; // skip next token — it's been consumed by this bigram
        }

        var wordCounts = new Dictionary<string, int>();

        // ── Pass B: unigram + fuzzy for remaining tokens ───────────────────────
        for (int i = 0; i < stemmed.Count; i++)
        {
            if (consumed.Contains(i)) continue;

            string rawWord = i < raw.Count ? raw[i] : stemmed[i];
            
            wordCounts.TryGetValue(stemmed[i], out int count);
            count++;
            wordCounts[stemmed[i]] = count;

            float  boost   = 1f + 0.3f * (count - 1f);

            // 2a. Exact / stem match
            var usig = ColorLexicon.Lookup(stemmed[i]);
            if (usig is not null)
            {
                Accumulate(usig, boost, rawWord, usig.Category);
                continue;
            }

            // 2b. Fuzzy fallback — only for tokens that didn't match anything
            var fsig = FuzzyMatcher.FindClosest(stemmed[i], out string? matchedKey);
            if (fsig is not null && matchedKey is not null)
            {
                // Small weight penalty for a fuzzy match (0.7×) to reflect uncertainty.
                Accumulate(fsig, boost * 0.7f, rawWord, fsig.Category);
                signal.FuzzyCorrections[rawWord] = matchedKey;
            }
        }

        if (!signal.HasSignal) return signal;

        // ── 4. Circular mean for hue ──────────────────────────────────────────
        double hueResult = Math.Atan2(sinSum, cosSum) * 180.0 / Math.PI;
        if (hueResult < 0) hueResult += 360.0;
        signal.Hue = (float)hueResult;

        // ── 5. Weighted means ─────────────────────────────────────────────────
        signal.Lightness  = lightnessWeightSum  > 0
            ? (float)(lightnessSum  / lightnessWeightSum)  : 0.55f;
        signal.Saturation = saturationWeightSum > 0
            ? (float)(saturationSum / saturationWeightSum) : 0.35f;
        signal.Warmth     = warmthWeightSum > 0
            ? (float)(warmthSum / warmthWeightSum) : 0f;

        // ── 6. Sentiment adjustment ───────────────────────────────────────────
        float v = signal.SentimentValence;
        signal.Lightness  = Math.Clamp(signal.Lightness  + v * 0.06f, 0.05f, 0.95f);
        signal.Saturation = Math.Clamp(signal.Saturation + v * 0.04f, 0.02f, 0.95f);

        return signal;
    }
}
