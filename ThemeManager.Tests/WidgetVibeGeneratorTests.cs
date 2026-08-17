using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for the Prompt-to-Widget NLP pipeline — reuses VibeTokenizer/PorterStemmer, adds its
/// own WidgetLexicon and WidgetFuzzyMatcher. Sibling to VibeNlpPipelineTests, same spirit:
/// exercise real prompts (including typos and nonsense) and check the generated SkinDefinition
/// is sane, not just that nothing throws.
/// </summary>
public class WidgetVibeGeneratorTests
{
    private readonly WidgetVibeGenerator _gen = new();

    // ── Basic generation ─────────────────────────────────────────────────────

    [Fact]
    public void Generate_EmptyInput_FallsBackToClock()
    {
        var skin = _gen.Generate("");
        Assert.NotNull(skin);
        Assert.NotEmpty(skin.Id);
        Assert.Contains(skin.Measures, m => m.Type == MeasureType.Time);
    }

    [Fact]
    public void Generate_WhitespaceInput_DoesNotCrash()
    {
        var skin = _gen.Generate("   ");
        Assert.NotNull(skin);
    }

    [Fact]
    public void Generate_NonsenseInput_FallsBackGracefully()
    {
        var (skin, analysis) = _gen.GenerateAndExplain("asdkjaslkdj qwzxpqz");
        Assert.True(analysis.UsedFallback);
        Assert.Equal(2, skin.Measures.Count);
        Assert.Equal(MeasureType.Time, skin.Measures[0].Type);
        Assert.Equal(MeasureType.Date, skin.Measures[1].Type);
    }

    // ── Measure keyword detection ─────────────────────────────────────────────

    [Theory]
    [InlineData("show me my cpu", MeasureType.Cpu)]
    [InlineData("ram usage", MeasureType.Memory)]
    [InlineData("how much memory am I using", MeasureType.Memory)]
    [InlineData("disk space", MeasureType.DiskFree)]
    [InlineData("storage widget", MeasureType.DiskFree)]
    [InlineData("battery level", MeasureType.Battery)]
    [InlineData("download speed", MeasureType.NetworkDown)]
    [InlineData("upload speed", MeasureType.NetworkUp)]
    [InlineData("a clock", MeasureType.Time)]
    [InlineData("today's date", MeasureType.Date)]
    [InlineData("system uptime", MeasureType.Uptime)]
    public void Generate_DetectsExpectedMeasure(string prompt, MeasureType expected)
    {
        var skin = _gen.Generate(prompt);
        Assert.Contains(skin.Measures, m => m.Type == expected);
    }

    [Fact]
    public void Generate_MultipleMeasures_AllDetected()
    {
        var skin = _gen.Generate("cpu and memory and disk monitor");
        var types = skin.Measures.Select(m => m.Type).ToList();
        Assert.Contains(MeasureType.Cpu, types);
        Assert.Contains(MeasureType.Memory, types);
        Assert.Contains(MeasureType.DiskFree, types);
    }

    [Fact]
    public void Generate_DuplicateMeasureWords_OnlyOneMeasureCreated()
    {
        var skin = _gen.Generate("cpu cpu cpu processor");
        Assert.Single(skin.Measures);
        Assert.Equal(MeasureType.Cpu, skin.Measures[0].Type);
    }

    // ── Fuzzy typo tolerance ───────────────────────────────────────────────────

    [Theory]
    [InlineData("cpu memry widget")]   // "memry" -> "memori" (memory)
    [InlineData("battary and network")] // "battary" -> "batteri" (battery)
    public void Generate_TypoedKeywords_StillDetected(string prompt)
    {
        var (_, analysis) = _gen.GenerateAndExplain(prompt);
        Assert.NotEmpty(analysis.FuzzyCorrections);
        Assert.False(analysis.UsedFallback);
    }

    // ── Size / style hints ─────────────────────────────────────────────────────

    [Fact]
    public void Generate_MinimalKeyword_ProducesSmallerScale()
    {
        var (_, analysis) = _gen.GenerateAndExplain("minimal clock");
        Assert.True(analysis.SizeScale < 1.0);
    }

    [Fact]
    public void Generate_BigKeyword_ProducesLargerScale()
    {
        var (_, analysis) = _gen.GenerateAndExplain("huge cpu monitor");
        Assert.True(analysis.SizeScale > 1.0);
    }

