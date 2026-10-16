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

            if (!string.IsNullOrWhiteSpace(meter.MeasureName) && !measureNames.Contains(meter.MeasureName))
                issues.Add(new($"meters.{meter.Id}.measureName", $"Measure '{meter.MeasureName}' does not exist."));
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

        return issues;
    }
}
