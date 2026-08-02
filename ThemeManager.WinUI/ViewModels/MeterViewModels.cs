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

    protected MeterViewModelBase(MeterDefinition definition)
    {
        X = definition.X;
        Y = definition.Y;
        Width = definition.Width;
        Height = definition.Height;
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

    private string _displayText;
    public string DisplayText
    {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    public StringMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _format = definition.Format;
        _staticText = definition.StaticText;
        FontSize = definition.FontSize;
        Bold = definition.Bold;
        _displayText = string.IsNullOrEmpty(_measureName) ? _staticText : "";
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (string.IsNullOrEmpty(_measureName))
        {
            DisplayText = _staticText;
            return;
        }

        if (measuresByName.TryGetValue(_measureName, out var measure))
        {
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
            return;

        _history.Enqueue(Math.Clamp(measure.Value / _barMax, 0.0, 1.0));
        while (_history.Count > _historyLength)
            _history.Dequeue();

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
            return;

        FillFraction = Math.Clamp(measure.Value / _barMax, 0.0, 1.0);
    }
}
