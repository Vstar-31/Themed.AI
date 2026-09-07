using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class SkinPackageServiceTests
{
    [Fact]
    public async Task ExportImport_RoundTripsWidgetAndCreatesNewIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "ThemedAI-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var package = Path.Combine(root, "clock.themedwidget");
        try
        {
            var original = new SkinDefinition
            {
                Id = "original-id",
                Name = "Clock",
                Description = "A portable clock widget",
                Tags = new() { "clock", "minimal" },
                UpdateIntervalMs = 250,
            };
            original.Measures.Add(new MeasureDefinition { Name = "Time", Type = MeasureType.Time });
            original.Meters.Add(new MeterDefinition { MeasureName = "Time", Format = "{1}", Width = 120, Height = 24, ZIndex = 2 });

            var service = new SkinPackageService();
            await service.ExportAsync(original, package);
            var imported = await service.ImportAsync(package);

            Assert.Equal(original.Name, imported.Name);
            Assert.Equal(original.Description, imported.Description);
            Assert.Equal(original.Tags, imported.Tags);
            Assert.Equal(original.UpdateIntervalMs, imported.UpdateIntervalMs);
            Assert.NotEqual(original.Id, imported.Id);
            Assert.False(imported.Enabled);
            Assert.Equal(2, imported.SchemaVersion);
            Assert.Equal(2, imported.Meters.Single().ZIndex);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
