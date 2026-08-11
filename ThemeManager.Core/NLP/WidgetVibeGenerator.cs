using ThemeManager.Core.Skins;

namespace ThemeManager.Core.NLP;

/// <summary>
/// The full output of a widget-generation pass — the widget-generation sibling of
/// <see cref="VibeAnalysisResult"/>, for an insights panel to explain what was detected.
/// </summary>
public sealed record WidgetAnalysisResult(
    List<string> MatchedKeywords,
    Dictionary<string, string> FuzzyCorrections,
    List<MeasureType> Measures,
    bool UsedFallback,
    double SizeScale,
    MeterKind? MeterKindPreference,
    string? VerticalHint,
    string? HorizontalHint
);

/// <summary>
/// Turns a plain-English widget description ("a big CPU and memory graph in the top right")
/// into a real <see cref="SkinDefinition"/> — same offline, no-external-API approach as
/// <see cref="VibeThemeGenerator"/>, reusing its tokenizer, just pointed at
/// <see cref="WidgetLexicon"/> instead of <see cref="ColorLexicon"/>. The generated widget's
/// card automatically picks up whatever Cozy theme is currently active (same as every other
/// widget), so this only needs to figure out *structure* — which measures, what kind of meter,
/// how big, roughly where — not color.
/// </summary>
public sealed class WidgetVibeGenerator
{
    private const double DefaultMargin = 16;
    private const double AssumedScreenWidth = 1920;  // most common resolution; drag it in the
    private const double AssumedScreenHeight = 1080; // editor in seconds if yours is different

    public SkinDefinition Generate(string promptText) => GenerateAndExplain(promptText).Skin;

    public WidgetAnalysisResult Explain(string promptText) => GenerateAndExplain(promptText).Analysis;

    public (SkinDefinition Skin, WidgetAnalysisResult Analysis) GenerateAndExplain(string promptText)
    {
        var tokens = VibeTokenizer.TokenizeFull(promptText);

        var measures = new List<MeasureType>();
        var matchedKeywords = new List<string>();
        var fuzzyCorrections = new Dictionary<string, string>();
        var kindVotes = new List<MeterKind>();
        var sizeVotes = new List<double>();
        bool preferBold = false;
        string? vertical = null;
        string? horizontal = null;

        void Absorb(WidgetSignal signal, string keyword)
        {
            matchedKeywords.Add(keyword);
            if (signal.Measure is { } m && !measures.Contains(m)) measures.Add(m);
            if (signal.PreferredKind is { } k) kindVotes.Add(k);
            if (signal.SizeMultiplier is { } sz) sizeVotes.Add(sz);
            if (signal.PreferBold == true) preferBold = true;
            if (signal.VerticalHint != null) vertical = signal.VerticalHint;
            if (signal.HorizontalHint != null) horizontal = signal.HorizontalHint;
        }

        foreach (var stem in tokens.Stemmed)
        {
            if (WidgetLexicon.Entries.TryGetValue(stem, out var exact))
            {
                Absorb(exact, stem);
                continue;
            }

            var fuzzy = WidgetFuzzyMatcher.FindClosest(stem, out var matchedKey);
            if (fuzzy != null && matchedKey != null)
            {
                fuzzyCorrections[stem] = matchedKey;
                Absorb(fuzzy, matchedKey);
            }
        }

        bool usedFallback = measures.Count == 0;
        if (usedFallback)
            measures.Add(MeasureType.Time); // simplest, always-meaningful default — a clock

        // Consumer expectation: a "clock" widget should show time AND date, not just bare digits.
        // Auto-add Date if Time is present but Date wasn't explicitly requested.
        if (measures.Contains(MeasureType.Time) && !measures.Contains(MeasureType.Date))
            measures.Add(MeasureType.Date);

        double sizeScale = Math.Clamp(sizeVotes.Count > 0 ? sizeVotes.Average() : 1.0, 0.5, 2.0);
        MeterKind? kindPreference = kindVotes.Count == 0 ? null
            : kindVotes.GroupBy(k => k).OrderByDescending(g => g.Count()).First().Key;

        // Find a matching emoji decoration based on matched keywords
        string? emoji = null;
        if (matchedKeywords.Contains("cat")) emoji = "🐱";
        else if (matchedKeywords.Contains("dog")) emoji = "🐶";
        else if (matchedKeywords.Contains("star")) emoji = "⭐";
        else if (matchedKeywords.Contains("floral")) emoji = "🌸";
        else if (matchedKeywords.Contains("magic") || matchedKeywords.Contains("dream")) emoji = "✨";
        else if (matchedKeywords.Contains("cyber") || matchedKeywords.Contains("neon")) emoji = "⚡";
        else if (matchedKeywords.Contains("cozi") || matchedKeywords.Contains("warm")) emoji = "☕";
        else if (matchedKeywords.Contains("retro")) emoji = "📻";
        else if (matchedKeywords.Contains("anim")) emoji = "🎌";
        else if (matchedKeywords.Contains("cute") || matchedKeywords.Contains("kawaii") || matchedKeywords.Contains("chibi")) emoji = "🎀";

        var skin = BuildSkin(measures, kindPreference, sizeScale, preferBold, vertical, horizontal, usedFallback, promptText, emoji);

        var analysis = new WidgetAnalysisResult(
            matchedKeywords, fuzzyCorrections, measures, usedFallback, sizeScale, kindPreference, vertical, horizontal);

        return (skin, analysis);
    }

