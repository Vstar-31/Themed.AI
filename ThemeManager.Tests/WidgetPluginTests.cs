using Microsoft.Extensions.Logging;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetPluginTests
{
    [Fact]
    public void Registry_RegistersAndCreatesCustomMeasure()
    {
        var registry = new WidgetPluginRegistry();
        registry.RegisterMeasure(new ConstantMeasurePlugin());

        var definition = new MeasureDefinition
        {
            Name = "PluginValue",
            PluginType = "tests.constant"
        };

        Assert.True(registry.TryCreateMeasure(definition.PluginType, definition, null, out var measure));
        measure.Refresh();
        Assert.Equal("PluginValue", measure.Name);
        Assert.Equal(42, measure.Value);
    }

    [Fact]
    public void Registry_RejectsDuplicateCapabilityIds()
    {
        var registry = new WidgetPluginRegistry();
        registry.RegisterMeasure(new ConstantMeasurePlugin());

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterMeasure(new ConstantMeasurePlugin()));
    }

    [Fact]
    public async Task Registry_ResolvesActionPluginsByScheme()
    {
        var registry = new WidgetPluginRegistry();
        var plugin = new RecordingActionPlugin();
        registry.RegisterAction(plugin);

        Assert.True(registry.TryGetActionPlugin("plugin", out var resolved));
        Assert.Same(plugin, resolved);

        var handled = await resolved!.ExecuteAsync(
            new WidgetActionContext("widget", "meter", "plugin://tests/ping", _ => "value"));

        Assert.True(handled);
        Assert.Equal("plugin://tests/ping", plugin.LastUri);
    }

    private sealed class ConstantMeasurePlugin : IMeasurePlugin
    {
        public string TypeId => "tests.constant";
        public IMeasure Create(MeasureDefinition definition, ILogger? logger = null) =>
            new ConstantMeasure(definition.Name);
    }

    private sealed class ConstantMeasure : IMeasure
    {
        public string Name { get; }
        public double Value => 42;
        public string Text => "42";
        public ConstantMeasure(string name) => Name = name;
        public void Refresh() { }
    }

    private sealed class RecordingActionPlugin : IWidgetActionPlugin
    {
        public string Scheme => "plugin";
        public string? LastUri { get; private set; }

        public bool CanHandle(Uri actionUri) =>
            actionUri.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase);

        public ValueTask<bool> ExecuteAsync(
            WidgetActionContext context,
            CancellationToken cancellationToken = default)
        {
            LastUri = context.ActionUri;
            return ValueTask.FromResult(true);
        }
    }
}
