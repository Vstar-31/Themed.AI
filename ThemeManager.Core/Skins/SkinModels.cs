namespace ThemeManager.Core.Skins;

/// <summary>
/// The kind of live system value a <see cref="MeasureDefinition"/> reads.
/// Mirrors the "measure" concept from Rainmeter-style desktop widgets.
/// </summary>
public enum MeasureType
{
    Cpu,
    CpuCore,
    Memory,
    DiskFree,
    DiskUsed,
    Time,
    Date,
    Uptime,
    NetworkDown,
    NetworkUp,
    Battery,
    MediaTitle,
    MediaArtist,
    MediaState,
    WeatherTemp,
    WeatherDesc,
    WeatherCity,
    WebJson,
    VibeTrackTitle,
    VibeTrackArtist,
    VibeMood,
}

/// <summary>The visual kind a <see cref="MeterDefinition"/> renders as.</summary>
public enum MeterKind
{
    /// <summary>A text label, optionally formatted from a measure's value.</summary>
    String,

    /// <summary>A horizontal fill bar showing a measure's value against <see cref="MeterDefinition.BarMax"/>.</summary>
    Bar,

    /// <summary>A scrolling line graph of a measure's recent values, against <see cref="MeterDefinition.BarMax"/>.</summary>
    Graph,

    /// <summary>A single Segoe Fluent Icons glyph (see <see cref="MeterDefinition.IconGlyph"/>),
    /// optionally recolored on threshold cross like a Bar/Graph meter.</summary>
    Icon,
}

/// <summary>
/// A single live data source inside a skin (e.g. "CpuUsage" → CPU %).
/// Referenced by name from one or more <see cref="MeterDefinition"/>s.
/// </summary>
public sealed class MeasureDefinition
{
    /// <summary>Unique (within the skin) name other meters reference via <see cref="MeterDefinition.MeasureName"/>.</summary>
    public string Name { get; set; } = "";

    public MeasureType Type { get; set; }

    /// <summary>
    /// Extra context some measure types need — currently only the drive path for
    /// <see cref="MeasureType.DiskFree"/>/<see cref="MeasureType.DiskUsed"/> (e.g. "C:\").
    /// Unused (and safely ignored) by every other measure type.
    /// </summary>
    public string? Target { get; set; }
}

/// <summary>
/// A single visual element inside a skin, positioned in the skin's own coordinate space
/// (0,0 = top-left of the widget window).
/// </summary>
public sealed class MeterDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MeterKind Kind { get; set; } = MeterKind.String;

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 140;
    public double Height { get; set; } = 22;

    /// <summary>
    /// Name of the <see cref="MeasureDefinition"/> this meter reads from.
    /// Leave null/empty for a purely static label (uses <see cref="StaticText"/> instead).
    /// </summary>
    public string? MeasureName { get; set; }

    /// <summary>Text shown when <see cref="MeasureName"/> is empty — a plain, unchanging label.</summary>
    public string StaticText { get; set; } = "";

    /// <summary>
    /// String.Format pattern applied as <c>string.Format(Format, measure.Value, measure.Text)</c>.
    /// {0} is the measure's raw numeric value, {1} is its already-formatted text (e.g. a clock face).
    /// Only used by <see cref="MeterKind.String"/>. Example: "CPU {0:F0}%" or just "{1}" for a clock.
    /// </summary>
    public string Format { get; set; } = "{0:F0}";

    public double FontSize { get; set; } = 13;
    public bool Bold { get; set; }

    /// <summary>Value a <see cref="MeterKind.Bar"/>/<see cref="MeterKind.Graph"/> meter treats as "100% full".</summary>
    public double BarMax { get; set; } = 100;

    /// <summary>How many recent samples a <see cref="MeterKind.Graph"/> meter keeps on screen. Ignored otherwise.</summary>
    public int HistoryLength { get; set; } = 60;

    /// <summary>
    /// The glyph a <see cref="MeterKind.Icon"/> meter draws — a single Segoe Fluent Icons
    /// character (paste one from Windows' Character Map, font "Segoe Fluent Icons", or type a
    /// "\uXXXX" escape in code). Ignored by every other meter kind. Falls back to a generic info
    /// glyph when left blank so a newly-added Icon meter is never invisible.
    /// </summary>
    public string IconGlyph { get; set; } = "";

    // ── Threshold alert ───────────────────────────────────────────────────
    /// <summary>When the measure value exceeds this percentage of <see cref="BarMax"/>, the meter
    /// switches to <see cref="ThresholdColorHex"/>. Set to 0 to disable.</summary>
    public double ThresholdPercent { get; set; }

    /// <summary>Hex color to use when the threshold is crossed (e.g. "#FF4444").
    /// Ignored when <see cref="ThresholdPercent"/> is 0.</summary>
    public string ThresholdColorHex { get; set; } = "#FF4444";

    /// <summary>When true, string meters also swap their foreground color on threshold cross.
    /// Bar/Graph meters always swap — this flag only gates text meters.</summary>
    public bool ThresholdAppliesToText { get; set; }
}

/// <summary>
/// A complete desktop widget: a small always-on-top window, its measures, and its meters.
/// Persisted as JSON by <see cref="Services.SkinRepository"/> — the whole file is hand-editable,
/// the same way a Rainmeter skin's .ini file is.
/// </summary>
public sealed class SkinDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Widget";
    public bool Enabled { get; set; } = true;

    // ── Position & size (screen pixels, top-left origin) ────────────────────
    public double X { get; set; } = 40;
    public double Y { get; set; } = 40;
    public double Width { get; set; } = 220;
    public double Height { get; set; } = 120;

    // ── Presentation ─────────────────────────────────────────────────────────
    /// <summary>Card background translucency, 0.0 (invisible) – 1.0 (solid). Text/bars stay fully opaque.</summary>
    public double Opacity { get; set; } = 0.90;

    /// <summary>When true, mouse clicks pass through the widget to whatever is beneath it.</summary>
    public bool ClickThrough { get; set; }

    /// <summary>When true, the widget can't be dragged (safety net once you like where it sits).</summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Experimental "Rainmeter behind icons" mode — attaches the widget behind the desktop
    /// icons instead of floating always-on-top. Relies on an undocumented Explorer trick that's
    /// confirmed flaky on some Windows 11 builds; if it can't attach, the widget silently stays
    /// always-on-top instead (see DesktopLayerInterop for the full story).
    /// </summary>
    public bool DesktopLayer { get; set; }

    /// <summary>
    /// Reserved for future per-skin refresh cadence. Phase 1 ticks every active skin on one shared
    /// 1-second timer in <c>SkinManagerService</c> regardless of this value — see the roadmap notes.
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 1000;

    public List<MeasureDefinition> Measures { get; set; } = new();
    public List<MeterDefinition> Meters { get; set; } = new();
}