    // ── Skin construction ────────────────────────────────────────────────────

    private static SkinDefinition BuildSkin(
        List<MeasureType> measures, MeterKind? kindPreference, double sizeScale, bool bold,
        string? vertical, string? horizontal, bool usedFallback, string promptText, string? emoji)
    {
        var skin = new SkinDefinition
        {
            Name = GenerateName(measures, usedFallback, promptText),
            Enabled = false,
        };

        double y = DefaultMargin;
        double meterWidth = 188 * sizeScale;

        // If an emoji decoration was triggered by the prompt, add a large text meter for it at the top
        if (emoji != null)
        {
            skin.Meters.Add(new MeterDefinition
            {
                Kind = MeterKind.String,
                StaticText = emoji,
                X = DefaultMargin,
                Y = y,
                Width = meterWidth,
                Height = 36 * sizeScale,
                FontSize = 28 * sizeScale,
                Bold = false,
            });
            y += (36 * sizeScale) + (4 * sizeScale);
        }

        foreach (var type in measures)
        {
            AddMeasureAndMeters(skin, type, kindPreference, sizeScale, bold, meterWidth, ref y);
        }

        skin.Width = meterWidth + DefaultMargin * 2;
        skin.Height = y + DefaultMargin;

        (skin.X, skin.Y) = ComputePosition(vertical, horizontal, skin.Width, skin.Height);

        return skin;
    }

