namespace ThemeManager.Core.Skins;

/// <summary>High-level composition modes for Rainmeter-class widget layouts.</summary>
public enum WidgetLayoutMode
{
    Freeform,
    Horizontal,
    Vertical,
    Grid,
    Radial,
}

/// <summary>Reusable layout recipe that can be applied to any collection of meters.</summary>
public sealed record WidgetLayoutDefinition
{
    public WidgetLayoutMode Mode { get; init; } = WidgetLayoutMode.Freeform;
    public double Padding { get; init; } = 12;
    public double Gap { get; init; } = 8;
    public int Columns { get; init; } = 2;
    public double CellWidth { get; init; } = 120;
    public double CellHeight { get; init; } = 28;
    public double Radius { get; init; } = 70;
    public double StartAngleDegrees { get; init; } = -90;
}

/// <summary>
/// Deterministic, UI-independent layout engine. It deliberately only mutates meter geometry,
/// making it safe for the visual editor, generators and future drag/drop composition tools to share.
/// </summary>
public static class WidgetLayoutEngine
{
    public static void Apply(SkinDefinition skin, WidgetLayoutDefinition layout)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(layout);

        var meters = skin.Meters.OrderBy(m => m.ZIndex).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
        if (meters.Count == 0 || layout.Mode == WidgetLayoutMode.Freeform)
            return;

        var padding = Math.Max(0, layout.Padding);
        var gap = Math.Max(0, layout.Gap);

        switch (layout.Mode)
        {
            case WidgetLayoutMode.Horizontal:
                ApplyHorizontal(meters, padding, gap);
                break;
            case WidgetLayoutMode.Vertical:
                ApplyVertical(meters, padding, gap);
                break;
            case WidgetLayoutMode.Grid:
                ApplyGrid(meters, padding, gap, layout.Columns, layout.CellWidth, layout.CellHeight);
                break;
            case WidgetLayoutMode.Radial:
                ApplyRadial(meters, skin.Width / 2, skin.Height / 2, Math.Max(0, layout.Radius), layout.StartAngleDegrees);
                break;
        }
    }

    private static void ApplyHorizontal(IReadOnlyList<MeterDefinition> meters, double padding, double gap)
    {
        var x = padding;
        foreach (var meter in meters)
        {
            meter.X = x;
            meter.Y = padding;
            x += Math.Max(0, meter.Width) + gap;
        }
    }

    private static void ApplyVertical(IReadOnlyList<MeterDefinition> meters, double padding, double gap)
    {
        var y = padding;
        foreach (var meter in meters)
        {
            meter.X = padding;
            meter.Y = y;
            y += Math.Max(0, meter.Height) + gap;
        }
    }

    private static void ApplyGrid(IReadOnlyList<MeterDefinition> meters, double padding, double gap, int columns, double cellWidth, double cellHeight)
    {
        columns = Math.Max(1, columns);
        cellWidth = Math.Max(1, cellWidth);
        cellHeight = Math.Max(1, cellHeight);

        for (var i = 0; i < meters.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            meters[i].X = padding + column * (cellWidth + gap);
            meters[i].Y = padding + row * (cellHeight + gap);
            meters[i].Width = cellWidth;
            meters[i].Height = cellHeight;
        }
    }

    private static void ApplyRadial(IReadOnlyList<MeterDefinition> meters, double centerX, double centerY, double radius, double startAngleDegrees)
    {
        if (meters.Count == 0) return;
        var step = 360.0 / meters.Count;
        for (var i = 0; i < meters.Count; i++)
        {
            var meter = meters[i];
            var angle = (startAngleDegrees + step * i) * Math.PI / 180.0;
            meter.X = centerX + Math.Cos(angle) * radius - meter.Width / 2;
            meter.Y = centerY + Math.Sin(angle) * radius - meter.Height / 2;
        }
    }
}

/// <summary>Named layout recipes intended for generator output and the visual editor.</summary>
public static class WidgetLayoutPresets
{
    public static IReadOnlyDictionary<string, WidgetLayoutDefinition> All { get; } =
        new Dictionary<string, WidgetLayoutDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Freeform"] = new(),
            ["Glass Stack"] = new() { Mode = WidgetLayoutMode.Vertical, Padding = 12, Gap = 6 },
            ["Dashboard Grid"] = new() { Mode = WidgetLayoutMode.Grid, Padding = 14, Gap = 10, Columns = 2, CellWidth = 120, CellHeight = 32 },
            ["HUD Orbit"] = new() { Mode = WidgetLayoutMode.Radial, Radius = 72, StartAngleDegrees = -90 },
            ["Command Strip"] = new() { Mode = WidgetLayoutMode.Horizontal, Padding = 10, Gap = 10 },
        };
}

/// <summary>Animation curves supported by the runtime without pulling UI dependencies into Core.</summary>
public enum WidgetEasing
{
    Linear,
    EaseOutCubic,
    EaseInOutCubic,
    EaseOutBack,
}

/// <summary>Small animation primitive used for buttery transitions between widget states.</summary>
public readonly record struct WidgetAnimation(double From, double To, TimeSpan Duration, WidgetEasing Easing = WidgetEasing.EaseOutCubic)
{
    public double Sample(TimeSpan elapsed)
    {
        if (Duration <= TimeSpan.Zero) return To;
        var t = Math.Clamp(elapsed.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
        var eased = Easing switch
        {
            WidgetEasing.Linear => t,
            WidgetEasing.EaseInOutCubic => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2,
            WidgetEasing.EaseOutBack => EaseOutBack(t),
            _ => 1 - Math.Pow(1 - t, 3),
        };
        return From + (To - From) * eased;
    }

    private static double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        var x = t - 1;
        return 1 + c3 * x * x * x + c1 * x * x;
    }
}
