using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

public class PaletteQualityValidatorTests
{
    [Fact]
    public void ValidateAndRepair_NeonAfterglowPalette_PassesAllTextContrastPairs()
    {
        var theme = new CozyTheme
        {
            Name = "Neon Afterglow",
            BackgroundBase = "#0B0616",
            BackgroundAlt = "#160D27",
            Surface = "#281343",
            AccentPrimary = "#43265D", // Legacy low-contrast accent
            AccentStrong = "#5B3C71",  // Legacy low-contrast strong accent
            TextPrimary = "#F7EFFF",
            TextMuted = "#B7A6C9",
            BorderSubtle = "#43265D"
        };

        var report = PaletteQualityValidator.ValidateAndRepair(theme, preferDark: true);

        Assert.NotEmpty(report.Repairs);
        Assert.True(report.PassesAA, $"Only {report.PassingPairs}/{report.TotalPairs} palette pairs pass AA.");
        Assert.All(ContrastChecker.Check(theme), pair =>
            Assert.True(pair.PassesAA, $"{pair.PairName} only has {pair.Ratio:F2}:1 contrast."));
        Assert.True(ColorMath.LuminanceFromHex(theme.BackgroundBase) <= 0.17f);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateAndRepair_RespectsExplicitLightDarkIntent(bool preferDark)
    {
        var theme = new CozyTheme
        {
            BackgroundBase = "#777777",
            BackgroundAlt = "#818181",
            Surface = "#686868",
            AccentPrimary = "#777777",
            AccentStrong = "#818181",
            TextPrimary = "#777777",
            TextMuted = "#818181",
            BorderSubtle = "#777777"
        };

        var report = PaletteQualityValidator.ValidateAndRepair(theme, preferDark);

        Assert.True(report.PassesAA);
        var luminance = ColorMath.LuminanceFromHex(theme.BackgroundBase);
        if (preferDark)
            Assert.True(luminance <= 0.17f, $"Expected a dark base, got luminance {luminance:F3}.");
        else
            Assert.True(luminance >= 0.42f, $"Expected a light base, got luminance {luminance:F3}.");
    }

    [Fact]
    public void ValidateAndRepair_IsIdempotentAfterFirstRepair()
    {
        var theme = new CozyTheme
        {
            BackgroundBase = "#555555",
            BackgroundAlt = "#777777",
            Surface = "#666666",
            AccentPrimary = "#888888",
            AccentStrong = "#999999",
            TextPrimary = "#777777",
            TextMuted = "#888888",
            BorderSubtle = "#777777"
        };

        PaletteQualityValidator.ValidateAndRepair(theme, preferDark: true);
        var secondReport = PaletteQualityValidator.ValidateAndRepair(theme, preferDark: true);

        Assert.Empty(secondReport.Repairs);
        Assert.True(secondReport.PassesAA);
    }
}
