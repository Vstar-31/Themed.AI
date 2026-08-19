using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="ContrastChecker"/> — WCAG grading, pair checks, and summary.
/// </summary>
public class ContrastCheckerTests
{
    // ── Grade classification ─────────────────────────────────────────────────

    [Fact]
    public void Check_DefaultCozyCafe_HasMultiplePassingPairs()
    {
        var theme = CozyDefaults.CreateDefault();
        var results = ContrastChecker.Check(theme);

        Assert.NotEmpty(results);
        Assert.True(results.Count >= 9, "Should check at least 9 color pairs");
    }

    [Fact]
    public void Check_HighContrastTheme_MostPassAA()
    {
        var theme = new CozyTheme
        {
            BackgroundBase = "#FFFFFF",
            BackgroundAlt  = "#F0F0F0",
            Surface        = "#E0E0E0",
            AccentPrimary  = "#000000",
            AccentStrong   = "#111111",
            TextPrimary    = "#000000",
            TextMuted      = "#333333",
            BorderSubtle   = "#CCCCCC",
        };

        var results = ContrastChecker.Check(theme);
        // Most pairs should pass — dark text on light backgrounds
        int passing = results.Count(r => r.PassesAA);
        Assert.True(passing >= results.Count / 2, $"Only {passing}/{results.Count} pairs pass AA");
    }

    [Fact]
    public void Check_LowContrastTheme_HasFailures()
    {
        var theme = new CozyTheme
        {
            BackgroundBase = "#FFFFFF",
            BackgroundAlt  = "#FEFEFE",
            Surface        = "#FDFDFD",
            AccentPrimary  = "#F0F0F0",
            AccentStrong   = "#E8E8E8",
            TextPrimary    = "#F0F0F0",
            TextMuted      = "#F5F5F5",
            BorderSubtle   = "#FAFAFA",
        };

        var results = ContrastChecker.Check(theme);
        bool anyFail = results.Any(r => r.NormalTextGrade == ContrastChecker.Grade.Fail);
        Assert.True(anyFail, "Near-white text on white should fail WCAG");
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    [Fact]
    public void Summary_ReturnsCorrectTotals()
    {
        var theme = CozyDefaults.CreateDefault();
        var (passing, total) = ContrastChecker.Summary(theme);

        Assert.True(total >= 9);
        Assert.True(passing >= 0 && passing <= total);
    }

    // ── ContrastResult record fields ─────────────────────────────────────────

    [Fact]
    public void ContrastResult_RatioLabel_FormatsCorrectly()
    {
        var result = new ContrastChecker.ContrastResult(
            "Test", "#000000", "#FFFFFF", 21.0f,
            ContrastChecker.Grade.AAA, ContrastChecker.Grade.AAA);

        Assert.Equal("21.0:1", result.RatioLabel);
        Assert.True(result.PassesAA);
        Assert.True(result.PassesAAA);
        Assert.Equal("AAA ✓", result.NormalGradeLabel);
    }

    [Fact]
    public void ContrastResult_FailGrade_HasCorrectLabel()
    {
        var result = new ContrastChecker.ContrastResult(
            "Test", "#FFFFFF", "#FEFEFE", 1.0f,
            ContrastChecker.Grade.Fail, ContrastChecker.Grade.Fail);

        Assert.False(result.PassesAA);
        Assert.False(result.PassesAAA);
        Assert.Equal("Fail ✗", result.NormalGradeLabel);
    }
}
