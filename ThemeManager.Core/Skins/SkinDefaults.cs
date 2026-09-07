namespace ThemeManager.Core.Skins;

/// <summary>Curated built-in widgets. Layouts favor generous typography, consistent spacing and readable data.</summary>
public static class SkinDefaults
{
    private const int CurrentDesignVersion = 3;

    private static SkinDefinition Base(string id, string name, double x, double y, double width, double height) => new()
    {
        Id = id, Name = name, X = x, Y = y, Width = width, Height = height, SchemaVersion = CurrentDesignVersion,
        Opacity = 0.86, AlwaysOnTop = true, Tags = new() { "built-in", "desktop" }
    };

    public static SkinDefinition CreateClock()
    {
        var skin = Base("builtin-clock", "Cozy Clock", 40, 40, 244, 112);
        skin.Measures.Add(new MeasureDefinition { Name = "Now", Type = MeasureType.Time });
        skin.Measures.Add(new MeasureDefinition { Name = "Today", Type = MeasureType.Date });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "Now", Format = "{1}", X = 18, Y = 14, Width = 208, Height = 48, FontSize = 34, Bold = true });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "Today", Format = "{1}", X = 18, Y = 66, Width = 208, Height = 26, FontSize = 14 });
        return skin;
    }

    public static SkinDefinition CreateSystemMonitor()
    {
        var skin = Base("builtin-system-monitor", "System Monitor", 40, 176, 268, 182);
        skin.Measures.Add(new MeasureDefinition { Name = "CpuUsage", Type = MeasureType.Cpu });
        skin.Measures.Add(new MeasureDefinition { Name = "MemUsage", Type = MeasureType.Memory });
        skin.Measures.Add(new MeasureDefinition { Name = "DiskFree", Type = MeasureType.DiskFree, Target = @"C:\" });
        AddMetric(skin, "CpuUsage", "CPU", 16, 14);
        AddMetric(skin, "MemUsage", "RAM", 16, 66);
        AddMetric(skin, "DiskFree", "C:\\ FREE", 16, 118, true);
        return skin;
    }

    private static void AddMetric(SkinDefinition skin, string measure, string label, double x, double y, bool free = false)
    {
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, StaticText = label, X = x, Y = y, Width = 72, Height = 22, FontSize = 11, Bold = true });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = measure, Format = free ? "{0:F0}% free" : "{0:F0}%", X = x + 70, Y = y, Width = 166, Height = 24, FontSize = 13, Bold = true, CenterText = false });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.Bar, MeasureName = measure, X = x, Y = y + 28, Width = 220, Height = 8, BarMax = 100 });
    }

    public static SkinDefinition CreateUptime()
    {
        var skin = Base("builtin-uptime", "Uptime", 320, 40, 220, 104);
        skin.Measures.Add(new MeasureDefinition { Name = "Up", Type = MeasureType.Uptime });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, StaticText = "UPTIME", X = 16, Y = 14, Width = 188, Height = 20, FontSize = 11, Bold = true });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "Up", Format = "{1}", X = 16, Y = 42, Width = 188, Height = 32, FontSize = 21, Bold = true });
        return skin;
    }

    public static SkinDefinition CreateNetworkMonitor()
    {
        var skin = Base("builtin-network", "Network", 320, 176, 268, 154);
        skin.Measures.Add(new MeasureDefinition { Name = "Down", Type = MeasureType.NetworkDown });
        skin.Measures.Add(new MeasureDefinition { Name = "Up", Type = MeasureType.NetworkUp });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "Down", Format = "↓  {1}", X = 16, Y = 14, Width = 112, Height = 24, FontSize = 13, Bold = true });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "Up", Format = "↑  {1}", X = 140, Y = 14, Width = 112, Height = 24, FontSize = 13, Bold = true });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.Graph, MeasureName = "Down", BarMax = 2048, HistoryLength = 60, X = 16, Y = 50, Width = 236, Height = 82 });
        return skin;
    }

    public static SkinDefinition CreateSystemRings()
    {
        var skin = Base("builtin-system-rings", "System Rings", 612, 40, 300, 150);
        skin.Measures.Add(new MeasureDefinition { Name = "CpuUsage", Type = MeasureType.Cpu });
        skin.Measures.Add(new MeasureDefinition { Name = "MemUsage", Type = MeasureType.Memory });
        skin.Measures.Add(new MeasureDefinition { Name = "DiskUsage", Type = MeasureType.DiskUsed, Target = @"C:\" });
        AddRing(skin, "CpuUsage", "CPU", 16);
        AddRing(skin, "MemUsage", "RAM", 112);
        AddRing(skin, "DiskUsage", "DISK", 208);
        return skin;
    }

    private static void AddRing(SkinDefinition skin, string measure, string label, double x)
    {
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.Ring, MeasureName = measure, X = x, Y = 18, Width = 76, Height = 76, BarMax = 100, ThresholdPercent = 85, ThresholdColorHex = "#E05252" });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = measure, Format = "{0:F0}%", X = x, Y = 43, Width = 76, Height = 28, FontSize = 15, Bold = true, CenterText = true });
        skin.Meters.Add(new MeterDefinition { Kind = MeterKind.String, StaticText = label, X = x, Y = 104, Width = 76, Height = 20, FontSize = 11, Bold = true, CenterText = true });
    }

    public static List<SkinDefinition> CreateAllDefaults() =>
        [CreateClock(), CreateSystemMonitor(), CreateUptime(), CreateNetworkMonitor(), CreateSystemRings()];
}
