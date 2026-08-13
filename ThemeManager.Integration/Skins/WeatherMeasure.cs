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
    
    private static readonly HttpClient _http = new();
    private static DateTime _lastFetch = DateTime.MinValue;
    private static JsonElement? _lastWeatherData;
    private static DateTime _lastDataFetch = DateTime.MinValue;
    private static readonly object _fetchLock = new();

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

        // Only fetch weather every 10 minutes to save API calls
        if ((DateTime.UtcNow - _lastDataFetch).TotalMinutes > 10)
        {
            Task.Run(FetchWeatherAsync);
        }

        UpdateFromCache();
    }
    
    private void UpdateFromCache()
    {
        if (_lastWeatherData == null) return;
        
        try
        {
            if (_type == MeasureType.WeatherTemp)
            {
                double temp = _lastWeatherData.Value.GetProperty("main").GetProperty("temp").GetDouble();
                Value = temp;
                Text = $"{temp:F0}°";
            }
            else if (_type == MeasureType.WeatherDesc)
            {
                var weatherArray = _lastWeatherData.Value.GetProperty("weather");
                if (weatherArray.GetArrayLength() > 0)
                {
                    Text = weatherArray[0].GetProperty("description").GetString() ?? "—";
                }
            }
        }
        catch { }
    }

    private async Task FetchWeatherAsync()
    {
        lock (_fetchLock)
        {
            if ((DateTime.UtcNow - _lastFetch).TotalMinutes < 10) return;
            _lastFetch = DateTime.UtcNow;
        }

        try
        {
            var parts = _target.Split('|');
            var city = Uri.EscapeDataString(parts[0]);
            var apiKey = Uri.EscapeDataString(parts[1]);
            
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";
            var json = await _http.GetStringAsync(url);
            
            using var doc = JsonDocument.Parse(json);
            _lastWeatherData = doc.RootElement.Clone();
            _lastDataFetch = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather data");
        }
    }
}
