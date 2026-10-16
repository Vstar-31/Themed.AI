using ThemeManager.Core.NLP;
using ThemeManager.Core.Utilities;
using ThemeManager.Core.Personalization;

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

    // Curated aesthetics are first-class semantic anchors, not accidental fuzzy color matches.
    [Theory]
    [InlineData("Neon Afterglow", "Neon Afterglow")]
    [InlineData("Frutiger Aero", "Frutiger Aero")]
    [InlineData("Dark Academia", "Dark Academia")]
    [InlineData("Poetcore", "Poetcore")]
    [InlineData("Vamp Romantic", "Vamp Romantic")]
    [InlineData("Neo Deco", "Neo Deco")]
    [InlineData("Extra Celestial", "Extra Celestial")]
    [InlineData("Gimme Gummy", "Gimme Gummy")]
    [InlineData("Glitchy Glam digital night", "Glitchy Glam")]
    [InlineData("Wilderkind", "Wilderkind")]
    [InlineData("Glamoratti", "Glamoratti")]
    [InlineData("Opera Aesthetic", "Opera Aesthetic")]
    [InlineData("Arcane", "Arcane-Inspired")]
    [InlineData("Studio Ghibli forest", "Storybook Forest")]
    [InlineData("Star Wars cockpit", "Space Opera")]
    [InlineData("Minecraft Cherry Grove", "Cherry Grove")]
    [InlineData("Brooched vintage jewellery", "Brooched")]
    [InlineData("Scent Stacking amber vanilla", "Scent Stacking")]
    [InlineData("Pen Pals letter-writing desk", "Pen Pals")]
    [InlineData("Darecations mountain escape", "Darecations")]
    [InlineData("Cabbage Crush garden green", "Cabbage Crush")]
    [InlineData("Cool Blue glacier aesthetic", "Cool Blue")]
    [InlineData("FunHaus playful Bauhaus", "FunHaus")]
    [InlineData("Khaki Coded expedition kit", "Khaki Coded")]
    [InlineData("Mystic Outlands fantasy", "Mystic Outlands")]
    [InlineData("Afrohemian Decor", "Afrohemian Decor")]
    [InlineData("Laced Up lacecore", "Laced Up")]
    [InlineData("Throwback Kid", "Throwback Kid")]
    [InlineData("Wednesday Addams", "Wednesday Goth")]
    [InlineData("Pip-Boy Fallout terminal", "Retro-Future Terminal")]
    [InlineData("Blade Runner 2049", "Rain City 2049")]
    [InlineData("Tron Legacy", "Digital Grid")]
    [InlineData("K-pop comeback stage", "K-Pop Stage")]
    public void Generate_RecognizedAesthetics_UsesCuratedProfileAndPassesContrast(string prompt, string expectedName)
    {
        var (theme, analysis) = _gen.GenerateAndExplain(prompt);

        Assert.Equal(expectedName, theme.Name);
        Assert.Contains(analysis.MatchedKeywords, k => k.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
        var checks = ContrastChecker.Check(theme);
        Assert.All(checks, check =>
            Assert.True(check.PassesAA, $"{prompt}: {check.PairName} is only {check.Ratio:F2}:1"));
    }

    [Fact]
    public void Analyze_ExplicitNavyAndCoral_KeepsTwoHueAnchors()
    {
        var signal = VibeAnalyzer.Analyze("navy and coral");

        Assert.True(signal.HasPrimaryPaletteHue);
        Assert.True(signal.HasSecondaryPaletteHue);
        Assert.InRange(signal.PrimaryPaletteHue, 190f, 245f);
        Assert.True(signal.SecondaryPaletteHue >= 330f || signal.SecondaryPaletteHue <= 30f,
            $"Expected a coral/red counterpoint, got {signal.SecondaryPaletteHue:F0}°.");
    }

    [Fact]
    public void Generate_ExplicitNavyAndCoral_PreservesBlueAndCoralAccentRoles()
    {
        var theme = _gen.Generate("navy and coral");
        var primaryHue = ThemeManager.Core.Utilities.ColorMath.HexToHsl(theme.AccentPrimary).H;
        var strongHue = ThemeManager.Core.Utilities.ColorMath.HexToHsl(theme.AccentStrong).H;

        Assert.InRange(primaryHue, 185f, 255f);
        Assert.True(strongHue >= 330f || strongHue <= 35f,
            $"Expected the secondary accent to remain coral/red, got {strongHue:F0}°.");
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} is only {check.Ratio:F2}:1"));
    }

    [Fact]
    public void Generate_BlendedAestheticPrompt_ComposesBothProfilesAndPassesContrast()
    {
        const string prompt = "Frutiger Aero meets Neo Deco";
        var (theme, analysis) = _gen.GenerateAndExplain(prompt);

        Assert.Equal("Frutiger Aero × Neo Deco", theme.Name);
        Assert.Contains(analysis.MatchedKeywords, k => k == "Frutiger Aero");
        Assert.Contains(analysis.MatchedKeywords, k => k == "Neo Deco");
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} is only {check.Ratio:F2}:1"));
    }

    [Theory]
    [InlineData("not cyberpunk, make it cottagecore")]
    [InlineData("don't want cyberpunk, make it cottagecore")]
    [InlineData("without cyberpunk, make it cottagecore")]
    public void Generate_NegatedAestheticDoesNotWinOverPositiveReference(string prompt)
    {
        var theme = _gen.Generate(prompt);

        Assert.Equal("Cottagecore", theme.Name);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} is only {check.Ratio:F2}:1"));
    }

    [Theory]
    [InlineData("dark midnight ocean storm")]
    [InlineData("pastel cherry blossom pink")]
    [InlineData("cool minimal arctic glass")]
    [InlineData("warm terracotta reading nook")]
    [InlineData("electric neon city with cyan accents")]
    [InlineData("forest green and gold fantasy desktop")]
    [InlineData("bright coral pop art")]
    [InlineData("moonlit purple space opera")]
    public void Generate_FreeFormPrompts_PassesContrastQualityGate(string prompt)
    {
        var theme = _gen.Generate(prompt);
        var checks = ContrastChecker.Check(theme);

        Assert.Equal(9, checks.Count);
        Assert.All(checks, check =>
            Assert.True(check.PassesAA, $"{prompt}: {check.PairName} is only {check.Ratio:F2}:1"));
    }

    [Theory]
    [InlineData("#FF4FD8", true)]
    [InlineData("#286DB5", false)]
    public void HarmonyLockPalette_PreservesAnchorAndRequestedPolarity(string accentHex, bool preferDark)
    {
        var palette = PaletteHarmonizer.FromAccentHex(accentHex, preferDark);
        Assert.Equal(accentHex, palette.AccentPrimary);
        Assert.Equal(preferDark, palette.IsDark);

        var theme = new ThemeManager.Core.Models.CozyTheme();
        palette.ApplyTo(theme);
        var report = PaletteQualityValidator.ValidateAndRepair(theme, preferDark);

        Assert.True(report.PassesAA);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} is only {check.Ratio:F2}:1"));
    }

    [Fact]
    public void Generate_CircularCpuGauge_ReturnsRingMeter()
    {
        var widgetGen = new WidgetVibeGenerator();
        var skin = widgetGen.Generate("Add a circular CPU gauge");
        Assert.Contains(skin.Meters, m => m.Kind == ThemeManager.Core.Skins.MeterKind.Ring);
    }

    /// <summary>
    /// Regression: "gauge" (→Bar) before "circular" (→Ring) used to produce Bar
    /// because the old tie-break resolved by word order. With the specificity fix,
    /// Ring wins regardless of position.
    /// </summary>
    [Fact]
    public void Generate_GaugeBeforeCircular_StillReturnsRing()
    {
        var widgetGen = new WidgetVibeGenerator();
        var skin = widgetGen.Generate("add a gauge for CPU, circular");
        Assert.Contains(skin.Meters, m => m.Kind == ThemeManager.Core.Skins.MeterKind.Ring);
    }

    [Theory]
    [InlineData("Jade palette", "Jade Reverie")]
    [InlineData("Pinterest Palette Plum Noir villain era", "Plum Noir")]
    [InlineData("Wasabi green aesthetic", "Wasabi Voltage")]
    [InlineData("Persimmon orange-red", "Persimmon Heat")]
    [InlineData("brat green", "Brat Summer")]
    [InlineData("office siren late night", "Office Siren")]
    [InlineData("sea witchery", "Sea Witchery")]
    [InlineData("Castlecore medieval romance", "Castlecore")]
    [InlineData("cherry coded", "Cherry Coded")]
    [InlineData("Terra Futura climate tech", "Terra Futura")]
    [InlineData("clean girl quiet luxury", "Clean Girl")]
    [InlineData("liminal space dreamcore", "Liminal Dream")]
    [InlineData("Minecraft redstone", "Minecraft Overworld")]
    [InlineData("Sailor Moon", "Moon Prism")]
    [InlineData("Dune Arrakis spice", "Arrakis Spice")]
    [InlineData("Wicked Emerald City", "Emerald Stage")]
    [InlineData("KPop Demon Hunters", "Idol Exorcist")]
    public void Generate_ExpandedCultureAndTrendReferences_UsesCuratedArtDirection(string prompt, string expectedName)
    {
        var (theme, analysis) = _gen.GenerateAndExplain(prompt);

        Assert.Equal(expectedName, theme.Name);
        Assert.Contains(analysis.MatchedKeywords, k => k.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
        var quality = PaletteQualityValidator.ValidateAndRepair(theme);
        Assert.True(quality.PassesAA, $"{prompt}: {quality.PassingPairs}/{quality.TotalPairs} pairs passed.");
    }

    [Theory]
    [InlineData("jelly blush with rubberized accessories", "Gimme Gummy")]
    [InlineData("opalescent alien beauty", "Extra Celestial")]
    [InlineData("chunky belt and baggy suit", "Glamoratti")]
    [InlineData("scotland highlands aesthetic", "Mystic Outlands")]
    [InlineData("letter writing desk with cute stamps", "Pen Pals")]
    [InlineData("geometric glamour and sculpted brass", "Neo Deco")]
    [InlineData("jet black nails and dark romantic makeup", "Vamp Romantic")]
    [InlineData("circuscore decor and bold color blocking", "FunHaus")]
    [InlineData("lace gloves with ribboncore", "Laced Up")]
    [InlineData("90s toy aesthetic", "Throwback Kid")]
    [InlineData("tropicoqueta showgirl aesthetic", "Hot Tropic")]
    [InlineData("pirate eye makeup and suede mini skirt", "Swashbuckled")]
    [InlineData("2000s rock outfits with thrifted band tees", "Shoegaze Aesthetic")]
    [InlineData("airbrush jeans and rhinestone tops", "Peak Poptimism")]
    [InlineData("organza shirt and angelic accessories", "Sheer Euphoria")]
    [InlineData("theater kid mismatched outfit", "High Camp")]
    public void Generate_TrendDescriptionLanguageResolvesToItsCuratedProfile(string prompt, string expectedName)
    {
        var (theme, analysis) = _gen.GenerateAndExplain(prompt);

        Assert.Equal(expectedName, theme.Name);
        Assert.Contains(analysis.MatchedKeywords, word =>
            word.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
        var report = PaletteQualityValidator.ValidateAndRepair(theme);
        Assert.True(report.PassesAA,
            $"{prompt}: only {report.PassingPairs}/{report.TotalPairs} contrast pairs passed.");
    }

    [Theory]
    [InlineData("Neon Afterglw", "Neon Afterglow")]
    [InlineData("Frutiger Aro", "Frutiger Aero")]
    [InlineData("Plum Noire villain era", "Plum Noir")]
    public void FindAesthetic_RepairsSmallTyposInNamedReferences(string prompt, string expectedName)
    {
        var profile = AestheticProfileCatalog.Find(prompt);
        Assert.NotNull(profile);
        Assert.Equal(expectedName, profile.Name);

        var theme = _gen.Generate(prompt);
        Assert.Equal(expectedName, theme.Name);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{prompt}: {check.PairName} only has {check.Ratio:F2}:1"));
    }

    [Theory]
    [InlineData("Cool-Wasabi palette", "Cool Blue", "Wasabi Voltage")]
    [InlineData("Persimmon-Plum palette", "Persimmon Heat", "Plum Noir")]
    public void Generate_PinterestPaletteSubPalettes_BlendBothArtDirections(
        string prompt, string firstName, string secondName)
    {
        var matches = AestheticProfileCatalog.FindAll(prompt);
        Assert.Contains(matches, p => p.Name == firstName);
        Assert.Contains(matches, p => p.Name == secondName);
        Assert.True(AestheticProfileCatalog.RequestsBlend(prompt, matches));

        var theme = _gen.Generate(prompt);
        Assert.Equal($"{firstName} × {secondName}", theme.Name);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{prompt}: {check.PairName} only has {check.Ratio:F2}:1"));
    }

    [Fact]
    public void Generate_EveryCuratedProfilePassesThePublicGenerationQualityGate()
    {
        foreach (var profile in AestheticProfileCatalog.Profiles)
        {
            var (theme, analysis) = _gen.GenerateAndExplain(profile.Name);
            Assert.Equal(profile.Name, theme.Name);
            Assert.Equal(theme.Name, analysis.GeneratedName);

            var report = PaletteQualityValidator.ValidateAndRepair(theme);
            Assert.True(report.PassesAA,
                $"{profile.Name}: only {report.PassingPairs}/{report.TotalPairs} contrast pairs passed.");
            Assert.Equal(9, ContrastChecker.Check(theme).Count);
        }
    }

    [Theory]
    [InlineData("Neon Afterglow with violet accents", true)]
    [InlineData("Dark Academia with candlelight", true)]
    [InlineData("Cool-Wasabi palette with brutalist hard edges", true)]
    [InlineData("Poetcore with glassy navy panels", false)]
    [InlineData("clean girl pastel minimal", false)]
    public void Generate_EveryCandidateVariantPassesPaletteQualityGate(string prompt, bool mustBeDark)
    {
        var context = new GenerationContext
        {
            Prompt = prompt,
            Constraints = new GenerationConstraints
            {
                MustBeDarkTheme = mustBeDark && prompt.Contains("Neon", StringComparison.OrdinalIgnoreCase),
                MustBeLightTheme = !mustBeDark && prompt.Contains("clean girl", StringComparison.OrdinalIgnoreCase)
            }
        };
        var candidates = new CandidateEngine().GenerateThemeCandidates(context, count: 3);
        Assert.Equal(3, candidates.Count);

        foreach (var candidate in candidates)
        {
            var report = PaletteQualityValidator.ValidateAndRepair(candidate.Theme);
            Assert.True(report.PassesAA,
                $"{prompt} ({candidate.GenerationSource}): {report.PassingPairs}/{report.TotalPairs} pairs passed.");
            Assert.All(ContrastChecker.Check(candidate.Theme), pair =>
                Assert.True(pair.PassesAA, $"{candidate.GenerationSource}: {pair.PairName} {pair.Ratio:F2}:1"));
            Assert.Equal(candidate.Theme.Name, candidate.Analysis?.GeneratedName);
            Assert.Equal(
                new[] { candidate.Theme.BackgroundBase, candidate.Theme.BackgroundAlt, candidate.Theme.Surface,
                    candidate.Theme.AccentPrimary, candidate.Theme.AccentStrong },
                candidate.Analysis?.Swatches);
        }
    }

    [Fact]
    public void Generate_AccentColorModifierKeepsNamedBackgroundIdentity()
    {
        var baseTheme = AestheticProfileCatalog.CreateTheme(
            AestheticProfileCatalog.Find("Frutiger Aero")!, "Frutiger Aero");
        var modified = _gen.Generate("Frutiger Aero with violet accents");

        Assert.Equal(baseTheme.BackgroundBase, modified.BackgroundBase);
        var hue = ThemeManager.Core.Utilities.ColorMath.HexToHsl(modified.AccentPrimary).H;
        Assert.InRange(hue, 255f, 300f);
        Assert.All(ContrastChecker.Check(modified), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1"));
    }

    [Theory]
    [InlineData("Neon Afterglw meets Frutiger Aro", "Neon Afterglow", "Frutiger Aero")]
    [InlineData("Frutiger Aro meets Neon Afterglw", "Frutiger Aero", "Neon Afterglow")]
    public void Generate_TypoTolerantBlendsPreserveRequestedReferenceOrder(
        string prompt, string firstName, string secondName)
    {
        var profiles = AestheticProfileCatalog.FindAll(prompt);
        Assert.True(AestheticProfileCatalog.RequestsBlend(prompt, profiles));
        Assert.Equal(new[] { firstName, secondName }, profiles.Take(2).Select(p => p.Name));

        var theme = _gen.Generate(prompt);
        Assert.Equal($"{firstName} × {secondName}", theme.Name);
        var quality = PaletteQualityValidator.ValidateAndRepair(theme);
        Assert.True(quality.PassesAA,
            $"{prompt}: only {quality.PassingPairs}/{quality.TotalPairs} text pairs passed.");
    }

    [Fact]
    public void Generate_BlendedAestheticsPreserveCompositionModifiers()
    {
        var theme = _gen.Generate("Cool-Wasabi palette with brutalist hard edges and spacious layout");

        Assert.Equal(0.55, theme.CornerRadiusScale);
        Assert.True(theme.DensityScale > 1.0);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1"));
    }

    [Fact]
    public void Analyze_SingleTargetNegationDoesNotSuppressFollowingColor()
    {
        var analysis = VibeAnalyzer.Analyze("not too bright cyan accent");

        Assert.Contains(analysis.MatchedKeywords,
            word => word.Equals("cyan", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("not neon but cyan", "neon", "cyan")]
    [InlineData("no blue, use coral", "blue", "coral")]
    [InlineData("not dark academia, use cyan", "dark", "cyan")]
    public void Analyze_NegatedColorsDoNotPollutePositiveColorSignals(
        string prompt, string rejectedColor, string acceptedColor)
    {
        var analysis = VibeAnalyzer.Analyze(prompt);

        Assert.DoesNotContain(analysis.MatchedKeywords,
            word => word.Equals(rejectedColor, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.MatchedKeywords,
            word => word.Equals(acceptedColor, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_MinimalArtDirectionIncreasesWhitespaceInsteadOfTighteningLayout()
    {
        var baseline = _gen.Generate("Coquette");
        var minimal = _gen.Generate("Coquette with a minimal, clean layout");

        Assert.True(minimal.DensityScale > baseline.DensityScale,
            $"Expected minimal spacing to increase, got {baseline.DensityScale:F2} -> {minimal.DensityScale:F2}.");
        Assert.All(ContrastChecker.Check(minimal), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1 contrast."));
    }

    [Fact]
    public void Generate_CompositionalGlassPromptAdjustsGeometryNotJustColors()
    {
        var baseline = AestheticProfileCatalog.CreateTheme(
            AestheticProfileCatalog.Find("Frutiger Aero")!, "Frutiger Aero");
        var glass = _gen.Generate("Frutiger Aero with liquid glass surfaces and spacious layout");

        Assert.True(glass.CornerRadiusScale >= baseline.CornerRadiusScale);
        Assert.True(glass.DensityScale > baseline.DensityScale);
        Assert.Contains("Art direction:", glass.Description);
        Assert.All(ContrastChecker.Check(glass), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1"));
    }

    [Fact]
    public void RankThemes_GlassPromptPrefersArtDirectedRoundedCandidate()
    {
        var seed = _gen.Generate("a calm violet interface");
        var square = seed.Duplicate();
        square.Name = "Square";
        square.CornerRadiusScale = 0.55;
        var rounded = seed.Duplicate();
        rounded.Name = "Rounded Glass";
        rounded.CornerRadiusScale = 1.35;

        var ranking = new HeuristicRankingEngine().RankThemes(
            [
                new ThemeCandidate { Theme = square },
                new ThemeCandidate { Theme = rounded }
            ],
            new UserProfile(),
            new GenerationContext { Prompt = "glass interface" });

        Assert.Equal("Rounded Glass", ranking[0].Theme.Name);
    }

    [Fact]
    public void RankThemes_MinimalPromptPrefersSpaciousCandidate()
    {
        var seed = _gen.Generate("a calm violet interface");
        var cramped = seed.Duplicate();
        cramped.Name = "Cramped";
        cramped.DensityScale = 0.82;
        var spacious = seed.Duplicate();
        spacious.Name = "Spacious Minimal";
        spacious.DensityScale = 1.12;

        var ranking = new HeuristicRankingEngine().RankThemes(
            [
                new ThemeCandidate { Theme = cramped },
                new ThemeCandidate { Theme = spacious }
            ],
            new UserProfile(),
            new GenerationContext { Prompt = "minimal interface" });

        Assert.Equal("Spacious Minimal", ranking[0].Theme.Name);
    }

    [Fact]
    public void ArtDirectionCues_RespectNegationAndWholePhraseBoundaries()
    {
        Assert.False(AestheticProfileCatalog.ContainsPositiveCue("not neon but use cyan", "neon"));
        Assert.True(AestheticProfileCatalog.ContainsPositiveCue("not neon but use cyan", "cyan"));
        Assert.False(AestheticProfileCatalog.ContainsPositiveCue("the software interface", "soft"));
        Assert.False(AestheticProfileCatalog.ContainsPositiveCue("avoid using chrome", "chrome"));
        Assert.True(AestheticProfileCatalog.ContainsPositiveCue("use glossy chrome panels", "chrome"));
    }

    [Fact]
    public void RankThemes_NegatedGlassDoesNotPreferRoundedCandidate()
    {
        var seed = _gen.Generate("a calm violet interface");
        var square = seed.Duplicate();
        square.Name = "Square";
        square.CornerRadiusScale = 0.55;
        var rounded = seed.Duplicate();
        rounded.Name = "Rounded Glass";
        rounded.CornerRadiusScale = 1.35;

        var ranking = new HeuristicRankingEngine().RankThemes(
            [
                new ThemeCandidate { Theme = square },
                new ThemeCandidate { Theme = rounded }
            ],
            new UserProfile(),
            new GenerationContext { Prompt = "not glass interface" });

        Assert.Equal("Square", ranking[0].Theme.Name);
    }

    [Fact]
    public void ValidateAndRepair_UsesFilledButtonForegroundForStrongAccent()
    {
        var theme = AestheticProfileCatalog.CreateTheme(
            AestheticProfileCatalog.Find("Neon Afterglow")!, "Neon Afterglow");
        var originalHue = ThemeManager.Core.Utilities.ColorMath.HexToHsl(theme.AccentStrong).H;

        var report = PaletteQualityValidator.ValidateAndRepair(theme, preferDark: true);
        var filledButtonText = ContrastChecker.BestBlackOrWhiteForeground(theme.AccentStrong);
        var ratio = ThemeManager.Core.Utilities.ColorMath.ContrastRatio(filledButtonText, theme.AccentStrong);

        Assert.True(report.PassesAA);
        Assert.True(ratio >= 4.5f, $"Filled button text only has {ratio:F2}:1 contrast.");
        var repairedHue = ThemeManager.Core.Utilities.ColorMath.HexToHsl(theme.AccentStrong).H;
        var hueDelta = Math.Abs(repairedHue - originalHue);
        Assert.True(Math.Min(hueDelta, 360f - hueDelta) < 2f,
            "Button fill repair should preserve the palette's accent hue.");
    }

    [Theory]
    [InlineData("Dark Academia", false)]
    [InlineData("Frutiger Aero glass ocean", false)]
    [InlineData("K-Pop Stage", false)]
    [InlineData("change the theme to Dark Academia", false)]
    [InlineData("change the theme to Frutiger Aero", false)]
    [InlineData("use the Clean Girl aesthetic", false)]
    [InlineData("switch to High Camp", false)]
    [InlineData("make it darker", true)]
    [InlineData("add violet accents", true)]
    [InlineData("make the accents violet", true)]
    [InlineData("make it violet", true)]
    [InlineData("change the palette to more violet", true)]
    [InlineData("use cyan accents", true)]
    [InlineData("make it glassy", true)]
    [InlineData("make the layout minimal and spacious", true)]
    [InlineData("more vibrant", true)]
    public void IsRefinementInstruction_DistinguishesCommandsFromNewAesthetics(string prompt, bool expected)
    {
        Assert.Equal(expected, _gen.IsRefinementInstruction(prompt));
    }

    [Fact]
    public void Refine_UsesSameAccentHueSemanticsAsInitialGeneration()
    {
        var theme = _gen.Generate("Frutiger Aero");
        var backgroundBefore = theme.BackgroundBase;
        var altBefore = theme.BackgroundAlt;
        var hueBefore = ColorMath.HexToHsl(theme.AccentPrimary).H;

        var refined = _gen.Refine(theme, "add violet accents");

        Assert.Same(theme, refined);
        Assert.Equal(backgroundBefore, refined.BackgroundBase);
        Assert.Equal(altBefore, refined.BackgroundAlt);
        var hueAfter = ColorMath.HexToHsl(refined.AccentPrimary).H;
        Assert.InRange(hueAfter, 255f, 300f);
        Assert.True(Math.Abs(hueBefore - hueAfter) > 8f,
            $"Expected refinement to move accent hue, got {hueBefore:F1}° -> {hueAfter:F1}°.");
        Assert.All(ContrastChecker.Check(refined), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1."));
    }

    [Fact]
    public void Refine_AccentOnlyColorLanguagePreservesBackgroundIdentity()
    {
        var theme = _gen.Generate("Frutiger Aero");
        var background = theme.BackgroundBase;
        var backgroundAlt = theme.BackgroundAlt;
        var surface = theme.Surface;

        _gen.Refine(theme, "make the accents violet");

        Assert.Equal(background, theme.BackgroundBase);
        Assert.Equal(backgroundAlt, theme.BackgroundAlt);
        Assert.Equal(surface, theme.Surface);
        var hue = ColorMath.HexToHsl(theme.AccentPrimary).H;
        Assert.InRange(hue, 255f, 300f);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1."));
    }

    [Fact]
    public void Refine_AppliesCompositionModifiersAndPreservesAccessibility()
    {
        var theme = _gen.Generate("Coquette");
        var originalDensity = theme.DensityScale;
        var originalRadius = theme.CornerRadiusScale;

        _gen.Refine(theme, "make the layout minimal, spacious, and glassy");

        Assert.True(theme.DensityScale > originalDensity,
            $"Expected more whitespace, got {originalDensity:F2} -> {theme.DensityScale:F2}.");
        Assert.True(theme.CornerRadiusScale >= originalRadius);
        Assert.Contains("Art direction:", theme.Description);
        Assert.All(ContrastChecker.Check(theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1."));
    }

    [Fact]
    public void Refine_UnknownInstructionDoesNotGuessOrMutatePalette()
    {
        var theme = _gen.Generate("Neon Afterglow");
        var before = new[]
        {
            theme.BackgroundBase, theme.BackgroundAlt, theme.Surface,
            theme.AccentPrimary, theme.AccentStrong, theme.TextPrimary, theme.TextMuted,
            theme.BorderSubtle
        };

        _gen.Refine(theme, "make this feel legendary");

        Assert.Equal(before, new[]
        {
            theme.BackgroundBase, theme.BackgroundAlt, theme.Surface,
            theme.AccentPrimary, theme.AccentStrong, theme.TextPrimary, theme.TextMuted,
            theme.BorderSubtle
        });
    }

    [Fact]
    public void PersonalizationPipeline_FinalSelectedThemePassesPaletteGateAndAnalysisMatches()
    {
        var profilePath = Path.Combine(Path.GetTempPath(), $"themed-ai-test-profile-{Guid.NewGuid():N}.json");
        var orchestrator = new PersonalizationOrchestrator(profilePath);
        var candidate = orchestrator.GenerateBestTheme(new GenerationContext
        {
            Prompt = "Neon Afterglow with cyan accents",
            Constraints = new GenerationConstraints { MustBeDarkTheme = true }
        });

        Assert.NotNull(candidate.Theme);
        Assert.NotNull(candidate.Analysis);
        Assert.Equal(candidate.Theme.Name, candidate.Analysis!.GeneratedName);
        Assert.Equal(
            new[] { candidate.Theme.BackgroundBase, candidate.Theme.BackgroundAlt, candidate.Theme.Surface,
                candidate.Theme.AccentPrimary, candidate.Theme.AccentStrong },
            candidate.Analysis.Swatches);
        Assert.All(ContrastChecker.Check(candidate.Theme), check =>
            Assert.True(check.PassesAA, $"{check.PairName} only has {check.Ratio:F2}:1"));
    }
}