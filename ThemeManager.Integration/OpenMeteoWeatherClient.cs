using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ThemeManager.Core.Services;

namespace ThemeManager.Integration;

public sealed record WeatherReading(double Temperature, string Description, string City,
    WeatherCondition? Condition, DateTimeOffset RetrievedAt);

// One configuration point for a compatible hosted proxy or self-hosted provider.
// No city, credentials, or publisher identity is compiled into weather requests.
public sealed class WeatherProviderOptions
{
    public string ForecastEndpoint { get; set; } = "https://api.open-meteo.com/v1/forecast";
    public string GeocodingEndpoint { get; set; } = "https://geocoding-api.open-meteo.com/v1/search";
    public int RefreshMinutes { get; set; } = 30;
    public int RetryMinutes { get; set; } = 2;
    public int MaxStaleMinutes { get; set; } = 120;

    public static WeatherProviderOptions Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "weather-provider.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<WeatherProviderOptions>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Weather provider configuration is empty.")
            : new();
    }

    public void Validate()
    {
        foreach (var endpoint in new[] { ForecastEndpoint, GeocodingEndpoint })
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Weather endpoints must be HTTPS URLs without credentials, query or fragment.");
        if (RefreshMinutes < 10 || RefreshMinutes > 1440 || RetryMinutes < 1 || RetryMinutes > RefreshMinutes ||
            MaxStaleMinutes < RefreshMinutes || MaxStaleMinutes > 1440)
            throw new ArgumentException("Invalid weather cache intervals.");
    }
}

public sealed class OpenMeteoWeatherClient
{
    private static readonly Lazy<OpenMeteoWeatherClient> SharedClient = new(() => new(
        new HttpClient { Timeout = TimeSpan.FromSeconds(15) }, WeatherProviderOptions.Load(), GetWindowsLocationAsync));
    public static OpenMeteoWeatherClient Shared => SharedClient.Value;
    private readonly HttpClient _http;
    private readonly WeatherProviderOptions _options;
    private readonly TimeProvider _clock;
    private readonly Func<Task<(double Latitude, double Longitude)>>? _location;
    private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private sealed class Entry
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public DateTimeOffset LastAttempt = DateTimeOffset.MinValue;
        public DateTimeOffset LocationTime = DateTimeOffset.MinValue;
        public (double Latitude, double Longitude, string Name)? Location;
        public WeatherReading? Reading;
    }

    public OpenMeteoWeatherClient(HttpClient http, WeatherProviderOptions options,
        Func<Task<(double Latitude, double Longitude)>>? location = null, TimeProvider? clock = null)
    {
        options.Validate();
        _http = http; _options = options; _location = location; _clock = clock ?? TimeProvider.System;
    }

    public async Task<WeatherReading?> GetAsync(string city)
    {
        city = city.Trim();
        if (city.Length < 2 || city.Length > 160) return null;
        var entry = _cache.GetOrAdd(city, _ => new Entry());
        await entry.Gate.WaitAsync();
        try
        {
            var now = _clock.GetUtcNow();
            var age = entry.Reading is null ? TimeSpan.MaxValue : now - entry.Reading.RetrievedAt;
            if (age < TimeSpan.FromMinutes(_options.RefreshMinutes)) return entry.Reading;
            if (now - entry.LastAttempt < TimeSpan.FromMinutes(_options.RetryMinutes))
                return age < TimeSpan.FromMinutes(_options.MaxStaleMinutes) ? entry.Reading : null;
            entry.LastAttempt = now;
            try
            {
                (double Latitude, double Longitude, string Name) place;
                if (city.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
                {
                    if (_location is null) return null;
                    var coordinates = await _location();
                    place = (coordinates.Latitude, coordinates.Longitude, "Current location");
                }
                else if (entry.Location is not null && now - entry.LocationTime < TimeSpan.FromDays(1)) place = entry.Location.Value;
                else
                {
                    var locationParts = city.Split(',', 2, StringSplitOptions.TrimEntries);
                    var country = locationParts.Length == 2 && locationParts[1].Length == 2 && locationParts[1].All(char.IsAsciiLetter)
                        ? locationParts[1].ToUpperInvariant() : null;
                    var name = country is null ? city : locationParts[0];
                    var countryFilter = country is null ? "" : $"&countryCode={country}";
                    using var geo = JsonDocument.Parse(await _http.GetStringAsync(
                        $"{_options.GeocodingEndpoint}?name={Uri.EscapeDataString(name)}{countryFilter}&count=1&language=en&format=json"));
                    if (!geo.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
                    var first = results[0];
                    place = (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble(),
                        first.GetProperty("name").GetString() ?? city);
                    entry.Location = place;
                    entry.LocationTime = now;
                }
                if (!double.IsFinite(place.Latitude) || !double.IsFinite(place.Longitude) || Math.Abs(place.Latitude) > 90 || Math.Abs(place.Longitude) > 180)
                    return null;
                // Weather does not need street-level coordinates. Invariant formatting works
                // on Windows locales whose decimal separator is a comma.
                var lat = Math.Round(place.Latitude, 2).ToString("F2", CultureInfo.InvariantCulture);
                var lon = Math.Round(place.Longitude, 2).ToString("F2", CultureInfo.InvariantCulture);
                using var data = JsonDocument.Parse(await _http.GetStringAsync(
                    $"{_options.ForecastEndpoint}?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&temperature_unit=celsius&forecast_days=1"));
                var current = data.RootElement.GetProperty("current");
                var temperature = current.GetProperty("temperature_2m").GetDouble();
                var code = current.GetProperty("weather_code").GetInt32();
                if (!double.IsFinite(temperature)) return null;
                var (condition, description) = MapCode(code);
                entry.Reading = new(temperature, description, place.Name, condition, now);
                return entry.Reading;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or OverflowException or JsonException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                return age < TimeSpan.FromMinutes(_options.MaxStaleMinutes) ? entry.Reading : null;
            }
        }
        finally { entry.Gate.Release(); }
    }

    public static (WeatherCondition? Condition, string Description) MapCode(int code) => code switch
    {
        0 => (WeatherCondition.Clear, "Clear sky"),
        1 or 2 or 3 => (WeatherCondition.Clouds, "Cloudy"),
        45 or 48 => (WeatherCondition.Fog, "Fog"),
        51 or 53 or 55 or 56 or 57 => (WeatherCondition.Rain, "Drizzle"),
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => (WeatherCondition.Rain, "Rain"),
        71 or 73 or 75 or 77 or 85 or 86 => (WeatherCondition.Snow, "Snow"),
        95 or 96 or 99 => (WeatherCondition.Thunderstorm, "Thunderstorm"),
        _ => (null, "Unknown conditions"),
    };

    private static async Task<(double Latitude, double Longitude)> GetWindowsLocationAsync()
    {
        var locator = new Windows.Devices.Geolocation.Geolocator();
        var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10));
        return (position.Coordinate.Point.Position.Latitude, position.Coordinate.Point.Position.Longitude);
    }
}

public sealed class ConfiguredWeatherConditionProvider : IWeatherConditionProvider
{
    private readonly OpenWeatherMapConditionProvider _legacy;
    public ConfiguredWeatherConditionProvider(Microsoft.Extensions.Logging.ILogger? logger = null) => _legacy = new(logger);
    public async Task<WeatherCondition?> GetCurrentConditionAsync(string city, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey)) return await _legacy.GetCurrentConditionAsync(city, apiKey);
        try { return (await OpenMeteoWeatherClient.Shared.GetAsync(city))?.Condition; }
        catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
}
