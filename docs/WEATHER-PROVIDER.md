# Weather provider configuration

The free, non-commercial preview defaults to Open-Meteo. Widgets and theme automation share one cache. No publisher key or fixed city is embedded. Explicit legacy OpenWeatherMap keys keep using OpenWeatherMap.

To use an Open-Meteo-compatible proxy or self-hosted service, place `weather-provider.json` beside the executable and restart:

```json
{
  "ForecastEndpoint": "https://weather.example/v1/forecast",
  "GeocodingEndpoint": "https://weather.example/v1/search",
  "RefreshMinutes": 30,
  "RetryMinutes": 2,
  "MaxStaleMinutes": 120
}
```

Omit the file for public defaults. Endpoints must be HTTPS without credentials, queries or fragments. Invalid configuration disables key-free weather. Keep paid-provider keys on your proxy, never in distributed desktop builds. A compatible proxy accepts the same geocoding and current-weather response shapes. A different provider can replace the client and implement IWeatherConditionProvider.

Cities are cached for one day, readings for 30 minutes. Errors back off for two minutes; readings expire after two hours. Concurrent callers share one request. Windows location is opt-in and rounded to two decimal places before transmission; manually entered cities never request Windows location. Unknown WMO codes do not trigger a guessed theme.

Open-Meteo limits its free service to non-commercial use and enforces application-wide and IP limits. Reassess hosting before a wider or commercial launch; desktop caching alone cannot guarantee staying below aggregate quotas.

Attribution: Weather data from [Open-Meteo](https://open-meteo.com/) under CC BY 4.0. Location data from [GeoNames](https://www.geonames.org/).

References checked 8 November 2026: [terms](https://open-meteo.com/en/terms), [forecast API](https://open-meteo.com/en/docs), [geocoding API](https://open-meteo.com/en/docs/geocoding-api).
