using System.Collections.ObjectModel;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Wraps a single <see cref="MeasureDefinition"/> for editing. Like <c>ThemeEditorViewModel</c>'s
/// token properties, each setter writes straight through to the underlying definition — there's
/// no separate "draft" object, the definition IS the draft (it already lives in
/// <see cref="SkinManagerService"/>'s list from the moment the widget was created).
/// </summary>
public sealed class MeasureEditorItem : ViewModelBase
{
    public MeasureDefinition Definition { get; }
    private readonly Action _onChanged;

    private string _name;
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) { Definition.Name = value; _onChanged(); } }
    }

    private MeasureType _type;
    public MeasureType Type
    {
        get => _type;
        set
        {
            if (!SetProperty(ref _type, value)) return;
            Definition.Type = value;
            OnPropertyChanged(nameof(NeedsTarget));
            OnPropertyChanged(nameof(TargetPlaceholder));
            OnPropertyChanged(nameof(IsWebJson));
            _onChanged();
        }
    }

    /// <summary>Gates the presets ComboBox in SkinEditorPage — only WebJson has ready-made
    /// Target values worth quick-filling; every other type's Target is either a single free-typed
    /// value (a drive path) or too personal to ship a preset for (weather city, VibeFinderAI
    /// credentials).</summary>
    public bool IsWebJson => Type == MeasureType.WebJson;

    /// <summary>The quick-fill options for the presets ComboBox, shown only when <see cref="IsWebJson"/>.</summary>
    public static IReadOnlyList<WebJsonPreset> WebJsonPresetOptions => WebJsonPresets.All;

    private string _target;
    public string Target
    {
        get => _target;
        set { if (SetProperty(ref _target, value)) { Definition.Target = value; _onChanged(); } }
    }

    /// <summary>Disk measures need a drive path and Weather measures need a city+API key — every
    /// other measure type is self-contained, so the property panel only shows the field then.</summary>
    public bool NeedsTarget => Type is MeasureType.DiskFree or MeasureType.DiskUsed
        or MeasureType.WeatherTemp or MeasureType.WeatherDesc or MeasureType.WeatherCity or MeasureType.WebJson or MeasureType.CpuCore
        or MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood;

    /// <summary>Hint text for the Target field, since what it means depends on the measure type.</summary>
    public string TargetPlaceholder => Type switch
    {
        MeasureType.WeatherTemp or MeasureType.WeatherDesc or MeasureType.WeatherCity => "City|API key, e.g. London,GB|your_openweathermap_key",
        MeasureType.WebJson => "URL|JSON path, e.g. https://api.example.com/data|results[0].price",
        MeasureType.CpuCore => "Core index, e.g. 0 (blank = core 0)",
        MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood
            => "VibeFinderAI username|password|vibe text (or $theme for the active theme's vibe), e.g. me|mypass|cozy rainy afternoon",
        _ => @"Drive, e.g. C:\",
    };

    public MeasureEditorItem(MeasureDefinition definition, Action onChanged)
    {
        Definition = definition;
        _onChanged = onChanged;
        _name = definition.Name;
        _type = definition.Type;
        _target = definition.Target ?? "";
    }
}

/// <summary>Same wrapping approach as <see cref="MeasureEditorItem"/>, applied to a <see cref="MeterDefinition"/>.</summary>
public sealed class MeterEditorItem : ViewModelBase
{
    public MeterDefinition Definition { get; }
    private readonly Action _onChanged;

    /// <summary>
    /// Fixed at creation. Changing a meter's kind after the fact (String → Bar, say) isn't
    /// supported in this editor — delete it and add the kind you want instead. Every field
    /// below means something different per kind, so allowing an in-place switch would leave
    /// half the panel showing stale, irrelevant values.
    /// </summary>
    public MeterKind Kind { get; }

    public bool IsString => Kind == MeterKind.String;
    public bool IsBar => Kind == MeterKind.Bar;
    public bool IsGraph => Kind == MeterKind.Graph;
    public bool IsIcon => Kind == MeterKind.Icon;
    public bool IsRing => Kind == MeterKind.Ring;
    public bool IsWebEmbed => Kind == MeterKind.WebEmbed;

