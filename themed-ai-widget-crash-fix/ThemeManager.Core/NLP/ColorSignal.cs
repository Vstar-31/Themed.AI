namespace ThemeManager.Core.NLP;

/// <summary>
/// The color "signal" contributed by a single matched word.
///
/// Think of this like a word vector, but in perceptual color-space (HSL) rather
/// than a high-dimensional embedding space. Each word pushes the final palette
/// in a particular direction along these axes.
///
/// Python analogy: one row in a word2vec embedding, but hand-crafted
/// for color semantics instead of trained on a corpus.
/// </summary>
public sealed record ColorSignal(
    /// <summary>Preferred hue in degrees (0–360, HSL).</summary>
    float HueDeg,

    /// <summary>
    /// How strongly this word anchors the hue (0–1).
    /// "Red" = 1.0 (very specific). "Cozy" = 0.4 (directional but soft).
    /// </summary>
    float HueWeight,

    /// <summary>Target lightness (0 = black, 1 = white).</summary>
    float Lightness,

    /// <summary>How strongly this word pushes lightness (0–1).</summary>
    float LightnessWeight,

    /// <summary>Target saturation (0 = grey, 1 = vivid).</summary>
    float Saturation,

    /// <summary>How strongly this word pushes saturation (0–1).</summary>
    float SaturationWeight,

    /// <summary>
    /// Warmth bias: +1 = very warm (nudge hue toward reds/oranges),
    /// -1 = very cool (nudge toward blues/teals), 0 = neutral.
    /// Applied AFTER hue averaging to subtly shift the final result.
    /// </summary>
    float Warmth,

    /// <summary>
    /// Category tag used for name generation.
    /// e.g. "environment", "mood", "time", "material", "color"
    /// </summary>
    string Category
);
