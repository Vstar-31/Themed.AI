namespace ThemeManager.Core.Skins;

/// <summary>One quick-fill option for a <see cref="MeasureType.WebJson"/> measure's Target field.</summary>
public sealed record WebJsonPreset(string Label, string Target);

/// <summary>
/// A handful of known-working, key-free public JSON APIs, pre-formatted into WebJsonMeasure's
/// "Url|JsonPath" Target — the Phase 6 "2-3 shipped presets on top of [WebJsonMeasure]" bullet in
/// phases.md. Each URL/path pair below was checked against that API's current documentation
/// rather than guessed, since a wrong path silently shows "—" forever with nothing in the UI to
/// explain why. All three are plain, unauthenticated GETs — no request headers beyond what
/// <c>WebJsonMeasure</c> already sends, so no code changes were needed there to support them.
/// </summary>
public static class WebJsonPresets
{
    public static readonly IReadOnlyList<WebJsonPreset> All = new[]
    {
        // Numeric example, and a nice personal touch — this repo's own star count.
        new WebJsonPreset(
            "Themed.AI GitHub stars",
            "https://api.github.com/repos/Vstar-31/Themed.AI|stargazers_count"),

        // Numeric example against a third-party service. CoinGecko's /simple/price endpoint is
        // explicitly the free, no-key "Demo" tier (api.coingecko.com, not pro-api.coingecko.com);
        // swap "bitcoin"/"usd" for any other supported coin/currency pair.
        new WebJsonPreset(
            "Bitcoin price (USD)",
            "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd|bitcoin.usd"),

        // String example, and deliberately the simplest possible one-level-deep path — a good
        // "does my WebJson setup work at all" sanity check for anyone building their own.
        new WebJsonPreset(
            "Random advice",
            "https://api.adviceslip.com/advice|slip.advice"),
    };
}
