using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;

namespace ThemeManager.Integration.Skins;

/// <summary>Builds the concrete <see cref="IMeasure"/> a <see cref="MeasureDefinition"/> describes.</summary>
public static class MeasureFactory
{
    /// <param name="activeThemeProvider">
    /// Optional. Only <see cref="VibeFinderMeasure"/> uses this today — it's how a "$theme"
    /// target resolves to a real vibe phrase (see phases.md, Phase 6). Every other measure
    /// ignores it. Left null by any call site that doesn't have a theme context handy (e.g. a
    /// future headless/test construction path); a VibeFinder measure built that way just can't
    /// use "$theme" — literal typed phrases still work exactly as before.
    /// </param>
    public static IMeasure Create(MeasureDefinition definition, ILogger? logger = null, IActiveThemeProvider? activeThemeProvider = null) => definition.Type switch
    {
        MeasureType.Cpu         => new CpuMeasure(definition.Name, logger),
        MeasureType.CpuCore     => new CpuCoreMeasure(definition.Name, definition.Target, logger),
        MeasureType.Memory      => new MemoryMeasure(definition.Name, logger),
        MeasureType.DiskFree    => new DiskMeasure(definition.Name, definition.Target, reportFreeSpace: true, logger),
        MeasureType.DiskUsed    => new DiskMeasure(definition.Name, definition.Target, reportFreeSpace: false, logger),
        MeasureType.Time        => new TimeMeasure(definition.Name, isDate: false),
        MeasureType.Date        => new TimeMeasure(definition.Name, isDate: true),
        MeasureType.Uptime      => new UptimeMeasure(definition.Name),
        MeasureType.NetworkDown => new NetworkMeasure(definition.Name, measureUpload: false, logger),
        MeasureType.NetworkUp   => new NetworkMeasure(definition.Name, measureUpload: true, logger),
        MeasureType.Battery     => new BatteryMeasure(definition.Name, logger),
        MeasureType.MediaTitle  => new MediaMeasure(definition.Name, MeasureType.MediaTitle, logger),
        MeasureType.MediaArtist => new MediaMeasure(definition.Name, MeasureType.MediaArtist, logger),
        MeasureType.MediaState  => new MediaMeasure(definition.Name, MeasureType.MediaState, logger),
        MeasureType.WeatherTemp => new WeatherMeasure(definition.Name, MeasureType.WeatherTemp, definition.Target, logger),
        MeasureType.WeatherDesc => new WeatherMeasure(definition.Name, MeasureType.WeatherDesc, definition.Target, logger),
        MeasureType.WeatherCity => new WeatherMeasure(definition.Name, MeasureType.WeatherCity, definition.Target, logger),
        MeasureType.WebJson     => new WebJsonMeasure(definition.Name, definition.Target, logger),
        MeasureType.VibeTrackTitle  => new VibeFinderMeasure(definition.Name, MeasureType.VibeTrackTitle, definition.Target, logger, activeThemeProvider),
        MeasureType.VibeTrackArtist => new VibeFinderMeasure(definition.Name, MeasureType.VibeTrackArtist, definition.Target, logger, activeThemeProvider),
        MeasureType.VibeMood        => new VibeFinderMeasure(definition.Name, MeasureType.VibeMood, definition.Target, logger, activeThemeProvider),
        _                       => new UnknownMeasure(definition.Name),
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
