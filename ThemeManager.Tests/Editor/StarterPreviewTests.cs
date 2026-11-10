using System.Text.Json;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

public sealed class StarterPreviewTests
{
    [Fact] public void EveryBundledStarterHasFiniteReadableSamplesWithoutChangingDefinitions()
    {
        foreach (var starter in WidgetStarterCatalog.Create())
        {
            var skin = starter.Definition;
            var before = JsonSerializer.Serialize(skin);
            int mutations = 0;
            var measures = skin.Measures.Select(m => new MeasureEditorItem(m, () => mutations++)).ToArray();
            foreach (var definition in skin.Meters)
            {
                var meter = new MeterEditorItem(definition, () => mutations++);
                var sample = MeterPreview.Compute(meter, measures);
                meter.ApplyPreview(sample.Text, sample.Fraction);
                Assert.False(string.IsNullOrWhiteSpace(sample.Text));
                Assert.DoesNotContain("invalid format", sample.Text);
                Assert.DoesNotContain("no measure", sample.Text);
                Assert.True(double.IsFinite(sample.Fraction)); Assert.InRange(sample.Fraction, 0, 1);
                Assert.True(meter.Width > 0 && meter.Height > 0);
                if (meter.Kind is MeterKind.Bar or MeterKind.Ring or MeterKind.Graph) Assert.True(sample.Fraction > 0);
            }
            Assert.Equal(0, mutations); Assert.Equal(before, JsonSerializer.Serialize(skin));
        }
    }
}
