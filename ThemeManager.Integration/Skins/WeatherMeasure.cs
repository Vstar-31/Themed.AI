using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Fetches weather data from OpenWeatherMap. Target should be formatted as "City|ApiKey".
/// </summary>
public sealed class WeatherMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "—";

    private readonly MeasureType _type;
    private readonly string _target;
    private readonly ILogger _logger;

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Keyed by the exact target string (city+key), NOT a single shared slot — otherwise two
    // widgets configured for two different cities would silently show whichever one fetched
    // most recently. Widgets that share the same target still correctly share one cache entry,
    // so pointing several meters (temp + description) at the same city only costs one API call.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry> _cache = new();

    private sealed class CacheEntry
    {
        public JsonElement? Data;
        public DateTime LastAttempt = DateTime.MinValue;
        public DateTime LastSuccess = DateTime.MinValue;
        public readonly object Lock = new();
    }

    public WeatherMeasure(string name, MeasureType type, string? target, ILogger? logger = null)
    {
        Name = name;
        _type = type;
        // Target format expected: "CityName|ApiKey"
        _target = target ?? "";
        _logger = logger ?? NullLogger.Instance;
    }

    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(_target) || !_target.Contains('|'))
        {
            Text = "Config Err";
            return;
        }

        var entry = _cache.GetOrAdd(_target, _ => new CacheEntry());

        // Only fetch weather every 10 minutes per city+key to save API calls.
        if ((DateTime.UtcNow - entry.LastSuccess).TotalMinutes > 10)
        {
            Task.Run(() => FetchWeatherAsync(entry));
        }

        UpdateFromCache(entry);
    }

    private void UpdateFromCache(CacheEntry entry)
    {
        if (entry.Data == null) return;

        try
        {
            if (_type == MeasureType.WeatherTemp)
            {
                double temp = entry.Data.Value.GetProperty("main").GetProperty("temp").GetDouble();
                Value = temp;
                Text = $"{temp:F0}°";
            }
            else if (_type == MeasureType.WeatherDesc)
            {
                var weatherArray = entry.Data.Value.GetProperty("weather");
                if (weatherArray.GetArrayLength() > 0)
                {
                    Text = weatherArray[0].GetProperty("description").GetString() ?? "—";
                }
            }
        }
        catch
        {
            Text = "Parse Err";
        }
    }

    private async Task FetchWeatherAsync(CacheEntry entry)
    {
        lock (entry.Lock)
        {
            if ((DateTime.UtcNow - entry.LastAttempt).TotalMinutes < 10) return;
            entry.LastAttempt = DateTime.UtcNow;
        }

        // Split before the try so the city (but never the key) is still available to log if the
        // request itself fails. Refresh() already guarantees '|' is present.
        var parts = _target.Split('|', 2);
        var cityRaw = parts[0];

        try
        {
            var city = Uri.EscapeDataString(cityRaw);
            var apiKey = Uri.EscapeDataString(parts[1]);

            var url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";
            var json = await _http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            entry.Data = doc.RootElement.Clone();
            entry.LastSuccess = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // Text deliberately isn't set to an error string here — entry.Data (and therefore
            // the last good reading) is left in place so a transient failure doesn't blank out
            // a widget that was working a moment ago. A persistent misconfiguration (bad key,
            // unknown city) will keep showing "—" from the initial state, which is a known gap:
            // see the "silent save/fetch failures have no UI feedback" note in the audit — worth
            // the same StatusMessage-style treatment as skins.json save failures at some point.
            _logger.LogWarning(ex, "Failed to fetch weather data for city \"{City}\"", cityRaw);
        }
    }
}
