namespace ThemeManager.Core.Skins;

/// <summary>Which edge or corner of the monitor area a widget is pinned to.</summary>
public enum WidgetAnchorEdge
{
    /// <summary>Not pinned; the widget keeps its saved X/Y.</summary>
    None,
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight,
}

/// <summary>Which monitor an anchored widget follows.</summary>
public enum WidgetMonitorTarget
{
    /// <summary>The monitor the widget window is currently on.</summary>
    Current,
    /// <summary>Always the primary monitor.</summary>
    Primary,
}

/// <summary>
/// Overrides applied when the monitor area is at most the given size. Unset (null/0) fields leave
/// the base anchor untouched.
/// </summary>
public sealed class WidgetResponsiveRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Rule applies when the area width is at most this many DIPs. 0 means no width limit.</summary>
    public double MaxWorkAreaWidth { get; set; }
    /// <summary>Rule applies when the area height is at most this many DIPs. 0 means no height limit.</summary>
    public double MaxWorkAreaHeight { get; set; }
    public WidgetAnchorEdge? Edge { get; set; }
    public double? MarginX { get; set; }
    public double? MarginY { get; set; }
    /// <summary>Uniform scale for the whole widget (window and contents).</summary>
    public double? Scale { get; set; }
    /// <summary>Hide the widget entirely at this size.</summary>
    public bool Hidden { get; set; }

    public WidgetResponsiveRule Clone() => new()
    {
        Id = Id, MaxWorkAreaWidth = MaxWorkAreaWidth, MaxWorkAreaHeight = MaxWorkAreaHeight,
        Edge = Edge, MarginX = MarginX, MarginY = MarginY, Scale = Scale, Hidden = Hidden,
    };
}

/// <summary>
/// Pins a widget to a monitor edge or corner. Margins are DIP distances inward from the pinned
/// edge; on a centered axis the margin is an offset from center (positive = right/down).
/// </summary>
public sealed class WidgetAnchorDefinition
{
    public WidgetAnchorEdge Edge { get; set; } = WidgetAnchorEdge.None;
    public WidgetMonitorTarget Monitor { get; set; } = WidgetMonitorTarget.Current;
    /// <summary>Use the monitor work area (excludes the taskbar) rather than the full monitor bounds.</summary>
    public bool UseWorkArea { get; set; } = true;
    public double MarginX { get; set; } = 16;
    public double MarginY { get; set; } = 16;
    public List<WidgetResponsiveRule> Responsive { get; set; } = new();

    /// <summary>Resets both base margins and explicit responsive overrides to the default inset.</summary>
    public void ResetMargins()
    {
        MarginX = MarginY = 16;
        foreach (var rule in Responsive ?? new List<WidgetResponsiveRule>())
        {
            if (rule is null) continue;
            if (rule.MarginX is not null) rule.MarginX = 16;
            if (rule.MarginY is not null) rule.MarginY = 16;
        }
    }

    public WidgetAnchorDefinition Clone() => new()
    {
        Edge = Edge, Monitor = Monitor, UseWorkArea = UseWorkArea, MarginX = MarginX, MarginY = MarginY,
        Responsive = (Responsive ?? new List<WidgetResponsiveRule>()).Select(r => r.Clone()).ToList(),
    };
}

public readonly record struct WidgetRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <summary>Final placement for one widget on one monitor area, in DIPs.</summary>
public sealed record WidgetLayout(
    double X, double Y, double Scale, bool Hidden,
    WidgetAnchorEdge Edge, double MarginX, double MarginY,
    WidgetResponsiveRule? ActiveRule);

public static class WidgetAnchorResolver
{
    /// <summary>
    /// The rule that applies to <paramref name="area"/>, or null. The most specific match wins:
    /// smallest width limit first, then smallest height limit, then list order. A limit of 0 counts as unbounded.
    /// </summary>
    public static WidgetResponsiveRule? SelectRule(WidgetAnchorDefinition anchor, WidgetRect area)
    {
        if (anchor.Responsive is null) return null;
        WidgetResponsiveRule? best = null;
        double bestW = double.PositiveInfinity, bestH = double.PositiveInfinity;

        foreach (var rule in anchor.Responsive)
        {
            if (rule is null) continue;
            double w = rule.MaxWorkAreaWidth > 0 ? rule.MaxWorkAreaWidth : double.PositiveInfinity;
            double h = rule.MaxWorkAreaHeight > 0 ? rule.MaxWorkAreaHeight : double.PositiveInfinity;
            if (area.Width > w || area.Height > h) continue;
            if (best is null || w < bestW || (w == bestW && h < bestH))
            {
                best = rule; bestW = w; bestH = h;
            }
        }
        return best;
    }