    /// <summary>Every kind except WebEmbed binds to a live measure (or falls back to static
    /// text) and can launch a click action — a WebEmbed meter has no measure to read and no
    /// synthetic click action of its own; the embedded page handles its own clicks once it's on
    /// screen. Gates the shared "Measure" combo, "Static text" field, and "Click actions" section
    /// in the property panel, which would otherwise show controls that do nothing for this kind.</summary>
    public bool UsesMeasureAndClickFields => !IsWebEmbed;

    /// <summary>Icon and Ring meters share Bar/Graph's "value that reads as full" field — it's
    /// what their threshold-percent check divides against — but not Graph's history-length field.</summary>
    public bool UsesBarMax => Kind is MeterKind.Bar or MeterKind.Graph or MeterKind.Icon or MeterKind.Ring;

    /// <summary>Icon meters use <see cref="IconGlyph"/> instead of a static-text label, so the
    /// generic "Static text" field only makes sense for the other kinds.</summary>
    public bool ShowStaticTextField => IsStaticText && !IsIcon;

    private double _x, _y, _width, _height, _fontSize, _barMax, _thresholdPercent;
    private int _historyLength;
    private string _measureName, _staticText, _format, _thresholdColorHex, _iconGlyph, _webEmbedUrl;
    private string? _actionUrl, _secondaryActionUrl;
    private bool _bold, _thresholdAppliesToText, _centerText;

    public double X { get => _x; set { if (SetProperty(ref _x, value)) { Definition.X = value; _onChanged(); } } }
    public double Y { get => _y; set { if (SetProperty(ref _y, value)) { Definition.Y = value; _onChanged(); } } }
    public double Width { get => _width; set { var v = Math.Max(0, value); if (SetProperty(ref _width, v)) { Definition.Width = v; _onChanged(); } } }
    public double Height { get => _height; set { var v = Math.Max(0, value); if (SetProperty(ref _height, v)) { Definition.Height = v; _onChanged(); } } }
    public double FontSize { get => _fontSize; set { if (SetProperty(ref _fontSize, value)) { Definition.FontSize = value; _onChanged(); } } }
    public double BarMax { get => _barMax; set { if (SetProperty(ref _barMax, value)) { Definition.BarMax = value; _onChanged(); } } }
    public int HistoryLength { get => _historyLength; set { if (SetProperty(ref _historyLength, value)) { Definition.HistoryLength = value; _onChanged(); } } }
    public bool Bold { get => _bold; set { if (SetProperty(ref _bold, value)) { Definition.Bold = value; _onChanged(); } } }
    public bool CenterText { get => _centerText; set { if (SetProperty(ref _centerText, value)) { Definition.CenterText = value; _onChanged(); } } }
    public string StaticText { get => _staticText; set { if (SetProperty(ref _staticText, value)) { Definition.StaticText = value; _onChanged(); } } }
    public string Format { get => _format; set { if (SetProperty(ref _format, value)) { Definition.Format = value; _onChanged(); } } }
    public string IconGlyph { get => _iconGlyph; set { if (SetProperty(ref _iconGlyph, value)) { Definition.IconGlyph = value; _onChanged(); } } }
    public string WebEmbedUrl { get => _webEmbedUrl; set { if (SetProperty(ref _webEmbedUrl, value)) { Definition.WebEmbedUrl = value; _onChanged(); } } }

    // ── Threshold alert ────────────────────────────────────────────────────
    public double ThresholdPercent
    {
        get => _thresholdPercent;
        set { var v = Math.Clamp(value, 0, 100); if (SetProperty(ref _thresholdPercent, v)) { Definition.ThresholdPercent = v; OnPropertyChanged(nameof(HasThreshold)); _onChanged(); } }
    }
    public string ThresholdColorHex
    {
        get => _thresholdColorHex;
        set { if (SetProperty(ref _thresholdColorHex, value)) { Definition.ThresholdColorHex = value; _onChanged(); } }
    }
    public bool ThresholdAppliesToText
    {
        get => _thresholdAppliesToText;
        set { if (SetProperty(ref _thresholdAppliesToText, value)) { Definition.ThresholdAppliesToText = value; _onChanged(); } }
    }
    public bool HasThreshold => ThresholdPercent > 0;

