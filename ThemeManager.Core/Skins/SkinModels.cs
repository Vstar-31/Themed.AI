namespace ThemeManager.Core.Skins;

public enum MeasureType
{
    Cpu, CpuCore, Memory, DiskFree, DiskUsed, Time, Date, Uptime, NetworkDown, NetworkUp, Battery,
    MediaTitle, MediaArtist, MediaState, WeatherTemp, WeatherDesc, WeatherCity, WebJson,
    VibeTrackTitle, VibeTrackArtist, VibeMood, VibeTrackProgress, VibePlaybackState
}

public enum MeterKind
{
    String, Bar, Graph, Icon, Ring, WebEmbed,
}

public sealed class MeasureDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public MeasureType Type { get; set; }
    public string? Target { get; set; }
}

public sealed class MeterDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MeterKind Kind { get; set; } = MeterKind.String;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 140;
    public double Height { get; set; } = 22;
    public int ZIndex { get; set; }
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1.0;
    public string? MeasureName { get; set; }
    public string StaticText { get; set; } = "";
    public string Format { get; set; } = "{0:F0}";
    public double FontSize { get; set; } = 13;
    public bool Bold { get; set; }
    public bool CenterText { get; set; }
    public double BarMax { get; set; } = 100;
    public int HistoryLength { get; set; } = 60;
    public string IconGlyph { get; set; } = "";
    public double ThresholdPercent { get; set; }
    public string ThresholdColorHex { get; set; } = "#FF4444";
    public bool ThresholdAppliesToText { get; set; }
    public string? ActionUrl { get; set; }
    public string? SecondaryActionUrl { get; set; }
    public string? WebEmbedUrl { get; set; }
}

public sealed class SkinDefinition
{
    public int SchemaVersion { get; set; } = 3;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Widget";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public double X { get; set; } = 40;
    public double Y { get; set; } = 40;
    public double Width { get; set; } = 220;
    public double Height { get; set; } = 120;
    public double Opacity { get; set; } = 0.90;
    public bool ClickThrough { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool Locked { get; set; }
    public bool DesktopLayer { get; set; }
    public int UpdateIntervalMs { get; set; } = 1000;
    public List<MeasureDefinition> Measures { get; set; } = new();
    public List<MeterDefinition> Meters { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
