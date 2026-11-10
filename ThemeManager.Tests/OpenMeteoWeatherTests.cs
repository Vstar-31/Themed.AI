using System.Globalization;
using System.Net;
using System.Text;
using ThemeManager.Core.Services;
using ThemeManager.Integration;

namespace ThemeManager.Tests;

public sealed class OpenMeteoWeatherTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();
        public bool Fail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (Fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var json = request.RequestUri!.AbsolutePath.Contains("search")
                ? """{"results":[{"latitude":26.91245,"longitude":75.78732,"name":"Jaipur"}]}"""
                : """{"current":{"temperature_2m":24.5,"weather_code":61}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ConcurrentWidgetAndAutomationRequestsShareLocationAndWeatherCache()
    {
        var handler = new Handler(); var clock = new Clock();
        var client = new OpenMeteoWeatherClient(new HttpClient(handler), new(), clock: clock);
        var readings = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetAsync(" Jaipur, IN ")));
        Assert.All(readings, x => { Assert.Equal(24.5, x!.Temperature); Assert.Equal(WeatherCondition.Rain, x.Condition); });
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("name=Jaipur&countryCode=IN", handler.Requests[0].Query);
        Assert.DoesNotContain("apikey", handler.Requests[1].Query);
        Assert.Contains("latitude=26.91&longitude=75.79", handler.Requests[1].Query);
        await client.GetAsync("jaipur, in");
        Assert.Equal(2, handler.Requests.Count);
        clock.Now += TimeSpan.FromMinutes(31);
        await client.GetAsync("Jaipur, IN");
        Assert.Equal(3, handler.Requests.Count); // Reuse geocoding for a day.
    }

    [Fact]
    public async Task OutageBacksOffAndEventuallyExpiresStaleWeather()
    {
        var handler = new Handler(); var clock = new Clock();
        var client = new OpenMeteoWeatherClient(new HttpClient(handler), new(), clock: clock);
        Assert.NotNull(await client.GetAsync("Jaipur"));
        handler.Fail = true;
        clock.Now += TimeSpan.FromMinutes(31);
        Assert.NotNull(await client.GetAsync("Jaipur"));
        Assert.NotNull(await client.GetAsync("Jaipur"));
        Assert.Equal(3, handler.Requests.Count);
        clock.Now += TimeSpan.FromHours(2);
        Assert.Null(await client.GetAsync("Jaipur"));
    }

    [Fact]
    public async Task AutomaticLocationUsesRoundedInvariantCoordinatesAndNoCitySearch()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var handler = new Handler();
            var client = new OpenMeteoWeatherClient(new HttpClient(handler), new(), () => Task.FromResult((12.34567, 76.54321)));
            Assert.NotNull(await client.GetAsync("AUTO"));
            Assert.Single(handler.Requests);
            Assert.Contains("latitude=12.35&longitude=76.54", handler.Requests[0].Query);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task ManualCityNeverRequestsWindowsLocation()
    {
        var client = new OpenMeteoWeatherClient(new HttpClient(new Handler()), new(), () => throw new InvalidOperationException("Location was not opted into"));
        Assert.NotNull(await client.GetAsync("Jaipur"));
    }

    [Theory]
    [InlineData(0, WeatherCondition.Clear)]
    [InlineData(3, WeatherCondition.Clouds)]
    [InlineData(48, WeatherCondition.Fog)]
    [InlineData(57, WeatherCondition.Rain)]
    [InlineData(82, WeatherCondition.Rain)]
    [InlineData(86, WeatherCondition.Snow)]
    [InlineData(99, WeatherCondition.Thunderstorm)]
    public void WeatherCodesDriveCorrectRules(int code, WeatherCondition expected) => Assert.Equal(expected, OpenMeteoWeatherClient.MapCode(code).Condition);

    [Fact]
    public void UnknownCodeDoesNotInventACondition() => Assert.Null(OpenMeteoWeatherClient.MapCode(1234).Condition);

    [Fact]
    public async Task ProviderEndpointsCanBeChangedWithoutChangingRequestsOrEmbeddingKeys()
    {
        var handler = new Handler();
        var client = new OpenMeteoWeatherClient(new HttpClient(handler), new() {
            ForecastEndpoint = "https://weather.example/current", GeocodingEndpoint = "https://weather.example/search"
        });
        Assert.NotNull(await client.GetAsync("Jaipur"));
        Assert.All(handler.Requests, uri => Assert.Equal("weather.example", uri.Host));
    }

    [Theory]
    [InlineData("http://weather.example/current")]
    [InlineData("https://user:secret@weather.example/current")]
    public void RejectInsecureProviderConfiguration(string endpoint) => Assert.Throws<ArgumentException>(() => new WeatherProviderOptions { ForecastEndpoint = endpoint }.Validate());
}