    public string? ActionUrl { get => _actionUrl; set { if (SetProperty(ref _actionUrl, value)) { Definition.ActionUrl = value; _onChanged(); } } }
    public string? SecondaryActionUrl { get => _secondaryActionUrl; set { if (SetProperty(ref _secondaryActionUrl, value)) { Definition.SecondaryActionUrl = value; _onChanged(); } } }

    /// <summary>Empty string means "static text, no measure" — the property panel's "measure" ComboBox
    /// has a matching blank entry for this at the top of its list.</summary>
    public string MeasureName
    {
        get => _measureName;
        set { if (SetProperty(ref _measureName, value)) { Definition.MeasureName = value; OnPropertyChanged(nameof(IsBoundToMeasure)); OnPropertyChanged(nameof(IsStaticText)); _onChanged(); } }
    }

    public bool IsBoundToMeasure => !string.IsNullOrEmpty(MeasureName);
    public bool IsStaticText => !IsBoundToMeasure;

    // ── Live preview (recomputed by the owning ViewModel whenever anything relevant changes) ──
    private string _previewText = "";
    public string PreviewText { get => _previewText; private set => SetProperty(ref _previewText, value); }

    private double _previewFraction;
    public double PreviewFraction { get => _previewFraction; private set => SetProperty(ref _previewFraction, value); }

    public MeterEditorItem(MeterDefinition definition, Action onChanged)
    {
        Definition = definition;
        _onChanged = onChanged;
        Kind = definition.Kind;
        _x = definition.X;
        _y = definition.Y;
        _width = definition.Width;
        _height = definition.Height;
        _fontSize = definition.FontSize;
        _barMax = definition.BarMax;
        _historyLength = definition.HistoryLength;
        _bold = definition.Bold;
        _centerText = definition.CenterText;
        _staticText = definition.StaticText;
        _format = definition.Format;
        _iconGlyph = definition.IconGlyph;
        _webEmbedUrl = definition.WebEmbedUrl ?? "";
        _measureName = definition.MeasureName ?? "";
        _thresholdPercent = definition.ThresholdPercent;
        _thresholdColorHex = definition.ThresholdColorHex;
        _thresholdAppliesToText = definition.ThresholdAppliesToText;
        _actionUrl = definition.ActionUrl;
        _secondaryActionUrl = definition.SecondaryActionUrl;
    }

    /// <summary>Called by the drag handler in the editor's preview canvas — same anchor-delta math
    /// pattern as SkinHostWindow's own drag-to-move, just against local Canvas coordinates instead
    /// of a cross-window AppWindow position.</summary>
    public void MoveTo(double x, double y)
    {
        X = Math.Max(0, x);
        Y = Math.Max(0, y);
    }

    internal void ApplyPreview(string text, double fraction)
    {
        PreviewText = text;
        PreviewFraction = fraction;
    }
}

