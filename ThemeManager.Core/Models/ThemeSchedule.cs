namespace ThemeManager.Core.Models;

/// <summary>
/// User-configured rules for <see cref="Services.ThemeAutomationService"/>: when the app should
/// switch the active theme on its own, and which theme to switch to. Lives on
/// <see cref="Services.AppSettings"/> so it persists and reloads with the rest of the user's
/// preferences via the same settings.json.
///
/// Times are stored as plain minutes-since-midnight (not <see cref="TimeSpan"/>) deliberately —
/// keeps this a flat, boring POCO that System.Text.Json can round-trip with zero custom
/// converters, and the WinUI settings page only ever needs the value at "read from a TimePicker,
/// write to a field" granularity anyway.
///
/// If more than one rule is enabled at once, <see cref="Services.ThemeAutomationService"/>
/// resolves them in a fixed priority: Battery Saver, then weather-reactive, then Light/Dark
/// following, then the time-of-day schedule. Any Theme*Id left null/empty means "no rule
/// configured for this slot", so partially filling this in (e.g. only Dusk and Midnight, or only
/// Rain and Snow) is fine — the automation service just leaves the active theme alone whenever it
/// resolves to an unset slot.
/// </summary>
public sealed class ThemeSchedule
{
    /// <summary>Master switch. Everything below is inert while this is false.</summary>
    public bool Enabled { get; set; } = false;

    // ── Time-of-day schedule ────────────────────────────────────────────────
    public int SunriseMinutes  { get; set; } = 6  * 60;
    public int NoonMinutes     { get; set; } = 12 * 60;
    public int DuskMinutes     { get; set; } = 18 * 60;
    public int MidnightMinutes { get; set; } = 22 * 60;

    public string? SunriseThemeId  { get; set; }
    public string? NoonThemeId     { get; set; }
    public string? DuskThemeId     { get; set; }
    public string? MidnightThemeId { get; set; }

    // ── Follow Windows light/dark mode ──────────────────────────────────────
    public bool FollowSystemLightDark { get; set; } = false;
    public string? LightThemeId { get; set; }
    public string? DarkThemeId  { get; set; }

    // ── Battery saver ────────────────────────────────────────────────────────
    public bool BatterySaverEnabled { get; set; } = false;
    public string? BatterySaverThemeId { get; set; }

    // ── Weather-reactive theming ─────────────────────────────────────────────
    /// <summary>City passed straight through to OpenWeatherMap's "q" parameter — e.g. "Jaipur,IN".
    /// Same free-text format <c>WeatherMeasure</c> expects, so a value copied from a widget's
    /// weather config works here unchanged.</summary>
    public bool WeatherReactiveEnabled { get; set; } = false;
    public string? WeatherCity { get; set; }
    public string? WeatherApiKey { get; set; }

    public string? WeatherClearThemeId        { get; set; }
    public string? WeatherCloudsThemeId       { get; set; }
    public string? WeatherRainThemeId         { get; set; }
    public string? WeatherThunderstormThemeId { get; set; }
    public string? WeatherSnowThemeId         { get; set; }
    public string? WeatherFogThemeId          { get; set; }

    // ── Transition ────────────────────────────────────────────────────────────
    /// <summary>How long an automatic switch takes to crossfade, in milliseconds. 0 = instant cut.</summary>
    public int CrossfadeMs { get; set; } = 1200;
}
