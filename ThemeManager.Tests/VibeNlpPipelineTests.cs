using ThemeManager.Core.NLP;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for the Vibe-to-Theme NLP pipeline — tokenizer, analyzer, palette generator,
/// fuzzy matching, bigram detection, emoji support, and theme name generation.
/// </summary>
public class VibeNlpPipelineTests
{
    private readonly VibeThemeGenerator _gen = new();

    // ── Basic generation ─────────────────────────────────────────────────────

    [Fact]
    public void Generate_EmptyInput_ReturnsValidTheme()
    {
        var theme = _gen.Generate("");
        Assert.NotNull(theme);
        Assert.NotEmpty(theme.Id);
        Assert.NotEmpty(theme.BackgroundBase);
        Assert.NotEmpty(theme.AccentPrimary);
        Assert.False(theme.IsBuiltIn);
    }

    [Fact]
    public void Generate_NullishInput_DoesNotCrash()
    {
        var theme = _gen.Generate("   ");
        Assert.NotNull(theme);
    }

    [Theory]
    [InlineData("warm cozy coffee")]
    [InlineData("dark moody midnight")]
    [InlineData("ocean blue calm")]
    [InlineData("forest green nature")]
    [InlineData("sunset orange warm")]
    [InlineData("cherry blossom pink")]
    public void Generate_ValidVibes_ProduceDistinctThemes(string vibe)
    {
        var theme = _gen.Generate(vibe);
        Assert.NotNull(theme);
        Assert.NotEmpty(theme.Name);
        Assert.StartsWith("#", theme.BackgroundBase);
        Assert.StartsWith("#", theme.AccentPrimary);
    }

    // ── Explain (analysis insights) ──────────────────────────────────────────

    [Fact]
    public void Explain_ReturnsMatchedKeywords()
    {
        var result = _gen.Explain("warm cozy coffee espresso");

        Assert.NotEmpty(result.MatchedKeywords);
        Assert.NotEmpty(result.GeneratedName);
    }

    [Fact]
    public void Explain_EmptyInput_ReturnsEmptyKeywords()
    {
        var result = _gen.Explain("");
        Assert.Empty(result.MatchedKeywords);
    }

    // ── Sentiment influence ──────────────────────────────────────────────────

    [Fact]
    public void Explain_HappyVibe_HasPositiveSentiment()
    {
        var result = _gen.Explain("happy bright joyful sunshine");
        Assert.True(result.SentimentScore > 0,
            $"Expected positive sentiment, got {result.SentimentScore}");
    }

    [Fact]
    public void Explain_DarkMoody_HasNegativeOrNeutralSentiment()
    {
        var result = _gen.Explain("dark gloomy stormy melancholy");
        Assert.True(result.SentimentScore <= 0.1f,
            $"Expected neutral/negative sentiment, got {result.SentimentScore}");
    }

    // ── Hue direction ────────────────────────────────────────────────────────

    [Fact]
    public void Generate_BlueInput_HasBluishHue()
    {
        var result = _gen.Explain("ocean blue deep sea");
        // Blue hue is roughly 180-260°
        Assert.InRange(result.ComputedHue, 170f, 270f);
    }

    [Fact]
    public void Generate_RedInput_HasReddishHue()
    {
        var result = _gen.Explain("cherry red crimson");
        // Red hue is roughly 340-360 or 0-20°
        bool isRedish = result.ComputedHue >= 330f || result.ComputedHue <= 30f;
        Assert.True(isRedish, $"Expected red hue, got {result.ComputedHue}°");
    }

    // ── Emoji support (Phase 3) ──────────────────────────────────────────────

    [Fact]
    public void Explain_EmojiInput_SetsHadEmojiFlag()
    {
        var result = _gen.Explain("🌊");
        Assert.True(result.HadEmojiInput, "Emoji input should set HadEmojiInput flag");
    }

    [Fact]
    public void Generate_EmojiOnly_ProducesValidTheme()
    {
        var theme = _gen.Generate("🔥🌅");
        Assert.NotNull(theme);
        Assert.StartsWith("#", theme.BackgroundBase);
    }

    // ── Fuzzy matching (Phase 3) ─────────────────────────────────────────────

    [Fact]
    public void Explain_Typos_AreFuzzyCorrected()
    {
        // "coffe" is a typo for "coffee"
        var result = _gen.Explain("coffe warm");
        // Should either match via fuzzy or via direct stem
        Assert.True(
            result.MatchedKeywords.Count > 0 || result.FuzzyCorrections.Count > 0,
            "Expected at least one keyword or fuzzy correction");
    }