/// <summary>
/// Computes a representative sample preview for a meter, since the editor has no real running
/// measures to read from. Deliberately approximate — the point is "does this look roughly right
/// and does my Format string parse", not pixel-perfect prediction of live data.
/// </summary>
internal static class MeterPreview
{
    public static (string Text, double Fraction) Compute(MeterEditorItem meter, IEnumerable<MeasureEditorItem> measures)
    {
        if (meter.Kind == MeterKind.Icon)
        {
            string glyphLabel = string.IsNullOrWhiteSpace(meter.IconGlyph) ? "default glyph" : "custom glyph";
            return (meter.IsBoundToMeasure ? $"Icon ({glyphLabel}, bound to {meter.MeasureName})" : $"Icon ({glyphLabel}, static)", 0.0);
        }

        if (meter.Kind == MeterKind.WebEmbed)
        {
            return (string.IsNullOrWhiteSpace(meter.WebEmbedUrl) ? "Web embed (no URL set)" : $"Web embed: {meter.WebEmbedUrl}", 0.0);
        }

        if (!meter.IsBoundToMeasure)
            return (string.IsNullOrEmpty(meter.StaticText) ? "(empty)" : meter.StaticText, 0.0);

        var match = measures.FirstOrDefault(m => m.Name == meter.MeasureName);
        if (match is null)
            return ("(no measure named this)", 0.0);

        var (sampleValue, sampleText) = match.Type switch
        {
            MeasureType.Time => (0.0, "14:32:07"),
            MeasureType.Date => (0.0, "Tue, Jul 28"),
            MeasureType.Uptime => (12_000.0, "3h 20m"),
            MeasureType.NetworkDown or MeasureType.NetworkUp => (450.0, "450 KB/s"),
            MeasureType.Battery => (76.0, "76% (charging)"),
            MeasureType.WeatherTemp => (21.0, "21°"),
            MeasureType.WeatherDesc => (0.0, "light rain"),
            MeasureType.WeatherCity => (0.0, "Seattle,US"),
            MeasureType.MediaTitle => (0.0, "Never Gonna Give You Up"),
            MeasureType.MediaArtist => (0.0, "Rick Astley"),
            MeasureType.MediaState => (0.0, "Playing"),
            MeasureType.VibeTrackTitle => (0.0, "Golden Hour"),
            MeasureType.VibeTrackArtist => (0.0, "JVKE"),
            MeasureType.VibeMood => (0.0, "cozy"),
            _ => (42.0, "42%"), // Cpu, Memory, DiskFree, DiskUsed
        };

        double fraction = meter.BarMax > 0 ? Math.Clamp(sampleValue / meter.BarMax, 0.0, 1.0) : 0.0;
        // Time/Date/Uptime aren't naturally 0-100-ish, so a Bar/Graph meter pointed at one of them
        // (unusual, but not forbidden) would otherwise preview as an empty bar — nudge it to a
        // representative fill instead. Real widgets always use the true live value; this only
        // affects what you see while editing.
        if (meter.UsesBarMax && match.Type is MeasureType.Time or MeasureType.Date or MeasureType.Uptime)
            fraction = 0.6;

        string text;
        try { text = string.Format(meter.Format, sampleValue, sampleText); }
        catch (FormatException) { text = "(invalid format string)"; }

        return (text, fraction);
    }
}

/// <summary>
/// Backs <see cref="Views.SkinEditorPage"/>. Edits a <see cref="SkinDefinition"/> in place —
/// the same "mutate the real object, Save just persists + refreshes the live window" approach
/// <c>ThemeEditorViewModel</c> uses for themes.
/// </summary>
public sealed class SkinEditorViewModel : ViewModelBase
{
    private readonly SkinManagerService _manager;
    private SkinDefinition _working = null!;

    public ObservableCollection<MeasureEditorItem> Measures { get; } = new();
    public ObservableCollection<MeterEditorItem> Meters { get; } = new();

