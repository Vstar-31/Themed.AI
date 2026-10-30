using ThemeManager.Core.Skins;
using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class WidgetInputRegressionTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteRenderingInputsAreRejected(double invalid)
    {
        var meter = new MeterDefinition { FontSize = invalid, BarMax = invalid, ThresholdPercent = invalid };
        var skin = new SkinDefinition { Name = "Input test", Meters = { meter } };
        var issues = WidgetDefinitionValidator.Validate(skin);
        Assert.Contains(issues, i => i.Path.EndsWith(".fontSize"));
        Assert.Contains(issues, i => i.Path.EndsWith(".barMax"));
        Assert.Contains(issues, i => i.Path.EndsWith(".thresholdPercent"));
    }

    [Theory]
    [InlineData("{", false)]
    [InlineData("{2}", false)]
    [InlineData("{0:F1}% — {1}", true)]
    [InlineData("{{literal}}", true)]
    public void DisplayFormatsAreCheckedBeforeSaving(string format, bool valid)
    {
        var skin = new SkinDefinition { Name = "Format", Meters = { new MeterDefinition { Format = format } } };
        Assert.Equal(!valid, WidgetDefinitionValidator.Validate(skin).Any(i => i.Path.EndsWith(".format")));
    }

    [Fact]
    public void InvalidRenderingRangesAreRejected()
    {
        var skin = new SkinDefinition { Name = "Ranges", Meters = { new MeterDefinition { FontSize = 0, BarMax = 0, HistoryLength = int.MaxValue, ThresholdPercent = 101 } } };
        Assert.Equal(4, WidgetDefinitionValidator.Validate(skin).Count);
    }

    [Fact]
    public void AllDefaultWidgetsStillPassValidation()
    {
        foreach (var skin in SkinDefaults.CreateAllDefaults())
            Assert.DoesNotContain(WidgetDefinitionValidator.Validate(skin), i => i.IsError);
    }

    [Fact]
    public async Task FailedCheckedSavePreservesPreviousWidgetFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ThemedAI-QA-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var repository = new SkinRepository(folder);
            await repository.SaveAllCheckedAsync(new[] { new SkinDefinition { Name = "Original" } });
            var path = Path.Combine(folder, "skins.json");
            var original = await File.ReadAllBytesAsync(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var error = await Record.ExceptionAsync(() => repository.SaveAllCheckedAsync(new[] { new SkinDefinition { Name = "Replacement" } }));
                Assert.True(error is IOException or UnauthorizedAccessException, $"Expected a storage failure, got {error?.GetType().Name ?? "success"}");
            }
            Assert.Equal(original, await File.ReadAllBytesAsync(path));
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally { Directory.Delete(folder, true); }
    }
}
