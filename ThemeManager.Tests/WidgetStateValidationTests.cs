using System.IO.Compression;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetStateValidationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ThemedAI-states", Guid.NewGuid().ToString("N"));

    public WidgetStateValidationTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static SkinDefinition SkinWith(Action<MeterDefinition>? configureMeter = null, Action<SkinDefinition>? configureSkin = null)
    {
        var skin = new SkinDefinition { Id = "w", Name = "W" };
        skin.Measures.Add(new MeasureDefinition { Id = "m", Name = "Cpu", Type = MeasureType.Cpu });
        var meter = new MeterDefinition { Id = "meter", MeasureName = "Cpu" };
        configureMeter?.Invoke(meter);
        skin.Meters.Add(meter);
        configureSkin?.Invoke(skin);
        return skin;
    }

    private static List<WidgetValidationIssue> Errors(SkinDefinition skin) =>
        WidgetDefinitionValidator.Validate(skin).Where(i => i.IsError).ToList();

    private static MeterStateDefinition Hover(Action<MeterStateDefinition>? configure = null)
    {
        var state = new MeterStateDefinition { Id = Guid.NewGuid().ToString(), Trigger = MeterStateTrigger.Hover, Scale = 1.1 };
        configure?.Invoke(state);
        return state;
    }

    [Fact]
    public void Valid_StatesAndAnchor_ProduceNoErrors()
    {
        var skin = SkinWith(m =>
        {
            m.States.Add(Hover());
            m.States.Add(new MeterStateDefinition
            {
                Id = "hot", Trigger = MeterStateTrigger.Threshold, MeasureName = "Cpu",
                Comparison = StateComparison.Above, Value = 90, ColorHex = "#FF4444", Opacity = 0.5,
            });
        }, s => s.Anchor = new WidgetAnchorDefinition
        {
            Edge = WidgetAnchorEdge.TopRight,
            Responsive = { new WidgetResponsiveRule { MaxWorkAreaWidth = 1366, Scale = 0.8 } },
        });

        Assert.Empty(Errors(skin));
    }

    [Fact]
    public void NullOrEmptyStatesAreAccepted()
    {
        Assert.Empty(Errors(SkinWith(m => m.States = null!)));
        Assert.Empty(Errors(SkinWith()));
    }

    public static IEnumerable<object[]> InvalidStates()
    {
        yield return new object[] { "threshold-needs-measure", new Action<MeterStateDefinition>(s => { s.Trigger = MeterStateTrigger.Threshold; s.MeasureName = null; }) };
        yield return new object[] { "unknown-measure", new Action<MeterStateDefinition>(s => { s.Trigger = MeterStateTrigger.Active; s.MeasureName = "Nope"; }) };
        yield return new object[] { "bad-trigger", new Action<MeterStateDefinition>(s => s.Trigger = (MeterStateTrigger)99) };
        yield return new object[] { "bad-comparison", new Action<MeterStateDefinition>(s => s.Comparison = (StateComparison)99) };
        yield return new object[] { "bad-easing", new Action<MeterStateDefinition>(s => s.Easing = (TransitionEasing)99) };
        yield return new object[] { "nan-value", new Action<MeterStateDefinition>(s => s.Value = double.NaN) };
        yield return new object[] { "negative-duration", new Action<MeterStateDefinition>(s => s.TransitionMs = -1) };
        yield return new object[] { "huge-duration", new Action<MeterStateDefinition>(s => s.TransitionMs = 10_001) };
        yield return new object[] { "opacity-high", new Action<MeterStateDefinition>(s => s.Opacity = 1.5) };
        yield return new object[] { "opacity-nan", new Action<MeterStateDefinition>(s => s.Opacity = double.NaN) };
        yield return new object[] { "scale-zero", new Action<MeterStateDefinition>(s => s.Scale = 0) };
        yield return new object[] { "scale-huge", new Action<MeterStateDefinition>(s => s.Scale = 11) };
        yield return new object[] { "rotation-inf", new Action<MeterStateDefinition>(s => s.Rotation = double.PositiveInfinity) };
        yield return new object[] { "offset-nan", new Action<MeterStateDefinition>(s => s.OffsetX = double.NaN) };
        yield return new object[] { "offset-huge", new Action<MeterStateDefinition>(s => s.OffsetY = 1e9) };
        yield return new object[] { "color-name", new Action<MeterStateDefinition>(s => s.ColorHex = "red") };
        yield return new object[] { "color-short", new Action<MeterStateDefinition>(s => s.ColorHex = "#FFF") };
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void InvalidStateIsRejected(string _, Action<MeterStateDefinition> corrupt)
    {
        var skin = SkinWith(m => m.States.Add(Hover(corrupt)));
        Assert.Contains(Errors(skin), e => e.Path.StartsWith("meters.meter.states"));
    }

    [Fact]
    public void DuplicateAndEmptyStateIdsAreRejected()
    {
        var dup = SkinWith(m => { m.States.Add(Hover(s => s.Id = "x")); m.States.Add(Hover(s => s.Id = "X")); });
        Assert.Contains(Errors(dup), e => e.Message.Contains("unique"));

        var empty = SkinWith(m => m.States.Add(Hover(s => s.Id = " ")));
        Assert.Contains(Errors(empty), e => e.Message.Contains("non-empty"));
    }

    [Fact]
    public void NullStateEntryAndTooManyStatesAreRejected()
    {
        var withNull = SkinWith(m => m.States.Add(null!));
        Assert.Contains(Errors(withNull), e => e.Message.Contains("null"));

        var tooMany = SkinWith(m =>
        {
            for (int i = 0; i < WidgetDefinitionValidator.MaxStatesPerMeter + 1; i++) m.States.Add(Hover());
        });
        Assert.Contains(Errors(tooMany), e => e.Message.Contains("at most"));
    }

    public static IEnumerable<object[]> InvalidAnchors()
    {
        yield return new object[] { "bad-edge", new Action<WidgetAnchorDefinition>(a => a.Edge = (WidgetAnchorEdge)99) };
        yield return new object[] { "bad-monitor", new Action<WidgetAnchorDefinition>(a => a.Monitor = (WidgetMonitorTarget)99) };
        yield return new object[] { "margin-nan", new Action<WidgetAnchorDefinition>(a => a.MarginX = double.NaN) };
        yield return new object[] { "margin-huge", new Action<WidgetAnchorDefinition>(a => a.MarginY = 1e9) };
        yield return new object[] { "negative-limit", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = -1 })) };
        yield return new object[] { "nan-limit", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaHeight = double.NaN })) };
        yield return new object[] { "rule-bad-edge", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = 1, Edge = (WidgetAnchorEdge)99 })) };
        yield return new object[] { "rule-scale-zero", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = 1, Scale = 0 })) };
        yield return new object[] { "rule-scale-huge", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = 1, Scale = 5 })) };
        yield return new object[] { "rule-margin-inf", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = 1, MarginX = double.PositiveInfinity })) };
        yield return new object[] { "rule-null", new Action<WidgetAnchorDefinition>(a => a.Responsive.Add(null!)) };
        yield return new object[] { "rule-duplicate-id", new Action<WidgetAnchorDefinition>(a =>
        {
            a.Responsive.Add(new WidgetResponsiveRule { Id = "r", MaxWorkAreaWidth = 1 });
            a.Responsive.Add(new WidgetResponsiveRule { Id = "R", MaxWorkAreaWidth = 2 });
        }) };
        yield return new object[] { "too-many-rules", new Action<WidgetAnchorDefinition>(a =>
        {
            for (int i = 0; i < WidgetDefinitionValidator.MaxResponsiveRules + 1; i++)
                a.Responsive.Add(new WidgetResponsiveRule { MaxWorkAreaWidth = i + 1 });
        }) };
    }

    [Theory]
    [MemberData(nameof(InvalidAnchors))]
    public void InvalidAnchorIsRejected(string _, Action<WidgetAnchorDefinition> corrupt)
    {
        var anchor = new WidgetAnchorDefinition();
        corrupt(anchor);
        Assert.Contains(Errors(SkinWith(configureSkin: s => s.Anchor = anchor)), e => e.Path.StartsWith("anchor"));
    }

    [Fact]
    public void RuleWithNoLimitsIsAWarningNotAnError()
    {
        var skin = SkinWith(configureSkin: s => s.Anchor = new WidgetAnchorDefinition
        {
            Responsive = { new WidgetResponsiveRule { Scale = 0.9 } },
        });
        Assert.Empty(Errors(skin));
        Assert.Contains(WidgetDefinitionValidator.Validate(skin), i => !i.IsError && i.Path.EndsWith(".limits"));
    }

    // ── Persistence and packages ─────────────────────────────────────────────

    [Fact]
    public async Task Repository_MigratesV5WidgetsToV6AndRepairsNullCollections()
    {
        var folder = Path.Combine(_root, "store");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "skins.json"), """
            [{
              "Id": "legacy", "Name": "Legacy", "SchemaVersion": 5,
              "Meters": [{ "Id": "m1", "States": null }, { "Id": "m2" }],
              "Anchor": { "Edge": "TopRight", "Responsive": null }
            }]
            """);

        var repo = new SkinRepository(folder);
        var skin = Assert.Single(await repo.LoadAllAsync());

        Assert.Equal(6, skin.SchemaVersion);
        Assert.All(skin.Meters, m => Assert.NotNull(m.States));
        Assert.NotNull(skin.Anchor);
        Assert.Equal(WidgetAnchorEdge.TopRight, skin.Anchor!.Edge);
        Assert.NotNull(skin.Anchor.Responsive);

        // Migration is written back, so a second load finds nothing left to change.
        var reloaded = Assert.Single(await new SkinRepository(folder).LoadAllAsync());
        Assert.Equal(6, reloaded.SchemaVersion);
    }

    [Fact]
    public async Task Repository_LegacyWidgetWithoutAnchorStaysUnanchored()
    {
        var folder = Path.Combine(_root, "store2");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "skins.json"),
            """[{ "Id": "legacy", "Name": "Legacy", "SchemaVersion": 5, "X": 321, "Y": 123 }]""");

        var skin = Assert.Single(await new SkinRepository(folder).LoadAllAsync());
        Assert.Null(skin.Anchor);
        Assert.Equal(321, skin.X);
        Assert.Equal(123, skin.Y);
    }

    [Fact]
    public async Task Package_RoundTripsStatesAndAnchor()
    {
        var original = SkinWith(m =>
        {
            m.States.Add(new MeterStateDefinition
            {
                Id = "hover", Trigger = MeterStateTrigger.Hover, Scale = 1.15, Opacity = 0.9,
                TransitionMs = 220, Easing = TransitionEasing.EaseInOut, ColorHex = "#FFAA00",
            });
            m.States.Add(new MeterStateDefinition
            {
                Id = "hot", Trigger = MeterStateTrigger.Threshold, MeasureName = "Cpu",
                Comparison = StateComparison.AtOrAbove, Value = 85, Priority = 5,
            });
        }, s => s.Anchor = new WidgetAnchorDefinition
        {
            Edge = WidgetAnchorEdge.BottomRight, Monitor = WidgetMonitorTarget.Primary, MarginX = 24, MarginY = 40,
            Responsive = { new WidgetResponsiveRule { Id = "small", MaxWorkAreaWidth = 1366, Scale = 0.75, Hidden = false } },
        });

        var package = Path.Combine(_root, "w.themedwidget");
        var service = new SkinPackageService(Path.Combine(_root, "assets"));
        await service.ExportAsync(original, package);
        var imported = await service.ImportAsync(package);

        Assert.Equal(SkinDefinition.CurrentSchemaVersion, imported.SchemaVersion);
        var states = imported.Meters.Single().States;
        Assert.Equal(2, states.Count);
        Assert.Equal(1.15, states[0].Scale);
        Assert.Equal(TransitionEasing.EaseInOut, states[0].Easing);
        Assert.Equal("#FFAA00", states[0].ColorHex);
        Assert.Equal(MeterStateTrigger.Threshold, states[1].Trigger);
        Assert.Equal(85, states[1].Value);
        Assert.Equal(5, states[1].Priority);

        Assert.NotNull(imported.Anchor);
        Assert.Equal(WidgetAnchorEdge.BottomRight, imported.Anchor!.Edge);
        Assert.Equal(WidgetMonitorTarget.Primary, imported.Anchor.Monitor);
        Assert.Equal(24, imported.Anchor.MarginX);
        var rule = Assert.Single(imported.Anchor.Responsive);
        Assert.Equal(1366, rule.MaxWorkAreaWidth);
        Assert.Equal(0.75, rule.Scale);
    }

    [Fact]
    public async Task Package_WithInvalidStateIsRejectedOnImport()
    {
        var package = Path.Combine(_root, "bad.themedwidget");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("skin.json").Open());
            writer.Write("""
                { "SchemaVersion": 6, "Id": "x", "Name": "x",
                  "Meters": [{ "Id": "m", "States": [{ "Id": "s", "Trigger": "Hover", "Scale": -3 }] }] }
                """);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => new SkinPackageService(Path.Combine(_root, "a2")).ImportAsync(package));
    }

    [Fact]
    public async Task Package_FromSchemaV5ImportsWithoutStatesOrAnchor()
    {
        var package = Path.Combine(_root, "v5.themedwidget");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("skin.json").Open());
            writer.Write("""{ "SchemaVersion": 5, "Id": "x", "Name": "Old", "Meters": [{ "Id": "m" }] }""");
        }
        var imported = await new SkinPackageService(Path.Combine(_root, "a3")).ImportAsync(package);
        Assert.Equal(SkinDefinition.CurrentSchemaVersion, imported.SchemaVersion);
        Assert.Empty(imported.Meters.Single().States);
        Assert.Null(imported.Anchor);
    }

    [Fact]
    public async Task RepositoryRepairsNullMeterCollectionDuringMigration()
    {
        var folder = Path.Combine(_root, "null-meters");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "skins.json"),
            """[{"Id":"legacy-null","Name":"Legacy","SchemaVersion":5,"Meters":null}]""");
        var skin = Assert.Single(await new SkinRepository(folder).LoadAllAsync());
        Assert.Empty(skin.Meters);
        Assert.Equal(SkinDefinition.CurrentSchemaVersion, skin.SchemaVersion);
    }
}
