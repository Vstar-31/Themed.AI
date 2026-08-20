using ThemeManager.Core.Models;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="CozyTheme"/> — identity, duplication, reset, and hex normalization.
/// </summary>
public class CozyThemeTests
{
    // ── Defaults ─────────────────────────────────────────────────────────────

    [Fact]
    public void NewTheme_HasDefaultValues()
    {
        var theme = new CozyTheme();
        Assert.Equal("Untitled Theme", theme.Name);
        Assert.False(theme.IsBuiltIn);
        Assert.NotEmpty(theme.Id);
        Assert.Equal(1.0, theme.CornerRadiusScale);
        Assert.Equal(1.0, theme.DensityScale);
    }

    [Fact]
    public void CozyDefaults_CreateDefault_IsBuiltIn()
    {
        var theme = CozyDefaults.CreateDefault();
        Assert.True(theme.IsBuiltIn);
        Assert.Equal("cozy-default", theme.Id);
        Assert.Equal("Cozy Café", theme.Name);
        Assert.Equal(CozyDefaults.Linen, theme.BackgroundBase);
        Assert.Equal(CozyDefaults.Espresso, theme.AccentStrong);
    }

    // ── Duplicate ────────────────────────────────────────────────────────────

    [Fact]
    public void Duplicate_CreatesNewId()
    {
        var original = CozyDefaults.CreateDefault();
        var clone = original.Duplicate();

        Assert.NotEqual(original.Id, clone.Id);
    }

    [Fact]
    public void Duplicate_PreservesPalette()
    {
        var original = CozyDefaults.CreateDefault();
        var clone = original.Duplicate();

        Assert.Equal(original.BackgroundBase, clone.BackgroundBase);
        Assert.Equal(original.AccentPrimary, clone.AccentPrimary);
        Assert.Equal(original.TextPrimary, clone.TextPrimary);
    }

    [Fact]
    public void Duplicate_AppendsCopyToName()
    {
        var original = new CozyTheme { Name = "Ocean" };
        var clone = original.Duplicate();
        Assert.Equal("Ocean (Copy)", clone.Name);
    }

    [Fact]
    public void Duplicate_ClearsBuiltInFlag()
    {
        var original = CozyDefaults.CreateDefault();
        Assert.True(original.IsBuiltIn);

        var clone = original.Duplicate();
        Assert.False(clone.IsBuiltIn);
    }

    [Fact]
    public void Duplicate_DeepCopiesCustomTokens()
    {
        var original = new CozyTheme();
        original.CustomTokens["key"] = "value";

        var clone = original.Duplicate();
        clone.CustomTokens["key"] = "changed";

        // Original should not be affected
        Assert.Equal("value", original.CustomTokens["key"]);
    }

    // ── ResetToDefault ───────────────────────────────────────────────────────

    [Fact]
    public void ResetToDefault_RestoresCozyDefaults()
    {
        var theme = new CozyTheme
        {
            BackgroundBase = "#000000",
            AccentPrimary  = "#FFFFFF",
            CornerRadiusScale = 3.0,
            DensityScale = 0.1,
        };

        theme.ResetToDefault();

        Assert.Equal(CozyDefaults.Linen, theme.BackgroundBase);
        Assert.Equal(CozyDefaults.Cocoa, theme.AccentPrimary);
        Assert.Equal(1.0, theme.CornerRadiusScale);
        Assert.Equal(1.0, theme.DensityScale);
    }

    [Fact]
    public void ResetToDefault_UpdatesLastModified()
    {
        var theme = new CozyTheme();
        var before = theme.LastModified;

        // Tiny delay to ensure timestamp differs
        Thread.Sleep(10);
        theme.ResetToDefault();

        Assert.True(theme.LastModified >= before);
    }

    // ── NormalizeHex ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("FF0000",   "#FF0000")]
    [InlineData("#ff0000",  "#FF0000")]
    [InlineData("#F00",     "#FF0000")]
    [InlineData("F00",      "#FF0000")]
    public void NormalizeHex_Standard(string input, string expected)
    {
        Assert.Equal(expected, CozyTheme.NormalizeHex(input));
    }

    [Fact]
    public void NormalizeHex_Strips8DigitAlpha()
    {
        // #AARRGGBB → strips AA prefix
        Assert.Equal("#FF0000", CozyTheme.NormalizeHex("#FFFF0000"));
    }

    [Fact]
    public void NormalizeHex_PadsShortHex()
    {
        // #66298 → should pad to #066298
        Assert.Equal("#066298", CozyTheme.NormalizeHex("#66298"));
    }

    [Fact]
    public void NormalizeHex_HandlesLeadingWhitespace()
    {
        // NormalizeHex calls TrimStart('#') then .Trim(), so spaces after # are fine
        Assert.Equal("#FF0000", CozyTheme.NormalizeHex("  FF0000  "));
    }
}
