using ThemeManager.Core.Models;
using ThemeManager.Core.Utilities;

namespace ThemeManager.Core.Utilities;

/// <summary>
/// Checks all critical text/background color pairs in a <see cref="CozyTheme"/>
/// against WCAG 2.1 success criteria 1.4.3 (AA) and 1.4.6 (AAA).
///
/// Python equivalent:
///   wcag_contrast_ratio.passes_AA(wcag_contrast_ratio.rgb(r1,g1,b1), wcag_contrast_ratio.rgb(r2,g2,b2))
///
/// Thresholds:
///   Normal text  AA  = 4.5 : 1
///   Normal text  AAA = 7.0 : 1
///   Large text   AA  = 3.0 : 1  (18pt+ or 14pt bold)
///   Large text   AAA = 4.5 : 1
/// </summary>
public static class ContrastChecker
{
    // ── Grade enum ────────────────────────────────────────────────────────────

    public enum Grade { Fail, AA_Large, AA, AAA }

    // ── Pair record ───────────────────────────────────────────────────────────

    /// <summary>A single checked foreground/background pairing.</summary>
    public sealed record ContrastResult(
        string PairName,
        string ForegroundHex,
        string BackgroundHex,
        float  Ratio,
        Grade  NormalTextGrade,
        Grade  LargeTextGrade
    )
    {
        public bool PassesAA     => NormalTextGrade >= Grade.AA;
        public bool PassesAAA    => NormalTextGrade >= Grade.AAA;
        public string RatioLabel => $"{Ratio:F1}:1";

        public string NormalGradeLabel => NormalTextGrade switch
        {
            Grade.AAA      => "AAA ✓",
            Grade.AA       => "AA ✓",
            Grade.AA_Large => "AA (large) ✓",
            _              => "Fail ✗",
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks all meaningful text/background pairs in the given theme.
    /// Returns one result per pair, ordered by importance.
    /// </summary>
    public static IReadOnlyList<ContrastResult> Check(CozyTheme theme)
    {
        var results = new List<ContrastResult>();

        void Add(string name, string fg, string bg)
        {
            float ratio = ColorMath.ContrastRatio(fg, bg);
            results.Add(new ContrastResult(
                name, fg, bg, ratio,
                NormalGrade(ratio),
                LargeGrade(ratio)));
        }

        // Primary text on all background surfaces
        Add("Body text / Background Base",   theme.TextPrimary, theme.BackgroundBase);
        Add("Body text / Background Alt",    theme.TextPrimary, theme.BackgroundAlt);
        Add("Body text / Surface",           theme.TextPrimary, theme.Surface);

        // Muted text
        Add("Muted text / Background Base",  theme.TextMuted,   theme.BackgroundBase);
        Add("Muted text / Background Alt",   theme.TextMuted,   theme.BackgroundAlt);

        // Accent text / interactive text on backgrounds
        Add("Accent / Background Base",      theme.AccentPrimary, theme.BackgroundBase);
        Add("Accent strong / Background",    theme.AccentStrong,  theme.BackgroundBase);

        // Text on Surface (buttons)
        Add("Text on Surface (buttons)",     theme.BackgroundBase, theme.Surface);
        Add("Strong text on Surface",        theme.AccentStrong,   theme.Surface);

        return results;
    }

    /// <summary>
    /// Quick summary: how many pairs pass AA out of the full check.
    /// </summary>
    public static (int Passing, int Total) Summary(CozyTheme theme)
    {
        var results = Check(theme);
        int passing = results.Count(r => r.PassesAA);
        return (passing, results.Count);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static Grade NormalGrade(float ratio) => ratio switch
    {
        >= 7.0f => Grade.AAA,
        >= 4.5f => Grade.AA,
        >= 3.0f => Grade.AA_Large,
        _       => Grade.Fail,
    };

    private static Grade LargeGrade(float ratio) => ratio switch
    {
        >= 4.5f => Grade.AAA,
        >= 3.0f => Grade.AA,
        _       => Grade.Fail,
    };
}
