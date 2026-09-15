using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;

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
    public string? ActionUrl
    {
        get => _actionUrl;
        protected set => SetProperty(ref _actionUrl, value);
    }

    private string? _secondaryActionUrl;
    public string? SecondaryActionUrl
    {
        get => _secondaryActionUrl;
        protected set => SetProperty(ref _secondaryActionUrl, value);
    }

    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        protected set => SetProperty(ref _imageUrl, value);
    }

    public MeterDefinition Definition { get; }

    protected MeterViewModelBase(MeterDefinition definition)
    {
        Definition = definition;
        X = definition.X;
        Y = definition.Y;
        Width = definition.Width;
        Height = definition.Height;
        ThresholdPercent = definition.ThresholdPercent;
        ThresholdColorHex = definition.ThresholdColorHex;
    }

    public abstract void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName);
}

public sealed class StringMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    public double FontSize { get; }
    public bool Bold { get; }
    public bool CenterText { get; }

    private string _displayText = "";
    public string DisplayText
    {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    public StringMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        FontSize = definition.FontSize <= 0 ? 14 : definition.FontSize;
        Bold = definition.Bold;
        CenterText = definition.CenterText;
        DisplayText = definition.StaticText ?? "";
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (_measureName is null || !measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = Definition.ActionUrl;
            SecondaryActionUrl = Definition.SecondaryActionUrl;
            ImageUrl = null;
            DisplayText = Definition.StaticText ?? "";
            return;
        }

        ActionUrl = Definition.ActionUrl ?? measure.ActionUrl;
        SecondaryActionUrl = Definition.SecondaryActionUrl ?? measure.SecondaryActionUrl;
        ImageUrl = measure.ImageUrl;
        DisplayText = measure.Text;

        if (HasThreshold)
            IsThresholdCrossed = (measure.Value >= ThresholdPercent);
    }
}

public sealed class BarMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly double _barMax;

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
            ActionUrl = Definition.ActionUrl;
            SecondaryActionUrl = Definition.SecondaryActionUrl;
            ImageUrl = null;
            return;
        }

        ActionUrl = Definition.ActionUrl ?? measure.ActionUrl;
        SecondaryActionUrl = Definition.SecondaryActionUrl ?? measure.SecondaryActionUrl;
        ImageUrl = measure.ImageUrl;
        FillFraction = Math.Clamp(measure.Value / _barMax, 0.0, 1.0);

        if (HasThreshold)
            IsThresholdCrossed = (FillFraction * 100) >= ThresholdPercent;
    }
}

/// <summary>
/// A single icon glyph — bound to a measure purely so it can recolor on threshold cross, the same
/// way a Bar or Graph meter does, same as a plain static caption. A VibePlaybackState-bound meter
/// swaps between the Play and Pause Segoe Fluent Icons glyphs using the authoritative
/// <see cref="VibeFinderWebState.IsPlaying"/> value whenever the VibeState measure is present.
/// </summary>
public sealed class IconMeterViewModel : MeterViewModelBase
{
    private readonly string? _measureName;
    private readonly double _barMax;

    private string _glyph;
    /// <summary>Segoe Fluent Icons glyph to draw. Falls back to a generic "info" glyph if the
    /// definition was left blank, so a freshly-added Icon meter is never an empty rectangle.</summary>
    public string Glyph
    {
        get => _glyph;
        private set => SetProperty(ref _glyph, value);
    }

    public IconMeterViewModel(MeterDefinition definition) : base(definition)
    {
        _measureName = definition.MeasureName;
        _barMax = definition.BarMax <= 0 ? 100 : definition.BarMax;
        _glyph = string.IsNullOrWhiteSpace(definition.IconGlyph) ? "\uE946" : definition.IconGlyph;
    }

    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName)
    {
        if (string.IsNullOrEmpty(_measureName))
        {
            ActionUrl = Definition.ActionUrl;
            SecondaryActionUrl = Definition.SecondaryActionUrl;
            ImageUrl = null;
            return;
        }

        if (measuresByName.TryGetValue(_measureName, out var measure))
        {
            ActionUrl = Definition.ActionUrl ?? measure.ActionUrl;
            SecondaryActionUrl = Definition.SecondaryActionUrl ?? measure.SecondaryActionUrl;
            ImageUrl = measure.ImageUrl;

            // VibeFinder's web bridge is the authoritative playback state. The measure text can
            // arrive on a different refresh tick, so don't let a stale "PLAYING" value keep the
            // Pause glyph visible after playback has actually stopped.
            if (string.Equals(_measureName, "VibeState", StringComparison.OrdinalIgnoreCase))
                Glyph = VibeFinderWebState.IsPlaying ? "\uE769" : "\uE768"; // Pause : Play
            else if (measure.Text == "PLAYING")
                Glyph = "\uE769"; // Pause icon
            else if (measure.Text == "PAUSED")
                Glyph = "\uE768"; // Play icon

            if (HasThreshold)
                IsThresholdCrossed = (measure.Value / _barMax * 100) >= ThresholdPercent;
        }
    }
}

/// <summary>
/// A live embedded web page — a real WebView2 rendered at the meter's bounds, not a native
/// approximation of one. Unlike every other meter kind here, it has no bound measure to read: the
/// embedded page is its own live thing (VibeFinderAI's compact player, in the one place this is
/// used today), so there's nothing for <see cref="Tick"/> to pull each second the way a Bar or
/// Icon meter pulls a measure's Value/Text. The URL is resolved once at construction and handed
/// to <see cref="Views.SkinHostWindow"/>'s BuildWebEmbedVisual, which owns the actual WebView2
/// control and its navigation.
/// </summary>
public sealed class WebEmbedMeterViewModel : MeterViewModelBase
{
    /// <summary>The URL this embed navigates to. "about:blank" if the definition left it empty,
    /// so a hand-edited skins.json with a blank WebEmbedUrl gets a harmless blank panel instead of
    /// a null-reference navigating the WebView2.</summary>
    public string Url { get; }

    public WebEmbedMeterViewModel(MeterDefinition definition) : base(definition)
    {
        Url = string.IsNullOrWhiteSpace(definition.WebEmbedUrl) ? "about:blank" : definition.WebEmbedUrl;
    }

    // No-op: nothing to re-read on a tick (see class remarks above). ActionUrl/SecondaryActionUrl/
    // ImageUrl all stay at MeterViewModelBase's null defaults, which is correct here — real clicks
    // inside the rendered page go to the WebView2 itself once it's on screen, not through this
    // app's own synthetic click-URL dispatch in SkinHostWindow.
    public override void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName) { }
}
