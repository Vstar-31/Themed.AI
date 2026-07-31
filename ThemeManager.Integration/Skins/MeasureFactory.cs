using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;

namespace ThemeManager.Integration.Skins;

/// <summary>Builds the concrete <see cref="IMeasure"/> a <see cref="MeasureDefinition"/> describes.</summary>
public static class MeasureFactory
{
    public static IMeasure Create(MeasureDefinition definition, ILogger? logger = null) => definition.Type switch
    {
        MeasureType.Cpu      => new CpuMeasure(definition.Name, logger),
        MeasureType.Memory   => new MemoryMeasure(definition.Name, logger),
        MeasureType.DiskFree => new DiskMeasure(definition.Name, definition.Target, reportFreeSpace: true, logger),
        MeasureType.DiskUsed => new DiskMeasure(definition.Name, definition.Target, reportFreeSpace: false, logger),
        MeasureType.Time     => new TimeMeasure(definition.Name, isDate: false),
        MeasureType.Date     => new TimeMeasure(definition.Name, isDate: true),
        MeasureType.Uptime   => new UptimeMeasure(definition.Name),
        _                    => new UnknownMeasure(definition.Name),
    };

    /// <summary>
    /// Defensive fallback for a <see cref="MeasureType"/> value that doesn't match anything above
    /// (e.g. a skins.json hand-edited with a typo'd or future enum value). Renders as "—" rather
    /// than crashing the widget.
    /// </summary>
    private sealed class UnknownMeasure : IMeasure
    {
        public string Name { get; }
        public double Value => 0;
        public string Text => "—";
        public UnknownMeasure(string name) => Name = name;
        public void Refresh() { }
    }
}
