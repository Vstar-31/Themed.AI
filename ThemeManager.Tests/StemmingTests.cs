using ThemeManager.Core.NLP;

namespace ThemeManager.Tests;

/// <summary>
/// Pins down <see cref="PorterStemmer.Stem"/> for the specific words <see cref="WidgetLexicon"/>
/// and <see cref="ColorLexicon"/> key off of. This replaces a string of one-off scratch console
/// apps and a non-asserting <c>PrintStems</c> test that accumulated across sessions (root
/// <c>Program.cs</c>/<c>StemTest.cs</c>/<c>test.cs</c>, <c>StemTest/</c>, <c>TestStem/</c>,
/// <c>TestStem2/</c>) — each one re-implemented "run the stemmer on a few words and eyeball the
/// output" instead of asserting it, so a regression could only be caught by a human reading
/// console output. Expected values here were computed from a faithful line-by-line Python port
/// of <c>PorterStemmer.cs</c> run against each word, same verification method already used for
/// the WidgetLexicon entries themselves (see phases.md, Phase 6 VibeFinderAI/lexicon notes) —
/// not guessed or hand-traced.
/// </summary>
public class StemmingTests
{
    // ── VibeFinderAI trigger words (WidgetLexicon "vibe"/"recommend"/"mood" entries) ──────────
    [Theory]
    [InlineData("vibe", "vibe")]
    [InlineData("vibes", "vibe")]
    [InlineData("vibefinder", "vibefind")]
    [InlineData("mood", "mood")]
    [InlineData("recommend", "recommend")]
    public void Stem_VibeFinderTriggerWords_MatchesLexiconKey(string word, string expectedStem)
        => Assert.Equal(expectedStem, PorterStemmer.Stem(word));

    // ── Ring/Icon meter trigger words added alongside WidgetLexicon in this session ────────────
    [Theory]
    [InlineData("ring", "ring")]
    [InlineData("rings", "ring")]
    [InlineData("circle", "circl")]
    [InlineData("circular", "circular")]
    [InlineData("icon", "icon")]
    [InlineData("icons", "icon")]
    [InlineData("glyph", "glyph")]
    [InlineData("image", "imag")]
    [InlineData("symbol", "symbol")]
    public void Stem_RingAndIconTriggerWords_MatchesLexiconKey(string word, string expectedStem)
        => Assert.Equal(expectedStem, PorterStemmer.Stem(word));

    // ── A non-obvious but correct existing entry, kept as a regression pin ─────────────────────
    // WidgetLexicon["play"] (-> style boost) is keyed for "playful", not for the bare word "play":
    // Stem("playful") reduces to "play" via the step-3 "-ful" rule, which runs *after* step 1c —
    // so step 1c's "trailing y after a vowel -> i" rule (the one that turns "play" typed on its
    // own into "plai") never gets a second look at the by-then-already-suffix-stripped "play".
    // Re-stemming "play" in isolation therefore does NOT reproduce the key (Stem("play") ==
    // "plai", confirmed against the Python port), even though the entry is correctly reachable
    // from real input via "playful" itself. (Checked, and NOT via "playfully" — step 1c's
    // y-after-vowel rule fires on "playfully" too, before step 3 ever sees it, producing
    // "playfulli" — a separate, pre-existing lexicon gap outside this session's scope.)
    // Asserting "every lexicon key is a fixed point of Stem" looks like a reasonable blanket
    // regression check and was the first version of this file — it was wrong, and would have
    // failed on this exact entry despite it working correctly, so it was replaced with this
    // narrower, verified pin instead.
    [Fact]
    public void Stem_Playful_ReducesToPlayLexiconKey()
        => Assert.Equal("play", PorterStemmer.Stem("playful"));
}
