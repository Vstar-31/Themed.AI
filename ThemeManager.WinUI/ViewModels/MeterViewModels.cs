using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Shared position/size for anything drawn inside a <see cref="Views.SkinHostWindow"/>.
/// Concrete meter kinds (<see cref="StringMeterViewModel"/>, <see cref="BarMeterViewModel"/>)
/// add whatever extra bindable state their visual needs.
/// </summary>
public abstract class MeterViewModelBase : ViewModelBase
{
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    /// <summary>Threshold percentage (0 = disabled) from the definition.</summary>
    public double ThresholdPercent { get; }

    /// <summary>Hex color string to use when the threshold is crossed.</summary>
    public string ThresholdColorHex { get; }

    /// <summary>True when the measure's current value exceeds <see cref="ThresholdPercent"/>.
    /// The rendering layer subscribes to this to swap fill/stroke/foreground colors.</summary>
    private bool _isThresholdCrossed;
    public bool IsThresholdCrossed
    {
        get => _isThresholdCrossed;
        protected set => SetProperty(ref _isThresholdCrossed, value);
    }

    /// <summary>Whether this meter has a non-zero threshold configured at all.</summary>
    public bool HasThreshold => ThresholdPercent > 0;

    private string? _actionUrl;
    /// <summary>Optional URL to open when this meter is clicked.</summary>
    public string? ActionUrl
    {
        get => _actionUrl;
        protected set => SetProperty(ref _actionUrl, value);
    }

    protected MeterViewModelBase(MeterDefinition definition)
    {
        X = definition.X;
        Y = definition.Y;
        Width = definition.Width;
        Height = definition.Height;
        ThresholdPercent = definition.ThresholdPercent;
        ThresholdColorHex = definition.ThresholdColorHex;
    }

    /// <summary>Re-reads its bound measure (if any) and refreshes bindable text/value. Called every tick.</summary>
    public abstract void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName);
}

/// <summary>A text label — either a static caption or formatted from a measure's live value/text.</summary>
public sealed class StringMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly string _format;
    private readonly string _staticText;

    public double FontSize { get; }
    public bool Bold { get; }
    public bool CenterText { get; }

    private string _displayText;
    public string DisplayText
    {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    private readonly bool _thresholdAppliesToText;
    private readonly double _barMax;

    public StringMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _format = definition.Format;
        _staticText = definition.StaticText;
        FontSize = definition.FontSize;
        Bold = definition.Bold;
        CenterText = definition.CenterText;
        _thresholdAppliesToText = definition.ThresholdAppliesToText;
        _barMax = definition.BarMax <= 0 ? 100 : definition.BarMax;
        _displayText = string.IsNullOrEmpty(_measureName) ? _staticText : "";
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (string.IsNullOrEmpty(_measureName))
        {
            DisplayText = _staticText;
            IsThresholdCrossed = false;
            ActionUrl = null;
            return;
        }

        if (measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = measure.ActionUrl;
            try
            {
                DisplayText = string.Format(_format, measure.Value, measure.Text);
            }
            catch (FormatException)
            {
                // A hand-edited skins.json with a bad Format string shouldn't crash the widget —
                // fall back to the measure's own plain text.
                DisplayText = measure.Text;
            }

            if (HasThreshold && _thresholdAppliesToText)
                IsThresholdCrossed = (measure.Value / _barMax * 100) >= ThresholdPercent;
        }
    }
}

/// <summary>
/// A scrolling line graph of a measure's recent values, normalized against
/// <see cref="MeterDefinition.BarMax"/>. Keeps its own small ring buffer — the rendering side
/// (<see cref="Views.SkinHostWindow"/>) subscribes to <see cref="HistoryUpdated"/> and redraws.
/// </summary>
public sealed class GraphMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly double _barMax;
    private readonly int _historyLength;
    private readonly Queue<double> _history = new();

    /// <summary>Raised after every <see cref="Tick"/> that has a new sample to show.</summary>
    public event Action? HistoryUpdated;

    public GraphMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _barMax = definition.BarMax <= 0 ? 100 : definition.BarMax;
        _historyLength = definition.HistoryLength <= 1 ? 60 : definition.HistoryLength;
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (_measureName is null || !measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = null;
            return;
        }

        ActionUrl = measure.ActionUrl;
        double normalized = Math.Clamp(measure.Value / _barMax, 0.0, 1.0);
        _history.Enqueue(normalized);
        while (_history.Count > _historyLength)
            _history.Dequeue();

        if (HasThreshold)
            IsThresholdCrossed = (normalized * 100) >= ThresholdPercent;

        HistoryUpdated?.Invoke();
    }

    /// <summary>Current samples, oldest first, each already normalized to 0.0–1.0.</summary>
    public double[] Snapshot() => _history.ToArray();
}

/// <summary>A horizontal fill bar showing a measure's value against <see cref="MeterDefinition.BarMax"/>.</summary>
public sealed class BarMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly double _barMax;

    /// <summary>0.0–1.0 fill fraction, ready to multiply straight into a bar's pixel width.</summary>
    private double _fillFraction;
    public double FillFraction
    {
        get => _fillFraction;
        private set => SetProperty(ref _fillFraction, value);
    }

    public BarMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _barMax = definition.BarMax <= 0 ? 100 : definition.BarMax;
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (_measureName is null || !measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = null;
            return;
        }

        ActionUrl = measure.ActionUrl;
        FillFraction = Math.Clamp(measure.Value / _barMax, 0.0, 1.0);

        if (HasThreshold)
            IsThresholdCrossed = (FillFraction * 100) >= ThresholdPercent;
    }
}

/// <summary>
/// A circular percentage gauge — same fill-fraction-from-BarMax data as
/// <see cref="BarMeterViewModel"/>, kept as a separate class rather than reusing it so the
/// rendering-side pattern match (<c>meter switch { BarMeterViewModel ... }</c>) can tell the two
/// shapes apart without an extra Kind check.
/// </summary>
public sealed class RingMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly double _barMax;

    /// <summary>0.0–1.0 fill fraction, ready to convert into an arc sweep angle.</summary>
    private double _fillFraction;
    public double FillFraction
    {
        get => _fillFraction;
        private set => SetProperty(ref _fillFraction, value);
    }

    public RingMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _barMax = definition.BarMax <= 0 ? 100 : definition.BarMax;
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (_measureName is null || !measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = null;
            return;
        }

        ActionUrl = measure.ActionUrl;
        FillFraction = Math.Clamp(measure.Value / _barMax, 0.0, 1.0);

        if (HasThreshold)
            IsThresholdCrossed = (FillFraction * 100) >= ThresholdPercent;
    }
}

/// <summary>
/// A single static icon glyph — optionally bound to a measure purely so it can recolor on
/// threshold cross, the same way a Bar or Graph meter does. Unlike those two, the glyph itself
/// never changes at runtime; only its color does.
/// </summary>
public sealed class IconMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly double _barMax;

    /// <summary>Segoe Fluent Icons glyph to draw. Falls back to a generic "info" glyph if the
    /// definition was left blank, so a freshly-added Icon meter is never an empty rectangle.</summary>
    public string Glyph { get; }

    public IconMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _barMax = definition.BarMax <= 0 ? 100 : definition.BarMax;
        Glyph = string.IsNullOrWhiteSpace(definition.IconGlyph) ? "\uE946" : definition.IconGlyph;
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (string.IsNullOrEmpty(_measureName))
        {
            ActionUrl = null;
            return;
        }

        if (measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = measure.ActionUrl;
            if (HasThreshold)
                IsThresholdCrossed = (measure.Value / _barMax * 100) >= ThresholdPercent;
        }
    }
}
