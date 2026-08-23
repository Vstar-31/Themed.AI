namespace ThemeManager.Core.Skins;

/// <summary>Built-in starter widgets, seeded the first time <see cref="Services.SkinRepository"/> runs.</summary>
public static class SkinDefaults
{
    /// <summary>Digital clock + date.</summary>
    public static SkinDefinition CreateClock() => new()
    {
        Id = "builtin-clock",
        Name = "Cozy Clock",
        X = 40,
        Y = 40,
        Width = 200,
        Height = 92,
        Measures =
        {
            new MeasureDefinition { Name = "Now", Type = MeasureType.Time },
            new MeasureDefinition { Name = "Today", Type = MeasureType.Date },
        },
        Meters =
        {
            new MeterDefinition
            {
                Kind = MeterKind.String, MeasureName = "Now", Format = "{1}",
                X = 16, Y = 14, Width = 168, Height = 36, FontSize = 28, Bold = true,
            },
            new MeterDefinition
            {
                Kind = MeterKind.String, MeasureName = "Today", Format = "{1}",
                X = 16, Y = 56, Width = 168, Height = 20, FontSize = 13,
            },
        }
    };

    /// <summary>CPU / memory / disk usage, each as a label + fill bar.</summary>
    public static SkinDefinition CreateSystemMonitor() => new()
    {
        Id = "builtin-system-monitor",
        Name = "System Monitor",
        X = 40,
        Y = 160,
        Width = 220,
        Height = 148,
        Measures =
        {
            new MeasureDefinition { Name = "CpuUsage",  Type = MeasureType.Cpu },
            new MeasureDefinition { Name = "MemUsage",  Type = MeasureType.Memory },
            new MeasureDefinition { Name = "DiskFree",  Type = MeasureType.DiskFree, Target = @"C:\" },
        },
        Meters =
        {
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "CpuUsage", Format = "CPU  {0:F0}%",  X = 16, Y = 12, Width = 188, Height = 18, FontSize = 13 },
            new MeterDefinition { Kind = MeterKind.Bar,    MeasureName = "CpuUsage", X = 16, Y = 32, Width = 188, Height = 10 },

            new MeterDefinition { Kind = MeterKind.String, MeasureName = "MemUsage", Format = "RAM  {0:F0}%",  X = 16, Y = 54, Width = 188, Height = 18, FontSize = 13 },
            new MeterDefinition { Kind = MeterKind.Bar,    MeasureName = "MemUsage", X = 16, Y = 74, Width = 188, Height = 10 },

            new MeterDefinition { Kind = MeterKind.String, MeasureName = "DiskFree", Format = "C:\\  {0:F0}% free", X = 16, Y = 96, Width = 188, Height = 18, FontSize = 13 },
            new MeterDefinition { Kind = MeterKind.Bar,    MeasureName = "DiskFree", X = 16, Y = 116, Width = 188, Height = 10 },
        }
    };