    private string _name = "";
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) { _working.Name = value; Dirty = true; } }
    }

    private double _width = 200;
    public double Width
    {
        get => _width;
        set { var v = Math.Max(0, value); if (SetProperty(ref _width, v)) { _working.Width = v; Dirty = true; } }
    }

    private double _height = 100;
    public double Height
    {
        get => _height;
        set { var v = Math.Max(0, value); if (SetProperty(ref _height, v)) { _working.Height = v; Dirty = true; } }
    }

    private bool _dirty;
    public bool Dirty
    {
        get => _dirty;
        set => SetProperty(ref _dirty, value);
    }

    private bool _snapToGrid = true;
    public bool SnapToGrid
    {
        get => _snapToGrid;
        set => SetProperty(ref _snapToGrid, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private MeterEditorItem? _selectedMeter;
    public MeterEditorItem? SelectedMeter
    {
        get => _selectedMeter;
        set { if (SetProperty(ref _selectedMeter, value)) OnPropertyChanged(nameof(HasSelectedMeter)); }
    }

    /// <summary>Drives the property panel's Visibility — x:Bind can't null-check inline, so this
    /// gives the XAML a plain bool to convert instead.</summary>
    public bool HasSelectedMeter => SelectedMeter is not null;

    /// <summary>Every measure type, for the "Type" ComboBox in the Measures list.</summary>
    public static Array AllMeasureTypes { get; } = Enum.GetValues<MeasureType>();

    public SkinEditorViewModel(SkinManagerService manager) => _manager = manager;

    public void LoadSkin(SkinDefinition skin)
    {
        _working = skin;
        _name = skin.Name;
        _width = skin.Width;
        _height = skin.Height;

        Measures.Clear();
        foreach (var m in skin.Measures)
            Measures.Add(new MeasureEditorItem(m, MarkDirty));

        Meters.Clear();
        foreach (var m in skin.Meters)
            Meters.Add(new MeterEditorItem(m, MarkDirty));

        SelectedMeter = Meters.FirstOrDefault();
        RecomputeAllPreviews();
        Dirty = false;
        OnPropertyChanged(string.Empty);
    }

    private void MarkDirty()
    {
        Dirty = true;
        RecomputeAllPreviews();
    }

    private void RecomputeAllPreviews()
    {
        foreach (var meter in Meters)
        {
            var (text, fraction) = MeterPreview.Compute(meter, Measures);
            meter.ApplyPreview(text, fraction);
        }
    }

    // ── Measures ──────────────────────────────────────────────────────────────

    public void AddMeasure()
    {
        var def = new MeasureDefinition { Name = NextMeasureName(), Type = MeasureType.Cpu };
        _working.Measures.Add(def);
        Measures.Add(new MeasureEditorItem(def, MarkDirty));
        MarkDirty();
    }

    public void RemoveMeasure(MeasureEditorItem item)
    {
        _working.Measures.Remove(item.Definition);
        Measures.Remove(item);
        MarkDirty();
    }

    private string NextMeasureName()
    {
        int i = 1;
        while (Measures.Any(m => m.Name == $"Measure{i}"))
            i++;
        return $"Measure{i}";
    }

    // ── Meters ────────────────────────────────────────────────────────────────

    public void AddMeter(MeterKind kind)
    {
        var def = new MeterDefinition
        {
            Kind = kind,
            X = 16,
            Y = 16,
            Width = kind switch { MeterKind.String => 140, MeterKind.Icon => 28, MeterKind.Ring => 60, MeterKind.WebEmbed => 260, _ => 160 },
            Height = kind switch { MeterKind.Bar => 10, MeterKind.Graph => 60, MeterKind.Icon => 28, MeterKind.Ring => 60, MeterKind.WebEmbed => 200, _ => 22 },
        };
        _working.Meters.Add(def);

        var item = new MeterEditorItem(def, MarkDirty);
        Meters.Add(item);
        SelectedMeter = item;
        MarkDirty();
    }

    public void RemoveMeter(MeterEditorItem item)
    {
        _working.Meters.Remove(item.Definition);
        Meters.Remove(item);
        if (SelectedMeter == item)
            SelectedMeter = Meters.FirstOrDefault();
        MarkDirty();
    }

    // ── Save / Delete ─────────────────────────────────────────────────────────

    public async Task SaveAsync()
    {
        // Auto-enable on save so the widget immediately appears on the desktop —
        // a freshly-generated or newly-edited widget shouldn't require a separate
        // trip to the Widgets page just to flip the Enabled toggle.
        _working.Enabled = true;
        await _manager.SaveSkinAsync(_working);
        Dirty = false;
        StatusMessage = $"\"{_working.Name}\" saved and enabled.";
    }

    public async Task DeleteAsync()
    {
        await _manager.DeleteSkinAsync(_working);
        StatusMessage = $"\"{_working.Name}\" deleted.";
    }
}
