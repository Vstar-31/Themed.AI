using System.Text.RegularExpressions;
using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Curated aesthetic vocabulary for references that a generic color lexicon cannot understand:
/// named internet aesthetics, visual movements, pop-culture shorthand, and Pinterest's 2026 trend
/// names. Profiles are art-directed starting points; prompt modifiers are layered onto the profile,
/// then PaletteQualityValidator makes the result readable before it reaches any UI.
/// </summary>
public static class AestheticProfileCatalog
{
    public sealed record Profile(
        string Id,
        string Name,
        string Description,
        string[] Aliases,
        string BackgroundBase,
        string BackgroundAlt,
        string Surface,
        string AccentPrimary,
        string AccentStrong,
        string TextPrimary,
        string TextMuted,
        string BorderSubtle);

    private static readonly Profile[] AllProfiles =
    [
        new("neon-afterglow", "Neon Afterglow", "A nocturnal synthwave palette: ink-dark surfaces, ultraviolet depth, hot-magenta glow and a clean cyan counterpoint.",
            ["neon afterglow", "neon nightlife", "neon tokyo", "cyberpunk", "cyberpunk 2077", "night city", "synthwave city"],
            "#0B0616", "#160D27", "#24113A", "#FF4FD8", "#77F7FF", "#F7EFFF", "#C8B8D8", "#4A2A63"),
        new("vaporwave", "Vaporwave Dream", "A nostalgic digital sunset in deep violet, electric pink, pool cyan and soft lilac.",
            ["vaporwave", "vapor wave", "aesthetic 80s", "retrowave", "retro wave", "synthwave", "outrun"],
            "#110B2B", "#211443", "#34205C", "#FF71CE", "#01CDFE", "#FFF0FC", "#C9B8F1", "#513B78"),
        new("frutiger-aero", "Frutiger Aero", "Glossy Y2K optimism: sky blue, liquid aqua, fresh whites and crisp oceanic accents.",
            ["frutiger aero", "aero", "windows vista aesthetic", "glossy aqua", "liquid glass aesthetic"],
            "#EAFBFF", "#CDEEF5", "#B5DFE8", "#087FCA", "#07566F", "#123747", "#416B7A", "#93C8D6"),
        new("y2k-chrome", "Chrome Dream", "Y2K futurism with polished silver-lilac, icy highlights and sharp violet hardware accents.",
            ["y2k", "y2k aesthetic", "chrome dream", "cyber y2k", "2000s futurism", "liquid chrome"],
            "#F3F1FC", "#DAD6EC", "#C1BBD9", "#5945A8", "#2F285F", "#211D35", "#5C5678", "#AAA3CD"),
        new("dark-academia", "Dark Academia", "Ink, old-library umber, worn leather and antique brass: cinematic, literate and warm without going muddy.",
            ["dark academia", "dark academic", "gothic academia", "old library", "old money library"],
            "#17191D", "#23262A", "#302D2C", "#B68A4A", "#E1C48D", "#F5EEE0", "#C9BDA7", "#555052"),
        new("light-academia", "Light Academia", "Sunlit paper, pale stone, quiet olive and warm archival ink with a soft scholarly glow.",
            ["light academia", "light academic", "romantic academia", "sunlit library"],
            "#F6F0E5", "#E8DDCB", "#D8C8AE", "#697A50", "#8D5C40", "#302A25", "#655A4F", "#C6B69C"),
        new("poetcore", "Poetcore", "Notebook cream, ink-blue, dried-rose leather and fountain-pen contrast for a quiet protagonist desk.",
            ["poetcore", "poet core", "poet aesthetic", "writercore", "writer aesthetic", "pen pals", "pen pals trend", "booktok desk"],
            "#F4E9D8", "#E6D4BE", "#D4BDA2", "#824E43", "#304453", "#302923", "#65574B", "#C6B49C"),
        new("cottagecore", "Cottagecore", "Meadow sage, weathered cream and botanical greens: handmade, natural and softly sunlit.",
            ["cottagecore", "forestcore", "naturecore", "grandmacore", "cozy cottage", "cottage garden"],
            "#F0F3E6", "#DEE8D2", "#C5D5B8", "#4F7550", "#31543D", "#263529", "#5C6D5B", "#B5C5A9"),
        new("wilderkind", "Wilderkind", "A storybook woodland palette of fern, fawn, moth-wing cream and restrained berry accents.",
            ["wilderkind", "wildkind", "deer aesthetic", "forest fairy", "fairycore", "mushroomcore", "enchanted forest"],
            "#F0F4E8", "#DCE8D0", "#C8D9B7", "#4F795D", "#A65B62", "#27372D", "#5D705F", "#B9CBB0"),
        new("vamp-romantic", "Vamp Romantic", "Romantic goth after dark: black-cherry shadows, oxblood, rose satin and candlelit ivory.",
            ["vamp romantic", "vamp romantic trend", "romantic goth", "gothic romance", "vampire aesthetic", "vampirecore", "carmilla aesthetic"],
            "#100910", "#21121F", "#321828", "#C32968", "#E6A8C4", "#FFF2F5", "#D1AEC0", "#593047"),
        new("neo-deco", "Neo Deco", "A modern Art Deco treatment: midnight plum, sculpted brass, lacquered surfaces and geometric glamour.",
            ["neo deco", "neo-deco", "art deco", "modern art deco", "deco glam", "brass and black"],
            "#15131C", "#24212E", "#332E3E", "#C59A55", "#F0D39A", "#F8F2E9", "#C7B9C7", "#5B4C61"),
        new("extra-celestial", "Extra Celestial", "Opalescent ice, deep orbital blue, lunar lavender and atmospheric cyan from a polished sci-fi future.",
            ["extra celestial", "extra-celestial", "spacecore", "cosmic aesthetic", "alien core", "galaxy princess", "interstellar", "celestial aesthetic"],
            "#081126", "#14264A", "#20385D", "#7DDFF5", "#BBA2FF", "#F3F7FF", "#B9C9E6", "#3D537B"),
        new("glamoratti", "Glamoratti", "Maximalist 80s luxury: lacquered aubergine, champagne metal and confident high-gloss highlights.",
            ["glamoratti", "glamoratti trend", "80s luxury", "maximalist luxury", "power dressing aesthetic"],
            "#171019", "#2B1C2C", "#403048", "#C99B52", "#F0D27C", "#FFF5E9", "#D4BED0", "#684B69"),
        new("opera-aesthetic", "Opera Aesthetic", "Velvet curtains, theatrical wine red, antique gold and candlelit ivory with dramatic depth.",
            ["opera aesthetic", "opera aesthetic trend", "dark cabaret", "midnight masquerade", "masquerade aesthetic", "theatrical romantic"],
            "#170B17", "#2C1027", "#451A35", "#A92A50", "#E6B46B", "#FFF0E3", "#D8B7C3", "#704052"),
        new("mystic-outlands", "Mystic Outlands", "An otherworldly landscape of lichen, mossed stone, stormy teal and mineral gold.",
            ["mystic outlands", "mystic outlands trend", "fantasy landscape", "mythic wilderness", "dark fairy forest"],
            "#101B22", "#1E2C2C", "#2B3D3A", "#69B798", "#D6BC72", "#F2F5E8", "#B9CBBB", "#455D55"),
        new("cool-blue", "Cool Blue", "Glacier blue, porcelain, mist and marine ink: fresh, precise and quietly futuristic.",
            ["cool blue", "cool blue trend", "glacier aesthetic", "ice blue aesthetic", "blue hour", "icy minimalism"],
            "#EAF4FF", "#D1E5F8", "#B8D4EB", "#286DB5", "#17436F", "#172F46", "#4E6B87", "#A4C4E0"),
        new("gimme-gummy", "Gimme Gummy", "Jelly-bright color with candy-glass pink, grape, creamy vanilla and playful translucent accents.",
            ["gimme gummy", "gimme gummy trend", "gummy aesthetic", "jellycore", "candycore", "gummy bear aesthetic", "jelly candy"],
            "#FFF3FA", "#F4DDF0", "#E5CAF1", "#C83E96", "#7047BF", "#351F3B", "#714E77", "#DFB9D9"),
        new("glitchy-glam", "Glitchy Glam", "Digital-night glamour: ink, ultraviolet, laser pink and mint-cyan interference with a polished finish.",
            ["glitchy glam", "glitchy glam trend", "glitchcore", "glitch aesthetic", "cyber glam", "digital rave"],
            "#0E0C1B", "#21172C", "#33213F", "#F653A3", "#43E1CB", "#FFF0FA", "#C9B3D8", "#563260"),
        new("afrohemian", "Afrohemian Decor", "Earth pigments, woven umber, ochre, terracotta and grounded botanical warmth.",
            ["afrohemian", "afrohemian decor", "afro bohemian", "boho maximalist", "earthy maximalism"],
            "#F4EBD8", "#E4D2B4", "#D3B88E", "#9B4B31", "#4D6B45", "#30271E", "#655545", "#BDA47C"),
        new("khaki-coded", "Khaki Coded", "Field-kit neutrals with olive, weathered stone, canvas and a practical deep-forest anchor.",
            ["khaki coded", "khaki-coded", "gorpcore", "utility aesthetic", "field jacket aesthetic", "expedition core"],
            "#F0EDDF", "#DCD6C0", "#C5C2A7", "#536B4D", "#465C70", "#282F2A", "#5C6558", "#B8B49B"),
        new("funhaus", "FunHaus", "Playful Bauhaus energy: cream, lilac, periwinkle and fearless coral-and-cobalt color blocking.",
            ["funhaus", "funhaus trend", "playful bauhaus", "color block aesthetic", "memphis design"],
            "#FBF2E8", "#EBD7E8", "#CCD8F0", "#D93661", "#403BB5", "#28223D", "#5F507A", "#DAB9CE"),
        new("laced-up", "Laced Up", "Modern lace and soft romantic detail translated into porcelain, mauve, rosewood and plum.",
            ["laced up", "laced-up", "laced up trend", "lacecore", "lace aesthetic", "romantic balletcore"],
            "#FFF7F9", "#F1E1E9", "#DCCBDE", "#A84C79", "#574071", "#342738", "#735A73", "#E0C9D7"),
        new("throwback-kid", "Throwback Kid", "Playful nostalgia: soft candy colors, retro toy-box blue and raspberry with clean readable ink.",
            ["throwback kid", "throwback kid trend", "kidcore", "90s nostalgia", "nostalgia core", "retro toy aesthetic"],
            "#FFF5EC", "#F7DCE4", "#D6EAF3", "#D74673", "#337EA6", "#30243A", "#69596C", "#E4BCC9"),
        new("barbiecore", "Barbiecore", "High-fashion pink with glossy rose, fuchsia and violet grounding; bold, playful and deliberately unapologetic.",
            ["barbiecore", "barbie core", "pink glamour", "hot pink aesthetic", "pink maximalism"],
            "#FFF0F7", "#FFD3E7", "#F2B8D8", "#D90086", "#7828A0", "#3B1838", "#7C476D", "#E9A5CC"),
        new("arcane-inspired", "Arcane-Inspired", "Painterly Zaun-and-Piltover contrast: bruised violet, petrol blue, brass firelight and pale enamel.",
            ["arcane", "arcane series", "arcane league of legends", "zaun", "piltover", "hextech"],
            "#111126", "#20213D", "#30314F", "#8861CC", "#F0B34D", "#F4F1FF", "#BDB9E0", "#4B4A72"),
        new("ghibli-inspired", "Storybook Forest", "Hand-painted storybook greens, airy sky, warm paper and moss-dark anchors; whimsical without muddy colors.",
            ["studio ghibli", "ghibli", "totoro", "howls moving castle", "spirited away", "storybook anime", "ghibli forest"],
            "#EDF5E8", "#D1E4CD", "#B8D4B5", "#3F795D", "#2F5B68", "#26372D", "#5C705E", "#AAC8AF"),
        new("zelda-inspired", "Hyrule Forest", "Adventure-fantasy forest green, ancient gold, moss light and crisp sky-blue relic accents.",
            ["zelda", "legend of zelda", "hyrule", "korok forest", "zelda forest"],
            "#10231C", "#1A392B", "#28503B", "#D7B653", "#8DDAA5", "#F2F5E7", "#BDCCB0", "#456653"),
        new("wednesday-goth", "Wednesday Goth", "Black-plum, graphite, smoky lavender and cold silver: sharp, gothic and deliberately restrained.",
            ["wednesday addams", "wednesday", "addams family", "nevermore academy", "goth girl aesthetic"],
            "#100F17", "#1E1B29", "#2D293B", "#9C83C8", "#D4CADF", "#F4F0F8", "#B7AEC4", "#50485E"),
        new("cherry-grove", "Cherry Grove", "Minecraft-inspired cherry blossoms: pixel-soft pink, moss, sky haze and earthy block tones.",
            ["minecraft cherry grove", "cherry grove", "minecraft cherry blossom", "minecraft", "pink forest pixel"],
            "#F8F0F5", "#EAD8E5", "#C8D7C1", "#C85C91", "#5E8063", "#302B35", "#6E626E", "#D7B8CC"),
        new("space-opera", "Space Opera", "Deep orbital navy, starship cyan, warm starlight and a disciplined cinematic control-room palette.",
            ["space opera", "star wars", "galactic empire", "jedi", "sith", "starship cockpit", "interstellar cockpit"],
            "#09111F", "#17263B", "#22364F", "#F2C14E", "#65D2EE", "#F3F7FC", "#B7C6D8", "#425A73"),
        new("kpop-stage", "K-Pop Stage", "A concert-light palette of glossy magenta, electric blue, cool black and a pearl highlight.",
            ["kpop", "k-pop", "k pop comeback", "idol stage", "k-pop stage", "album concept", "comeback concept"],
            "#100B1B", "#251332", "#38204A", "#F34CA7", "#47B9FF", "#FFF2FB", "#CBB7D8", "#57356B"),
        new("fallout-terminal", "Retro-Future Terminal", "Analog green phosphor, charcoal glass and amber warning lights on a retro-futurist terminal.",
            ["fallout", "pip boy", "pip-boy", "retro terminal", "green phosphor", "terminal hacker", "hacker terminal"],
            "#0C1510", "#17271B", "#24392A", "#8FE388", "#F0C568", "#F0F7E9", "#B7CCAE", "#3E5B44"),
        new("blade-runner", "Rain City 2049", "Rain-slick petrol blue with sodium amber, smoky violet and cinematic night contrast.",
            ["blade runner", "blade runner 2049", "rain city 2049", "neo noir city", "neo-noir", "orange teal cinematic"],
            "#0B111B", "#151F2D", "#223040", "#FFAA52", "#57D4D0", "#F4F6FA", "#B7C4D2", "#40566B"),
        new("tron", "Digital Grid", "Black glass and precise cyan light lines, with vivid blue reserved for interactive highlights.",
            ["tron", "tron legacy", "digital grid", "laser grid", "blue cyber grid"],
            "#050C14", "#0A1925", "#112A39", "#43E8FF", "#3C8DFF", "#F1FCFF", "#ADD8E3", "#245064"),
        new("city-pop", "City Pop Afterhours", "Japanese city-pop warmth: sunset coral, poolside cyan, night-indigo and a nostalgic cream highlight.",
            ["city pop", "japanese city pop", "citypop", "city-pop", "tokyo city pop", "80s anime city"],
            "#11132A", "#20294A", "#2D3A5A", "#FF826D", "#69DCE2", "#FFF4E6", "#C0C7DA", "#4F5879"),
        new("solarpunk", "Solarpunk Garden", "Hopeful green technology: living leaf, clean ivory, sunlit gold and clear sky teal.",
            ["solarpunk", "solar punk", "eco futurism", "eco-futurism", "green future"],
            "#F0F7E9", "#D7EAD0", "#B8D7B8", "#2C795D", "#C18A35", "#23382B", "#566F5B", "#B0C9B1"),
        new("coquette", "Coquette", "Soft ballet pink, powder porcelain, rosewood and vintage ribbon tones with a crisp plum anchor.",
            ["coquette", "coquettecore", "balletcore", "ballet core", "soft girl aesthetic", "bow aesthetic"],
            "#FFF5F8", "#F4E1EC", "#E2C9DB", "#B64E7A", "#654272", "#35263A", "#745B77", "#E3C7D8"),
        new("cyber-sigil", "Cyber Sigil", "Internet-era occult futurism: black-violet, acid lime, bruised silver and neon chartreuse.",
            ["cyber sigil", "sigilcore", "cyber sigilism", "internet occult", "dark web aesthetic"],
            "#0B0B12", "#1B1624", "#2B2336", "#C3F53C", "#D35DFF", "#F4F5EF", "#C4C3D0", "#4E425F"),
    ];

