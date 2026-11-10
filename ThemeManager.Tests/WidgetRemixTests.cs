using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetRemixTests
{
    [Fact] public void FreshIdentityPreservesBindingsAndRemapsGroups()
    {
        var source = SkinDefaults.CreateClock();
        source.Meters[0].States.Add(new() { Name = "Hover", Scale = 1.2 });
        source.Groups.Add(new() { MeterIds = source.Meters.Select(m => m.Id).ToList(), Variables = new() { ["accent"] = "blue" } });
        var copy = WidgetRemix.Create(source);
        Assert.NotEqual(source.Id, copy.Id); Assert.False(copy.Enabled);
        Assert.Empty(source.Meters.Select(m => m.Id).Intersect(copy.Meters.Select(m => m.Id)));
        Assert.Empty(source.Measures.Select(m => m.Id).Intersect(copy.Measures.Select(m => m.Id)));
        Assert.NotEqual(source.Groups[0].Id, copy.Groups[0].Id);
        Assert.Equal(copy.Meters.Select(m => m.Id), copy.Groups[0].MeterIds);
        Assert.Equal(source.Meters[0].MeasureName, copy.Meters[0].MeasureName);
        Assert.NotEqual(source.Meters[0].States[0].Id, copy.Meters[0].States[0].Id);
        copy.Groups[0].Variables["accent"] = "red"; copy.Meters[0].States[0].Scale = 2;
        Assert.Equal("blue", source.Groups[0].Variables["accent"]); Assert.Equal(1.2, source.Meters[0].States[0].Scale);
    }
    [Fact] public void NameCollisionUsesCaseInsensitiveSuffix()
    {
        var source = new SkinDefinition { Name = "Clock" };
        Assert.Equal("Clock remix 3", WidgetRemix.Create(source, new[] { "CLOCK REMIX", "Clock remix 2" }).Name);
    }
    [Fact] public void CopyHasEditableWindowFlagsAndPreservesSourceFlags()
    {
        var source = new SkinDefinition { Enabled = true, Locked = true, ClickThrough = true, DesktopLayer = true, AlwaysOnTop = true };
        var copy = WidgetRemix.Create(source);
        Assert.False(copy.Locked || copy.ClickThrough || copy.DesktopLayer || copy.AlwaysOnTop || copy.Enabled);
        Assert.True(source.Locked && source.ClickThrough && source.DesktopLayer && source.AlwaysOnTop && source.Enabled);
    }
    [Fact] public void AnchorsAndAssetsAreRetainedWithoutSharingMutableRules()
    {
        var source = new SkinDefinition { Anchor = new() { Edge = WidgetAnchorEdge.TopRight }, Meters = new() { new() { Kind = MeterKind.Image, ImagePath = "C:\\image.png" } } };
        var copy = WidgetRemix.Create(source); copy.Anchor!.MarginX = 99;
        Assert.NotEqual(99, source.Anchor!.MarginX); Assert.Equal(source.Meters[0].ImagePath, copy.Meters[0].ImagePath);
    }
    [Fact] public void InvalidGroupReferencesAreRejected()
    {
        var source = new SkinDefinition { Groups = new() { new() { MeterIds = new() { "missing" } } } };
        Assert.Throws<ArgumentException>(() => WidgetRemix.Create(source));
    }
    [Fact] public void DuplicateMeterIdentityIsRejected()
    {
        var source = new SkinDefinition { Meters = new() { new() { Id = "same" }, new() { Id = "same" } } };
        Assert.Throws<ArgumentException>(() => WidgetRemix.Create(source));
    }
    [Fact] public void AllBundledTemplatesProduceIndependentCurrentSchemaCopies()
    {
        foreach(var template in SkinDefaults.CreateAllDefaults())
        {
            var copy = WidgetRemix.Create(template);
            Assert.Equal(SkinDefinition.CurrentSchemaVersion, copy.SchemaVersion); Assert.NotEqual(template.Id, copy.Id);
            Assert.DoesNotContain("built-in", copy.Tags); Assert.Contains("remix", copy.Tags);
        }
    }
}
