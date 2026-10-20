using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class WidgetStateTests
{
    private static MeterStateInputs WithMeasures(params (string Name, double Value)[] measures) =>
        Inputs(false, false, false, measures);

    private static MeterStateInputs Inputs(bool hover = false, bool pressed = false, bool active = false,
        (string Name, double Value)[]? measures = null)
    {
        var map = (measures ?? Array.Empty<(string Name, double Value)>()).ToDictionary(m => m.Name, m => m.Value, StringComparer.OrdinalIgnoreCase);
        return new MeterStateInputs
        {
            Hovered = hover, Pressed = pressed, WidgetActive = active,
            MeasureValue = name => map.TryGetValue(name, out var v) ? v : null,
        };
    }

    [Fact]
    public void Evaluate_NoStates_IsNeutralWithNoTransition()
    {
        var target = MeterStateEvaluator.Evaluate(null, Inputs(hover: true));
        Assert.Equal(MeterVisualState.Neutral, target.State);
        Assert.Equal(0, target.DurationMs);
        Assert.Equal(MeterVisualState.Neutral, MeterStateEvaluator.Evaluate(new List<MeterStateDefinition>(), Inputs()).State);
    }

    [Fact]
    public void Evaluate_HoverAppliesOnlyWhileHovered()
    {
        var states = new List<MeterStateDefinition>
        {
            new() { Id = "h", Trigger = MeterStateTrigger.Hover, Scale = 1.2, Opacity = 0.8, TransitionMs = 200 },
        };

        var idle = MeterStateEvaluator.Evaluate(states, Inputs(), releaseMs: 90);
        Assert.Equal(MeterVisualState.Neutral, idle.State);
        Assert.Equal(90, idle.DurationMs);

        var hover = MeterStateEvaluator.Evaluate(states, Inputs(hover: true));
        Assert.Equal(1.2, hover.State.Scale);
        Assert.Equal(0.8, hover.State.Opacity);
        Assert.Equal(200, hover.DurationMs);
    }

    [Fact]
    public void Evaluate_HigherPriorityWinsPerProperty_LowerStillContributesOthers()
    {
        var states = new List<MeterStateDefinition>
        {
            new() { Id = "hover", Trigger = MeterStateTrigger.Hover, Priority = 0, Scale = 1.1, Rotation = 5, TransitionMs = 100 },
            new() { Id = "press", Trigger = MeterStateTrigger.Pressed, Priority = 10, Scale = 0.9, TransitionMs = 40 },
        };

        var target = MeterStateEvaluator.Evaluate(states, Inputs(hover: true, pressed: true));
        Assert.Equal(0.9, target.State.Scale);      // pressed wins scale
        Assert.Equal(5, target.State.Rotation);     // hover still supplies rotation
        Assert.Equal(40, target.DurationMs);        // winner's duration
    }

    [Fact]
    public void Evaluate_EqualPriorityLaterListEntryWins()
    {
        var states = new List<MeterStateDefinition>
        {
            new() { Id = "a", Trigger = MeterStateTrigger.Hover, OffsetX = 1 },
            new() { Id = "b", Trigger = MeterStateTrigger.Hover, OffsetX = 7 },
        };
        Assert.Equal(7, MeterStateEvaluator.Evaluate(states, Inputs(hover: true)).State.OffsetX);
    }

    [Theory]
    [InlineData(StateComparison.Above, 80, 80, false)]
    [InlineData(StateComparison.Above, 80, 80.1, true)]
    [InlineData(StateComparison.AtOrAbove, 80, 80, true)]
    [InlineData(StateComparison.Below, 20, 20, false)]
    [InlineData(StateComparison.Below, 20, 19.9, true)]
    [InlineData(StateComparison.AtOrBelow, 20, 20, true)]
    public void Threshold_ComparisonsAreExact(StateComparison comparison, double limit, double reading, bool expected)
    {
        var state = new MeterStateDefinition
        {
            Trigger = MeterStateTrigger.Threshold, MeasureName = "Cpu", Comparison = comparison, Value = limit,
        };
        Assert.Equal(expected, MeterStateEvaluator.IsTriggered(state, WithMeasures(("Cpu", reading))));
    }

    [Fact]
    public void Threshold_UnknownMeasureOrNaNOrMissingNameNeverTriggers()
    {
        var state = new MeterStateDefinition { Trigger = MeterStateTrigger.Threshold, MeasureName = "Cpu", Value = 0 };
        Assert.False(MeterStateEvaluator.IsTriggered(state, Inputs()));
        Assert.False(MeterStateEvaluator.IsTriggered(state, WithMeasures(("Cpu", double.NaN))));
        state.MeasureName = null;
        Assert.False(MeterStateEvaluator.IsTriggered(state, WithMeasures(("Cpu", 100))));
        state.MeasureName = "Cpu";
        Assert.False(MeterStateEvaluator.IsTriggered(state, new MeterStateInputs()));
    }

    [Fact]
    public void Active_UsesWidgetFocusWithoutMeasureAndNonZeroWithMeasure()
    {
        var focus = new MeterStateDefinition { Trigger = MeterStateTrigger.Active };
        Assert.True(MeterStateEvaluator.IsTriggered(focus, Inputs(active: true)));
        Assert.False(MeterStateEvaluator.IsTriggered(focus, Inputs()));

        var playing = new MeterStateDefinition { Trigger = MeterStateTrigger.Active, MeasureName = "Playing" };
        Assert.True(MeterStateEvaluator.IsTriggered(playing, WithMeasures(("Playing", 1))));
        Assert.False(MeterStateEvaluator.IsTriggered(playing, WithMeasures(("Playing", 0))));
        Assert.False(MeterStateEvaluator.IsTriggered(playing, Inputs(active: true))); // focus ignored when a measure is named
    }

    [Fact]
    public void Evaluate_IgnoresInvalidOverridesAndClampsOpacityAndDuration()
    {
        var states = new List<MeterStateDefinition>
        {
            new()
            {
                Trigger = MeterStateTrigger.Hover, Opacity = 4, Scale = -1, Rotation = double.NaN,
                OffsetX = double.PositiveInfinity, ColorHex = "red", TransitionMs = 999_999,
            },
        };
        var target = MeterStateEvaluator.Evaluate(states, Inputs(hover: true));
        Assert.Equal(1.0, target.State.Opacity);   // clamped
        Assert.Equal(1.0, target.State.Scale);     // ignored
        Assert.Equal(0.0, target.State.Rotation);  // ignored
        Assert.Equal(0.0, target.State.OffsetX);   // ignored
        Assert.Null(target.State.ColorHex);        // ignored
        Assert.Equal(MeterStateEvaluator.MaxTransitionMs, target.DurationMs);
    }

    [Theory]
    [InlineData("#FF8800", true, 255, 255, 136, 0)]
    [InlineData("#80FF8800", true, 128, 255, 136, 0)]
    [InlineData("#abc", false, 0, 0, 0, 0)]
    [InlineData("FF8800", false, 0, 0, 0, 0)]
    [InlineData("#GG8800", false, 0, 0, 0, 0)]
    [InlineData(null, false, 0, 0, 0, 0)]
    public void TryParseColor_AcceptsOnlyRgbAndArgb(string? hex, bool ok, int a, int r, int g, int b)
    {
        Assert.Equal(ok, MeterStateEvaluator.TryParseColor(hex, out var c));
        if (!ok) return;
        Assert.Equal(((byte)a, (byte)r, (byte)g, (byte)b), c);
    }

    // ── Transitions ──────────────────────────────────────────────────────────

    private static MeterStateTarget To(double scale, int ms, TransitionEasing easing = TransitionEasing.Linear, string? color = null) =>
        new(MeterVisualState.Neutral with { Scale = scale, ColorHex = color }, ms, easing);

    [Fact]
    public void Tracker_LinearTransitionInterpolatesAndFinishes()
    {
        var tracker = new MeterTransitionTracker();
        Assert.Equal(1.0, tracker.Update(To(2.0, 100), 1000).Scale); // starts at the origin value
        Assert.True(tracker.IsAnimating);
        Assert.Equal(1.5, tracker.Update(To(2.0, 100), 1050).Scale, 6);
        Assert.Equal(2.0, tracker.Update(To(2.0, 100), 1100).Scale);
        Assert.False(tracker.IsAnimating);
        Assert.Equal(2.0, tracker.Update(To(2.0, 100), 5000).Scale);
    }

    [Fact]
    public void Tracker_ZeroDurationSnapsImmediately()
    {
        var tracker = new MeterTransitionTracker();
        Assert.Equal(3.0, tracker.Update(To(3.0, 0), 10).Scale);
        Assert.False(tracker.IsAnimating);
    }

    [Fact]
    public void Tracker_RetargetMidFlightStartsFromCurrentValue()
    {
        var tracker = new MeterTransitionTracker();
        tracker.Update(To(2.0, 100), 0);
        Assert.Equal(1.5, tracker.Update(To(2.0, 100), 50).Scale, 6);

        // Reverse at t=50: must continue from 1.5, not jump to 2.0 or 1.0.
        Assert.Equal(1.5, tracker.Update(To(1.0, 100), 50).Scale, 6);
        Assert.Equal(1.25, tracker.Update(To(1.0, 100), 100).Scale, 6);
        Assert.Equal(1.0, tracker.Update(To(1.0, 100), 150).Scale, 6);
        Assert.False(tracker.IsAnimating);
    }

    [Theory]
    [InlineData(TransitionEasing.Linear, 0.5, 0.5)]
    [InlineData(TransitionEasing.EaseIn, 0.5, 0.25)]
    [InlineData(TransitionEasing.EaseOut, 0.5, 0.75)]
    [InlineData(TransitionEasing.EaseInOut, 0.5, 0.5)]
    [InlineData(TransitionEasing.EaseInOut, 0.25, 0.125)]
    public void Ease_MatchesCurves(TransitionEasing easing, double t, double expected) =>
        Assert.Equal(expected, MeterTransitionTracker.Ease(t, easing), 6);

    [Theory]
    [InlineData(TransitionEasing.Linear)]
    [InlineData(TransitionEasing.EaseIn)]
    [InlineData(TransitionEasing.EaseOut)]
    [InlineData(TransitionEasing.EaseInOut)]
    public void Ease_HasFixedEndpointsAndClampsInput(TransitionEasing easing)
    {
        Assert.Equal(0, MeterTransitionTracker.Ease(-5, easing));
        Assert.Equal(0, MeterTransitionTracker.Ease(0, easing));
        Assert.Equal(1, MeterTransitionTracker.Ease(1, easing), 9);
        Assert.Equal(1, MeterTransitionTracker.Ease(9, easing), 9);
    }

    [Fact]
    public void Tracker_BlendsColorsWhenBothEndsHaveOneAndSnapsAtHalfwayOtherwise()
    {
        var both = new MeterTransitionTracker();
        both.Update(To(1.0, 0, color: "#000000"), 0);
        both.Update(To(1.0, 100, color: "#FF0000"), 0);
        Assert.Equal("#FF800000", both.Update(To(1.0, 100, color: "#FF0000"), 50).ColorHex);

        var gain = new MeterTransitionTracker();
        gain.Update(To(1.0, 100, color: "#FF0000"), 0);
        Assert.Null(gain.Update(To(1.0, 100, color: "#FF0000"), 40).ColorHex);
        Assert.Equal("#FF0000", gain.Update(To(1.0, 100, color: "#FF0000"), 60).ColorHex);
    }

    [Fact]
    public void Tracker_ResetReturnsToNeutral()
    {
        var tracker = new MeterTransitionTracker();
        tracker.Update(To(2.0, 0), 0);
        tracker.Reset();
        Assert.Equal(MeterVisualState.Neutral, tracker.Current);
        Assert.False(tracker.IsAnimating);
    }

    // ── Model plumbing ───────────────────────────────────────────────────────

    [Fact]
    public void Clone_DeepCopiesMeterStates()
    {
        var skin = new SkinDefinition();
        var meter = new MeterDefinition { Id = "m" };
        meter.States.Add(new MeterStateDefinition { Id = "s", Trigger = MeterStateTrigger.Hover, Scale = 1.1, ColorHex = "#FFFFFF" });
        skin.Meters.Add(meter);

        var copy = skin.Clone("copy");
        copy.Meters[0].States[0].Scale = 9;
        copy.Meters[0].States.Add(new MeterStateDefinition());

        Assert.Equal(1.1, skin.Meters[0].States[0].Scale);
        Assert.Single(skin.Meters[0].States);
        Assert.Equal("#FFFFFF", copy.Meters[0].States[0].ColorHex);
    }

    [Fact]
    public void Json_RoundTripsStatesWithStringEnums()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var meter = new MeterDefinition { Id = "m" };
        meter.States.Add(new MeterStateDefinition
        {
            Id = "hot", Trigger = MeterStateTrigger.Threshold, MeasureName = "Cpu",
            Comparison = StateComparison.Above, Value = 90, Easing = TransitionEasing.EaseInOut, ColorHex = "#FF4444",
        });

        var json = JsonSerializer.Serialize(meter, options);
        Assert.Contains("\"Threshold\"", json);
        var back = JsonSerializer.Deserialize<MeterDefinition>(json, options)!;
        var state = Assert.Single(back.States);
        Assert.Equal(MeterStateTrigger.Threshold, state.Trigger);
        Assert.Equal(StateComparison.Above, state.Comparison);
        Assert.Equal(TransitionEasing.EaseInOut, state.Easing);
        Assert.Equal(90, state.Value);
        Assert.Null(state.Scale); // unset overrides stay unset
    }

    [Fact]
    public void LegacyMeterJsonWithoutStatesDeserializesToEmptyList()
    {
        var meter = JsonSerializer.Deserialize<MeterDefinition>("{\"Id\":\"old\"}")!;
        Assert.NotNull(meter.States);
        Assert.Empty(meter.States);
    }
}
