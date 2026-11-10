using System.Globalization;

namespace ThemeManager.Core.Skins;

/// <summary>What causes a <see cref="MeterStateDefinition"/> to become active.</summary>
public enum MeterStateTrigger
{
    /// <summary>The pointer is over the meter.</summary>
    Hover,
    /// <summary>A pointer button is held down on the meter.</summary>
    Pressed,
    /// <summary>
    /// With <see cref="MeterStateDefinition.MeasureName"/> set: the measure's value is non-zero.
    /// Without a measure: the widget window is the active (focused) window.
    /// </summary>
    Active,
    /// <summary>A measure's value satisfies <see cref="MeterStateDefinition.Comparison"/> against <see cref="MeterStateDefinition.Value"/>.</summary>
    Threshold,
}

public enum StateComparison { Above, AtOrAbove, Below, AtOrBelow }

public enum TransitionEasing { Linear, EaseIn, EaseOut, EaseInOut }

/// <summary>
/// A named visual state of a meter. When its trigger holds, its non-null overrides are blended
/// onto the meter's configured look over <see cref="TransitionMs"/>. Overrides are relative to the
/// meter's own settings: opacity and scale multiply, rotation and offsets add.
/// </summary>
public sealed class MeterStateDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public MeterStateTrigger Trigger { get; set; } = MeterStateTrigger.Hover;

    /// <summary>Measure read by <see cref="MeterStateTrigger.Threshold"/> (required) and <see cref="MeterStateTrigger.Active"/> (optional).</summary>
    public string? MeasureName { get; set; }
    public StateComparison Comparison { get; set; } = StateComparison.AtOrAbove;
    public double Value { get; set; }

    /// <summary>When several states are active, higher priority wins each property it sets. Ties go to the later state in the list.</summary>
    public int Priority { get; set; }

    /// <summary>Time to blend into this state. Leaving it blends back using the duration of the state being left.</summary>
    public int TransitionMs { get; set; } = 150;
    public TransitionEasing Easing { get; set; } = TransitionEasing.EaseOut;

    /// <summary>Multiplier on the meter's opacity, 0 to 1.</summary>
    public double? Opacity { get; set; }
    /// <summary>Multiplier on the meter's size, applied around its center.</summary>
    public double? Scale { get; set; }
    /// <summary>Degrees added to the meter's rotation.</summary>
    public double? Rotation { get; set; }
    public double? OffsetX { get; set; }
    public double? OffsetY { get; set; }
    /// <summary>Foreground/fill color override as #RRGGBB or #AARRGGBB.</summary>
    public string? ColorHex { get; set; }

    public MeterStateDefinition Clone() => new()
    {
        Id = Id, Name = Name, Trigger = Trigger, MeasureName = MeasureName, Comparison = Comparison,
        Value = Value, Priority = Priority, TransitionMs = TransitionMs, Easing = Easing,
        Opacity = Opacity, Scale = Scale, Rotation = Rotation, OffsetX = OffsetX, OffsetY = OffsetY,
        ColorHex = ColorHex,
    };
}

/// <summary>Resolved look of a meter at one instant. <see cref="Neutral"/> means "no state applied".</summary>
public readonly record struct MeterVisualState(
    double Opacity, double Scale, double Rotation, double OffsetX, double OffsetY, string? ColorHex)
{
    public static MeterVisualState Neutral { get; } = new(1.0, 1.0, 0.0, 0.0, 0.0, null);
}

/// <summary>Live inputs the evaluator needs. Measure lookups return null for unknown measures.</summary>
public sealed class MeterStateInputs
{
    public bool Hovered { get; init; }
    public bool Pressed { get; init; }
    public bool WidgetActive { get; init; }
    public Func<string, double?>? MeasureValue { get; init; }
}

/// <summary>Where a transition is heading and how to get there.</summary>
public readonly record struct MeterStateTarget(MeterVisualState State, int DurationMs, TransitionEasing Easing);

public static class MeterStateEvaluator
{
    public const int MaxTransitionMs = 10_000;
    public const int DefaultReleaseMs = 150;