    public static IReadOnlyList<Profile> Profiles => AllProfiles;

    public static Profile? Find(string? prompt)
    {
        var normalized = Normalize(prompt);
        if (normalized.Length == 0) return null;
        var padded = $" {normalized} ";

        Profile? best = null;
        var bestScore = -1;
        foreach (var profile in AllProfiles)
        foreach (var alias in profile.Aliases)
        {
            var normalizedAlias = Normalize(alias);
            if (normalizedAlias.Length == 0 || !padded.Contains($" {normalizedAlias} ", StringComparison.Ordinal))
                continue;

            // Prefer the most specific alias: "Neon Afterglow" must beat the generic "neon"
            // profile, and a franchise/sub-aesthetic phrase must beat a broad vibe label.
            var tokenCount = normalizedAlias.Count(c => c == ' ') + 1;
            var score = tokenCount * 1000 + normalizedAlias.Length;
            if (normalized.Equals(normalizedAlias, StringComparison.Ordinal)) score += 10000;
            if (score > bestScore)
            {
                best = profile;
                bestScore = score;
            }
        }

        return best;
    }

    public static CozyTheme CreateTheme(Profile profile, string prompt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var theme = new CozyTheme
        {
            Id = Guid.NewGuid().ToString(),
            Name = profile.Name,
            Description = profile.Description,
            IsBuiltIn = false,
            LastModified = DateTimeOffset.UtcNow,
            BackgroundBase = profile.BackgroundBase,
            BackgroundAlt = profile.BackgroundAlt,
            Surface = profile.Surface,
            AccentPrimary = profile.AccentPrimary,
            AccentStrong = profile.AccentStrong,
            TextPrimary = profile.TextPrimary,
            TextMuted = profile.TextMuted,
            BorderSubtle = profile.BorderSubtle,
        };

        ApplyPromptModifiers(theme, prompt);
        PaletteQualityValidator.ValidateAndRepair(theme);
        return theme;
    }

