using ThemeManager.Core.Skins;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Time since this PC last booted. Uses <see cref="Environment.TickCount64"/> — a plain
/// BCL API (no P/Invoke) that wraps GetTickCount64 for us.
/// </summary>
public sealed class UptimeMeasure : IMeasure
{
    public string Name { get; }

    /// <summary>Total uptime in seconds.</summary>
    public double Value { get; private set; }

    public string Text { get; private set; } = "";

    public UptimeMeasure(string name) => Name = name;

    public void Refresh()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        Value = uptime.TotalSeconds;
        Text = uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m";
    }
}
