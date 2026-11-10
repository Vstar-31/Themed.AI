namespace ThemeManager.Core.Skins;

/// <summary>
/// Small, UI-independent scheduler used by the desktop widget host. It deliberately knows
/// nothing about WinUI, threads, or measures: it only answers the question "which widgets are due
/// right now?". That makes scheduling deterministic and testable and keeps the platform layer from
/// growing another copy of timing policy.
/// </summary>
public sealed class WidgetTickScheduler
{
    private readonly Dictionary<string, long> _nextDue = new();
    private readonly long _minimumIntervalMs;
    private readonly long _maximumIntervalMs;

    public WidgetTickScheduler(long minimumIntervalMs = 50, long maximumIntervalMs = 60_000)
    {
        if (minimumIntervalMs <= 0 || maximumIntervalMs < minimumIntervalMs)
            throw new ArgumentOutOfRangeException(nameof(minimumIntervalMs));

        _minimumIntervalMs = minimumIntervalMs;
        _maximumIntervalMs = maximumIntervalMs;
    }

    public IReadOnlyList<SkinDefinition> GetDue(IEnumerable<SkinDefinition> skins, long nowMs)
    {
        var due = new List<SkinDefinition>();
        var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skin in skins)
        {
            if (!skin.Enabled || string.IsNullOrWhiteSpace(skin.Id))
                continue;

            activeIds.Add(skin.Id);
            if (!_nextDue.TryGetValue(skin.Id, out var next) || nowMs >= next)
            {
                due.Add(skin);
                var interval = Math.Clamp((long)skin.UpdateIntervalMs, _minimumIntervalMs, _maximumIntervalMs);
                // Schedule from "now" rather than the old deadline. A slow refresh must not create
                // a burst of catch-up work after a temporary stall.
                _nextDue[skin.Id] = nowMs + interval;
            }
        }

        foreach (var staleId in _nextDue.Keys.Where(id => !activeIds.Contains(id)).ToList())
            _nextDue.Remove(staleId);

        return due;
    }

    public void RunImmediately(string skinId, long nowMs)
    {
        if (!string.IsNullOrWhiteSpace(skinId))
            _nextDue[skinId] = nowMs;
    }

    public void Remove(string skinId) => _nextDue.Remove(skinId);

    public void Clear() => _nextDue.Clear();
}

/// <summary>Validation result for hand-authored/imported widget definitions.</summary>
public sealed record WidgetValidationIssue(string Path, string Message, bool IsError = true);