    public static bool IsTriggered(MeterStateDefinition state, MeterStateInputs inputs)
    {
        switch (state.Trigger)
        {
            case MeterStateTrigger.Hover:
                return inputs.Hovered;
            case MeterStateTrigger.Pressed:
                return inputs.Pressed;
            case MeterStateTrigger.Active:
                if (string.IsNullOrWhiteSpace(state.MeasureName)) return inputs.WidgetActive;
                var active = inputs.MeasureValue?.Invoke(state.MeasureName);
                return active is { } a && double.IsFinite(a) && a != 0;
            case MeterStateTrigger.Threshold:
                if (string.IsNullOrWhiteSpace(state.MeasureName)) return false;
                var value = inputs.MeasureValue?.Invoke(state.MeasureName);
                if (value is not { } v || !double.IsFinite(v)) return false;
                return state.Comparison switch
                {
                    StateComparison.Above => v > state.Value,
                    StateComparison.AtOrAbove => v >= state.Value,
                    StateComparison.Below => v < state.Value,
                    StateComparison.AtOrBelow => v <= state.Value,
                    _ => false,
                };
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves every triggered state into one target. Lower priorities are applied first so higher
    /// ones override per property. The transition duration and easing come from the winning
    /// (highest-priority, last) triggered state. When nothing is triggered the target is
    /// <see cref="MeterVisualState.Neutral"/> and <paramref name="releaseMs"/>/<paramref name="releaseEasing"/> apply.
    /// </summary>
    public static MeterStateTarget Evaluate(
        IReadOnlyList<MeterStateDefinition>? states,
        MeterStateInputs inputs,
        int releaseMs = DefaultReleaseMs,
        TransitionEasing releaseEasing = TransitionEasing.EaseOut)
    {
        if (states is null || states.Count == 0)
            return new MeterStateTarget(MeterVisualState.Neutral, 0, TransitionEasing.Linear);

        var triggered = new List<(MeterStateDefinition State, int Order)>();
        for (int i = 0; i < states.Count; i++)
            if (states[i] is { } s && IsTriggered(s, inputs))
                triggered.Add((s, i));

        if (triggered.Count == 0)
            return new MeterStateTarget(MeterVisualState.Neutral, ClampDuration(releaseMs), releaseEasing);

        triggered.Sort((a, b) => a.State.Priority != b.State.Priority
            ? a.State.Priority.CompareTo(b.State.Priority)
            : a.Order.CompareTo(b.Order));

        var result = MeterVisualState.Neutral;
        foreach (var (state, _) in triggered)
        {
            result = result with
            {
                Opacity = state.Opacity is { } o && double.IsFinite(o) ? Math.Clamp(o, 0, 1) : result.Opacity,
                Scale = state.Scale is { } sc && double.IsFinite(sc) && sc > 0 ? sc : result.Scale,
                Rotation = state.Rotation is { } r && double.IsFinite(r) ? r : result.Rotation,
                OffsetX = state.OffsetX is { } ox && double.IsFinite(ox) ? ox : result.OffsetX,
                OffsetY = state.OffsetY is { } oy && double.IsFinite(oy) ? oy : result.OffsetY,
                ColorHex = TryParseColor(state.ColorHex, out _) ? state.ColorHex : result.ColorHex,
            };
        }

        var winner = triggered[^1].State;
        return new MeterStateTarget(result, ClampDuration(winner.TransitionMs), winner.Easing);
    }

    /// <summary>Accepts #RRGGBB or #AARRGGBB (case-insensitive).</summary>
    public static bool TryParseColor(string? hex, out (byte A, byte R, byte G, byte B) color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#') return false;
        var digits = hex.AsSpan(1);
        if (digits.Length != 6 && digits.Length != 8) return false;
        if (!uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var raw))
            return false;
        byte a = digits.Length == 8 ? (byte)(raw >> 24) : (byte)255;
        color = (a, (byte)(raw >> 16), (byte)(raw >> 8), (byte)raw);
        return true;
    }

    internal static int ClampDuration(int ms) => Math.Clamp(ms, 0, MaxTransitionMs);
}

/// <summary>
/// Interpolates one meter between visual states. UI-independent and clock-injected: callers pass a
/// monotonic millisecond clock, so behavior is deterministic and unit-testable. Retargeting
/// mid-transition starts from the current blended value, never from a snapped endpoint.
/// </summary>
public sealed class MeterTransitionTracker
{
    private MeterVisualState _from = MeterVisualState.Neutral;
    private MeterVisualState _to = MeterVisualState.Neutral;
    private long _startMs;
    private int _durationMs;
    private TransitionEasing _easing = TransitionEasing.Linear;
    private MeterVisualState _current = MeterVisualState.Neutral;

    public MeterVisualState Current => _current;

    /// <summary>True while the last <see cref="Update"/> had not yet reached its target.</summary>
    public bool IsAnimating { get; private set; }

    public MeterVisualState Update(MeterStateTarget target, long nowMs)
    {
        if (!target.State.Equals(_to))
        {
            _from = Sample(nowMs);
            _to = target.State;
            _startMs = nowMs;
            _durationMs = MeterStateEvaluator.ClampDuration(target.DurationMs);
            _easing = target.Easing;
        }

        _current = Sample(nowMs);
        IsAnimating = !_current.Equals(_to);
        return _current;
    }

    public void Reset()
    {
        _from = _to = _current = MeterVisualState.Neutral;
        _durationMs = 0;
        IsAnimating = false;
    }

    private MeterVisualState Sample(long nowMs)
    {
        if (_durationMs <= 0 || nowMs >= _startMs + _durationMs) return _to;
        if (nowMs <= _startMs) return _from;

        double t = Ease((double)(nowMs - _startMs) / _durationMs, _easing);
        return new MeterVisualState(
            Lerp(_from.Opacity, _to.Opacity, t),
            Lerp(_from.Scale, _to.Scale, t),
            Lerp(_from.Rotation, _to.Rotation, t),
            Lerp(_from.OffsetX, _to.OffsetX, t),
            Lerp(_from.OffsetY, _to.OffsetY, t),
            LerpColor(_from.ColorHex, _to.ColorHex, t));
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double Ease(double t, TransitionEasing easing)
    {
        t = Math.Clamp(t, 0, 1);
        return easing switch
        {
            TransitionEasing.EaseIn => t * t,
            TransitionEasing.EaseOut => 1 - (1 - t) * (1 - t),
            TransitionEasing.EaseInOut => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
            _ => t,
        };
    }

    // A color can only be blended when both ends have one. Gaining or losing an override snaps at
    // the halfway point so the tint appears/disappears in step with the other properties.
    private static string? LerpColor(string? from, string? to, double t)
    {
        bool hasFrom = MeterStateEvaluator.TryParseColor(from, out var f);
        bool hasTo = MeterStateEvaluator.TryParseColor(to, out var c);
        if (hasFrom && hasTo)
        {
            byte Mix(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
            return $"#{Mix(f.A, c.A):X2}{Mix(f.R, c.R):X2}{Mix(f.G, c.G):X2}{Mix(f.B, c.B):X2}";
        }
        return t >= 0.5 ? to : from;
    }
}