    /// <summary>
    /// Computes where a widget of <paramref name="width"/> x <paramref name="height"/> (unscaled DIPs)
    /// goes inside <paramref name="area"/>. With no effective edge the fallback X/Y is returned unchanged.
    /// Anchored positions are clamped to stay inside the area whenever the widget fits.
    /// </summary>
    public static WidgetLayout Resolve(
        WidgetAnchorDefinition anchor, WidgetRect area, double width, double height,
        double fallbackX = 0, double fallbackY = 0)
    {
        var rule = SelectRule(anchor, area);
        var edge = rule?.Edge ?? anchor.Edge;
        double marginX = rule?.MarginX ?? anchor.MarginX;
        double marginY = rule?.MarginY ?? anchor.MarginY;
        double scale = rule?.Scale is { } s && double.IsFinite(s) && s > 0 ? s : 1.0;
        bool hidden = rule?.Hidden ?? false;

        if (edge == WidgetAnchorEdge.None)
            return new WidgetLayout(fallbackX, fallbackY, scale, hidden, edge, marginX, marginY, rule);

        double w = width * scale, h = height * scale;
        var (hSide, vSide) = Split(edge);

        double x = hSide switch
        {
            -1 => area.X + marginX,
            1 => area.Right - w - marginX,
            _ => area.X + (area.Width - w) / 2 + marginX,
        };
        double y = vSide switch
        {
            -1 => area.Y + marginY,
            1 => area.Bottom - h - marginY,
            _ => area.Y + (area.Height - h) / 2 + marginY,
        };

        x = ClampAxis(x, w, area.X, area.Width);
        y = ClampAxis(y, h, area.Y, area.Height);
        return new WidgetLayout(x, y, scale, hidden, edge, marginX, marginY, rule);
    }

    /// <summary>
    /// Inverse of <see cref="Resolve"/> for a drag: the margins that would place the widget at
    /// (<paramref name="x"/>, <paramref name="y"/>). Margins on a pinned side never go negative.
    /// </summary>
    public static (double MarginX, double MarginY) MarginsFromPosition(
        WidgetAnchorEdge edge, WidgetRect area, double width, double height, double scale, double x, double y)
    {
        if (edge == WidgetAnchorEdge.None) return (0, 0);
        double w = width * scale, h = height * scale;
        var (hSide, vSide) = Split(edge);

        double mx = hSide switch
        {
            -1 => x - area.X,
            1 => area.Right - w - x,
            _ => x - (area.X + (area.Width - w) / 2),
        };
        double my = vSide switch
        {
            -1 => y - area.Y,
            1 => area.Bottom - h - y,
            _ => y - (area.Y + (area.Height - h) / 2),
        };
        return (hSide == 0 ? mx : Math.Max(0, mx), vSide == 0 ? my : Math.Max(0, my));
    }

    // -1 = low side (left/top), 1 = high side (right/bottom), 0 = centered on that axis.
    private static (int H, int V) Split(WidgetAnchorEdge edge) => edge switch
    {
        WidgetAnchorEdge.TopLeft => (-1, -1),
        WidgetAnchorEdge.Top => (0, -1),
        WidgetAnchorEdge.TopRight => (1, -1),
        WidgetAnchorEdge.Left => (-1, 0),
        WidgetAnchorEdge.Center => (0, 0),
        WidgetAnchorEdge.Right => (1, 0),
        WidgetAnchorEdge.BottomLeft => (-1, 1),
        WidgetAnchorEdge.Bottom => (0, 1),
        WidgetAnchorEdge.BottomRight => (1, 1),
        _ => (0, 0),
    };

    private static double ClampAxis(double pos, double size, double areaStart, double areaSize) =>
        size >= areaSize ? areaStart : Math.Clamp(pos, areaStart, areaStart + areaSize - size);
}
