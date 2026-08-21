using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// Identifies which rule most recently decided the active theme. Named (rather than just
/// returning a theme id) so <see cref="Services.ThemeAutomationService"/> can tell "we're still in
/// the slot we already applied" from "the slot changed, fire again" without re-deriving it from
/// the clock every poll. The six Weather* values mirror <see cref="WeatherCondition"/> one-to-one
/// rather than collapsing to a single "Weather" slot — that's what lets a switch from, say, Rain
/// to Snow correctly re-fire even though both are "weather-reactive is on".
/// </summary>
public enum ScheduleSlot
{
    None, Sunrise, Noon, Dusk, Midnight, Light, Dark, BatterySaver,
    WeatherClear, WeatherClouds, WeatherRain, WeatherThunderstorm, WeatherSnow, WeatherFog,
}

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

    /// <summary>
    /// Maps a fetched <see cref="WeatherCondition"/> to its <see cref="ScheduleSlot"/> and the
    /// theme configured for it. Pure one-to-one lookup — no time-of-day or "most recent" logic
    /// needed here, unlike <see cref="ResolveTimeSlot"/>, since only one condition is ever active
    /// at once.
    /// </summary>
    public static (ScheduleSlot Slot, string? ThemeId) ResolveWeatherSlot(ThemeSchedule schedule, WeatherCondition condition) =>
        condition switch
        {
            WeatherCondition.Clear        => (ScheduleSlot.WeatherClear, schedule.WeatherClearThemeId),
            WeatherCondition.Clouds       => (ScheduleSlot.WeatherClouds, schedule.WeatherCloudsThemeId),
            WeatherCondition.Rain         => (ScheduleSlot.WeatherRain, schedule.WeatherRainThemeId),
            WeatherCondition.Thunderstorm => (ScheduleSlot.WeatherThunderstorm, schedule.WeatherThunderstormThemeId),
            WeatherCondition.Snow         => (ScheduleSlot.WeatherSnow, schedule.WeatherSnowThemeId),
            WeatherCondition.Fog          => (ScheduleSlot.WeatherFog, schedule.WeatherFogThemeId),
            _                             => (ScheduleSlot.None, null),
        };
}
