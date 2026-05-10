namespace ThemeManager.Core.NLP;

/// <summary>
/// Maps stemmed words to <see cref="ColorSignal"/> values.
///
/// Python analogy: a hand-crafted word-vector dictionary where each entry is a
/// point in perceptual color-space (HSL) rather than a dense float array.
///
/// Design principles:
///   • Cover environments, moods, times, seasons, materials, explicit color names.
///   • HueWeight reflects specificity: named colors = 1.0, vague moods = 0.3–0.5.
///   • Lightness/Saturation weights are tuned so dark + ocean → dark teal (not just dark).
///   • Warmth shifts the hue post-averaging to maintain analogous harmony.
/// </summary>
public static class ColorLexicon
{
    // C(hue, hueW, lightness, lightW, saturation, satW, warmth, category)
    private static ColorSignal C(
        float h, float hw, float l, float lw, float s, float sw, float warm, string cat)
        => new(h, hw, l, lw, s, sw, warm, cat);

    // ── Master lookup: stemmed word → signal ──────────────────────────────────
    // Stems are pre-computed with PorterStemmer so they match what VibeTokenizer produces.

    public static readonly IReadOnlyDictionary<string, ColorSignal> Entries =
        new Dictionary<string, ColorSignal>(StringComparer.OrdinalIgnoreCase)
    {
        // ════ ENVIRONMENTS ════════════════════════════════════════════════════

        // Ocean / Water
        ["ocean"]   = C(195, .9f, .55f, .6f, .60f, .7f, -.5f, "environment"),
        ["sea"]     = C(195, .8f, .55f, .6f, .58f, .6f, -.5f, "environment"),
        ["marin"]   = C(192, .8f, .50f, .5f, .62f, .6f, -.6f, "environment"),
        ["wave"]    = C(198, .6f, .58f, .4f, .55f, .5f, -.4f, "environment"),
        ["tidal"]   = C(195, .5f, .55f, .4f, .50f, .4f, -.4f, "environment"),
        ["aquat"]   = C(185, .7f, .58f, .5f, .58f, .6f, -.5f, "environment"),
        ["coral"]   = C(  5, .8f, .60f, .6f, .72f, .7f,  .6f, "environment"),
        ["reef"]    = C(170, .7f, .50f, .5f, .60f, .6f, -.3f, "environment"),
        ["lagoon"]  = C(180, .7f, .60f, .5f, .55f, .6f, -.4f, "environment"),
        ["nautical"]= C(220, .7f, .40f, .5f, .60f, .6f, -.5f, "environment"),

        // Forest / Nature
        ["forest"]  = C(125, .9f, .38f, .6f, .50f, .7f,  .1f, "environment"),
        ["woodland"]= C(120, .8f, .38f, .5f, .45f, .6f,  .1f, "environment"),
        ["jungl"]   = C(130, .9f, .35f, .7f, .60f, .7f,  .0f, "environment"),
        ["fern"]    = C(128, .7f, .42f, .5f, .50f, .6f,  .0f, "environment"),
        ["moss"]    = C(115, .7f, .40f, .5f, .42f, .5f,  .1f, "environment"),
        ["pine"]    = C(130, .7f, .35f, .6f, .48f, .6f,  .1f, "environment"),
        ["cedar"]   = C(125, .6f, .38f, .5f, .40f, .5f,  .2f, "environment"),
        ["meadow"]  = C(105, .7f, .58f, .5f, .48f, .6f,  .1f, "environment"),
        ["grove"]   = C(120, .7f, .42f, .5f, .45f, .6f,  .1f, "environment"),
        ["bamboo"]  = C(110, .7f, .52f, .5f, .48f, .6f,  .0f, "environment"),

        // Desert / Earth
        ["desert"]  = C( 35, .8f, .68f, .6f, .38f, .6f,  .7f, "environment"),
        ["sand"]    = C( 40, .7f, .72f, .5f, .38f, .5f,  .6f, "environment"),
        ["dune"]    = C( 38, .7f, .70f, .5f, .35f, .5f,  .7f, "environment"),
        ["arid"]    = C( 33, .6f, .65f, .5f, .32f, .5f,  .6f, "environment"),
        ["canyon"]  = C( 20, .8f, .52f, .6f, .50f, .6f,  .6f, "environment"),
        ["terracot"]= C( 14, .8f, .55f, .6f, .58f, .7f,  .7f, "environment"),
        ["earth"]   = C( 28, .6f, .42f, .5f, .35f, .5f,  .5f, "environment"),
        ["clay"]    = C( 18, .7f, .55f, .5f, .40f, .6f,  .6f, "environment"),
        ["sahara"]  = C( 42, .8f, .72f, .6f, .40f, .6f,  .7f, "environment"),

        // Arctic / Snow / Ice
        ["arctic"]  = C(205, .7f, .88f, .8f, .18f, .6f, -.7f, "environment"),
        ["snow"]    = C(210, .5f, .93f, .8f, .10f, .6f, -.5f, "environment"),
        ["ic"]      = C(200, .6f, .88f, .7f, .20f, .6f, -.6f, "environment"),
        ["frost"]   = C(205, .6f, .86f, .7f, .22f, .6f, -.5f, "environment"),
        ["blizzard"]= C(215, .6f, .80f, .7f, .18f, .6f, -.6f, "environment"),
        ["polar"]   = C(210, .6f, .85f, .7f, .15f, .6f, -.7f, "environment"),
        ["tundra"]  = C(200, .5f, .75f, .6f, .20f, .5f, -.5f, "environment"),
        ["glacier"] = C(195, .7f, .82f, .7f, .25f, .6f, -.6f, "environment"),

        // Mountain / Rock
        ["mountain"]= C(205, .6f, .50f, .5f, .25f, .5f, -.2f, "environment"),
        ["alpin"]   = C(205, .6f, .55f, .5f, .28f, .5f, -.2f, "environment"),
        ["peak"]    = C(208, .5f, .52f, .4f, .22f, .4f, -.2f, "environment"),
        ["cliff"]   = C(200, .5f, .45f, .4f, .25f, .4f, -.1f, "environment"),
        ["rock"]    = C(  0, .3f, .45f, .4f, .12f, .4f,  .0f, "material"),
        ["stone"]   = C(  0, .3f, .50f, .4f, .10f, .4f,  .0f, "material"),
        ["granit"]  = C(  0, .3f, .50f, .3f, .10f, .3f, -.1f, "material"),
        ["slate"]   = C(215, .4f, .42f, .4f, .12f, .4f, -.2f, "material"),

        // Beach / Coastal / Tropical
        ["beach"]   = C( 48, .6f, .72f, .6f, .42f, .6f,  .5f, "environment"),
        ["coast"]   = C(195, .5f, .65f, .4f, .40f, .5f, -.1f, "environment"),
        ["tropic"]  = C( 45, .7f, .65f, .5f, .60f, .6f,  .4f, "environment"),
        ["island"]  = C( 42, .7f, .68f, .5f, .50f, .6f,  .3f, "environment"),
        ["palm"]    = C( 95, .6f, .58f, .4f, .52f, .5f,  .2f, "environment"),
        ["coconut"] = C( 38, .5f, .58f, .4f, .35f, .5f,  .3f, "environment"),

        // Urban / City
        ["citi"]    = C(225, .5f, .35f, .5f, .25f, .5f, -.2f, "environment"),
        ["urban"]   = C(220, .5f, .38f, .5f, .22f, .5f, -.1f, "environment"),
        ["metro"]   = C(225, .5f, .35f, .5f, .22f, .4f, -.2f, "environment"),
        ["skylin"]  = C(218, .5f, .30f, .5f, .28f, .5f, -.3f, "environment"),
        ["street"]  = C(  0, .2f, .38f, .4f, .15f, .3f,  .0f, "environment"),
        ["concreet"]= C(  0, .2f, .50f, .4f, .08f, .4f,  .0f, "material"),
        ["asphalt"] = C(220, .3f, .25f, .4f, .10f, .3f, -.1f, "material"),

        // Spaces / Study
        ["librari"] = C( 30, .6f, .38f, .6f, .28f, .6f,  .4f, "environment"),
        ["academ"]  = C( 28, .6f, .40f, .6f, .30f, .6f,  .3f, "environment"),
        ["scholar"] = C( 30, .5f, .38f, .5f, .28f, .5f,  .3f, "environment"),
        ["studi"]   = C( 28, .5f, .42f, .4f, .28f, .4f,  .3f, "environment"),
        ["bookshelf"]= C(28, .5f, .40f, .4f, .30f, .5f,  .4f, "environment"),
        ["parchment"]= C(45, .6f, .78f, .5f, .28f, .5f,  .5f, "material"),

        // Café / Coffee
        ["cafe"]    = C( 28, .7f, .42f, .5f, .32f, .5f,  .5f, "environment"),
        ["bistro"]  = C( 25, .5f, .45f, .4f, .30f, .4f,  .4f, "environment"),
        ["tavern"]  = C( 25, .5f, .35f, .5f, .30f, .5f,  .4f, "environment"),

        // Space / Cosmos
        ["space"]   = C(255, .7f, .08f, .8f, .45f, .7f, -.7f, "environment"),
        ["cosmic"]  = C(258, .7f, .10f, .7f, .50f, .7f, -.6f, "environment"),
        ["nebula"]  = C(270, .7f, .12f, .7f, .60f, .7f, -.4f, "environment"),
        ["galaxi"]  = C(260, .7f, .08f, .8f, .55f, .7f, -.6f, "environment"),
        ["star"]    = C(250, .5f, .10f, .6f, .40f, .5f, -.4f, "environment"),
        ["cosmos"]  = C(260, .6f, .10f, .7f, .48f, .6f, -.5f, "environment"),
        ["void"]    = C(240, .4f, .06f, .8f, .20f, .5f, -.6f, "environment"),

        // Garden / Floral
        ["garden"]  = C(130, .5f, .60f, .4f, .45f, .5f,  .1f, "environment"),
        ["floral"]  = C(330, .6f, .68f, .5f, .52f, .6f,  .3f, "environment"),
        ["bloom"]   = C(330, .6f, .70f, .5f, .55f, .6f,  .3f, "environment"),
        ["blossom"] = C(335, .6f, .72f, .5f, .50f, .6f,  .3f, "environment"),
        ["sakura"]  = C(340, .7f, .78f, .5f, .48f, .6f,  .2f, "environment"),
        ["cherri"]  = C(345, .7f, .68f, .5f, .52f, .6f,  .3f, "environment"),
        ["petal"]   = C(335, .5f, .78f, .4f, .45f, .5f,  .3f, "environment"),
        ["lavend"]  = C(270, .8f, .65f, .5f, .42f, .6f, -.1f, "environment"),
        ["rose"]    = C(348, .8f, .60f, .5f, .55f, .6f,  .4f, "environment"),
        ["peonni"]  = C(340, .6f, .70f, .4f, .48f, .5f,  .3f, "environment"),

        // Vineyard / Wine
        ["vineyard"]= C(355, .6f, .45f, .5f, .42f, .6f,  .4f, "environment"),
        ["tuscan"]  = C( 30, .6f, .55f, .5f, .40f, .6f,  .5f, "environment"),
        ["mediterr"]= C( 25, .5f, .60f, .4f, .38f, .5f,  .3f, "environment"),

        // ════ TIMES OF DAY ════════════════════════════════════════════════════

        ["dawn"]    = C( 38, .7f, .72f, .7f, .52f, .7f,  .7f, "time"),
        ["sunris"]  = C( 32, .7f, .68f, .6f, .58f, .7f,  .7f, "time"),
        ["morn"]    = C( 42, .6f, .75f, .6f, .48f, .6f,  .6f, "time"),
        ["noon"]    = C( 50, .5f, .85f, .7f, .40f, .6f,  .5f, "time"),
        ["midday"]  = C( 50, .5f, .85f, .7f, .38f, .5f,  .5f, "time"),
        ["afternoon"]= C(38, .5f, .75f, .5f, .45f, .5f,  .5f, "time"),
        ["sunset"]  = C( 22, .8f, .55f, .7f, .72f, .8f,  .8f, "time"),
        ["dusk"]    = C( 20, .7f, .45f, .6f, .62f, .7f,  .6f, "time"),
        ["twilight"]= C( 18, .7f, .38f, .6f, .55f, .7f,  .4f, "time"),
        ["evening"] = C( 25, .6f, .42f, .5f, .45f, .6f,  .5f, "time"),
        ["night"]   = C(240, .7f, .12f, .8f, .30f, .7f, -.5f, "time"),
        ["midnight"]= C(245, .7f, .08f, .9f, .28f, .7f, -.6f, "time"),
        ["nocturnal"]= C(240,.6f, .12f, .7f, .30f, .6f, -.5f, "time"),
        ["golden"]  = C( 45, .7f, .65f, .6f, .70f, .7f,  .7f, "time"),

        // ════ SEASONS ═════════════════════════════════════════════════════════

        ["spring"]  = C(330, .5f, .72f, .5f, .48f, .6f,  .2f, "season"),
        ["summer"]  = C( 52, .5f, .78f, .5f, .58f, .6f,  .5f, "season"),
        ["autumn"]  = C( 25, .7f, .52f, .6f, .58f, .7f,  .7f, "season"),
        ["fall"]    = C( 22, .7f, .50f, .6f, .58f, .7f,  .7f, "season"),
        ["harvest"] = C( 30, .6f, .55f, .5f, .52f, .6f,  .6f, "season"),
        ["maple"]   = C( 15, .7f, .50f, .6f, .65f, .7f,  .7f, "season"),
        ["winter"]  = C(210, .5f, .78f, .6f, .18f, .6f, -.4f, "season"),
        ["cold"]    = C(205, .4f, .72f, .4f, .15f, .4f, -.4f, "season"),
        ["bleak"]   = C(210, .4f, .50f, .5f, .12f, .5f, -.3f, "season"),

        // ════ MOODS / FEELINGS ════════════════════════════════════════════════

        ["cozi"]    = C( 30, .5f, .62f, .5f, .30f, .6f,  .6f, "mood"),
        ["warm"]    = C( 32, .5f, .62f, .5f, .40f, .5f,  .7f, "mood"),
        ["comfort"] = C( 28, .5f, .60f, .4f, .28f, .5f,  .5f, "mood"),
        ["snug"]    = C( 28, .5f, .58f, .4f, .28f, .4f,  .5f, "mood"),
        ["calm"]    = C(190, .5f, .60f, .5f, .25f, .5f, -.3f, "mood"),
        ["seren"]   = C(185, .5f, .62f, .5f, .22f, .5f, -.3f, "mood"),
        ["peac"]    = C(170, .5f, .65f, .4f, .25f, .4f, -.2f, "mood"),
        ["tranquil"]= C(185, .6f, .65f, .5f, .22f, .5f, -.3f, "mood"),
        ["zen"]     = C(160, .6f, .65f, .5f, .25f, .5f, -.2f, "mood"),
        ["mysteri"] = C(265, .6f, .25f, .6f, .38f, .6f, -.2f, "mood"),
        ["shadow"]  = C(250, .4f, .20f, .6f, .22f, .5f, -.2f, "mood"),
        ["noir"]    = C(240, .5f, .10f, .8f, .18f, .6f, -.3f, "mood"),
        ["gothic"]  = C(270, .6f, .15f, .7f, .30f, .6f, -.2f, "mood"),
        ["dark"]    = C(240, .3f, .12f, .8f, .15f, .4f, -.2f, "mood"),
        ["moodi"]   = C(245, .4f, .22f, .5f, .25f, .5f, -.2f, "mood"),
        ["melancholi"]= C(240,.4f, .25f, .5f, .20f, .5f, -.3f, "mood"),
        ["romant"]  = C(348, .6f, .50f, .5f, .50f, .6f,  .5f, "mood"),
        ["passion"] = C(  5, .7f, .45f, .6f, .70f, .7f,  .7f, "mood"),
        ["velvet"]  = C(275, .6f, .30f, .5f, .45f, .6f, -.1f, "mood"),
        ["luxuri"]  = C(275, .5f, .28f, .5f, .48f, .6f,  .0f, "mood"),
        ["elegant"] = C(270, .4f, .35f, .4f, .38f, .5f, -.1f, "mood"),
        ["energet"] = C( 30, .4f, .60f, .4f, .72f, .7f,  .4f, "mood"),
        ["vibrant"] = C( 30, .3f, .60f, .4f, .80f, .8f,  .3f, "mood"),
        ["bold"]    = C(  0, .3f, .40f, .4f, .75f, .7f,  .2f, "mood"),
        ["electr"]  = C(190, .5f, .40f, .5f, .85f, .8f, -.3f, "mood"),
        ["neon"]    = C(290, .5f, .35f, .5f, .90f, .9f, -.1f, "mood"),
        ["cyber"]   = C(190, .5f, .25f, .6f, .85f, .8f, -.5f, "mood"),
        ["playful"] = C( 55, .4f, .65f, .4f, .70f, .7f,  .4f, "mood"),
        ["whimsic"] = C( 55, .4f, .70f, .4f, .62f, .6f,  .3f, "mood"),
        ["cheerful"]= C( 48, .4f, .72f, .4f, .65f, .6f,  .5f, "mood"),
        ["mellow"]  = C( 38, .4f, .60f, .4f, .35f, .5f,  .5f, "mood"),
        ["nostalg"] = C( 30, .5f, .58f, .4f, .32f, .5f,  .4f, "mood"),
        ["rustic"]  = C( 28, .5f, .48f, .5f, .32f, .5f,  .5f, "mood"),
        ["vintag"]  = C( 33, .5f, .60f, .4f, .30f, .4f,  .4f, "mood"),
        ["retro"]   = C( 28, .4f, .62f, .4f, .35f, .4f,  .3f, "mood"),
        ["minimal"] = C(  0, .2f, .82f, .5f, .05f, .5f,  .0f, "mood"),
        ["clean"]   = C(200, .2f, .85f, .4f, .08f, .4f, -.1f, "mood"),
        ["crisp"]   = C(195, .3f, .88f, .4f, .10f, .4f, -.2f, "mood"),
        ["futurist"]= C(190, .5f, .30f, .5f, .60f, .7f, -.4f, "mood"),
        ["techno"]  = C(195, .4f, .25f, .5f, .65f, .7f, -.4f, "mood"),
        ["dreami"]  = C(265, .5f, .68f, .4f, .35f, .5f, -.1f, "mood"),
        ["soft"]    = C( 20, .2f, .78f, .4f, .18f, .4f,  .2f, "mood"),
        ["glow"]    = C( 45, .5f, .70f, .5f, .55f, .6f,  .6f, "mood"),
        ["haze"]    = C(210, .4f, .62f, .4f, .12f, .4f, -.2f, "mood"),

        // ════ MATERIALS / TEXTURES ════════════════════════════════════════════

        ["wood"]    = C( 28, .6f, .42f, .5f, .35f, .6f,  .5f, "material"),
        ["timber"]  = C( 26, .6f, .38f, .5f, .38f, .6f,  .5f, "material"),
        ["oak"]     = C( 30, .6f, .45f, .5f, .32f, .5f,  .5f, "material"),
        ["walnut"]  = C( 22, .6f, .28f, .5f, .30f, .5f,  .4f, "material"),
        ["mahogani"]= C( 15, .7f, .25f, .6f, .40f, .6f,  .5f, "material"),
        ["leather"] = C( 22, .6f, .35f, .5f, .35f, .6f,  .4f, "material"),
        ["suede"]   = C( 28, .5f, .50f, .5f, .28f, .5f,  .4f, "material"),
        ["silk"]    = C(270, .4f, .70f, .4f, .30f, .4f, -.1f, "material"),
        ["velour"]  = C(275, .5f, .40f, .4f, .35f, .5f, -.1f, "material"),
        ["linen"]   = C( 45, .4f, .82f, .5f, .18f, .5f,  .3f, "material"),
        ["cotton"]  = C( 40, .3f, .88f, .4f, .10f, .4f,  .2f, "material"),
        ["wool"]    = C( 32, .4f, .72f, .4f, .20f, .4f,  .4f, "material"),
        ["denim"]   = C(215, .6f, .38f, .5f, .40f, .6f, -.3f, "material"),
        ["metal"]   = C(215, .4f, .55f, .4f, .10f, .5f, -.3f, "material"),
        ["steel"]   = C(210, .4f, .52f, .4f, .12f, .4f, -.3f, "material"),
        ["chrome"]  = C(210, .3f, .62f, .3f, .08f, .4f, -.2f, "material"),
        ["gold"]    = C( 45, .8f, .55f, .5f, .78f, .7f,  .7f, "material"),
        ["brass"]   = C( 42, .7f, .48f, .5f, .65f, .7f,  .6f, "material"),
        ["copper"]  = C( 18, .7f, .50f, .5f, .62f, .7f,  .6f, "material"),
        ["amber"]   = C( 38, .8f, .58f, .6f, .72f, .7f,  .7f, "material"),
        ["glass"]   = C(195, .3f, .78f, .3f, .18f, .3f, -.2f, "material"),
        ["crystal"] = C(195, .3f, .82f, .3f, .15f, .3f, -.2f, "material"),
        ["marble"]  = C(  0, .2f, .82f, .3f, .05f, .3f,  .0f, "material"),
        ["obsidian"]= C(240, .4f, .08f, .6f, .15f, .5f, -.3f, "material"),
        ["ember"]   = C( 15, .7f, .45f, .6f, .80f, .7f,  .8f, "material"),

        // ════ FOOD / DRINK ════════════════════════════════════════════════════

        ["chocol"]  = C( 20, .7f, .22f, .6f, .38f, .6f,  .5f, "food"),
        ["espresso"]= C( 22, .7f, .15f, .7f, .32f, .6f,  .4f, "food"),
        ["coffe"]   = C( 26, .6f, .28f, .6f, .30f, .6f,  .5f, "food"),
        ["brew"]    = C( 28, .5f, .32f, .5f, .30f, .5f,  .4f, "food"),
        ["vanill"]  = C( 48, .4f, .88f, .5f, .20f, .5f,  .5f, "food"),
        ["cream"]   = C( 46, .3f, .90f, .5f, .18f, .5f,  .4f, "food"),
        ["honey"]   = C( 42, .7f, .62f, .6f, .72f, .7f,  .7f, "food"),
        ["caramel"] = C( 35, .7f, .52f, .6f, .65f, .7f,  .7f, "food"),
        ["matcha"]  = C(110, .7f, .50f, .5f, .52f, .6f,  .1f, "food"),
        ["tea"]     = C( 90, .4f, .65f, .4f, .35f, .4f,  .2f, "food"),
        ["wine"]    = C(345, .7f, .30f, .6f, .52f, .6f,  .4f, "food"),
        ["merlot"]  = C(350, .7f, .25f, .6f, .50f, .6f,  .4f, "food"),
        ["bourbon"] = C( 28, .6f, .38f, .5f, .48f, .6f,  .6f, "food"),
        ["mint"]    = C(160, .6f, .68f, .4f, .48f, .6f, -.2f, "food"),

        // ════ PLACES ══════════════════════════════════════════════════════════

        ["tokyo"]   = C(290, .5f, .20f, .6f, .72f, .7f, -.2f, "place"),
        ["japan"]   = C(340, .4f, .65f, .4f, .45f, .5f,  .1f, "place"),
        ["pari"]    = C( 28, .4f, .62f, .4f, .30f, .5f,  .3f, "place"),
        ["french"]  = C( 30, .3f, .62f, .3f, .28f, .4f,  .3f, "place"),
        ["itali"]   = C( 28, .5f, .60f, .4f, .45f, .5f,  .4f, "place"),
        ["grec"]    = C(195, .5f, .68f, .4f, .40f, .5f, -.1f, "place"),
        ["nordic"]  = C(210, .4f, .72f, .4f, .20f, .4f, -.4f, "place"),
        ["scandinavian"]= C(210,.4f,.72f,.4f,.18f,.4f,-.4f,"place"),
        ["moroccan"]= C( 28, .6f, .55f, .5f, .60f, .6f,  .5f, "place"),
        ["bohem"]   = C( 28, .5f, .52f, .4f, .48f, .5f,  .4f, "place"),

        // ════ EXPLICIT COLOR NAMES ═══════════════════════════════════════════
        // Very high HueWeight (0.95–1.0) since these are unambiguous.

        ["red"]     = C(  0, 1.0f, .50f, .5f, .80f, .8f,  .8f, "color"),
        ["scarlet"] = C(  5, 0.95f,.48f, .5f, .82f, .8f,  .8f, "color"),
        ["crimson"] = C(348, 0.95f,.38f, .6f, .78f, .8f,  .7f, "color"),
        ["orange"]  = C( 25, 1.0f, .55f, .5f, .80f, .8f,  .8f, "color"),
        ["peach"]   = C( 20, .8f,  .78f, .5f, .60f, .7f,  .7f, "color"),
        ["yellow"]  = C( 58, 1.0f, .65f, .5f, .85f, .8f,  .6f, "color"),
        ["green"]   = C(120, 1.0f, .45f, .5f, .70f, .8f,  .0f, "color"),
        ["teal"]    = C(175, 1.0f, .45f, .5f, .65f, .8f, -.4f, "color"),
        ["cyan"]    = C(180, 1.0f, .55f, .5f, .75f, .8f, -.4f, "color"),
        ["blue"]    = C(220, 1.0f, .50f, .5f, .72f, .8f, -.6f, "color"),
        ["cobalt"]  = C(225, .9f,  .40f, .5f, .75f, .8f, -.6f, "color"),
        ["indigo"]  = C(245, 1.0f, .35f, .5f, .65f, .8f, -.4f, "color"),
        ["purpl"]   = C(270, 1.0f, .45f, .5f, .65f, .8f, -.2f, "color"),
        ["violet"]  = C(268, .95f, .48f, .5f, .62f, .8f, -.2f, "color"),
        ["lilac"]   = C(265, .8f,  .70f, .5f, .40f, .7f, -.1f, "color"),
        ["mauve"]   = C(300, .8f,  .55f, .5f, .30f, .7f,  .1f, "color"),
        ["pink"]    = C(335, 1.0f, .70f, .5f, .55f, .8f,  .4f, "color"),
        ["magenta"] = C(310, .9f,  .55f, .5f, .80f, .8f,  .2f, "color"),
        ["brown"]   = C( 25, .7f,  .32f, .6f, .38f, .7f,  .4f, "color"),
        ["tan"]     = C( 35, .6f,  .62f, .5f, .35f, .6f,  .5f, "color"),
        ["beig"]    = C( 42, .5f,  .80f, .5f, .22f, .5f,  .4f, "color"),
        ["grey"]    = C(  0, .2f,  .55f, .5f, .05f, .5f,  .0f, "color"),
        ["gray"]    = C(  0, .2f,  .55f, .5f, .05f, .5f,  .0f, "color"),
        ["silver"]  = C(210, .3f,  .72f, .4f, .08f, .5f, -.1f, "color"),
        ["black"]   = C(  0, .1f,  .05f, .9f, .05f, .5f,  .0f, "color"),
        ["white"]   = C(  0, .1f,  .96f, .9f, .02f, .5f,  .0f, "color"),
        ["ivory"]   = C( 48, .3f,  .92f, .5f, .15f, .4f,  .3f, "color"),
        ["cream2"]  = C( 46, .3f,  .90f, .4f, .18f, .4f,  .4f, "color"),
        ["ecru"]    = C( 44, .4f,  .86f, .5f, .20f, .4f,  .3f, "color"),

        // ════ MULTILINGUAL ════════════════════════════════════════════════════
        // Spanish, French, Japanese (Romanized) — all pointing to the same HSL
        // signals as their English equivalents so vibe text in any of these
        // languages gets exactly the same palette as English would.

        // ── Spanish ───────────────────────────────────────────────────────────
        ["noche"]     = C(240, .7f, .10f, .8f, .28f, .7f, -.5f, "time"),       // night
        ["bosque"]    = C(125, .8f, .38f, .6f, .48f, .7f,  .1f, "environment"),// forest
        ["oceano"]    = C(195, .8f, .55f, .6f, .58f, .7f, -.5f, "environment"),// ocean
        ["desierto"]  = C( 35, .7f, .68f, .6f, .38f, .6f,  .7f, "environment"),// desert
        ["montana"]   = C(205, .6f, .50f, .5f, .25f, .5f, -.2f, "environment"),// mountain
        ["calido"]    = C( 32, .5f, .62f, .5f, .40f, .5f,  .7f, "mood"),       // warm
        ["oscuro"]    = C(240, .4f, .12f, .7f, .15f, .4f, -.2f, "mood"),       // dark
        ["suave"]     = C( 20, .3f, .78f, .4f, .18f, .4f,  .2f, "mood"),       // soft
        ["dorado"]    = C( 45, .7f, .60f, .6f, .72f, .7f,  .7f, "color"),      // golden
        ["rojo"]      = C(  0, .9f, .48f, .6f, .75f, .8f,  .7f, "color"),      // red
        ["azul"]      = C(220, .9f, .50f, .6f, .70f, .8f, -.5f, "color"),      // blue
        ["verde"]     = C(120, .9f, .45f, .6f, .65f, .8f,  .0f, "color"),      // green
        ["amarillo"]  = C( 55, .9f, .65f, .5f, .80f, .8f,  .6f, "color"),      // yellow
        ["rosa"]      = C(340, .9f, .72f, .5f, .52f, .8f,  .4f, "color"),      // pink
        ["morado"]    = C(270, .9f, .48f, .5f, .60f, .8f, -.2f, "color"),      // purple
        ["naranja"]   = C( 25, .9f, .55f, .5f, .78f, .8f,  .7f, "color"),      // orange
        ["invierno"]  = C(210, .5f, .78f, .6f, .18f, .6f, -.4f, "season"),     // winter
        ["verano"]    = C( 52, .5f, .78f, .5f, .55f, .6f,  .5f, "season"),     // summer
        ["otono"]     = C( 25, .6f, .52f, .6f, .58f, .7f,  .7f, "season"),     // autumn
        ["primavera"] = C(330, .5f, .72f, .5f, .48f, .6f,  .2f, "season"),     // spring
        ["playa"]     = C( 48, .6f, .72f, .6f, .42f, .6f,  .5f, "environment"),// beach
        ["cielo"]     = C(200, .7f, .72f, .5f, .50f, .6f, -.3f, "environment"),// sky
        ["fuego"]     = C( 12, .8f, .48f, .6f, .80f, .8f,  .8f, "material"),   // fire
        ["niebla"]    = C(205, .5f, .62f, .4f, .12f, .4f, -.2f, "mood"),       // fog/haze
        ["luna"]      = C(240, .6f, .12f, .7f, .22f, .6f, -.5f, "time"),       // moon

        // ── French ────────────────────────────────────────────────────────────
        ["nuit"]      = C(240, .7f, .10f, .8f, .28f, .7f, -.5f, "time"),       // night
        ["foret"]     = C(125, .8f, .38f, .6f, .48f, .7f,  .1f, "environment"),// forest
        ["mer"]       = C(195, .8f, .55f, .6f, .58f, .7f, -.5f, "environment"),// sea
        ["desert"]    = C( 35, .7f, .68f, .6f, .38f, .6f,  .7f, "environment"),// desert (also English)
        ["montagne"]  = C(205, .6f, .50f, .5f, .25f, .5f, -.2f, "environment"),// mountain
        ["chaud"]     = C( 32, .5f, .62f, .5f, .40f, .5f,  .7f, "mood"),       // warm
        ["sombre"]    = C(240, .4f, .15f, .7f, .15f, .4f, -.2f, "mood"),       // dark
        ["doux"]      = C( 20, .3f, .78f, .4f, .18f, .4f,  .2f, "mood"),       // soft
        ["or"]        = C( 45, .7f, .60f, .5f, .72f, .7f,  .7f, "color"),      // gold
        ["rouge"]     = C(  0, .9f, .48f, .6f, .75f, .8f,  .7f, "color"),      // red
        ["bleu"]      = C(220, .9f, .50f, .6f, .70f, .8f, -.5f, "color"),      // blue
        ["vert"]      = C(120, .9f, .45f, .6f, .65f, .8f,  .0f, "color"),      // green
        ["jaune"]     = C( 55, .9f, .65f, .5f, .80f, .8f,  .6f, "color"),      // yellow
        ["rose"]      = C(340, .8f, .72f, .5f, .52f, .7f,  .4f, "color"),      // pink (also English)
        ["violet"]    = C(270, .9f, .48f, .5f, .60f, .8f, -.2f, "color"),      // purple (also English)
        ["hiver"]     = C(210, .5f, .78f, .6f, .18f, .6f, -.4f, "season"),     // winter
        ["ete"]       = C( 52, .5f, .78f, .5f, .55f, .6f,  .5f, "season"),     // summer
        ["automne"]   = C( 25, .6f, .52f, .6f, .58f, .7f,  .7f, "season"),     // autumn
        ["printemps"] = C(330, .5f, .72f, .5f, .48f, .6f,  .2f, "season"),     // spring
        ["plage"]     = C( 48, .6f, .72f, .6f, .42f, .6f,  .5f, "environment"),// beach
        ["ciel"]      = C(200, .7f, .72f, .5f, .50f, .6f, -.3f, "environment"),// sky
        ["brume"]     = C(205, .5f, .62f, .4f, .12f, .4f, -.2f, "mood"),       // mist
        ["lune"]      = C(240, .6f, .12f, .7f, .22f, .6f, -.5f, "time"),       // moon
        ["aurore"]    = C( 38, .7f, .72f, .6f, .52f, .7f,  .7f, "time"),       // dawn
        ["coucher"]   = C( 22, .7f, .55f, .7f, .70f, .8f,  .8f, "time"),       // sunset
        ["crepuscule"]= C( 18, .7f, .38f, .6f, .55f, .7f,  .4f, "time"),       // twilight

        // ── Japanese (Romanized) ──────────────────────────────────────────────
        ["yoru"]      = C(240, .7f, .10f, .8f, .28f, .7f, -.5f, "time"),       // 夜 night
        ["mori"]      = C(125, .8f, .38f, .6f, .48f, .7f,  .1f, "environment"),// 森 forest
        ["umi"]       = C(195, .8f, .55f, .6f, .58f, .7f, -.5f, "environment"),// 海 ocean
        ["sora"]      = C(200, .7f, .72f, .5f, .50f, .6f, -.3f, "environment"),// 空 sky
        ["tsuki"]     = C(240, .6f, .12f, .7f, .22f, .6f, -.5f, "time"),       // 月 moon
        ["hana"]      = C(340, .7f, .72f, .5f, .48f, .7f,  .3f, "environment"),// 花 flower
        ["yuki"]      = C(210, .6f, .88f, .7f, .12f, .6f, -.5f, "environment"),// 雪 snow
        ["hi"]        = C( 12, .7f, .48f, .6f, .75f, .7f,  .8f, "material"),   // 火 fire
        ["kawa"]      = C(195, .6f, .55f, .5f, .45f, .6f, -.3f, "environment"),// 川 river
        ["yama"]      = C(205, .6f, .50f, .5f, .25f, .5f, -.2f, "environment"),// 山 mountain
        ["shiro"]     = C(  0, .1f, .92f, .7f, .04f, .4f,  .0f, "color"),      // 白 white
        ["kuro"]      = C(  0, .1f, .06f, .8f, .05f, .4f,  .0f, "color"),      // 黒 black
        ["aka"]       = C(  0, .9f, .48f, .6f, .75f, .8f,  .7f, "color"),      // 赤 red
        ["ao"]        = C(220, .9f, .50f, .6f, .70f, .8f, -.5f, "color"),      // 青 blue/green
        ["midori"]    = C(130, .9f, .45f, .6f, .65f, .8f,  .0f, "color"),      // 緑 green
        ["ki"]        = C( 55, .8f, .65f, .5f, .78f, .7f,  .6f, "color"),      // 黄 yellow
        ["murasaki"]  = C(270, .9f, .45f, .5f, .60f, .8f, -.2f, "color"),      // 紫 purple
        ["kin"]       = C( 45, .8f, .55f, .5f, .72f, .7f,  .7f, "color"),      // 金 gold
        ["gin"]       = C(210, .5f, .70f, .4f, .10f, .5f, -.1f, "color"),      // 銀 silver
        ["haru"]      = C(330, .5f, .72f, .5f, .48f, .6f,  .2f, "season"),     // 春 spring
        ["natsu"]     = C( 52, .5f, .78f, .5f, .55f, .6f,  .5f, "season"),     // 夏 summer
        ["aki"]       = C( 25, .6f, .52f, .6f, .58f, .7f,  .7f, "season"),     // 秋 autumn
        ["fuyu"]      = C(210, .5f, .78f, .6f, .18f, .6f, -.4f, "season"),     // 冬 winter
        ["shizuka"]   = C(185, .5f, .62f, .5f, .22f, .5f, -.3f, "mood"),       // 静か quiet/calm
        ["kawaii"]    = C(335, .5f, .75f, .4f, .52f, .6f,  .4f, "mood"),       // cute/pastel
        ["wabi"]      = C( 28, .5f, .48f, .4f, .25f, .5f,  .3f, "mood"),       // わび wabi-sabi
        ["sabi"]      = C( 25, .5f, .42f, .4f, .22f, .5f,  .3f, "mood"),       // さび wabi-sabi
        ["yugen"]     = C(245, .6f, .25f, .5f, .30f, .5f, -.2f, "mood"),       // 幽玄 mysterious beauty
        ["komorebi"]  = C(100, .7f, .70f, .5f, .45f, .6f,  .2f, "environment"),// 木漏れ日 light through leaves
        ["kintsug"]   = C( 40, .7f, .48f, .6f, .62f, .7f,  .6f, "material"),   // 金継ぎ gold repair
    };

    /// <summary>
    /// Look up a (possibly stemmed) word. Returns null if no match.
    /// Tries exact match first, then removes common suffixes as a fallback.
    /// </summary>
    public static ColorSignal? Lookup(string word)
    {
        word = word.ToLowerInvariant();
        if (Entries.TryGetValue(word, out var sig)) return sig;

        // Fallback: try the stem (already stemmed by VibeTokenizer, but callers
        // may pass raw words too, so stem here as a safety net).
        var stem = PorterStemmer.Stem(word);
        if (stem != word && Entries.TryGetValue(stem, out sig)) return sig;

        return null;
    }
}
