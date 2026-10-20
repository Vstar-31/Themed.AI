using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// Drives one meter's hover/pressed/active/threshold states. All decisions come from the pure
/// Core evaluator and tracker; this class only owns the WinUI transform and applies each frame's
/// result. Opacity and scale multiply the meter's configured values, rotation and offsets add.
/// Color overrides apply to text meters; other meter kinds ignore <c>ColorHex</c>.
/// </summary>
internal sealed class MeterStateController
{
    private readonly FrameworkElement _element;
    private readonly IReadOnlyList<MeterStateDefinition> _states;
    private readonly Func<string, double?> _measureValue;
    private readonly MeterTransitionTracker _tracker = new();
    private readonly CompositeTransform _transform;
    private readonly double _baseOpacity;
    private readonly double _baseRotation;
    private readonly TextBlock? _text;
    private Brush? _baseForeground;
    private Brush? _stateForeground;
    private string? _appliedColor;
    private int _releaseMs = MeterStateEvaluator.DefaultReleaseMs;
    private TransitionEasing _releaseEasing = TransitionEasing.EaseOut;

    public bool Hovered { get; set; }
    public bool Pressed { get; set; }
    public bool WidgetActive { get; set; }
    public bool IsAnimating => _tracker.IsAnimating;

    public MeterStateController(
        FrameworkElement element,
        MeterDefinition definition,
        IReadOnlyList<MeterStateDefinition> states,
        Func<string, double?> measureValue)
    {
        _element = element;
        _states = states;
        _measureValue = measureValue;
        _text = element as TextBlock;
        _baseOpacity = Math.Clamp(definition.Opacity, 0.0, 1.0);
        _baseRotation = double.IsFinite(definition.Rotation) ? definition.Rotation : 0.0;

        // Replaces any static rotation the host set; the same rotation is re-applied as the base angle.
        _transform = new CompositeTransform
        {
            CenterX = definition.Width / 2,
            CenterY = definition.Height / 2,
            Rotation = _baseRotation,
        };
        element.RenderTransform = _transform;
        element.Opacity = _baseOpacity;
    }

    /// <summary>Re-evaluates the states against current inputs and applies this frame's blended look.</summary>
    public void Evaluate(long nowMs)
    {
        var inputs = new MeterStateInputs
        {
            Hovered = Hovered,
            Pressed = Pressed,
            WidgetActive = WidgetActive,
            MeasureValue = _measureValue,
        };

        var target = MeterStateEvaluator.Evaluate(_states, inputs, _releaseMs, _releaseEasing);
        if (!target.State.Equals(MeterVisualState.Neutral))
        {
            // Leaving a state blends back at that state's own speed.
            _releaseMs = target.DurationMs;
            _releaseEasing = target.Easing;
        }

        Apply(_tracker.Update(target, nowMs));
    }

    private void Apply(MeterVisualState state)
    {
        _transform.ScaleX = state.Scale;
        _transform.ScaleY = state.Scale;
        _transform.Rotation = _baseRotation + state.Rotation;
        _transform.TranslateX = state.OffsetX;
        _transform.TranslateY = state.OffsetY;
        _element.Opacity = Math.Clamp(_baseOpacity * state.Opacity, 0.0, 1.0);
        ApplyColor(state.ColorHex);
    }

    private void ApplyColor(string? hex)
    {
        if (_text is null) return;

        // A meter refresh can change threshold coloring while the state is still active.
        // Preserve that new base brush, then reapply the state tint until it is released.
        if (_appliedColor is not null && !ReferenceEquals(_text.Foreground, _stateForeground))
            _baseForeground = _text.Foreground;
        if (string.Equals(hex, _appliedColor, StringComparison.Ordinal))
        {
            if (_appliedColor is not null) _text.Foreground = _stateForeground;
            return;
        }

        if (hex is not null && MeterStateEvaluator.TryParseColor(hex, out var c))
        {
            // Remember the meter's own brush the first time we override it, so releasing the
            // state restores exactly what the meter (or its threshold coloring) had.
            if (_appliedColor is null) _baseForeground = _text.Foreground;
            _stateForeground = new SolidColorBrush(Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B));
            _text.Foreground = _stateForeground;
            _appliedColor = hex;
        }
        else if (_appliedColor is not null)
        {
            _text.Foreground = _baseForeground;
            _baseForeground = null;
            _stateForeground = null;
            _appliedColor = null;
        }
    }
}
