using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Personalization;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

/// <summary>
/// Cross-pipeline contracts for recognizable art direction. These tests deliberately inspect the
/// returned palettes directly; they must not call ValidateAndRepair before checking generation.
/// </summary>
public class ThemeGenerationQualityContractTests
{
    private readonly VibeThemeGenerator _generator = new();

    [Theory]
    [InlineData("Neon Afterglow", "primary", 300f, 330f)]
    [InlineData("Dark Academia", "primary", 25f, 50f)]
    [InlineData("Frutiger Aero", "primary", 185f, 225f)]
    [InlineData("Cottagecore", "primary", 95f, 150f)]
    [InlineData("Y3K", "primary", 65f, 95f)]
    [InlineData("Desi-Core", "strong", 325f, 355f)]
    public void Generate_CuratedReferencesRetainTheirSignatureHue(
        string prompt, string accentRole, float minimumHue, float maximumHue)
    {
        var (theme, analysis) = _generator.GenerateAndExplain(prompt);
        var color = accentRole == "strong" ? theme.AccentStrong : theme.AccentPrimary;
        var hue = ColorMath.HexToHsl(color).H;

        Assert.Equal(prompt, theme.Name);
        Assert.Equal(theme.Name, analysis.GeneratedName);
        Assert.InRange(hue, minimumHue, maximumHue);

        var checks = ContrastChecker.Check(theme);
        Assert.Equal(9, checks.Count);
        Assert.All(checks, check =>
            Assert.True(check.PassesAA,
                $"{prompt}: {check.PairName} is only {check.Ratio:F2}:1."));
    }

    [Fact]
    public void Generate_RecognizedAliasProducesTheSameCuratedArtDirection()
    {
        var canonical = _generator.Generate("Jade palette");
        var descriptive = _generator.Generate("jade green aesthetic");

        Assert.Equal("Jade Reverie", canonical.Name);
        Assert.Equal(canonical.Name, descriptive.Name);
        Assert.Equal(
            new[]
            {
                canonical.BackgroundBase, canonical.BackgroundAlt, canonical.Surface,
                canonical.AccentPrimary, canonical.AccentStrong, canonical.TextPrimary,
                canonical.TextMuted, canonical.BorderSubtle
            },
            new[]
            {
                descriptive.BackgroundBase, descriptive.BackgroundAlt, descriptive.Surface,
                descriptive.AccentPrimary, descriptive.AccentStrong, descriptive.TextPrimary,
                descriptive.TextMuted, descriptive.BorderSubtle
            });
        Assert.All(ContrastChecker.Check(canonical), check => Assert.True(check.PassesAA));
        Assert.All(ContrastChecker.Check(descriptive), check => Assert.True(check.PassesAA));
    }

    [Fact]
    public void CandidatePipeline_ThreeWayCultureBlendRetainsArtDirectionAndAccurateAnalysis()
    {
        const string prompt =
            "Y3K + Indie Sleaze + Desi-Core with brutalist hard edges and spacious layout";
        var profiles = AestheticProfileCatalog.FindAll(prompt);

        Assert.Equal(
            new[] { "Y3K", "Indie Sleaze", "Desi-Core" },
            profiles.Take(3).Select(profile => profile.Name));

        var candidates = new CandidateEngine().GenerateThemeCandidates(
            new GenerationContext { Prompt = prompt, VariationSeed = 1 }, count: 3);

        Assert.Equal(3, candidates.Count);
        foreach (var candidate in candidates)
        {
            var theme = candidate.Theme;
            var analysis = Assert.IsType<VibeAnalysisResult>(candidate.Analysis);

            Assert.StartsWith("Y3K × Indie Sleaze × Desi-Core", theme.Name);
            Assert.Contains("hard-edged geometry", theme.Description);
            Assert.Contains("generous negative space", theme.Description);
            Assert.Equal(0.55, theme.CornerRadiusScale);
            Assert.True(theme.DensityScale > 1.0);
            Assert.Equal(theme.Name, analysis.GeneratedName);
            Assert.Contains("Y3K", analysis.MatchedKeywords);
            Assert.Contains("Indie Sleaze", analysis.MatchedKeywords);
            Assert.Contains("Desi-Core", analysis.MatchedKeywords);
            Assert.Equal(
                new[]
                {
                    theme.BackgroundBase, theme.BackgroundAlt, theme.Surface,
                    theme.AccentPrimary, theme.AccentStrong
                },
                analysis.Swatches);

            var (accentHue, accentSaturation, _) = ColorMath.HexToHsl(theme.AccentPrimary);
            var (_, _, backgroundLightness) = ColorMath.HexToHsl(theme.BackgroundBase);
            Assert.Equal(accentHue, analysis.ComputedHue);
            Assert.Equal(accentSaturation, analysis.ComputedSaturation);
            Assert.Equal(backgroundLightness, analysis.ComputedLightness);
            Assert.Equal(ColorMath.LuminanceFromHex(theme.BackgroundBase) < 0.18f, analysis.IsDark);

            var checks = ContrastChecker.Check(theme);
            Assert.Equal(9, checks.Count);
            Assert.All(checks, check =>
                Assert.True(check.PassesAA,
                    $"{candidate.GenerationSource}: {check.PairName} is only {check.Ratio:F2}:1."));

            var composition = PaletteCompositionScorer.Evaluate(theme, prompt);
            Assert.InRange(composition.Score, 0f, 100f);
        }
    }

    [Fact]
    public void PersonalizationBoundary_ReturnsReadableThemeWithAnalysisForTheSelectedVariant()
    {
        const string prompt =
            "Y3K meets Indie Sleaze meets Desi-Core with spacious layout and violet accents";
        var profilePath = Path.Combine(
            Path.GetTempPath(), $"themed-ai-contract-{Guid.NewGuid():N}.json");

        try
        {
            var orchestrator = new PersonalizationOrchestrator(profilePath);
            var selected = orchestrator.GenerateBestTheme(new GenerationContext
            {
                Prompt = prompt,
                VariationSeed = 2
            });

            Assert.NotNull(selected.Theme);
            var analysis = Assert.IsType<VibeAnalysisResult>(selected.Analysis);
            Assert.StartsWith("Y3K × Indie Sleaze × Desi-Core", selected.Theme.Name);
            Assert.Contains("Y3K", analysis.MatchedKeywords);
            Assert.Contains("Indie Sleaze", analysis.MatchedKeywords);
            Assert.Contains("Desi-Core", analysis.MatchedKeywords);
            Assert.Equal(selected.Theme.Name, analysis.GeneratedName);
            Assert.Equal(
                new[]
                {
                    selected.Theme.BackgroundBase, selected.Theme.BackgroundAlt, selected.Theme.Surface,
                    selected.Theme.AccentPrimary, selected.Theme.AccentStrong
                },
                analysis.Swatches);

            var (hue, saturation, _) = ColorMath.HexToHsl(selected.Theme.AccentPrimary);
            var (_, _, lightness) = ColorMath.HexToHsl(selected.Theme.BackgroundBase);
            Assert.Equal(hue, analysis.ComputedHue);
            Assert.Equal(saturation, analysis.ComputedSaturation);
            Assert.Equal(lightness, analysis.ComputedLightness);

            var checks = ContrastChecker.Check(selected.Theme);
            Assert.Equal(9, checks.Count);
            Assert.All(checks, check =>
                Assert.True(check.PassesAA,
                    $"{check.PairName} is only {check.Ratio:F2}:1."));
        }
        finally
        {
            if (File.Exists(profilePath))
                File.Delete(profilePath);
        }
    }
}
