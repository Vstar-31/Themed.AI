using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Utilities;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// Bindable view-model wrapper around <see cref="ContrastChecker.ContrastResult"/>.
/// x:DataType in DataTemplate requires a concrete class, not a record from another assembly,
/// so we project the record into this thin adapter.
/// </summary>
public sealed class ContrastResultProxy
{
    public string PairName        { get; init; } = string.Empty;
    public string ForegroundHex   { get; init; } = "#000000";
    public string BackgroundHex   { get; init; } = "#FFFFFF";
    public string RatioLabel      { get; init; } = "—";
    public string NormalGradeLabel{ get; init; } = "—";

    /// <summary>
    /// Green for AAA/AA, amber for AA-Large, red for Fail.
    /// Used directly as the badge background in the XAML DataTemplate.
    /// </summary>
    public SolidColorBrush GradeBrush { get; init; } =
        new(Windows.UI.Color.FromArgb(0xFF, 0x7F, 0x70, 0x65));

    // ── Factory ───────────────────────────────────────────────────────────────

    public static ContrastResultProxy From(ContrastChecker.ContrastResult r)
    {
        var brush = r.NormalTextGrade switch
        {
            ContrastChecker.Grade.AAA      => new SolidColorBrush(
                Windows.UI.Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32)), // green
            ContrastChecker.Grade.AA       => new SolidColorBrush(
                Windows.UI.Color.FromArgb(0xFF, 0x55, 0x8B, 0x2F)), // light green
            ContrastChecker.Grade.AA_Large => new SolidColorBrush(
                Windows.UI.Color.FromArgb(0xFF, 0xE6, 0x5C, 0x00)), // amber
            _                              => new SolidColorBrush(
                Windows.UI.Color.FromArgb(0xFF, 0xC6, 0x28, 0x28)), // red
        };

        return new ContrastResultProxy
        {
            PairName         = r.PairName,
            ForegroundHex    = r.ForegroundHex,
            BackgroundHex    = r.BackgroundHex,
            RatioLabel       = r.RatioLabel,
            NormalGradeLabel = r.NormalGradeLabel,
            GradeBrush       = brush,
        };
    }

    /// <summary>Projects a list of results into proxies for ItemsRepeater binding.</summary>
    public static IReadOnlyList<ContrastResultProxy> FromList(
        IEnumerable<ContrastChecker.ContrastResult> results)
        => results.Select(From).ToList();
}