/// <summary>
/// Validates a skin before it reaches the native window layer. The JSON format stays intentionally
/// permissive, but malformed dimensions, duplicate measure names and impossible refresh settings
/// are surfaced early instead of becoming mysterious blank widgets.
/// </summary>
public static class WidgetDefinitionValidator
{
    public static IReadOnlyList<WidgetValidationIssue> Validate(SkinDefinition skin)
    {
        var issues = new List<WidgetValidationIssue>();

        if (string.IsNullOrWhiteSpace(skin.Id))
            issues.Add(new("id", "Widget id cannot be empty."));
        if (string.IsNullOrWhiteSpace(skin.Name))
            issues.Add(new("name", "Widget name cannot be empty."));
        if (!double.IsFinite(skin.X) || !double.IsFinite(skin.Y))
            issues.Add(new("position", "Widget coordinates must be finite."));
        if (!double.IsFinite(skin.Opacity) || skin.Opacity < 0 || skin.Opacity > 1)
            issues.Add(new("opacity", "Widget opacity must be between 0 and 1."));
        if (!double.IsFinite(skin.Width) || !double.IsFinite(skin.Height) || skin.Width <= 0 || skin.Height <= 0)
            issues.Add(new("size", "Widget width and height must be greater than zero."));
        if (skin.UpdateIntervalMs < 50 || skin.UpdateIntervalMs > 60_000)
            issues.Add(new("updateIntervalMs", "Refresh interval must be between 50 and 60000 ms."));

        if (skin.Measures is null || skin.Meters is null || skin.Groups is null
            || skin.Measures.Any(m => m is null) || skin.Meters.Any(m => m is null) || skin.Groups.Any(g => g is null))
        {
            issues.Add(new("collections", "Measures, meters and groups must be arrays without null items."));
            return issues;
        }
        foreach (var measure in skin.Measures)
            if (!Enum.IsDefined(measure.Type))
                issues.Add(new($"measures.{measure.Id}.type", "Unknown measure type."));
        foreach (var group in skin.Meters.GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase))
            if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                issues.Add(new("meters.id", "Meter ids must be unique and non-empty."));
        foreach (var group in skin.Groups.GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase))
            if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                issues.Add(new("groups.id", "Group ids must be unique and non-empty."));

        var duplicateMeasures = skin.Measures
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1);
        foreach (var group in duplicateMeasures)
            issues.Add(new($"measures.{group.Key}", "Measure names must be unique and non-empty."));

        foreach (var formula in skin.Measures.Where(m => m.Type == MeasureType.Formula))
        {
            if (string.IsNullOrWhiteSpace(formula.Expression))
            {
                issues.Add(new($"measures.{formula.Name}.expression", "Formula expression cannot be empty."));
                continue;
            }

            if (!SafeExpressionEvaluator.TryEvaluate(formula.Expression, _ => 0, out _))
                issues.Add(new($"measures.{formula.Name}.expression", "Formula expression has invalid syntax or unsupported functions."));
        }

        var measureNames = skin.Measures
            .Select(m => m.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var meter in skin.Meters)
        {
            if (!Enum.IsDefined(meter.Kind))
                issues.Add(new($"meters.{meter.Id}.kind", "Unknown meter kind."));
            if (!double.IsFinite(meter.X) || !double.IsFinite(meter.Y) || !double.IsFinite(meter.Rotation))
                issues.Add(new($"meters.{meter.Id}.position", "Meter coordinates and rotation must be finite."));
            if (!double.IsFinite(meter.Opacity) || meter.Opacity < 0 || meter.Opacity > 1)
                issues.Add(new($"meters.{meter.Id}.opacity", "Meter opacity must be between 0 and 1."));
            if (!double.IsFinite(meter.Width) || !double.IsFinite(meter.Height) || meter.Width <= 0 || meter.Height <= 0)
                issues.Add(new($"meters.{meter.Id}.size", "Meter width and height must be greater than zero."));

            if (!double.IsFinite(meter.FontSize) || meter.FontSize <= 0 || meter.FontSize > 512)
                issues.Add(new($"meters.{meter.Id}.fontSize", "Font size must be between 1 and 512."));
            if (!double.IsFinite(meter.BarMax) || meter.BarMax <= 0)
                issues.Add(new($"meters.{meter.Id}.barMax", "The full-scale value must be greater than zero."));
            if (!double.IsFinite(meter.ThresholdPercent) || meter.ThresholdPercent < 0 || meter.ThresholdPercent > 100)
                issues.Add(new($"meters.{meter.Id}.thresholdPercent", "Threshold must be between 0 and 100."));
            if (meter.HistoryLength < 1 || meter.HistoryLength > 10000)
                issues.Add(new($"meters.{meter.Id}.historyLength", "Graph history must contain between 1 and 10000 samples."));
            try { _ = string.Format(meter.Format ?? "{0}", 0d, "preview"); }
            catch (FormatException)
            {
                issues.Add(new($"meters.{meter.Id}.format", "Invalid display format. Use {0} for a number or {1} for text."));
            }

            if (!string.IsNullOrWhiteSpace(meter.MeasureName) && !measureNames.Contains(meter.MeasureName))
                issues.Add(new($"meters.{meter.Id}.measureName", $"Measure '{meter.MeasureName}' does not exist."));

            ValidateStates(meter, measureNames, issues);
        }

        var meterIds = skin.Meters
            .Select(m => m.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateGroupMeters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in skin.Groups ?? new List<WidgetGroupDefinition>())
        {
            if (string.IsNullOrWhiteSpace(group.Id))
                issues.Add(new($"groups.{group.Name}.id", "Group id cannot be empty."));
            if (!double.IsFinite(group.X) || !double.IsFinite(group.Y) || !double.IsFinite(group.Rotation))
                issues.Add(new($"groups.{group.Id}.position", "Group coordinates and rotation must be finite."));
            if (!double.IsFinite(group.Width) || !double.IsFinite(group.Height) || group.Width <= 0 || group.Height <= 0)
                issues.Add(new($"groups.{group.Id}.size", "Group width and height must be greater than zero."));
            if (!double.IsFinite(group.ScaleX) || !double.IsFinite(group.ScaleY) || group.ScaleX <= 0 || group.ScaleY <= 0)
                issues.Add(new($"groups.{group.Id}.scale", "Group scale must be greater than zero."));
            if (!double.IsFinite(group.Opacity) || group.Opacity < 0 || group.Opacity > 1)
                issues.Add(new($"groups.{group.Id}.opacity", "Group opacity must be between 0 and 1."));

            foreach (var meterId in group.MeterIds ?? new List<string>())
            {
                if (!meterIds.Contains(meterId))
                    issues.Add(new($"groups.{group.Id}.meterIds", $"Meter '{meterId}' does not exist."));
                else if (!duplicateGroupMeters.Add(meterId))
                    issues.Add(new($"groups.{group.Id}.meterIds", $"Meter '{meterId}' belongs to more than one group."));
            }
        }

        ValidateAnchor(skin.Anchor, issues);
        return issues;
    }

    public const int MaxStatesPerMeter = 16;
    public const int MaxResponsiveRules = 16;
    private const double MaxCoordinate = 100_000;

    private static void ValidateStates(MeterDefinition meter, HashSet<string> measureNames, List<WidgetValidationIssue> issues)
    {
        if (meter.States is null || meter.States.Count == 0) return;
        string path = $"meters.{meter.Id}.states";

        if (meter.States.Count > MaxStatesPerMeter)
            issues.Add(new(path, $"A meter can have at most {MaxStatesPerMeter} states."));
        if (meter.States.Any(st => st is null))
        {
            issues.Add(new(path, "State entries cannot be null."));
            return;
        }

        foreach (var group in meter.States.GroupBy(st => st.Id, StringComparer.OrdinalIgnoreCase))
            if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                issues.Add(new(path, "State ids must be unique and non-empty."));

        foreach (var state in meter.States)
        {
            string sp = $"{path}.{state.Id}";
            if (!Enum.IsDefined(state.Trigger))
                issues.Add(new($"{sp}.trigger", "Unknown state trigger."));
            if (!Enum.IsDefined(state.Comparison))
                issues.Add(new($"{sp}.comparison", "Unknown state comparison."));
            if (!Enum.IsDefined(state.Easing))
                issues.Add(new($"{sp}.easing", "Unknown transition easing."));

            bool hasMeasure = !string.IsNullOrWhiteSpace(state.MeasureName);
            if (state.Trigger == MeterStateTrigger.Threshold && !hasMeasure)
                issues.Add(new($"{sp}.measureName", "Threshold states need a measure."));
            if (hasMeasure && !measureNames.Contains(state.MeasureName!))
                issues.Add(new($"{sp}.measureName", $"Measure '{state.MeasureName}' does not exist."));
            if (!double.IsFinite(state.Value))
                issues.Add(new($"{sp}.value", "State threshold value must be finite."));
            if (state.TransitionMs < 0 || state.TransitionMs > MeterStateEvaluator.MaxTransitionMs)
                issues.Add(new($"{sp}.transitionMs", $"Transition must be between 0 and {MeterStateEvaluator.MaxTransitionMs} ms."));

            if (state.Opacity is { } o && (!double.IsFinite(o) || o < 0 || o > 1))
                issues.Add(new($"{sp}.opacity", "State opacity must be between 0 and 1."));
            if (state.Scale is { } sc && (!double.IsFinite(sc) || sc <= 0 || sc > 10))
                issues.Add(new($"{sp}.scale", "State scale must be greater than 0 and at most 10."));
            if (state.Rotation is { } r && !double.IsFinite(r))
                issues.Add(new($"{sp}.rotation", "State rotation must be finite."));
            if (state.OffsetX is { } ox && (!double.IsFinite(ox) || Math.Abs(ox) > MaxCoordinate)
                || state.OffsetY is { } oy && (!double.IsFinite(oy) || Math.Abs(oy) > MaxCoordinate))
                issues.Add(new($"{sp}.offset", "State offsets must be finite."));
            if (!string.IsNullOrWhiteSpace(state.ColorHex) && !MeterStateEvaluator.TryParseColor(state.ColorHex, out _))
                issues.Add(new($"{sp}.colorHex", "State color must be #RRGGBB or #AARRGGBB."));
        }
    }

    private static void ValidateAnchor(WidgetAnchorDefinition? anchor, List<WidgetValidationIssue> issues)
    {
        if (anchor is null) return;

        if (!Enum.IsDefined(anchor.Edge))
            issues.Add(new("anchor.edge", "Unknown anchor edge."));
        if (!Enum.IsDefined(anchor.Monitor))
            issues.Add(new("anchor.monitor", "Unknown anchor monitor."));
        if (!double.IsFinite(anchor.MarginX) || !double.IsFinite(anchor.MarginY)
            || Math.Abs(anchor.MarginX) > MaxCoordinate || Math.Abs(anchor.MarginY) > MaxCoordinate)
            issues.Add(new("anchor.margin", "Anchor margins must be finite."));

        if (anchor.Responsive is null || anchor.Responsive.Count == 0) return;
        if (anchor.Responsive.Count > MaxResponsiveRules)
            issues.Add(new("anchor.responsive", $"An anchor can have at most {MaxResponsiveRules} responsive rules."));
        if (anchor.Responsive.Any(r => r is null))
        {
            issues.Add(new("anchor.responsive", "Responsive rules cannot be null."));
            return;
        }

        foreach (var group in anchor.Responsive.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
            if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                issues.Add(new("anchor.responsive.id", "Responsive rule ids must be unique and non-empty."));

        foreach (var rule in anchor.Responsive)
        {
            string rp = $"anchor.responsive.{rule.Id}";
            if (!double.IsFinite(rule.MaxWorkAreaWidth) || rule.MaxWorkAreaWidth < 0
                || !double.IsFinite(rule.MaxWorkAreaHeight) || rule.MaxWorkAreaHeight < 0)
                issues.Add(new($"{rp}.limits", "Work-area limits must be zero or greater."));
            else if (rule.MaxWorkAreaWidth == 0 && rule.MaxWorkAreaHeight == 0)
                issues.Add(new($"{rp}.limits", "Rule has no size limit and always applies.", IsError: false));
            if (rule.Edge is { } edge && !Enum.IsDefined(edge))
                issues.Add(new($"{rp}.edge", "Unknown anchor edge."));
            if (rule.Scale is { } scale && (!double.IsFinite(scale) || scale <= 0 || scale > 4))
                issues.Add(new($"{rp}.scale", "Responsive scale must be greater than 0 and at most 4."));
            if (rule.MarginX is { } mx && (!double.IsFinite(mx) || Math.Abs(mx) > MaxCoordinate)
                || rule.MarginY is { } my && (!double.IsFinite(my) || Math.Abs(my) > MaxCoordinate))
                issues.Add(new($"{rp}.margin", "Responsive margins must be finite."));
        }
    }
}
