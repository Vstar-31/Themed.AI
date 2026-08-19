using ThemeManager.Core.Models;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// Identifies which rule most recently decided the active theme. Named (rather than just
/// returning a theme id) so <see cref="Services.ThemeAutomationService"/> can tell "we're still in
/// the slot we already applied" from "the slot changed, fire again" without re-deriving it from
/// the clock every poll.
/// </summary>
public enum ScheduleSlot { None, Sunrise, Noon, Dusk, Midnight, Light, Dark, BatterySaver }

/// <summary>
/// Pure "given a schedule and the current time, which time-of-day slot is active" logic — kept
/// separate from <see cref="Services.ThemeAutomationService"/> so it's testable without a timer,
/// a system clock, or a Windows machine.
/// </summary>
public static class ScheduleResolver
{
    /// <summary>
    /// Picks whichever of the four time-of-day slots most recently started, wrapping around
    /// midnight — e.g. at 2 AM with slots at Sunrise 06:00 / Noon 12:00 / Dusk 18:00 / Midnight
    /// 22:00, none of today's slots have started yet, so yesterday's Midnight slot (the latest
    /// one overall) is still "current".
    /// </summary>
    public static (ScheduleSlot Slot, string? ThemeId) ResolveTimeSlot(ThemeSchedule schedule, int nowMinutes)
    {
        var slots = new (int Minutes, ScheduleSlot Slot, string? ThemeId)[]
        {
            (schedule.SunriseMinutes,  ScheduleSlot.Sunrise,  schedule.SunriseThemeId),
            (schedule.NoonMinutes,     ScheduleSlot.Noon,     schedule.NoonThemeId),
            (schedule.DuskMinutes,     ScheduleSlot.Dusk,     schedule.DuskThemeId),
            (schedule.MidnightMinutes, ScheduleSlot.Midnight, schedule.MidnightThemeId),
        };

        var passed = slots.Where(s => s.Minutes <= nowMinutes)
                           .OrderByDescending(s => s.Minutes)
                           .ToList();

        var chosen = passed.Count > 0
            ? passed[0]
            : slots.OrderByDescending(s => s.Minutes).First();

        return (chosen.Slot, chosen.ThemeId);
    }
}