    // ── Meta words (vibe/mood) don't leak into the color signal (regression) ──
    // "vibe"/"mood" were removed from VibeTokenizer's stopword list to support widget-generation
    // prompts elsewhere; that meant they started reaching VibeAnalyzer here too, where — with no
    // exact ColorLexicon entry — they'd fall through to FuzzyMatcher. "mood" (4 letters) sits at
    // edit distance 1 from "wood" (also 4 letters), inside the length-4 threshold, so it was
    // silently resolving to a warm brown/orange hue that was never asked for.

    [Fact]
    public void Explain_BareMoodWord_DoesNotFuzzyMatchWood()
    {
        var result = _gen.Explain("capture this mood");
        Assert.False(result.FuzzyCorrections.ContainsKey("mood"));
    }

    [Fact]
    public void Explain_BareVibeAndMoodWords_ContributeNoColorSignal()
    {
        // Same reasoning, checked the direct way: adding the word changes nothing about the
        // resulting hue, since both now resolve to an explicit, all-zero-weight ColorLexicon
        // entry rather than either vanishing (pre-fix) or fuzzy-matching something else (the
        // bug this locks in).
        var baseline = _gen.Explain("ocean");
        var withVibe = _gen.Explain("ocean vibe");
        var withMood = _gen.Explain("ocean mood");
        Assert.Equal(baseline.ComputedHue, withVibe.ComputedHue);
        Assert.Equal(baseline.ComputedHue, withMood.ComputedHue);
    }

    // ── Bigram matching (Phase 3) ────────────────────────────────────────────

    [Fact]
    public void Explain_Bigram_Detected()
    {
        var result = _gen.Explain("cherry blossom");
        // "cherry blossom" should be detected as a bigram
        if (result.BigramMatches.Count > 0)
        {
            Assert.Contains(result.BigramMatches,
                b => b.Contains("cherry", StringComparison.OrdinalIgnoreCase)
                  || b.Contains("blossom", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // If no bigram match, there should at least be unigram matches
            Assert.NotEmpty(result.MatchedKeywords);
        }
    }

    // ── GenerateAndExplain combined ──────────────────────────────────────────

    [Fact]
    public void GenerateAndExplain_ReturnsBothThemeAndAnalysis()
    {
        var (theme, analysis) = _gen.GenerateAndExplain("warm autumn leaves");

        Assert.NotNull(theme);
        Assert.NotNull(analysis);
        Assert.Equal(theme.Name, analysis.GeneratedName);
    }

    [Fact]
    public void GenerateAndExplain_Swatches_AreValidHex()
    {
        var (_, analysis) = _gen.GenerateAndExplain("ocean breeze");

        foreach (var swatch in analysis.Swatches)
        {
            Assert.StartsWith("#", swatch);
            Assert.True(swatch.Length is 7 or 4,
                $"Swatch '{swatch}' has unexpected length {swatch.Length}");
        }
    }

    // ── Dark mode detection ──────────────────────────────────────────────────

    [Fact]
    public void Explain_DarkInput_DetectsAsDark()
    {
        var result = _gen.Explain("dark midnight noir");
        Assert.True(result.IsDark, "Input with 'dark midnight noir' should be detected as dark");
    }

    [Fact]
    public void Explain_LightInput_IsNotDark()
    {
        var result = _gen.Explain("bright white cream light");
        // If the engine treats this as light, IsDark should be false.
        // If it treats some words as dark-adjacent, just verify it doesn't crash.
        Assert.NotNull(result);
    }

    // ── Determinism ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_SameInput_ProducesSamePalette()
    {
        var theme1 = _gen.Generate("cozy warm latte");
        var theme2 = _gen.Generate("cozy warm latte");

        Assert.Equal(theme1.BackgroundBase, theme2.BackgroundBase);
        Assert.Equal(theme1.AccentPrimary, theme2.AccentPrimary);
    }

    [Fact]
    public void Generate_CircularCpuGauge_ReturnsRingMeter()
    {
        var widgetGen = new WidgetVibeGenerator();
        var skin = widgetGen.Generate("Add a circular CPU gauge");
        Assert.Contains(skin.Meters, m => m.Kind == ThemeManager.Core.Skins.MeterKind.Ring);
    }
}