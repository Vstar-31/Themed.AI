namespace ThemeManager.Core.Skins;

/// <summary>
/// A live-updating data source for a skin (mirrors Rainmeter's "measure" concept).
/// Implementations live in ThemeManager.Integration since most of them read real OS state.
/// Like <c>ISystemThemeIntegrator</c>, <see cref="Refresh"/> must never throw to callers —
/// a widget losing one reading should never crash the app.
/// </summary>
public interface IMeasure
{
    /// <summary>The name this measure is referenced by from <see cref="MeterDefinition.MeasureName"/>.</summary>
    string Name { get; }

    /// <summary>
    /// Latest numeric reading. For CPU/Memory/Disk measures this is a 0–100 percentage.
    /// For Time/Date/Uptime it's a secondary, less meaningful number — use <see cref="Text"/> instead.
    /// </summary>
    double Value { get; }

    /// <summary>Latest human-readable representation (e.g. "42%" or "14:32:07"). Always populated.</summary>
    string Text { get; }

    /// <summary>Re-reads the underlying value. Safe to call every tick; must never throw.</summary>
    void Refresh();

    /// <summary>Optional URL or URI to launch when a meter bound to this measure is clicked.</summary>
    string? ActionUrl => null;
}
