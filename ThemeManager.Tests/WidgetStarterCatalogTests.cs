using System.Text.Json;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetStarterCatalogTests
{
    [Fact] public void CatalogCoversBundledStartersWithDescriptionsAndIndependentDefinitions()
    {
        var catalog = WidgetStarterCatalog.Create();
        Assert.Equal(SkinDefaults.CreateAllDefaults().Select(s => s.Id).Order(), catalog.Select(s => s.Definition.Id).Order());
        Assert.All(catalog, item => Assert.False(string.IsNullOrWhiteSpace(item.Summary)));
        catalog[0].Definition.Meters[0].StaticText = "changed";
        Assert.NotEqual("changed", WidgetStarterCatalog.Create()[0].Definition.Meters[0].StaticText);
    }

    [Theory]
    [InlineData("  cLoCk  ", "builtin-clock")]
    [InlineData("date digital", "builtin-clock")]
    [InlineData("CPU BAR", "builtin-system-monitor")]
    [InlineData("RAM bar", "builtin-system-monitor")]
    [InlineData("network\tgraph", "builtin-network")]
    [InlineData("disk\nring", "builtin-system-rings")]
    [InlineData("ring", "builtin-system-rings")]
    [InlineData("clo", "builtin-clock")]
    [InlineData("computer running", "builtin-uptime")]
    public void SearchUsesAllTermsAcrossNamesDescriptionsAndTypes(string query, string expected)
    {
        Assert.Equal(expected, Assert.Single(WidgetStarterCatalog.Search(WidgetStarterCatalog.Create(), query)).Definition.Id);
    }

    [Fact] public void RamFindsBothMemoryStarters()
    {
        Assert.Equal(new[] { "builtin-system-monitor", "builtin-system-rings" },
            WidgetStarterCatalog.Search(WidgetStarterCatalog.Create(), "RAM").Select(s => s.Definition.Id));
    }

    [Theory] [InlineData(null)] [InlineData("")] [InlineData(" \t\n ")]
    public void EmptyQueryPreservesCatalogOrderAndItemIdentity(string? query)
    {
        var catalog = WidgetStarterCatalog.Create(); var found = WidgetStarterCatalog.Search(catalog, query);
        Assert.Equal(catalog.Count, found.Count);
        for (int i = 0; i < found.Count; i++) Assert.Same(catalog[i], found[i]);
    }

    [Theory] [InlineData("no-such-widget")] [InlineData("clock network")]
    [InlineData("[.*](test)")]
    public void NoMatchesOrPatternLikeTextReturnNoResults(string query)
    {
        Assert.Empty(WidgetStarterCatalog.Search(WidgetStarterCatalog.Create(), query));
    }

    [Fact] public void SearchDoesNotMutateSourceAndRemixKeepsFreshIdentity()
    {
        var catalog = WidgetStarterCatalog.Create(); var before = JsonSerializer.Serialize(catalog);
        var selected = Assert.Single(WidgetStarterCatalog.Search(catalog, "clock"));
        var copy = WidgetRemix.Create(selected.Definition);
        Assert.NotEqual(selected.Definition.Id, copy.Id); Assert.False(copy.Enabled);
        Assert.Equal(before, JsonSerializer.Serialize(catalog));
    }
}