    private static void ApplyPromptModifiers(CozyTheme theme, string prompt)
    {
        var text = Normalize(prompt);
        var (baseH, baseS, baseL) = ColorMath.HexToHsl(theme.BackgroundBase);
        var (altH, altS, altL) = ColorMath.HexToHsl(theme.BackgroundAlt);
        var (surfaceH, surfaceS, surfaceL) = ColorMath.HexToHsl(theme.Surface);
        var (primaryH, primaryS, primaryL) = ColorMath.HexToHsl(theme.AccentPrimary);
        var (strongH, strongS, strongL) = ColorMath.HexToHsl(theme.AccentStrong);
        var (borderH, borderS, borderL) = ColorMath.HexToHsl(theme.BorderSubtle);

        var lightnessDelta = 0f;
        if (HasAny(text, "darker", "make it darker", "darken the palette", "less bright", "dim it"))
            lightnessDelta = -0.055f;
        else if (HasAny(text, "lighter", "make it lighter", "lighten the palette", "brighter background"))
            lightnessDelta = 0.055f;

        var saturationDelta = 0f;
        if (HasAny(text, "more muted", "muted", "desaturated", "less saturated", "soften the colors", "softer"))
            saturationDelta = -0.13f;
        else if (HasAny(text, "more vibrant", "vivid", "more saturated", "punchier", "electric", "extra neon"))
            saturationDelta = 0.13f;

        var hueShift = 0f;
        var hueTarget = MatchHueModifier(text);
        if (hueTarget is { } target)
        {
            var delta = target - baseH;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            hueShift = delta;
        }
        else if (HasAny(text, "warmer", "more warm", "warmer tones"))
            hueShift = 10;
        else if (HasAny(text, "cooler", "more cool", "cooler tones", "make it icy"))
            hueShift = -10;

        theme.BackgroundBase = ColorMath.HslToHex(baseH + hueShift, baseS, baseL + lightnessDelta);
        theme.BackgroundAlt = ColorMath.HslToHex(altH + hueShift, altS, altL + lightnessDelta);
        theme.Surface = ColorMath.HslToHex(surfaceH + hueShift, surfaceS, surfaceL + lightnessDelta);
        theme.AccentPrimary = ColorMath.HslToHex(primaryH + hueShift, primaryS + saturationDelta, primaryL + lightnessDelta);
        theme.AccentStrong = ColorMath.HslToHex(strongH + hueShift, strongS + saturationDelta, strongL + lightnessDelta);
        theme.BorderSubtle = ColorMath.HslToHex(borderH + hueShift, borderS, borderL + lightnessDelta);
    }

