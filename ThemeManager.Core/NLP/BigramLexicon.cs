namespace ThemeManager.Core.NLP;

/// <summary>
/// Maps stemmed two-word phrases to <see cref="ColorSignal"/> values.
///
/// Bigrams are checked BEFORE individual word (unigram) lookups in
/// <see cref="VibeAnalyzer"/> so that a compound phrase like "rose gold"
/// gets its own coherent signal rather than two weak, possibly contradictory signals.
///
/// Keys are "stem1 stem2" (joined by a single space, already Porter-stemmed).
/// They are matched against consecutive stemmed token pairs.
///
/// HueWeight is set higher than the constituent unigrams because a compound
/// phrase is a more specific, intentional signal.
/// </summary>
public static class BigramLexicon
{
    private static ColorSignal C(
        float h, float hw, float l, float lw, float s, float sw, float warm, string cat)
        => new(h, hw, l, lw, s, sw, warm, cat);

    // Helper: stem both words and produce the bigram key.
    private static string Key(string a, string b)
        => $"{PorterStemmer.Stem(a)} {PorterStemmer.Stem(b)}";

    public static readonly IReadOnlyDictionary<string, ColorSignal> Entries;

    static BigramLexicon()
    {
        var d = new Dictionary<string, ColorSignal>(StringComparer.OrdinalIgnoreCase);

        void Add(string a, string b, ColorSignal sig) => d[Key(a, b)] = sig;

        // ── Color + material compounds ─────────────────────────────────────────
        Add("rose",    "gold",     C( 22, .95f, .58f, .7f, .60f, .8f,  .7f, "material"));
        Add("rose",    "quartz",   C( 10, .85f, .72f, .7f, .42f, .8f,  .5f, "material"));
        Add("dusty",   "rose",     C(350, .85f, .62f, .7f, .32f, .8f,  .3f, "color"));
        Add("dusty",   "pink",     C(345, .85f, .68f, .7f, .30f, .8f,  .3f, "color"));
        Add("dusty",   "blue",     C(212, .85f, .60f, .7f, .28f, .8f, -.3f, "color"));
        Add("dusty",   "purple",   C(270, .85f, .55f, .7f, .28f, .8f, -.1f, "color"));
        Add("sage",    "green",    C(120, .90f, .55f, .7f, .30f, .8f,  .0f, "color"));
        Add("olive",   "green",    C( 80, .90f, .42f, .7f, .38f, .8f,  .2f, "color"));
        Add("forest",  "green",    C(125, .95f, .30f, .8f, .45f, .9f,  .1f, "environment"));
        Add("mint",    "green",    C(155, .90f, .68f, .7f, .42f, .8f, -.2f, "color"));
        Add("teal",    "blue",     C(185, .90f, .48f, .7f, .62f, .8f, -.5f, "color"));
        Add("sky",     "blue",     C(200, .90f, .65f, .7f, .58f, .8f, -.4f, "color"));
        Add("navy",    "blue",     C(225, .95f, .22f, .8f, .65f, .9f, -.6f, "color"));
        Add("royal",   "blue",     C(228, .90f, .30f, .7f, .72f, .8f, -.5f, "color"));
        Add("cobalt",  "blue",     C(225, .90f, .35f, .7f, .78f, .8f, -.5f, "color"));
        Add("midnight","blue",     C(240, .95f, .15f, .9f, .55f, .9f, -.6f, "color"));
        Add("deep",    "blue",     C(225, .85f, .20f, .8f, .65f, .8f, -.5f, "color"));
        Add("ice",     "blue",     C(200, .85f, .82f, .7f, .30f, .8f, -.5f, "color"));
        Add("baby",    "blue",     C(205, .85f, .78f, .7f, .35f, .8f, -.4f, "color"));
        Add("powder",  "blue",     C(207, .85f, .78f, .7f, .32f, .8f, -.3f, "color"));
        Add("golden",  "yellow",   C( 48, .90f, .60f, .7f, .80f, .8f,  .7f, "color"));
        Add("burnt",   "orange",   C( 20, .90f, .48f, .7f, .72f, .8f,  .7f, "color"));
        Add("deep",    "red",      C(  0, .90f, .28f, .8f, .72f, .8f,  .7f, "color"));
        Add("blood",   "red",      C(  5, .95f, .22f, .8f, .78f, .9f,  .7f, "color"));
        Add("hot",     "pink",     C(330, .90f, .55f, .7f, .80f, .8f,  .5f, "color"));
        Add("pastel",  "pink",     C(340, .85f, .82f, .7f, .45f, .8f,  .4f, "color"));
        Add("neon",    "green",    C(120, .85f, .40f, .6f, .95f, .9f, -.1f, "color"));
        Add("neon",    "pink",     C(315, .85f, .45f, .6f, .95f, .9f,  .2f, "color"));
        Add("neon",    "blue",     C(195, .85f, .35f, .6f, .95f, .9f, -.4f, "color"));
        Add("electric","blue",     C(195, .90f, .35f, .7f, .90f, .9f, -.5f, "color"));
        Add("slate",   "gray",     C(215, .80f, .48f, .7f, .18f, .8f, -.2f, "color"));
        Add("charcoal","gray",     C(220, .70f, .22f, .7f, .10f, .7f, -.1f, "color"));
        Add("warm",    "gray",     C( 30, .60f, .55f, .6f, .10f, .6f,  .3f, "color"));
        Add("cool",    "gray",     C(210, .60f, .55f, .6f, .10f, .6f, -.3f, "color"));
        Add("off",     "white",    C( 48, .40f, .92f, .6f, .08f, .5f,  .2f, "color"));
        Add("warm",    "white",    C( 42, .50f, .91f, .6f, .10f, .5f,  .3f, "color"));
        Add("cool",    "white",    C(210, .40f, .93f, .6f, .06f, .5f, -.2f, "color"));

        // ── Compound environments ─────────────────────────────────────────────
        Add("midnight","ocean",    C(218, .95f, .12f, .9f, .55f, .9f, -.6f, "environment"));
        Add("midnight","forest",   C(135, .90f, .10f, .9f, .40f, .9f, -.1f, "environment"));
        Add("midnight","sky",      C(240, .90f, .08f, .9f, .38f, .8f, -.5f, "environment"));
        Add("dark",    "forest",   C(128, .90f, .15f, .8f, .38f, .8f,  .0f, "environment"));
        Add("dark",    "ocean",    C(205, .90f, .12f, .9f, .48f, .9f, -.6f, "environment"));
        Add("deep",    "ocean",    C(210, .90f, .12f, .9f, .52f, .9f, -.6f, "environment"));
        Add("deep",    "sea",      C(200, .90f, .12f, .9f, .50f, .9f, -.6f, "environment"));
        Add("deep",    "jungle",   C(128, .90f, .15f, .8f, .52f, .9f,  .0f, "environment"));
        Add("golden",  "hour",     C( 38, .95f, .62f, .8f, .78f, .9f,  .8f, "time"));
        Add("golden",  "sunset",   C( 30, .95f, .58f, .8f, .82f, .9f,  .8f, "time"));
        Add("cherry",  "blossom",  C(340, .95f, .78f, .8f, .50f, .9f,  .3f, "environment"));
        Add("autumn",  "leaves",   C( 22, .90f, .52f, .7f, .68f, .8f,  .7f, "season"));
        Add("autumn",  "forest",   C( 28, .90f, .42f, .7f, .55f, .8f,  .6f, "season"));
        Add("spring",  "morning",  C( 60, .80f, .75f, .7f, .42f, .7f,  .3f, "season"));
        Add("winter",  "night",    C(220, .90f, .10f, .9f, .22f, .8f, -.6f, "season"));
        Add("tropical","sunset",   C( 22, .90f, .60f, .7f, .80f, .8f,  .7f, "environment"));
        Add("desert",  "sunset",   C( 20, .90f, .55f, .8f, .72f, .8f,  .8f, "environment"));
        Add("arctic",  "dawn",     C(195, .90f, .80f, .7f, .25f, .8f, -.5f, "environment"));
        Add("northern","light",    C(160, .90f, .25f, .7f, .72f, .8f, -.3f, "environment"));
        Add("northern","lights",   C(160, .90f, .25f, .7f, .72f, .8f, -.3f, "environment"));
        Add("aurora",  "borealis", C(158, .95f, .22f, .8f, .78f, .9f, -.3f, "environment"));

        // ── Material compounds ────────────────────────────────────────────────
        Add("dark",    "wood",     C( 22, .80f, .22f, .7f, .32f, .7f,  .4f, "material"));
        Add("light",   "wood",     C( 35, .75f, .62f, .6f, .32f, .7f,  .5f, "material"));
        Add("raw",     "wood",     C( 30, .75f, .55f, .6f, .35f, .7f,  .5f, "material"));
        Add("dark",    "leather",  C( 18, .80f, .20f, .7f, .32f, .7f,  .4f, "material"));
        Add("aged",    "leather",  C( 24, .75f, .32f, .6f, .30f, .6f,  .4f, "material"));
        Add("raw",     "linen",    C( 46, .70f, .85f, .5f, .18f, .6f,  .4f, "material"));
        Add("brushed", "gold",     C( 44, .90f, .55f, .7f, .70f, .8f,  .7f, "material"));
        Add("brushed", "silver",   C(210, .80f, .68f, .6f, .12f, .7f, -.1f, "material"));
        Add("polished","marble",   C(  0, .40f, .88f, .5f, .04f, .4f,  .0f, "material"));
        Add("black",   "marble",   C(230, .60f, .12f, .7f, .08f, .6f, -.1f, "material"));
        Add("old",     "paper",    C( 44, .65f, .82f, .5f, .22f, .5f,  .5f, "material"));
        Add("dark",    "chocolate",C( 18, .85f, .15f, .7f, .35f, .7f,  .4f, "food"));
        Add("dark",    "espresso", C( 20, .85f, .10f, .8f, .28f, .7f,  .4f, "food"));

        // ── Mood compounds ────────────────────────────────────────────────────
        Add("dark",    "academia", C( 28, .80f, .28f, .7f, .28f, .7f,  .3f, "mood"));
        Add("light",   "academia", C( 38, .80f, .72f, .7f, .22f, .7f,  .4f, "mood"));
        Add("cozy",    "cabin",    C( 26, .80f, .42f, .6f, .32f, .7f,  .6f, "environment"));
        Add("rainy",   "day",      C(210, .75f, .48f, .6f, .18f, .7f, -.3f, "mood"));
        Add("foggy",   "morning",  C(205, .70f, .62f, .6f, .15f, .6f, -.2f, "time"));
        Add("stormy",  "night",    C(225, .80f, .15f, .8f, .25f, .8f, -.4f, "time"));
        Add("sunny",   "day",      C( 50, .80f, .80f, .7f, .55f, .7f,  .6f, "time"));
        Add("clear",   "sky",      C(200, .85f, .72f, .7f, .52f, .8f, -.3f, "environment"));
        Add("starry",  "night",    C(245, .90f, .08f, .9f, .38f, .8f, -.5f, "time"));

        // ── Place compounds ───────────────────────────────────────────────────
        Add("new",     "york",     C(220, .70f, .30f, .6f, .22f, .7f, -.2f, "place"));
        Add("los",     "angeles",  C( 30, .65f, .65f, .5f, .38f, .6f,  .5f, "place"));
        Add("san",     "francisco",C(200, .65f, .58f, .5f, .35f, .6f, -.2f, "place"));
        Add("hong",    "kong",     C(220, .65f, .22f, .6f, .35f, .6f, -.2f, "place"));

        Entries = d;
    }

    /// <summary>
    /// Looks up a stemmed bigram key ("stem1 stem2").
    /// Returns null if not found.
    /// </summary>
    public static ColorSignal? Lookup(string stem1, string stem2)
    {
        string key = $"{stem1} {stem2}";
        return Entries.TryGetValue(key, out var sig) ? sig : null;
    }
}