    private static void AddMeasureAndMeters(
        SkinDefinition skin, MeasureType type, MeterKind? kindPreference,
        double sizeScale, bool bold, double meterWidth, ref double y)
    {
        string measureName = type.ToString();
        string? target = type is MeasureType.DiskFree or MeasureType.DiskUsed ? @"C:\" : null;
        skin.Measures.Add(new MeasureDefinition { Name = measureName, Type = type, Target = target });

        double labelHeight = 18 * sizeScale;
        double fontSize = 13 * sizeScale;
        double barHeight = 10 * sizeScale;
        double graphHeight = 60 * sizeScale;
        double rowGap = 4 * sizeScale;
        double sectionGap = 14 * sizeScale;

        var (_, format) = LabelAndFormat(type);

        skin.Meters.Add(new MeterDefinition
        {
            Kind = MeterKind.String,
            MeasureName = measureName,
            Format = format,
            X = DefaultMargin,
            Y = y,
            Width = meterWidth,
            Height = labelHeight,
            FontSize = fontSize,
            Bold = bold,
        });
        y += labelHeight + rowGap;

        // Time/Date/Uptime/Battery are naturally "read as text" measures — a bar or graph of
        // the clock doesn't mean anything, so they stay text-only unless a bar/graph was
        // explicitly requested (kindPreference), in which case we respect the ask anyway.
        bool naturallyTextOnly = type is MeasureType.Time or MeasureType.Date or MeasureType.Uptime or MeasureType.Battery;

        // For Time measures, use a bigger, bolder font by default — consumers expect a clock
        // to look like a clock, not a tiny label. Date gets a smaller companion size.
        if (type == MeasureType.Time)
        {
            // Override the label meter we just added with clock-appropriate sizing
            var timeMeter = skin.Meters.Last();
            timeMeter.FontSize = Math.Max(fontSize, 26 * sizeScale);
            timeMeter.Bold = true;
            timeMeter.Height = Math.Max(labelHeight, 34 * sizeScale);
            y = timeMeter.Y + timeMeter.Height + rowGap;
        }
        else if (type == MeasureType.Date)
        {
            // Date as companion to time — slightly smaller, not bold
            var dateMeter = skin.Meters.Last();
            dateMeter.FontSize = Math.Max(12, 12 * sizeScale);
            dateMeter.Bold = false;
        }

        MeterKind? effectiveKind = kindPreference ?? (naturallyTextOnly ? null : MeterKind.Bar);

        if (effectiveKind == MeterKind.Bar)
        {
            skin.Meters.Add(new MeterDefinition
            {
                Kind = MeterKind.Bar, MeasureName = measureName,
                X = DefaultMargin, Y = y, Width = meterWidth, Height = barHeight,
                BarMax = DefaultBarMax(type),
            });
            y += barHeight + sectionGap;
        }
        else if (effectiveKind == MeterKind.Graph)
        {
            skin.Meters.Add(new MeterDefinition
            {
                Kind = MeterKind.Graph, MeasureName = measureName,
                X = DefaultMargin, Y = y, Width = meterWidth, Height = graphHeight,
                BarMax = DefaultBarMax(type), HistoryLength = 60,
            });
            y += graphHeight + sectionGap;
        }
        else
        {
            y += rowGap * 2;
        }
    }

    private static (double X, double Y) ComputePosition(string? vertical, string? horizontal, double width, double height)
    {
        double x = horizontal == "right" ? AssumedScreenWidth - width - 40 : 40;
        double yPos = vertical == "bottom" ? AssumedScreenHeight - height - 40 : 40;
        return (x, yPos);
    }

    private static (string Label, string Format) LabelAndFormat(MeasureType type) => type switch
    {
        MeasureType.Cpu => ("CPU", "CPU  {0:F0}%"),
        MeasureType.Memory => ("RAM", "RAM  {0:F0}%"),
        MeasureType.DiskFree => ("Disk", "C:\\  {0:F0}% free"),
        MeasureType.DiskUsed => ("Disk", "C:\\  {0:F0}% used"),
        MeasureType.NetworkDown => ("Down", "↓ {1}"),
        MeasureType.NetworkUp => ("Up", "↑ {1}"),
        MeasureType.Battery => ("Battery", "🔋 {1}"),
        MeasureType.Time => ("Time", "{1}"),
        MeasureType.Date => ("Date", "{1}"),
        MeasureType.Uptime => ("Uptime", "⏱ {1}"),
        _ => ("", "{1}"),
    };

    private static double DefaultBarMax(MeasureType type) =>
        type is MeasureType.NetworkDown or MeasureType.NetworkUp ? 2048 : 100;

    private static string GenerateName(List<MeasureType> measures, bool usedFallback, string promptText)
    {
        // Use the user's original prompt to create a more personal name
        var trimmed = promptText.Trim();
        if (trimmed.Length > 0 && trimmed.Length <= 40)
        {
            // Title-case the first letter of each word
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titled = string.Join(' ', words.Select(w =>
                char.ToUpper(w[0]) + (w.Length > 1 ? w[1..] : "")));
            return titled;
        }

        if (usedFallback) return "Custom Widget";

        var labels = measures.Select(m => LabelAndFormat(m).Label).Where(l => l.Length > 0).Distinct().ToList();
        return labels.Count switch
        {
            0 => "Custom Widget",
            1 => $"{labels[0]} Widget",
            2 => $"{labels[0]} + {labels[1]} Widget",
            _ => $"{labels[0]} + {labels[1]} + {labels.Count - 2} more",
        };
    }
}
