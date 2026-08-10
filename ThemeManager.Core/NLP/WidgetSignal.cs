using ThemeManager.Core.Skins;

namespace ThemeManager.Core.NLP;

/// <summary>
/// What a single matched word votes for when generating a widget from a text prompt.
/// Sibling to <see cref="ColorSignal"/>, but for widget *structure* (which measures, how big,
/// what kind of meter, roughly where) rather than continuous color blending — so this is a set
/// of mostly-independent votes rather than values meant to be weighted-averaged together.
///
/// Python analogy: still a hand-crafted word → meaning dictionary, just each entry is a sparse
/// "ballot" (mostly-null fields) instead of a dense vector — closer to a one-hot feature row
/// than a word embedding.
/// </summary>
public sealed record WidgetSignal(
    /// <summary>A measure this word implies including, e.g. "cpu" → MeasureType.Cpu. Null if this word isn't about a measure.</summary>
    MeasureType? Measure,

    /// <summary>A meter-kind preference this word implies, e.g. "graph" → MeterKind.Graph.</summary>
    MeterKind? PreferredKind,

    /// <summary>Size scaling this word implies — 1.0 = no opinion, &lt;1 = "make it smaller", &gt;1 = "make it bigger".</summary>
    double? SizeMultiplier,

    /// <summary>Whether this word implies bold text (e.g. "bold", "big"). Null = no opinion.</summary>
    bool? PreferBold,

    /// <summary>"top" | "bottom" | null — vertical placement this word implies.</summary>
    string? VerticalHint,

    /// <summary>"left" | "right" | null — horizontal placement this word implies.</summary>
    string? HorizontalHint,

    /// <summary>Category tag, purely for the explanation panel ("measure", "style", "kind", "position").</summary>
    string Category
);
