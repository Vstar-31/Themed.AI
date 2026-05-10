using System.Text;
using System.Text.RegularExpressions;

namespace ThemeManager.Core.NLP;

/// <summary>
/// Expands emoji into their semantic prose equivalents BEFORE the tokenizer runs.
///
/// Strategy: a simple dictionary replace pass over the raw input string.
/// This means 🌊 becomes "ocean" and participates in the normal lexicon lookup
/// and bigram detection — no special-casing needed downstream.
///
/// Python analogy: emoji.demojize() from the `emoji` library, but targeted only
/// at the ~70 emoji most useful for vibe/color description.
/// </summary>
public static class EmojiSignalMap
{
    // Ordered longest-first so multi-codepoint sequences (👨‍🍳) match before
    // their component parts. For our use-case single emoji are sufficient,
    // but the ordering habit is good practice.
    private static readonly (string Emoji, string Expansion)[] Mappings =
    [
        // ── Nature / environments ──────────────────────────────────────────────
        ("🌊", "ocean"),       ("🌀", "storm"),       ("🌧", "rain"),
        ("❄️",  "snow"),       ("🌨", "snow"),        ("🌩", "thunder storm"),
        ("⛈",  "dark storm"), ("🌫", "haze fog"),    ("🌪", "storm"),
        ("🌶",  "fire"),       ("🌋", "dark volcanic"),("🏔",  "mountain"),
        ("🗻",  "snowy mountain"),("🏕","forest camp"),("🌲", "forest pine"),
        ("🌳", "forest"),      ("🌴", "tropical palm"),("🎋", "bamboo"),
        ("🍀", "green"),       ("🌿", "green nature"),("🌱", "spring fresh"),
        ("🌾", "harvest autumn"),("🍂","autumn fall"), ("🍁", "maple autumn"),
        ("🌸", "cherry blossom sakura"),("🌺","floral"),("🌹","rose"),
        ("🌷", "tulip floral"),("🌻","sunflower"),    ("🌼", "spring bloom"),
        ("💐", "floral bloom"),("🌵","desert cactus"),("🏜","desert"),
        ("🏖", "beach"),       ("🏝", "island tropical"),("🌅","sunrise warm"),
        ("🌄", "sunrise mountain"),("🌇","sunset city"),("🌆","golden city evening"),
        ("🌃", "night city stars"),("🌉","bridge night"),("🌌","galaxy space cosmos"),
        ("⭐", "star"),         ("🌟", "star glow"),   ("✨", "glow sparkle"),
        ("💫", "glow ethereal"),("🌙","midnight night"),("🌛","crescent night"),
        ("☀️",  "sunny warm"),  ("⛅", "cloudy soft"),  ("🌤","warm sunny"),
        ("🌈", "vibrant colorful"),("🌊","ocean wave"),

        // ── Materials / textures ──────────────────────────────────────────────
        ("🪨", "stone rock"),  ("🪵","wood timber"),  ("🧱","brick rustic"),
        ("💎", "crystal"),     ("🔮","mystic crystal"),("🪙","gold copper"),
        ("🥇", "gold"),        ("🥈","silver"),        ("🥉","bronze copper"),
        ("🔥", "fire ember warm"),("💧","water"),      ("🌊","ocean"),
        ("❄️",  "ice frost cold"),("⚡","electric neon"),

        // ── Food & drink ──────────────────────────────────────────────────────
        ("☕", "coffee espresso warm"),("🍵","tea matcha"),
        ("🧋", "milk tea"),    ("🍫","chocolate cocoa"),("🍯","honey amber"),
        ("🍷", "wine merlot"), ("🍸","cocktail"),      ("🥂","champagne gold"),
        ("🍊", "orange"),      ("🍋","yellow citrus"), ("🍇","purple grape"),
        ("🍓", "red berry"),   ("🫐","blue berry"),    ("🍒","cherry red"),
        ("🥭", "tropical mango orange"),("🍑","peach warm"),

        // ── Moods / feelings ──────────────────────────────────────────────────
        ("😌", "serene calm"), ("🥰","warm romantic"), ("😎","cool bold"),
        ("🌙", "midnight mysterious"),("👻","dark mysterious"),
        ("🖤", "dark noir"),   ("🤍","soft white minimal"),("💜","purple"),
        ("💙", "blue calm"),   ("💚","green"),         ("💛","yellow warm"),
        ("🧡", "orange warm"), ("❤️",  "red passionate"),("🩵","light blue soft"),
        ("🩷", "pink soft"),   ("🤎","brown earthy"),  ("🪷","lavender"),

        // ── Places / scenes ───────────────────────────────────────────────────
        ("🗼", "paris"),       ("⛩",  "japan"),       ("🕌","moroccan"),
        ("🏯", "japan castle"),("🏰","medieval castle"),("🌉","city bridge night"),
        ("🎑", "japan autumn"),("🎎","japan"),
    ];

    // Pre-build a single replacement map for O(n) scanning.
    private static readonly IReadOnlyList<(string, string)> _sorted =
        Mappings.OrderByDescending(m => m.Emoji.Length).ToList();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces all recognised emoji in <paramref name="text"/> with their
    /// prose expansion. The result is a plain ASCII-ish string that the
    /// normal tokenizer pipeline can handle.
    ///
    /// Example:
    ///   "🌙 ocean vibes ☕" → "midnight night ocean vibes coffee espresso warm"
    /// </summary>
    public static string Expand(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var sb = new StringBuilder(text);
        foreach (var (emoji, expansion) in _sorted)
        {
            sb.Replace(emoji, $" {expansion} ");
        }

        // Collapse runs of whitespace introduced by replacements.
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>
    /// Returns true if the text contains at least one recognised emoji.
    /// Used by the insight panel to show an "emoji detected" badge.
    /// </summary>
    public static bool ContainsEmoji(string text)
        => _sorted.Any(m => text.Contains(m.Item1));
}
