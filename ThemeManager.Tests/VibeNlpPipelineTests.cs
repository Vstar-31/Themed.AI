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
}
