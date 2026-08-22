using ThemeManager.Core.Skins;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Maps stemmed words to <see cref="WidgetSignal"/> votes — the widget-generation sibling of
/// <see cref="ColorLexicon"/>. Every key below is the *actual* output of
/// <see cref="PorterStemmer.Stem"/> for that word (verified, not guessed — stemming produces
/// some non-obvious results, e.g. "memory" → "memori", "massive" → "mass", "corner" → "corn").
/// </summary>
public static class WidgetLexicon
{
    private static WidgetSignal ForMeasure(MeasureType type) =>
        new(type, null, null, null, null, null, "measure");

    private static WidgetSignal ForStyle(double size, bool bold) =>
        new(null, null, size, bold, null, null, "style");

    private static WidgetSignal ForKind(MeterKind kind) =>
        new(null, kind, null, null, null, null, "kind");

    private static WidgetSignal ForPosition(string? vertical, string? horizontal) =>
        new(null, null, null, null, vertical, horizontal, "position");

    public static readonly IReadOnlyDictionary<string, WidgetSignal> Entries =
        new Dictionary<string, WidgetSignal>(StringComparer.OrdinalIgnoreCase)
    {
        // ════ MEASURES ════════════════════════════════════════════════════════
        ["cpu"] = ForMeasure(MeasureType.Cpu),
        ["processor"] = ForMeasure(MeasureType.Cpu),
        ["usag"] = ForMeasure(MeasureType.Cpu),       // "usage" — as in "cpu usage"

        ["ram"] = ForMeasure(MeasureType.Memory),
        ["memori"] = ForMeasure(MeasureType.Memory), // "memory"

        ["disk"] = ForMeasure(MeasureType.DiskFree),
        ["storag"] = ForMeasure(MeasureType.DiskFree), // "storage"
        ["drive"] = ForMeasure(MeasureType.DiskFree),
        ["space"] = ForMeasure(MeasureType.DiskFree),  // "disk spaces" — was fuzzy-matching "date"

        ["network"] = ForMeasure(MeasureType.NetworkDown),
        ["internet"] = ForMeasure(MeasureType.NetworkDown),
        ["wifi"] = ForMeasure(MeasureType.NetworkDown),
        ["download"] = ForMeasure(MeasureType.NetworkDown),
        ["upload"] = ForMeasure(MeasureType.NetworkUp),

        ["batteri"] = ForMeasure(MeasureType.Battery), // "battery"
        ["charg"] = ForMeasure(MeasureType.Battery),   // "charge"/"charging"

        ["clock"] = ForMeasure(MeasureType.Time),
        ["time"] = ForMeasure(MeasureType.Time),
        ["watch"] = ForMeasure(MeasureType.Time),

        ["date"] = ForMeasure(MeasureType.Date),
        ["calendar"] = ForMeasure(MeasureType.Date),
        ["dai"] = ForMeasure(MeasureType.Date), // "day"

        ["uptim"] = ForMeasure(MeasureType.Uptime), // "uptime"

        ["weath"] = ForMeasure(MeasureType.WeatherTemp),      // "weather"
        ["temperatur"] = ForMeasure(MeasureType.WeatherTemp), // "temperature"
        ["forecast"] = ForMeasure(MeasureType.WeatherTemp),

        ["rain"] = ForMeasure(MeasureType.WeatherDesc),
        ["precipit"] = ForMeasure(MeasureType.WeatherDesc), // "precipitation"
        ["snow"] = ForMeasure(MeasureType.WeatherDesc),
        ["cloud"] = ForMeasure(MeasureType.WeatherDesc),
        ["sun"] = ForMeasure(MeasureType.WeatherDesc),
        ["storm"] = ForMeasure(MeasureType.WeatherDesc),

        ["music"] = ForMeasure(MeasureType.MediaTitle),
        ["song"] = ForMeasure(MeasureType.MediaTitle),
        ["playlist"] = ForMeasure(MeasureType.MediaTitle),
        ["artist"] = ForMeasure(MeasureType.MediaArtist),
        // Deliberately not adding "track" — it collides semantically with "track my CPU"-style
        // phrasing (verb sense, implying a graph), and Absorb() only adds measures, never
        // overrides, so a CPU-graph request would silently gain an unwanted media measure too.
        // "play" is already taken by the style entry below ("playful"), so no play/pause word
        // for MediaState yet — Title/Artist covers what most people mean by "a music widget."

        // VibeFinderAI (vibefinderai.onrender.com) integration — a *different* "what song" than
        // MediaTitle above: Media reads Windows' currently-playing session, this asks Vijay's own
        // backend to recommend a track for a typed vibe phrase (see VibeFinderMeasure). Not
        // "song"/"playlist" — those already mean MediaTitle and reassigning them would silently
        // change existing widget-generation behavior.
        ["vibe"] = ForMeasure(MeasureType.VibeTrackTitle),
        ["vibefind"] = ForMeasure(MeasureType.VibeTrackTitle), // "vibefinder"/"vibefinderai"
        ["recommend"] = ForMeasure(MeasureType.VibeTrackTitle), // "recommend"/"recommended"/"recommendation"
        ["mood"] = ForMeasure(MeasureType.VibeMood),

        // ════ STYLE / SIZE ════════════════════════════════════════════════════
        ["minim"] = ForStyle(0.75, bold: false),       // "minimal"
        ["minimalist"] = ForStyle(0.75, bold: false),
        ["simpl"] = ForStyle(0.80, bold: false),       // "simple"
        ["clean"] = ForStyle(0.85, bold: false),
        ["compact"] = ForStyle(0.75, bold: false),
        ["smal"] = ForStyle(0.75, bold: false),        // "small"
        ["tini"] = ForStyle(0.65, bold: false),        // "tiny"

        ["big"] = ForStyle(1.35, bold: true),
        ["larg"] = ForStyle(1.30, bold: true),         // "large"
        ["huge"] = ForStyle(1.50, bold: true),
        ["bold"] = ForStyle(1.15, bold: true),
        ["mass"] = ForStyle(1.50, bold: true),         // "massive"
        ["chunki"] = ForStyle(1.30, bold: true),       // "chunky"

        // ════ CONSUMER-FRIENDLY AESTHETICS ════════════════════════════════════
        // Normal users describe *feelings*, not specs — "cute cat clock" not "13px time meter"
        ["cute"] = ForStyle(1.10, bold: true),
        ["chibi"] = ForStyle(0.90, bold: true),
        ["kawaii"] = ForStyle(1.10, bold: true),
        ["pretti"] = ForStyle(1.10, bold: false),      // "pretty"
        ["beauti"] = ForStyle(1.10, bold: false),      // "beautiful"
        ["aesthet"] = ForStyle(1.05, bold: false),     // "aesthetic"
        ["cozi"] = ForStyle(1.10, bold: true),         // "cozy"
        ["warm"] = ForStyle(1.05, bold: true),
        ["retro"] = ForStyle(1.15, bold: true),
        ["vintag"] = ForStyle(1.10, bold: true),       // "vintage"
        ["neon"] = ForStyle(1.20, bold: true),
        ["glow"] = ForStyle(1.10, bold: true),
        ["cyber"] = ForStyle(1.15, bold: true),
        ["futurist"] = ForStyle(1.20, bold: true),     // "futuristic"
        ["modern"] = ForStyle(1.05, bold: false),
        ["sleek"] = ForStyle(0.90, bold: false),
        ["eleg"] = ForStyle(1.05, bold: false),        // "elegant"
        ["luxuri"] = ForStyle(1.15, bold: true),       // "luxury"/"luxurious"
        ["premium"] = ForStyle(1.15, bold: true),
        ["play"] = ForStyle(1.10, bold: true),         // "playful"
        ["funki"] = ForStyle(1.10, bold: true),        // "funky"
        ["cool"] = ForStyle(1.05, bold: false),
        ["pasti"] = ForStyle(1.00, bold: false),       // "pastel"
        ["dark"] = ForStyle(1.00, bold: true),
        ["moodi"] = ForStyle(1.00, bold: true),        // "moody"
        ["bright"] = ForStyle(1.10, bold: true),
        ["pixel"] = ForStyle(0.85, bold: true),
        ["round"] = ForStyle(1.10, bold: false),       // "rounded"
        ["flat"] = ForStyle(0.90, bold: false),
        ["glass"] = ForStyle(1.05, bold: false),       // "glassmorphism"
        ["anim"] = ForStyle(1.00, bold: false),        // "anime"
        ["cat"] = ForStyle(1.05, bold: true),          // decorative — maps to style, not measure
        ["dog"] = ForStyle(1.05, bold: true),
        ["star"] = ForStyle(1.10, bold: true),
        ["floral"] = ForStyle(1.05, bold: false),
        ["dream"] = ForStyle(1.10, bold: false),       // "dreamy"
        ["magic"] = ForStyle(1.10, bold: true),        // "magical"

        // ════ METER KIND PREFERENCE ═══════════════════════════════════════════
        ["graph"] = ForKind(MeterKind.Graph),
        ["chart"] = ForKind(MeterKind.Graph),
        ["trend"] = ForKind(MeterKind.Graph),
        ["histori"] = ForKind(MeterKind.Graph),        // "history"

        ["bar"] = ForKind(MeterKind.Bar),
        ["progress"] = ForKind(MeterKind.Bar),
        ["gaug"] = ForKind(MeterKind.Bar),              // "gauge"

        // ════ POSITION ════════════════════════════════════════════════════════
        ["top"] = ForPosition("top", null),
        ["upp"] = ForPosition("top", null),             // "upper"
        ["bottom"] = ForPosition("bottom", null),
        ["lower"] = ForPosition("bottom", null),
        ["left"] = ForPosition(null, "left"),
        ["right"] = ForPosition(null, "right"),
        ["corn"] = ForPosition("top", "right"),         // "corner" alone → default to top-right
        ["cent"] = ForPosition("center", "center"),     // "center"/"centered"
        ["middl"] = ForPosition("center", "center"),    // "middle"

        // ════ STOPWORDS ═══════════════════════════════════════════════════════
        // "widget" is so common in prompts that it fuzzy-matches to "right" (distance 3)
        // because of its length, unintentionally throwing the widget to the right side of the screen.
        ["widget"] = new(null, null, null, null, null, null, "stopword"),
        // "show"/"display"/"monitor" are verbs/nouns that appear in almost every widget
        // prompt ("show me...", "display my...", "cpu monitor") — without these, "show"
        // fuzzy-matched "glow" (distance 2) and "monitor" could match measure entries.
        ["show"] = new(null, null, null, null, null, null, "stopword"),
        ["displai"] = new(null, null, null, null, null, null, "stopword"), // "display"
        ["monitor"] = new(null, null, null, null, null, null, "stopword"),
    };
}