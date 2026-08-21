namespace ThemeManager.Core.Services;

/// <summary>
/// Coarse weather-condition buckets used to drive <see cref="ThemeAutomationService"/>'s
/// weather-reactive rule. Deliberately smaller than OpenWeatherMap's full condition-code list
/// (nine "main" groups: Thunderstorm, Drizzle, Rain, Snow, Clear, Clouds, Mist, Smoke, Haze, Dust,
/// Fog, Sand, Ash, Squall, Tornado) — six buckets is already six theme pickers in the Settings UI,
/// and the long tail (Squall, Tornado, Ash, Sand, Dust, Smoke) is both rare for most users and not
/// meaningfully different from its nearest bucket for theming purposes. See
/// <c>OpenWeatherMapConditionProvider</c> in ThemeManager.Integration for the actual
/// code-to-bucket mapping.
/// </summary>
public enum WeatherCondition { Clear, Clouds, Rain, Thunderstorm, Snow, Fog }

/// <summary>
/// Abstraction over "what's the weather like right now in this city" — kept separate from the
/// widget-side weather fetch (<c>ThemeManager.Integration.Skins.WeatherMeasure</c>) deliberately:
/// that one drives a single widget's per-meter refresh loop and reads temperature/description,
/// this one drives the whole app's active theme on a much slower cadence and only cares about the
/// coarse <see cref="WeatherCondition"/> bucket. Both happen to hit the same OpenWeatherMap
/// endpoint, but keeping the two independent means a widget's weather config can't accidentally
/// break theme automation, or vice versa.
///
/// Like <see cref="ISystemThemeIntegrator"/>, every method here is async and must be
/// failure-tolerant — never throw to <see cref="ThemeAutomationService"/>.
/// </summary>
public interface IWeatherConditionProvider
{
    /// <summary>
    /// Returns the current weather condition for <paramref name="city"/>, or null if it couldn't
    /// be determined (no network, bad API key, unrecognized city, or a fetch simply hasn't
    /// completed yet since Themed.AI started). A null result means "leave weather out of the
    /// decision this time" — <see cref="ThemeAutomationService"/> falls through to the
    /// next-priority rule rather than freezing on the last-known condition.
    /// </summary>
    Task<WeatherCondition?> GetCurrentConditionAsync(string city, string apiKey);
}
