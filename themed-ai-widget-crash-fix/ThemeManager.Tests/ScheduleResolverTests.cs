using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Tests;

/// <summary>
/// Tests for <see cref="ScheduleResolver"/> — the pure "which time-of-day slot is active right
/// now" logic behind Phase 7's automatic theme switching. Deliberately exercised with plain
/// minute values rather than a live system clock, so these run the same at any time of day.
/// </summary>
public class ScheduleResolverTests
{
    private static ThemeSchedule MakeSchedule() => new()
    {
        SunriseMinutes = 6 * 60,   // 06:00
        NoonMinutes = 12 * 60,     // 12:00
        DuskMinutes = 18 * 60,     // 18:00
        MidnightMinutes = 22 * 60, // 22:00
        SunriseThemeId = "sunrise",
        NoonThemeId = "noon",
        DuskThemeId = "dusk",
        MidnightThemeId = "midnight",
    };

    [Theory]
    [InlineData(6 * 60, ScheduleSlot.Sunrise, "sunrise")]       // exactly at Sunrise
    [InlineData(9 * 60, ScheduleSlot.Sunrise, "sunrise")]       // mid-morning, still Sunrise slot
    [InlineData(12 * 60, ScheduleSlot.Noon, "noon")]            // exactly at Noon
    [InlineData(15 * 60, ScheduleSlot.Noon, "noon")]            // afternoon, still Noon slot
    [InlineData(18 * 60, ScheduleSlot.Dusk, "dusk")]            // exactly at Dusk
    [InlineData(20 * 60, ScheduleSlot.Dusk, "dusk")]            // evening, still Dusk slot
    [InlineData(22 * 60, ScheduleSlot.Midnight, "midnight")]    // exactly at Midnight
    [InlineData(23 * 60, ScheduleSlot.Midnight, "midnight")]    // late night, still Midnight slot
    public void ResolveTimeSlot_PicksTheMostRecentlyStartedSlot(int nowMinutes, ScheduleSlot expectedSlot, string expectedThemeId)
    {
        var (slot, themeId) = ScheduleResolver.ResolveTimeSlot(MakeSchedule(), nowMinutes);

        Assert.Equal(expectedSlot, slot);
        Assert.Equal(expectedThemeId, themeId);
    }

    [Theory]
    [InlineData(0)]      // midnight exactly
    [InlineData(3 * 60)] // 3 AM
    [InlineData(5 * 60 + 59)] // one minute before Sunrise
    public void ResolveTimeSlot_BeforeFirstSlot_WrapsToLatestSlot(int nowMinutes)
    {
        // Before Sunrise (06:00) with no slot having started "today" yet, the last slot from
        // "yesterday" — Midnight (22:00), the latest configured time — is still the active one.
        var (slot, themeId) = ScheduleResolver.ResolveTimeSlot(MakeSchedule(), nowMinutes);

        Assert.Equal(ScheduleSlot.Midnight, slot);
        Assert.Equal("midnight", themeId);
    }

    [Fact]
    public void ResolveTimeSlot_UnassignedSlot_ReturnsNullThemeId()
    {
        var schedule = MakeSchedule();
        schedule.NoonThemeId = null;

        var (slot, themeId) = ScheduleResolver.ResolveTimeSlot(schedule, 13 * 60);

        Assert.Equal(ScheduleSlot.Noon, slot);
        Assert.Null(themeId);
    }

    [Fact]
    public void ResolveTimeSlot_IsDeterministic_SameInputSameOutput()
    {
        var schedule = MakeSchedule();

        var first = ScheduleResolver.ResolveTimeSlot(schedule, 14 * 60);
        var second = ScheduleResolver.ResolveTimeSlot(schedule, 14 * 60);

        Assert.Equal(first, second);
    }
}
