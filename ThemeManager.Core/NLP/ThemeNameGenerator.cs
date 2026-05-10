namespace ThemeManager.Core.NLP;

/// <summary>
/// Generates a creative theme name and description from the matched keywords
/// and their categories in a <see cref="VibeSignal"/>.
///
/// Strategy:
///   1. Prefer "environment" + "mood" word pairing for the name (most evocative).
///   2. Fall back to "time" + "environment", or "color" + "material".
///   3. Title-case and compose into a 2–3 word name.
///   4. Build description from all unique categories present.
///
/// This is a rule-based template system — equivalent to a simple NLG (Natural
/// Language Generation) pipeline without a language model.
/// </summary>
public static class ThemeNameGenerator
{
    // ── Mood adjective upgrades ────────────────────────────────────────────────
    // When a mood word is used in the name, use a more evocative synonym
    // so the name sounds like a product name, not a description.
    private static readonly Dictionary<string, string> MoodUpgrade =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["cozy"]       = "Cozy",     ["warm"]       = "Warm",
        ["calm"]       = "Still",    ["serene"]      = "Serene",
        ["peaceful"]   = "Quiet",    ["tranquil"]    = "Tranquil",
        ["mysterious"] = "Mystic",   ["dark"]        = "Dark",
        ["noir"]       = "Noir",     ["gothic"]      = "Shadow",
        ["romantic"]   = "Velvet",   ["passionate"]  = "Ember",
        ["elegant"]    = "Lux",      ["luxurious"]   = "Luxe",
        ["minimal"]    = "Bare",     ["clean"]       = "Pure",
        ["playful"]    = "Playful",  ["energetic"]   = "Vivid",
        ["vibrant"]    = "Electric", ["dreamy"]      = "Drift",
        ["nostalgic"]  = "Memory",   ["rustic"]      = "Rustic",
        ["vintage"]    = "Vintage",  ["retro"]       = "Retro",
        ["moody"]      = "Moody",    ["bold"]        = "Bold",
        ["soft"]       = "Soft",     ["glow"]        = "Glow",
        ["mellow"]     = "Mellow",   ["whimsical"]   = "Whimsy",
        ["golden"]     = "Golden",   ["shadow"]      = "Shadow",
        ["melancholy"] = "Dusk",     ["hazy"]        = "Haze",
    };

    // ── Time-of-day name fragments ─────────────────────────────────────────────
    private static readonly Dictionary<string, string> TimeUpgrade =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["dawn"]       = "Dawn",     ["sunrise"]     = "Sunrise",
        ["morning"]    = "Morning",  ["noon"]        = "Midday",
        ["afternoon"]  = "Afternoon",["sunset"]      = "Sunset",
        ["dusk"]       = "Dusk",     ["twilight"]    = "Twilight",
        ["evening"]    = "Evening",  ["night"]       = "Night",
        ["midnight"]   = "Midnight", ["golden"]      = "Golden Hour",
        ["nocturnal"]  = "Nocturne",
    };

    // ── Preposition connectors for names ─────────────────────────────────────
    private static readonly string[] Connectors =
        ["at", "in", "of the", "by the", "under the", "along the", "above the"];

    private static readonly Random Rng = new();

    // ── Public API ────────────────────────────────────────────────────────────

    public static (string Name, string Description) Generate(VibeSignal signal)
    {
        if (!signal.HasSignal)
            return ("My Vibe", "A custom generated theme.");

        // Categorise matched keywords.
        var byCategory = signal.MatchedKeywords
            .GroupBy(k => signal.KeywordCategories.GetValueOrDefault(k, "other"))
            .ToDictionary(g => g.Key, g => g.ToList());

        string name = BuildName(byCategory, signal);
        string desc = BuildDescription(byCategory, signal);
        return (name, desc);
    }

    // ── Name construction ─────────────────────────────────────────────────────

    private static string BuildName(
        Dictionary<string, List<string>> byCategory, VibeSignal signal)
    {
        // Priority 1: mood + environment  → "Mystic Forest" / "Velvet Ocean"
        if (TryGet(byCategory, "mood", out var mood) &&
            TryGet(byCategory, "environment", out var env))
        {
            return $"{Upgrade(mood, MoodUpgrade)} {TitleCase(env)}";
        }

        // Priority 2: time + environment  → "Midnight Forest" / "Dawn Reef"
        if (TryGet(byCategory, "time", out var time) &&
            TryGet(byCategory, "environment", out env))
        {
            return $"{Upgrade(time, TimeUpgrade)} {TitleCase(env)}";
        }

        // Priority 3: environment + connector + environment  → "Forest at Dusk"
        if (TryGet(byCategory, "time", out time) &&
            TryGet(byCategory, "mood", out mood))
        {
            return $"{Upgrade(time, TimeUpgrade)} {Upgrade(mood, MoodUpgrade)}";
        }

        // Priority 4: season + environment  → "Autumn Canyon"
        if (TryGet(byCategory, "season", out var season) &&
            TryGet(byCategory, "environment", out env))
        {
            return $"{TitleCase(season)} {TitleCase(env)}";
        }

        // Priority 5: material + mood  → "Velvet Noir" / "Amber Glow"
        if (TryGet(byCategory, "material", out var mat) &&
            TryGet(byCategory, "mood", out mood))
        {
            return $"{TitleCase(mat)} {Upgrade(mood, MoodUpgrade)}";
        }

        // Priority 6: food + environment  → "Espresso Dusk"
        if (TryGet(byCategory, "food", out var food) &&
            TryGet(byCategory, "environment", out env))
        {
            return $"{TitleCase(food)} {TitleCase(env)}";
        }

        // Priority 7: place + time  → "Tokyo Twilight"
        if (TryGet(byCategory, "place", out var place) &&
            TryGet(byCategory, "time", out time))
        {
            return $"{TitleCase(place)} {Upgrade(time, TimeUpgrade)}";
        }

        // Fallback: just use the most prominent word + Dark/Light suffix
        var first = signal.MatchedKeywords.FirstOrDefault() ?? "Custom";
        var suffix = signal.IsDark ? "Noir" : "Light";
        return $"{TitleCase(first)} {suffix}";
    }

    // ── Description construction ──────────────────────────────────────────────

    private static string BuildDescription(
        Dictionary<string, List<string>> byCategory, VibeSignal signal)
    {
        var parts = new List<string>();

        // Opening clause based on tone
        string tone = signal.SentimentValence switch
        {
            > 0.4f  => "Warm and inviting",
            > 0.1f  => "Soft and welcoming",
            < -0.4f => "Moody and atmospheric",
            < -0.1f => "Cool and contemplative",
            _       => "Balanced and versatile",
        };
        parts.Add(tone);

        // Environment descriptor
        if (byCategory.TryGetValue("environment", out var envs))
            parts.Add($"— inspired by {CommaSep(envs.Take(2).Select(TitleCase))}");

        // Time or season
        if (byCategory.TryGetValue("time", out var times))
            parts.Add($"at {CommaSep(times.Take(1).Select(TitleCase))}");
        else if (byCategory.TryGetValue("season", out var seasons))
            parts.Add($"in {CommaSep(seasons.Take(1).Select(TitleCase))}");

        // Material texture
        if (byCategory.TryGetValue("material", out var mats))
            parts.Add($"with {CommaSep(mats.Take(2).Select(TitleCase))} textures");

        // Mood close
        if (byCategory.TryGetValue("mood", out var moods))
            parts.Add($"evoking a {CommaSep(moods.Take(2))} atmosphere");

        // Dark mode note
        if (signal.IsDark)
            parts.Add("with a dark, rich palette");

        return string.Join(" ", parts) + ".";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryGet(
        Dictionary<string, List<string>> d, string key, out string value)
    {
        value = string.Empty;
        if (!d.TryGetValue(key, out var list) || list.Count == 0) return false;
        value = list[0];
        return true;
    }

    private static string Upgrade(string word, Dictionary<string, string> map)
        => map.TryGetValue(word, out var upgraded) ? upgraded : TitleCase(word);

    private static string TitleCase(string w)
        => string.IsNullOrEmpty(w) ? w
         : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant();

    private static string CommaSep(IEnumerable<string> items)
        => string.Join(" & ", items);
}