    /// <summary>Small "how long has this PC been running" widget with a bit of Cozy branding.</summary>
    public static SkinDefinition CreateUptime() => new()
    {
        Id = "builtin-uptime",
        Name = "Uptime",
        X = 280,
        Y = 40,
        Width = 180,
        Height = 76,
        Measures =
        {
            new MeasureDefinition { Name = "Up", Type = MeasureType.Uptime },
        },
        Meters =
        {
            new MeterDefinition { Kind = MeterKind.String, StaticText = "☕  humming along for", X = 14, Y = 12, Width = 152, Height = 18, FontSize = 12 },
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "Up", Format = "{1}", X = 14, Y = 34, Width = 152, Height = 28, FontSize = 20, Bold = true },
        }
    };

    /// <summary>Download speed as a live graph, plus current down/up speed as text.</summary>
    public static SkinDefinition CreateNetworkMonitor() => new()
    {
        Id = "builtin-network",
        Name = "Network",
        X = 280,
        Y = 160,
        Width = 220,
        Height = 128,
        Measures =
        {
            new MeasureDefinition { Name = "Down", Type = MeasureType.NetworkDown },
            new MeasureDefinition { Name = "Up",   Type = MeasureType.NetworkUp },
        },
        Meters =
        {
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "Down", Format = "↓ {1}", X = 16,  Y = 12, Width = 96, Height = 18, FontSize = 13 },
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "Up",   Format = "↑ {1}", X = 114, Y = 12, Width = 96, Height = 18, FontSize = 13 },
            // BarMax = 2048 KB/s (2 MB/s) is a reasonable ceiling for everyday browsing — a bigger
            // download just clips the graph at full height rather than doing anything wrong; the
            // editor lets you raise it if your connection regularly blows past that.
            new MeterDefinition { Kind = MeterKind.Graph, MeasureName = "Down", BarMax = 2048, HistoryLength = 60, X = 16, Y = 36, Width = 188, Height = 74 },
        }
    };

    /// <summary>Three circular gauges (CPU / RAM / Disk), each with a centered percentage over it
    /// and a small caption below — Rainmeter's most-imitated widget pattern (the glowing ring
    /// gauges "Jarvis"/HUD-style skins are built around), and the natural showcase for
    /// <see cref="MeterKind.Ring"/>. Composed the same way every other preset here is —
    /// independent String meters layered on top of the gauge, not a special "ring with a
    /// built-in label" kind. <see cref="MeterDefinition.CenterText"/> (added alongside Ring) is
    /// what makes the percentage actually sit centered over the gauge instead of stuck to its
    /// left edge like every other String meter in this file.</summary>
    public static SkinDefinition CreateSystemRings() => new()
    {
        Id = "builtin-system-rings",
        Name = "System Rings",
        X = 520,
        Y = 40,
        Width = 272,
        Height = 128,
        Measures =
        {
            new MeasureDefinition { Name = "CpuUsage",  Type = MeasureType.Cpu },
            new MeasureDefinition { Name = "MemUsage",  Type = MeasureType.Memory },
            // DiskUsed, not DiskFree (CreateSystemMonitor above uses DiskFree) — DiskFree's value
            // is *free* space, so a high number there is good news. A ring gauge reads as "how
            // full is this", so DiskUsed is the one that makes "full ring = getting concerning"
            // true, and lets all three gauges below share the same 85%-threshold meaning.
            new MeasureDefinition { Name = "DiskUsage", Type = MeasureType.DiskUsed, Target = @"C:\" },
        },
        Meters =
        {
            new MeterDefinition { Kind = MeterKind.Ring,   MeasureName = "CpuUsage", X = 16,  Y = 20, Width = 64, Height = 64, BarMax = 100, ThresholdPercent = 85, ThresholdColorHex = "#E05252" },
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "CpuUsage", Format = "{0:F0}%", X = 16,  Y = 41, Width = 64, Height = 22, FontSize = 15, Bold = true, CenterText = true },
            new MeterDefinition { Kind = MeterKind.String, StaticText = "CPU", X = 16,  Y = 92, Width = 64, Height = 16, FontSize = 11, CenterText = true },

            new MeterDefinition { Kind = MeterKind.Ring,   MeasureName = "MemUsage", X = 104, Y = 20, Width = 64, Height = 64, BarMax = 100, ThresholdPercent = 85, ThresholdColorHex = "#E05252" },
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "MemUsage", Format = "{0:F0}%", X = 104, Y = 41, Width = 64, Height = 22, FontSize = 15, Bold = true, CenterText = true },
            new MeterDefinition { Kind = MeterKind.String, StaticText = "RAM", X = 104, Y = 92, Width = 64, Height = 16, FontSize = 11, CenterText = true },

            new MeterDefinition { Kind = MeterKind.Ring,   MeasureName = "DiskUsage", X = 192, Y = 20, Width = 64, Height = 64, BarMax = 100, ThresholdPercent = 85, ThresholdColorHex = "#E05252" },
            new MeterDefinition { Kind = MeterKind.String, MeasureName = "DiskUsage", Format = "{0:F0}%", X = 192, Y = 41, Width = 64, Height = 22, FontSize = 15, Bold = true, CenterText = true },
            new MeterDefinition { Kind = MeterKind.String, StaticText = "DISK", X = 192, Y = 92, Width = 64, Height = 16, FontSize = 11, CenterText = true },
        }
    };

    /// <summary>All built-in widgets, in the order they should appear on first run.</summary>
    public static List<SkinDefinition> CreateAllDefaults() =>
        [CreateClock(), CreateSystemMonitor(), CreateUptime(), CreateNetworkMonitor(), CreateSystemRings()];
}
