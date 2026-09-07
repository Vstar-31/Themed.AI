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
        if (skin.Width <= 0 || skin.Height <= 0)
            issues.Add(new("size", "Widget width and height must be greater than zero."));
        if (skin.UpdateIntervalMs < 50)
            issues.Add(new("updateIntervalMs", "Refresh interval must be at least 50 ms."));

        var duplicateMeasures = skin.Measures
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1);
        foreach (var group in duplicateMeasures)
            issues.Add(new($"measures.{group.Key}", "Measure names must be unique and non-empty."));

        var measureNames = skin.Measures
            .Select(m => m.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var meter in skin.Meters)
        {
            if (meter.Width <= 0 || meter.Height <= 0)
                issues.Add(new($"meters.{meter.Id}.size", "Meter width and height must be greater than zero."));

            if (!string.IsNullOrWhiteSpace(meter.MeasureName) && !measureNames.Contains(meter.MeasureName))
                issues.Add(new($"meters.{meter.Id}.measureName", $"Measure '{meter.MeasureName}' does not exist."));
        }

        return issues;
    }
}
