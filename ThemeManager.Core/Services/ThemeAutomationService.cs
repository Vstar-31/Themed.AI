using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.Services;

/// <summary>
/// Periodically checks wall-clock time, Windows light/dark mode, and battery-saver status against
/// the user's <see cref="ThemeSchedule"/>, and raises <see cref="AutomationTriggered"/> with
/// whichever theme should now be active. Deliberately does NOT touch <see cref="ThemeService"/>'s
/// active-theme setter or any XAML resource directly — it only decides *what* should happen; the
/// WinUI layer (see App.xaml.cs) owns *how* that gets applied (dispatched to the UI thread,
/// optionally crossfaded). That split keeps the decision logic in Core, testable without a UI
/// thread, and reusable if Themed.AI ever grows a second front-end.
///
/// Runs on a plain <see cref="System.Threading.Timer"/>, deliberately NOT on the widgets'
/// once-a-second tick loop (that lives in the WinUI layer's SkinManagerService anyway, so Core
/// can't share it without a UI dependency). Nothing this watches — a clock slot boundary, the OS
/// theme, battery-saver state — changes fast enough to need better-than-30-second resolution, and
/// polling less often means fewer registry/WinRT reads over a long-running session.
/// </summary>
public sealed class ThemeAutomationService : IDisposable
{
    private readonly ThemeService _themeService;
    private readonly ISystemThemeIntegrator _systemIntegrator;
    private readonly Func<ThemeSchedule> _getSchedule;
    private readonly Timer _timer;

    /// <summary>Which rule most recently fired, so repeated polls inside the same slot/state don't
    /// keep re-triggering a crossfade to a theme that's already active.</summary>
    private ScheduleSlot _lastFiredSlot = ScheduleSlot.None;

    /// <summary>
    /// Fired on a thread-pool thread (from the internal timer), not the UI thread — subscribers
    /// that touch UI must dispatch themselves. See App.xaml.cs, which follows the same pattern
    /// already established there for the tray icon's global-hotkey event.
    /// </summary>
    public event Action<CozyTheme>? AutomationTriggered;

    public ThemeAutomationService(ThemeService themeService, ISystemThemeIntegrator systemIntegrator, Func<ThemeSchedule> getSchedule)
    {
        _themeService = themeService;
        _systemIntegrator = systemIntegrator;
        _getSchedule = getSchedule;

        // First check shortly after startup (so a schedule that's already "due" applies promptly
        // without waiting a full period), then every 30s thereafter.
        _timer = new Timer(_ => SafeCheck(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    private void SafeCheck()
    {
        try
        {
            CheckAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Automation is a background convenience layered on top of a fully-functional manual
            // theme switcher — a transient registry/WinRT failure here should never surface to the
            // user or take the app down. Worst case, a scheduled switch is late by one poll.
        }
    }

    private async Task CheckAsync()
    {
        var schedule = _getSchedule();
        if (schedule is null || !schedule.Enabled)
        {
            _lastFiredSlot = ScheduleSlot.None;
            return;
        }

        // ── Battery saver takes priority over everything else — it's a "the machine needs
        // help" signal that should win regardless of what time it is or what Windows' own
        // light/dark setting says. ──
        if (schedule.BatterySaverEnabled && !string.IsNullOrEmpty(schedule.BatterySaverThemeId))
        {
            bool active = await _systemIntegrator.IsBatterySaverActiveAsync();
            if (active)
            {
                if (_lastFiredSlot != ScheduleSlot.BatterySaver)
                    Fire(ScheduleSlot.BatterySaver, schedule.BatterySaverThemeId);
                return;
            }

            // No longer in battery saver. If we were the one who applied its theme, forget that —
            // otherwise the checks below would think "we're already in slot X" for a slot we never
            // actually applied, and silently skip re-applying the real one.
            if (_lastFiredSlot == ScheduleSlot.BatterySaver)
                _lastFiredSlot = ScheduleSlot.None;
        }

        // ── Follow Windows light/dark mode ──
        if (schedule.FollowSystemLightDark && !string.IsNullOrEmpty(schedule.LightThemeId) && !string.IsNullOrEmpty(schedule.DarkThemeId))
        {
            var info = await _systemIntegrator.GetCurrentSystemThemeAsync();
            var slot = info.IsLightMode ? ScheduleSlot.Light : ScheduleSlot.Dark;

            if (slot != _lastFiredSlot)
                Fire(slot, info.IsLightMode ? schedule.LightThemeId : schedule.DarkThemeId);

            return; // light/dark following and the time-of-day schedule are mutually exclusive
        }

        // ── Time-of-day schedule ──
        int nowMinutes = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        var (slotNow, themeId) = ScheduleResolver.ResolveTimeSlot(schedule, nowMinutes);
        if (slotNow != _lastFiredSlot)
            Fire(slotNow, themeId);
    }

    private void Fire(ScheduleSlot slot, string? themeId)
    {
        _lastFiredSlot = slot;
        if (string.IsNullOrEmpty(themeId)) return; // slot has no theme assigned — nothing to apply

        var theme = _themeService.Themes.FirstOrDefault(t => t.Id == themeId);
        if (theme is not null)
            AutomationTriggered?.Invoke(theme);
    }

    public void Dispose() => _timer.Dispose();
}