    private static float? MatchHueModifier(string text)
    {
        var cues = new (string[] Phrases, float Hue)[]
        {
            (["more red", "red accents", "make it red", "in red"], 0),
            (["more orange", "orange accents", "make it orange", "in orange"], 28),
            (["more gold", "gold accents", "make it gold", "in gold"], 45),
            (["more yellow", "yellow accents", "make it yellow", "in yellow"], 55),
            (["more green", "green accents", "make it green", "in green"], 125),
            (["more teal", "teal accents", "make it teal", "in teal"], 180),
            (["more cyan", "cyan accents", "make it cyan", "in cyan"], 190),
            (["more blue", "blue accents", "make it blue", "in blue", "bluer"], 220),
            (["more indigo", "indigo accents", "make it indigo", "in indigo"], 245),
            (["more purple", "purple accents", "make it purple", "in purple"], 275),
            (["more magenta", "magenta accents", "make it magenta", "in magenta"], 315),
            (["more pink", "pink accents", "make it pink", "in pink", "pinker"], 335),
        };

        foreach (var (phrases, hue) in cues)
            if (HasAny(text, phrases)) return hue;
        return null;
    }

    private static bool HasAny(string text, params string[] phrases)
        => phrases.Any(phrase => text.Contains(Normalize(phrase), StringComparison.Ordinal));

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lower = value.ToLowerInvariant().Replace("’", "'");
        lower = Regex.Replace(lower, @"[^a-z0-9]+", " ");
        return Regex.Replace(lower, @"s+", " ").Trim();
    }
}