    [Fact]
    public void Generate_SizeScale_AlwaysWithinSaneBounds()
    {
        var (_, analysis) = _gen.GenerateAndExplain("tiny minimal compact small huge massive big bold");
        Assert.InRange(analysis.SizeScale, 0.5, 2.0);
    }

    // ── Meter kind preference ─────────────────────────────────────────────────

    [Fact]
    public void Generate_GraphKeyword_PrefersGraphMeter()
    {
        var skin = _gen.Generate("a cpu graph");
        Assert.Contains(skin.Meters, m => m.Kind == MeterKind.Graph);
    }

    [Fact]
    public void Generate_BarKeyword_PrefersBarMeter()
    {
        var skin = _gen.Generate("a cpu bar");
        Assert.Contains(skin.Meters, m => m.Kind == MeterKind.Bar);
    }

    [Fact]
    public void Generate_NoKindKeyword_DefaultsToBarForNumericMeasures()
    {
        var skin = _gen.Generate("cpu widget");
        Assert.Contains(skin.Meters, m => m.Kind == MeterKind.Bar);
    }

    [Fact]
    public void Generate_TimeMeasure_DefaultsToTextOnlyWithoutExplicitKindRequest()
    {
        // Without an explicit "bar"/"graph" word, a clock has no reason to be anything but text.
        var skin = _gen.Generate("a clock");
        Assert.All(skin.Meters, m => Assert.Equal(MeterKind.String, m.Kind));
    }

    [Fact]
    public void Generate_TimeMeasure_HonorsExplicitGraphRequestEvenThoughUnusual()
    {
        // If someone explicitly asks for a graph of the clock, that's an unusual choice but not
        // an invalid one — the generator respects an explicit ask rather than second-guessing
        // it, same as it would for any other measure. (They can always change it in the editor.)
        var skin = _gen.Generate("a graph of the clock");
        Assert.Contains(skin.Meters, m => m.Kind == MeterKind.Graph);
    }

    // ── Position hints ─────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TopRight_PositionsAccordingly()
    {
        var skin = _gen.Generate("cpu monitor in the top right");
        Assert.True(skin.X > 500); // pushed toward the right edge, not the default left margin
        Assert.True(skin.Y < 100); // stayed near the top
    }

    [Fact]
    public void Generate_BottomLeft_PositionsAccordingly()
    {
        var skin = _gen.Generate("cpu monitor in the bottom left");
        Assert.True(skin.X < 100);
        Assert.True(skin.Y > 500);
    }

    [Fact]
    public void Generate_NoPositionHint_DefaultsNearTopLeft()
    {
        var skin = _gen.Generate("cpu monitor");
        Assert.True(skin.X < 100);
        Assert.True(skin.Y < 100);
    }

    // ── Structural sanity across every generated widget ─────────────────────────

    [Theory]
    [InlineData("cpu")]
    [InlineData("a big memory graph")]
    [InlineData("minimal battery indicator")]
    [InlineData("network and disk and cpu and battery")]
    public void Generate_AlwaysProducesValidSkin(string prompt)
    {
        var skin = _gen.Generate(prompt);

        Assert.NotEmpty(skin.Id);
        Assert.NotEmpty(skin.Name);
        Assert.False(skin.Enabled); // generated widgets start disabled until confirmed in the editor
        Assert.True(skin.Width > 0);
        Assert.True(skin.Height > 0);
        Assert.NotEmpty(skin.Measures);
        Assert.NotEmpty(skin.Meters);

        // Every meter must reference a measure that actually exists in this same skin —
        // no meter pointing at a measure name we never created.
        var measureNames = skin.Measures.Select(m => m.Name).ToHashSet();
        foreach (var meter in skin.Meters.Where(m => !string.IsNullOrEmpty(m.MeasureName)))
            Assert.Contains(meter.MeasureName!, measureNames);
    }

    [Fact]
    public void Generate_SameInput_IsDeterministic()
    {
        var skinA = _gen.Generate("cpu and memory graph in the top right");
        var skinB = _gen.Generate("cpu and memory graph in the top right");

        Assert.Equal(skinA.Name, skinB.Name);
        Assert.Equal(skinA.Measures.Count, skinB.Measures.Count);
        Assert.Equal(skinA.Meters.Count, skinB.Meters.Count);
        Assert.Equal(skinA.X, skinB.X);
        Assert.Equal(skinA.Y, skinB.Y);
    }
}
