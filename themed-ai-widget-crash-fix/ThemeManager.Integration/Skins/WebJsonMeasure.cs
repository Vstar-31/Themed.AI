using System.Text.Json;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Fetches a value from any URL that returns JSON. Target is formatted as "Url|JsonPath" —
/// e.g. "https://api.example.com/price.json|data.usd" or, for an array,
/// "https://api.example.com/list.json|results[0].value". An empty path just uses the
/// response's root value directly (for an endpoint that's already a bare number or string).
///
/// This is the one generic building block WeatherMeasure could have been a special case of —
/// weather, stock/crypto price, RSS-item-count, GitHub star count, or any other JSON API all
/// fit through the same Target format without a bespoke measure class per source.
/// </summary>
public sealed class WebJsonMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "—";

    private readonly string _target;
    private readonly ILogger _logger;

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Keyed by the exact target (URL+path), same reasoning as WeatherMeasure's per-city cache —
    // two widgets pointed at two different URLs must never share a cached response.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry> _cache = new();

    private sealed class CacheEntry
    {
        public (double Value, string Text)? Data;
        public DateTime LastAttempt = DateTime.MinValue;
        public DateTime LastSuccess = DateTime.MinValue;
        public readonly object Lock = new();
    }

    public WebJsonMeasure(string name, string? target, ILogger? logger = null)
    {
        Name = name;
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

        // Poll at most every 5 minutes per URL+path — frequent enough for most external data,
        // infrequent enough not to hammer someone else's API from a desktop widget.
        if ((DateTime.UtcNow - entry.LastSuccess).TotalMinutes > 5)
            Task.Run(() => FetchAsync(entry));

        if (entry.Data is { } data)
        {
            Value = data.Value;
            Text = data.Text;
        }
    }

    private async Task FetchAsync(CacheEntry entry)
    {
        lock (entry.Lock)
        {
            if ((DateTime.UtcNow - entry.LastAttempt).TotalMinutes < 5) return;
            entry.LastAttempt = DateTime.UtcNow;
        }

        var parts = _target.Split('|', 2);
        string url = parts[0];
        string path = parts.Length > 1 ? parts[1] : "";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            _logger.LogWarning("WebJsonMeasure target has an invalid URL: \"{Url}\"", url);
            return;
        }

        try
        {
            string json = await _http.GetStringAsync(uri);
            using var doc = JsonDocument.Parse(json);

            if (!TryNavigate(doc.RootElement, path, out var element))
            {
                _logger.LogWarning("WebJsonMeasure path \"{Path}\" not found in response from {Url}", path, url);
                return;
            }

            entry.Data = ExtractValue(element);
            entry.LastSuccess = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // Same reasoning as WeatherMeasure: entry.Data is left as whatever it was before, so
            // a transient failure doesn't blank out a widget that was working a moment ago.
            _logger.LogWarning(ex, "Failed to fetch/parse JSON from {Url}", url);
        }
    }

    /// <summary>Walks a dot/bracket path like "results[0].value" through a JsonElement tree.
    /// Not a full JSONPath implementation — just nested-object and array-index access, which
    /// covers the large majority of real JSON APIs without pulling in a parsing library.</summary>
    private static bool TryNavigate(JsonElement root, string path, out JsonElement result)
    {
        result = root;
        if (string.IsNullOrWhiteSpace(path))
            return true;

        foreach (var rawSegment in path.Split('.'))
        {
            string segment = rawSegment;
            int? arrayIndex = null;

            int bracketStart = segment.IndexOf('[');
            if (bracketStart >= 0 && segment.EndsWith(']'))
            {
                string indexText = segment[(bracketStart + 1)..^1];
                if (!int.TryParse(indexText, out int idx))
                    return false;
                arrayIndex = idx;
                segment = segment[..bracketStart];
            }

            if (segment.Length > 0)
            {
                if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(segment, out result))
                    return false;
            }

            if (arrayIndex is { } i)
            {
                if (result.ValueKind != JsonValueKind.Array || i < 0 || i >= result.GetArrayLength())
                    return false;
                result = result[i];
            }
        }

        return true;
    }

    private static (double Value, string Text) ExtractValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => (element.GetDouble(), FormatNumber(element.GetDouble())),
        JsonValueKind.String => ParseStringValue(element.GetString() ?? ""),
        JsonValueKind.True   => (1, "true"),
        JsonValueKind.False  => (0, "false"),
        _                    => (0, element.ToString()),
    };

    private static (double, string) ParseStringValue(string s)
        => (double.TryParse(s, out double parsed) ? parsed : 0, s);

    private static string FormatNumber(double n)
        => n == Math.Floor(n) && Math.Abs(n) < 1_000_000 ? n.ToString("F0") : n.ToString("F2");
}
