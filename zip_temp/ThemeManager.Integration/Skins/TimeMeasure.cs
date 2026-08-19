using ThemeManager.Core.Skins;

namespace ThemeManager.Integration.Skins;

/// <summary>Current time or date, formatted as text. Purely local — no interop, no dependency.</summary>
public sealed class TimeMeasure : IMeasure
{
    public string Name { get; }

    /// <summary>Minutes-since-midnight — a modestly useful numeric fallback; prefer <see cref="Text"/>.</summary>
    public double Value { get; private set; }

    public string Text { get; private set; } = "";

    private readonly bool _isDate;

    public TimeMeasure(string name, bool isDate)
    {
        Name = name;
        _isDate = isDate;
    }

    public void Refresh()
    {
        var now = DateTime.Now;
        if (_isDate)
        {
            Value = 0;
            Text = now.ToString("ddd, MMM d");
        }
        else
        {
            Value = now.Hour * 60 + now.Minute;
            Text = now.ToString("HH:mm:ss");
        }
    }
}
