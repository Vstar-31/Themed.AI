using System.Net.Http;
using System.Text.Json;
using ThemeManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration;

/// <summary>
/// Windows-independent (plain HTTP) implementation of <see cref="IWeatherConditionProvider"/>.
/// Hits the same OpenWeatherMap "current weather" endpoint as
/// <c>ThemeManager.Integration.Skins.WeatherMeasure</c>, but reads the <c>weather[0].main</c>
/// condition code instead of temperature/description, and caches per city+key on its own — the
/// two fetchers deliberately don't share a cache (see <see cref="IWeatherConditionProvider"/> for
/// why) even though a real deployment would very often have both configured for the same city.
/// </summary>
public sealed class OpenWeatherMapConditionProvider : IWeatherConditionProvider
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>How long a successful reading is trusted before the next call triggers a refetch.
    /// Matches <c>WeatherMeasure</c>'s cadence — weather automation doesn't need to be any more
    /// current than the widget does, and keeping both at 10 minutes keeps combined API usage
    /// predictable for anyone on OpenWeatherMap's free tier.</summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(10);

    /// <summary>Floor between fetch *attempts* (successful or not) for the same city+key, so a
    /// persistently bad API key or unreachable network can't turn a 30-second automation poll into
    /// a 30-second hammer on OpenWeatherMap.</summary>
    private static readonly TimeSpan MinRetryGap = TimeSpan.FromMinutes(2);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry> _cache = new();

    private sealed class CacheEntry
    {
        public WeatherCondition? LastCondition;
        public DateTime LastAttemptUtc = DateTime.MinValue;
        public DateTime LastSuccessUtc = DateTime.MinValue;
        public readonly SemaphoreSlim Gate = new(1, 1);
    }

    private readonly ILogger _logger;

    public OpenWeatherMapConditionProvider(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<WeatherCondition?> GetCurrentConditionAsync(string city, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(apiKey))
            return null;

        var entry = _cache.GetOrAdd($"{city}|{apiKey}", _ => new CacheEntry());

        // Fresh enough already — return without touching the network at all.
        if (DateTime.UtcNow - entry.LastSuccessUtc < FreshFor)
            return entry.LastCondition;

        // Only one fetch per cache entry in flight at a time; a second caller that lands here
        // while a fetch is already running just waits for it rather than starting a redundant one.
        await entry.Gate.WaitAsync();
        try
        {
            // Re-check now that we hold the gate — another caller may have just refreshed this
            // entry while we were waiting for it.
            if (DateTime.UtcNow - entry.LastSuccessUtc < FreshFor)
                return entry.LastCondition;

            if (DateTime.UtcNow - entry.LastAttemptUtc < MinRetryGap)
                return entry.LastCondition; // still within the retry floor — hand back whatever we last had, stale or null

            entry.LastAttemptUtc = DateTime.UtcNow;

            string url;
            if (city.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
            {
                var geolocator = new Windows.Devices.Geolocation.Geolocator();
                var pos = await geolocator.GetGeopositionAsync();
                double lat = pos.Coordinate.Point.Position.Latitude;
                double lon = pos.Coordinate.Point.Position.Longitude;
                url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={Uri.EscapeDataString(apiKey)}&units=metric";
            }
            else
            {
                url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={Uri.EscapeDataString(apiKey)}&units=metric";
            }
            
            var json = await _http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var weatherArray = doc.RootElement.GetProperty("weather");
            var main = weatherArray.GetArrayLength() > 0
                ? weatherArray[0].GetProperty("main").GetString()
                : null;

            var mapped = MapCondition(main);
            if (mapped is not null)
            {
                entry.LastCondition = mapped;
                entry.LastSuccessUtc = DateTime.UtcNow;
            }
            else
            {
                _logger.LogWarning("OpenWeatherMap returned an unrecognized condition {Main} for city {City} — leaving weather-reactive theming on its last known condition.", main, city);
            }

            // On failure (exception below) or an unmapped condition, entry.LastCondition is left
            // exactly as it was — same "don't blank out a reading that was working a moment ago"
            // choice WeatherMeasure makes, so a transient hiccup doesn't undo the user's theme.
            return entry.LastCondition;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather condition for city \"{City}\"", city);
            return entry.LastCondition;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    /// <summary>
    /// Maps an OpenWeatherMap <c>weather[0].main</c> value to our 6-bucket
    /// <see cref="WeatherCondition"/>. Returns null for anything not in the known set of ~15
    /// values rather than guessing — see the caller, which treats null as "leave weather out of
    /// the decision" rather than silently picking a bucket that might be wrong.
    /// </summary>
    private static WeatherCondition? MapCondition(string? main) => main?.Trim().ToLowerInvariant() switch
    {
        "clear" => WeatherCondition.Clear,
        "clouds" => WeatherCondition.Clouds,
        "rain" or "drizzle" => WeatherCondition.Rain,
        "thunderstorm" => WeatherCondition.Thunderstorm,
        "snow" => WeatherCondition.Snow,
        "mist" or "smoke" or "haze" or "dust" or "fog" or "sand" or "ash" or "squall" or "tornado" => WeatherCondition.Fog,
        _ => null,
    };
}
