using System.IO;
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

    public SkinDefinition Generate(string promptText, double? screenWidth = null, double? screenHeight = null) =>
        GenerateAndExplain(promptText, screenWidth, screenHeight).Skin;

    public WidgetAnalysisResult Explain(string promptText, double? screenWidth = null, double? screenHeight = null) =>
        GenerateAndExplain(promptText, screenWidth, screenHeight).Analysis;

    /// <summary>
    /// Phase 5 Conversational Refinement: Parses a follow-up prompt to modify an existing skin in place.
    /// Example: "make the clock bigger", "remove the cpu graph"
    /// </summary>
    public SkinDefinition Refine(SkinDefinition baseSkin, string promptText)
    {
        var tokens = VibeTokenizer.TokenizeFull(promptText);
        
        bool makeBigger = false;
        bool makeSmaller = false;
        bool removeMode = false;
        
        foreach(var raw in tokens.Raw)
        {
            if (raw == "bigger" || raw == "large" || raw == "larger") makeBigger = true;
            if (raw == "smaller" || raw == "small" || raw == "tiny") makeSmaller = true;
            if (raw == "remove" || raw == "delete" || raw == "hide") removeMode = true;
        }

        var targetMeasures = new List<MeasureType>();
        foreach (var stem in tokens.Stemmed)
        {
            if (WidgetLexicon.Entries.TryGetValue(stem, out var exact))
            {
                if (exact.Measure.HasValue) targetMeasures.Add(exact.Measure.Value);
            }
            else 
            {
                var fuzzy = WidgetFuzzyMatcher.FindClosest(stem, out var matchedKey);
                if (fuzzy?.Measure != null) targetMeasures.Add(fuzzy.Measure.Value);
            }
        }

        if (removeMode && targetMeasures.Count > 0)
        {
            foreach(var t in targetMeasures)
            {
                string targetName = t.ToString();
                baseSkin.Meters.RemoveAll(m => m.MeasureName == targetName);
                baseSkin.Measures.RemoveAll(m => m.Name == targetName);
            }
            return baseSkin;
        }

        double scaleFactor = 1.0;
        if (makeBigger) scaleFactor = 1.25;
        if (makeSmaller) scaleFactor = 0.8;

        if (scaleFactor != 1.0)
        {
            foreach (var meter in baseSkin.Meters)
            {
                bool applies = targetMeasures.Count == 0 || (meter.MeasureName != null && targetMeasures.Select(m => m.ToString()).Contains(meter.MeasureName));
                if (applies)
                {
                    // Clamped so repeated refinements ("bigger" several times in a row) can't
                    // compound into something absurd or illegible - same bounds as the widget
                    // editor's own size controls.
                    meter.Width = Math.Clamp(meter.Width * scaleFactor, 20, 800);
                    meter.Height = Math.Clamp(meter.Height * scaleFactor, 20, 800);
                    meter.FontSize = Math.Clamp(meter.FontSize * scaleFactor, 8, 72);
                }
            }
            if (targetMeasures.Count == 0)
            {
                baseSkin.Width = Math.Clamp(baseSkin.Width * scaleFactor, 80, 1200);
                baseSkin.Height = Math.Clamp(baseSkin.Height * scaleFactor, 80, 1200);
            }
        }

        return baseSkin;
    }

    /// <param name="promptText">The plain-English widget description.</param>
    /// <param name="screenWidth">Target monitor's work-area width, in DIPs. Defaults to
    /// <see cref="AssumedScreenWidth"/> when null. Callers with a live window (see
    /// WidgetGeneratorViewModel.GetScreenSizeDips) should pass the real size so "top right" /
    /// "bottom left" land correctly on anything other than a 1920x1080 primary display.</param>
    /// <param name="screenHeight">Same as <paramref name="screenWidth"/>, for height.</param>
    public (SkinDefinition Skin, WidgetAnalysisResult Analysis) GenerateAndExplain(
        string promptText, double? screenWidth = null, double? screenHeight = null)
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

        // Auto-add WeatherCity if WeatherTemp is present but City wasn't explicitly requested.
        if (measures.Contains(MeasureType.WeatherTemp) && !measures.Contains(MeasureType.WeatherCity))
            measures.Insert(measures.IndexOf(MeasureType.WeatherTemp), MeasureType.WeatherCity);

        // Same reasoning as Time->Date: a track recommendation without the artist is half the
        // answer, so "vibe"/"recommend" pull in the artist line too even though only the title
        // measure is in the lexicon.
        if (measures.Contains(MeasureType.VibeTrackTitle) && !measures.Contains(MeasureType.VibeTrackArtist))
            measures.Insert(measures.IndexOf(MeasureType.VibeTrackTitle) + 1, MeasureType.VibeTrackArtist);

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

        var skin = BuildSkin(measures, kindPreference, sizeScale, preferBold, vertical, horizontal, usedFallback, promptText, emoji, screenWidth, screenHeight);

        var analysis = new WidgetAnalysisResult(
            matchedKeywords, fuzzyCorrections, measures, usedFallback, sizeScale, kindPreference, vertical, horizontal);

        return (skin, analysis);
    }

    // ── Skin construction ────────────────────────────────────────────────────

    private static SkinDefinition BuildSkin(
        List<MeasureType> measures, MeterKind? kindPreference, double sizeScale, bool bold,
        string? vertical, string? horizontal, bool usedFallback, string promptText, string? emoji,
        double? screenWidth, double? screenHeight)
    {
        var skin = new SkinDefinition
        {
            Name = GenerateName(measures, usedFallback, promptText),
            // Starts disabled, same as CreateNewSkinAsync's blank widget — a freshly-generated
            // widget hasn't been reviewed yet, so it shouldn't silently start showing on the
            // desktop (or start doing so on the next app launch, since SkinManagerService's
            // startup pass opens a window for every Enabled skin). The user enables it from the
            // editor/SkinsPage once they've confirmed it's what they wanted.
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
            if (type is MeasureType.DiskFree or MeasureType.DiskUsed)
            {
                var drives = GetFixedDrives();
                foreach (var drive in drives)
                {
                    string driveLetter = drive.TrimEnd('\\', ':');
                    AddMeasureAndMeters(skin, type, kindPreference, sizeScale, bold, meterWidth, ref y, drive, driveLetter);
                }
            }
            else
            {
                AddMeasureAndMeters(skin, type, kindPreference, sizeScale, bold, meterWidth, ref y);
            }
        }

        skin.Width = meterWidth + DefaultMargin * 2;
        skin.Height = y + DefaultMargin;

        // Ring/Icon widgets look best floating directly on the desktop (modular mode).
        // Text-heavy widgets get a semi-transparent card for readability.
        bool isModular = kindPreference is MeterKind.Ring or MeterKind.Icon;
        skin.Opacity = isModular ? 0.0 : 0.5;

        (skin.X, skin.Y) = ComputePosition(vertical, horizontal, skin.Width, skin.Height, screenWidth, screenHeight);

        return skin;
    }

    private static void AddMeasureAndMeters(
        SkinDefinition skin, MeasureType type, MeterKind? kindPreference,
        double sizeScale, bool bold, double meterWidth, ref double y,
        string? driveTarget = null, string? driveLetter = null)
    {
        string measureName = driveLetter != null ? $"{type}_{driveLetter}" : type.ToString();
        string? target = type is MeasureType.DiskFree or MeasureType.DiskUsed
            ? (driveTarget ?? @"C:\")
            : null;
        skin.Measures.Add(new MeasureDefinition { Name = measureName, Type = type, Target = target });

        double labelHeight = 18 * sizeScale;
        double fontSize = 13 * sizeScale;
        double barHeight = 10 * sizeScale;
        double graphHeight = 60 * sizeScale;
        double rowGap = 4 * sizeScale;
        double sectionGap = 14 * sizeScale;

        var (_, format) = LabelAndFormat(type, driveLetter);

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
        bool naturallyTextOnly = type is MeasureType.Time or MeasureType.Date or MeasureType.Uptime or MeasureType.Battery
            or MeasureType.WeatherTemp or MeasureType.WeatherDesc or MeasureType.WeatherCity
            or MeasureType.MediaTitle or MeasureType.MediaArtist or MeasureType.MediaState
            or MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood;

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
        else if (effectiveKind == MeterKind.Ring)
        {
            double ringSize = 100 * sizeScale;
            skin.Meters.Add(new MeterDefinition
            {
                Kind = MeterKind.Ring, MeasureName = measureName,
                X = DefaultMargin + (meterWidth - ringSize) / 2, 
                Y = y, Width = ringSize, Height = ringSize,
                BarMax = DefaultBarMax(type)
            });
            y += ringSize + sectionGap;
        }
        else if (effectiveKind == MeterKind.Icon)
        {
            double iconSize = 48 * sizeScale;
            skin.Meters.Add(new MeterDefinition
            {
                Kind = MeterKind.Icon, MeasureName = measureName,
                X = DefaultMargin + (meterWidth - iconSize) / 2, 
                Y = y, Width = iconSize, Height = iconSize,
                IconGlyph = "\uE946"
            });
            y += iconSize + sectionGap;
        }
        else
        {
            y += rowGap * 2;
        }
    }

    private static (double X, double Y) ComputePosition(
        string? vertical, string? horizontal, double width, double height,
        double? screenWidth, double? screenHeight)
    {
        double sw = screenWidth is > 0 ? screenWidth.Value : AssumedScreenWidth;
        double sh = screenHeight is > 0 ? screenHeight.Value : AssumedScreenHeight;

        double x = horizontal switch
        {
            "center" => (sw - width) / 2,
            "right"  => sw - width - 40,
            _        => 40,                // left / default
        };
        double yPos = vertical switch
        {
            "center" => (sh - height) / 2,
            "bottom" => sh - height - 40,
            _        => 40,                // top / default
        };
        return (x, yPos);
    }

    private static (string Label, string Format) LabelAndFormat(MeasureType type, string? driveLetter = null) => type switch
    {
        MeasureType.Cpu => ("CPU", "CPU  {0:F0}%"),
        MeasureType.Memory => ("RAM", "RAM  {0:F0}%"),
        MeasureType.DiskFree => (driveLetter != null ? $"{driveLetter}:" : "Disk", $"{driveLetter ?? "C"}:  {{1}} free"),
        MeasureType.DiskUsed => (driveLetter != null ? $"{driveLetter}:" : "Disk", $"{driveLetter ?? "C"}:  {{1}} used"),
        MeasureType.NetworkDown => ("Down", "↓ {1}"),
        MeasureType.NetworkUp => ("Up", "↑ {1}"),
        MeasureType.Battery => ("Battery", "🔋 {1}"),
        MeasureType.Time => ("Time", "{1}"),
        MeasureType.Date => ("Date", "{1}"),
        MeasureType.Uptime => ("Uptime", "⏱ {1}"),
        MeasureType.WeatherTemp => ("Weather", "🌤 {1}"),
        MeasureType.WeatherDesc => ("Forecast", "🌧 {1}"),
        MeasureType.WeatherCity => ("Location", "📍 {1}"),
        MeasureType.MediaTitle => ("Now Playing", "🎵 {1}"),
        MeasureType.MediaArtist => ("Artist", "{1}"),
        MeasureType.MediaState => ("Status", "{1}"),
        MeasureType.VibeTrackTitle => ("Vibe Match", "🎧 {1}"),
        MeasureType.VibeTrackArtist => ("Artist", "{1}"),
        MeasureType.VibeMood => ("Mood", "{1}"),
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

    /// <summary>
    /// Returns the root paths of all fixed, ready drives on this machine (e.g. ["C:\", "D:\"]).
    /// Falls back to just C:\ if drive enumeration fails for any reason.
    /// </summary>
    private static List<string> GetFixedDrives()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .OrderBy(d => d)
                .ToList();
            return drives.Count > 0 ? drives : new List<string> { @"C:\" };
        }
        catch
        {
            return new List<string> { @"C:\" };
        }
    }
}