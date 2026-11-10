using ThemeManager.Core.NLP;
using ThemeManager.Core.Personalization;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

public class PaletteCompositionScorerTests
{
    [Fact]
    public void Evaluate_AllCuratedProfiles_ReturnsBoundedCompositionScore()
    {
        foreach (var profile in AestheticProfileCatalog.Profiles)
        {
            var theme = AestheticProfileCatalog.CreateTheme(profile, profile.Name);
            var quality = PaletteQualityValidator.ValidateAndRepair(theme);
            Assert.True(quality.PassesAA,
                $"{profile.Name}: {quality.PassingPairs}/{quality.TotalPairs} contrast pairs passed.");

            var composition = PaletteCompositionScorer.Evaluate(theme, profile.Name);
            Assert.InRange(composition.Score, 0f, 100f);
            Assert.NotEmpty(composition.Signals);
        }
    }

    [Fact]
    public void Evaluate_HighEnergyPromptRewardsDistinctSaturatedAccentRoles()
    {
        var profile = AestheticProfileCatalog.Find("Neon Afterglow")!;
        var vivid = AestheticProfileCatalog.CreateTheme(profile, "Neon Afterglow");
        PaletteQualityValidator.ValidateAndRepair(vivid, preferDark: true);

        var neutralized = vivid.Duplicate();
        neutralized.AccentPrimary = "#777777";
        neutralized.AccentStrong = "#555555";
        PaletteQualityValidator.ValidateAndRepair(neutralized, preferDark: true);

        var vividScore = PaletteCompositionScorer.Evaluate(vivid, "neon maximalist dopamine").Score;
        var neutralScore = PaletteCompositionScorer.Evaluate(neutralized, "neon maximalist dopamine").Score;

        Assert.True(vividScore > neutralScore + 5f,
            $"Expected the expressive palette to rank higher, got {vividScore:F1} vs {neutralScore:F1}.");
    }

    [Fact]
    public void Evaluate_RanksGlassAndMinimalCompositionAgainstTheRequestedArtDirection()
    {
        var profile = AestheticProfileCatalog.Find("Frutiger Aero")!;
        var matching = AestheticProfileCatalog.CreateTheme(
            profile, "Frutiger Aero liquid glass with a spacious layout");
        PaletteQualityValidator.ValidateAndRepair(matching);

        var mismatched = matching.Duplicate();
        mismatched.CornerRadiusScale = 0.55;
        mismatched.DensityScale = 0.80;

        var matchingScore = PaletteCompositionScorer.Evaluate(
            matching, "Frutiger Aero liquid glass with a spacious layout").Score;
        var mismatchedScore = PaletteCompositionScorer.Evaluate(
            mismatched, "Frutiger Aero liquid glass with a spacious layout").Score;

        Assert.True(matchingScore > mismatchedScore + 8f,
            $"Expected prompt-aligned geometry to score higher, got {matchingScore:F1} vs {mismatchedScore:F1}.");
    }

    [Fact]
    public void RankThemes_VariationSeedRotatesPreferredGenerationTreatment()
    {
        var seedTheme = AestheticProfileCatalog.CreateTheme(
            AestheticProfileCatalog.Find("Neon Afterglow")!, "Neon Afterglow");
        var candidates = new List<ThemeCandidate>
        {
            new() { Theme = seedTheme.Duplicate(), GenerationSource = "ArtDirectedBase" },
            new() { Theme = seedTheme.Duplicate(), GenerationSource = "SoftFocusVariant" },
            new() { Theme = seedTheme.Duplicate(), GenerationSource = "ElectricCutVariant" }
        };
        var ranking = new HeuristicRankingEngine();

        var expected = new[] { "ArtDirectedBase", "SoftFocusVariant", "ElectricCutVariant" };
        for (int seed = 0; seed < expected.Length; seed++)
        {
            var selected = ranking.RankThemes(candidates, new UserProfile(), new GenerationContext
            {
                Prompt = "Neon Afterglow",
                VariationSeed = seed
            }).First();

            Assert.Equal(expected[seed], selected.GenerationSource);
        }
    }

    [Fact]
    public void Evaluate_MonochromeIntentDoesNotDemandMulticolorAccents()
    {
        var profile = AestheticProfileCatalog.Find("Frutiger Aero")!;
        var theme = AestheticProfileCatalog.CreateTheme(profile, "Frutiger Aero monochrome blue");
        var report = PaletteCompositionScorer.Evaluate(theme, "monochrome blue");

        Assert.Contains(report.Signals, signal => signal == "tonal palette intent preserved" ||
            signal == "accent roles are visually distinct");
        Assert.InRange(report.Score, 0f, 100f);
    }
}
