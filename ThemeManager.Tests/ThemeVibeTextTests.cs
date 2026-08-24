using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="ThemeVibeText"/> — the Name+Description → free-text phrase mapping
/// that <c>VibeFinderMeasure</c>'s "$theme" target relies on (see phases.md, Phase 6).
/// </summary>
public class ThemeVibeTextTests
{
    private static CozyTheme MakeTheme(string name, string description) =>
        new() { Name = name, Description = description };

    [Fact]
    public void Describe_WithDescription_CombinesNameAndDescription()
    {
        var theme = MakeTheme("Mystic Forest", "Cool and contemplative, evoking forest, mystic, night.");
        Assert.Equal("Mystic Forest. Cool and contemplative, evoking forest, mystic, night.", ThemeVibeText.Describe(theme));
    }

    [Fact]
    public void Describe_EmptyDescription_FallsBackToNameOnly()
    {
        var theme = MakeTheme("New Theme", "");
        Assert.Equal("New Theme", ThemeVibeText.Describe(theme));
    }

    [Fact]
    public void Describe_WhitespaceOnlyDescription_FallsBackToNameOnly()
    {
        // Guards the IsNullOrWhiteSpace check specifically — a naive IsNullOrEmpty check would
        // let a description of "   " through and produce "Name.    " instead of just "Name".
        var theme = MakeTheme("New Theme", "   ");
        Assert.Equal("New Theme", ThemeVibeText.Describe(theme));
    }
}
