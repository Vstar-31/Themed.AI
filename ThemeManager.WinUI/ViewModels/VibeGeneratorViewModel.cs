using System.Collections.ObjectModel;
using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Personalization;
using ThemeManager.Core.Services;
using Microsoft.Extensions.Logging;
using ThemeManager.WinUI;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Drives the Vibe Generator page.
///
/// State machine:
///   Idle → Analyzing → Result (with preview)
///                   ↘ NoMatch (suggestion shown)
/// </summary>
public sealed class VibeGeneratorViewModel : ViewModelBase
{
    private readonly ThemeService       _themeService;
    private readonly ILogger<VibeGeneratorViewModel> _logger;

    // ── Input ─────────────────────────────────────────────────────────────────
    private string _vibeText = string.Empty;
    public string VibeText
    {
        get => _vibeText;
        set
        {
            if (SetProperty(ref _vibeText, value))
            {
                OnPropertyChanged(nameof(CanGenerate));
                // Live-update the insight panel as the user types (debounced via
                // the 300 ms delay in the view's TextChanged handler).
            }
        }
    }

    public bool CanGenerate => !string.IsNullOrWhiteSpace(VibeText) && !IsBusy;

    // ── State flags ───────────────────────────────────────────────────────────
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { SetProperty(ref _isBusy, value); OnPropertyChanged(nameof(CanGenerate)); }
    }

    private bool _hasResult;
    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    private bool _hasNoMatch;
    public bool HasNoMatch
    {
        get => _hasNoMatch;
        set => SetProperty(ref _hasNoMatch, value);
    }

    // ── Generated theme + analysis ────────────────────────────────────────────
    private CozyTheme? _generatedTheme;
    public CozyTheme? GeneratedTheme
    {
        get => _generatedTheme;
        set => SetProperty(ref _generatedTheme, value);
    }

    private VibeAnalysisResult? _analysis;
    public VibeAnalysisResult? Analysis
    {
        get => _analysis;
        set
        {
            if (SetProperty(ref _analysis, value))
            {
                OnPropertyChanged(nameof(MatchedWordsDisplay));
                OnPropertyChanged(nameof(BigramDisplay));
                OnPropertyChanged(nameof(FuzzyDisplay));
                OnPropertyChanged(nameof(EmojiDisplay));
                OnPropertyChanged(nameof(SentimentDisplay));
                OnPropertyChanged(nameof(HueDegDisplay));
                OnPropertyChanged(nameof(LightnessDisplay));
                OnPropertyChanged(nameof(SaturationDisplay));
                OnPropertyChanged(nameof(ModeDisplay));
                OnPropertyChanged(nameof(HasBigrams));
                OnPropertyChanged(nameof(HasFuzzyCorrections));
                OnPropertyChanged(nameof(HadEmoji));
            }
        }
    }

    // ── Insight panel computed strings ────────────────────────────────────────

    public string MatchedWordsDisplay => Analysis is null || Analysis.MatchedKeywords.Count == 0
        ? "— none yet —"
        : string.Join("  ·  ", Analysis.MatchedKeywords
            .Select(k => $"{k} [{Analysis.KeywordCategories.GetValueOrDefault(k, "?")}]"));

    // Phase 3: bigram display
    public bool   HasBigrams    => Analysis?.BigramMatches.Count > 0;
    public string BigramDisplay => HasBigrams
        ? string.Join("  ·  ", Analysis!.BigramMatches.Select(b => $"\"{b}\""))
        : "—";

    // Phase 3: fuzzy correction display
    public bool   HasFuzzyCorrections => Analysis?.FuzzyCorrections.Count > 0;
    public string FuzzyDisplay        => HasFuzzyCorrections
        ? string.Join("  ·  ", Analysis!.FuzzyCorrections
            .Select(kv => $"{kv.Key} → {kv.Value}"))
        : "—";

    // Phase 3: emoji badge
    public bool   HadEmoji     => Analysis?.HadEmojiInput == true;
    public string EmojiDisplay => HadEmoji ? "Yes — emoji expanded to prose" : "No";

    public string SentimentDisplay => Analysis is null ? "—"
        : Analysis.SentimentScore switch
        {
            > 0.4f  => $"+{Analysis.SentimentScore:F2}  ✨ positive",
            < -0.4f => $"{Analysis.SentimentScore:F2}  🌑 negative",
            _       => $"{Analysis.SentimentScore:F2}  ⚖️ neutral",
        };

    public string HueDegDisplay     => Analysis is null ? "—" : $"{Analysis.ComputedHue:F0}°";
    public string LightnessDisplay  => Analysis is null ? "—" : $"{Analysis.ComputedLightness * 100:F0} %";
    public string SaturationDisplay => Analysis is null ? "—" : $"{Analysis.ComputedSaturation * 100:F0} %";
    public string ModeDisplay       => Analysis is null ? "—" : (Analysis.IsDark ? "Dark 🌑" : "Light ☀️");

    // ── Swatches (list of hex strings for the preview strip) ─────────────────
    public ObservableCollection<string> PreviewSwatches { get; } = new();

    // ── Suggestion chips ──────────────────────────────────────────────────────
    public ObservableCollection<string> SuggestionChips { get; } = new()
    {
        "midnight ocean storm",
        "cozy autumn library",
        "neon tokyo night",
        "golden desert sunset",
        "nordic winter frost",
        "dark espresso noir",
        "cherry blossom spring",
        "emerald forest mist",
        "velvet midnight jazz",
        "terracotta tuscan noon",
        "soft lavender dream",
        "copper ember glow",
        "arctic glacier dawn",
        "moody rainy evening",
        "vibrant tropical reef",
    };

    // ── Status message ────────────────────────────────────────────────────────
    private string _status = "Type a vibe, mood, place, or scene above.";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public VibeGeneratorViewModel(ThemeService themeService)
    {
        _themeService = themeService;
        _logger = App.LoggerFactory.CreateLogger<VibeGeneratorViewModel>();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full NLP pipeline on the current VibeText.
    /// Uses Task.Run to avoid blocking the UI thread during lexicon traversal.
    /// </summary>
    public async Task GenerateAsync()
    {
        if (!CanGenerate) return;

        IsBusy     = true;
        HasResult  = false;
        HasNoMatch = false;
        Status     = "Reading your vibe…";

        _logger.LogInformation("Generating vibe theme for text: {VibeText}", VibeText);

        try
        {
            var text = VibeText.Trim();

            // A previous result that was generated but never saved is a real, if soft,
            // negative signal, same reasoning as the widget side.
            if (HasResult && GeneratedTheme != null)
            {
                App.Personalization.SubmitFeedback(new FeedbackAction
                {
                    ItemId = GeneratedTheme.Id,
                    IsWidget = false,
                    Type = FeedbackType.ImplicitDismissed,
                    ThemeAccentColor = GeneratedTheme.AccentPrimary,
                });
            }

            // Run CPU-bound NLP on thread pool, keep UI responsive.
            var context = new GenerationContext { Prompt = text };
            var candidate = await Task.Run(() => App.Personalization.GenerateBestTheme(context));
            var theme = candidate.Theme;
            var analysis = candidate.Analysis;

            // Infer mood from the generated analysis for future generations in this session.
            if (analysis is not null)
                context.Mood = MoodInferrer.InferMood(analysis);

            Analysis       = analysis;
            GeneratedTheme = theme;

            // Update swatch strip.
            PreviewSwatches.Clear();
            if (analysis != null)
                foreach (var hex in analysis.Swatches)
                    PreviewSwatches.Add(hex);

            if (analysis is null || analysis.MatchedKeywords.Count == 0)
            {
                HasNoMatch = true;
                Status = "Couldn't find colour signals in that text. Try adding more descriptive words.";
                _logger.LogWarning("Vibe generation resulted in no match for text: {VibeText}", text);
            }
            else
            {
                HasResult = true;
                Status = $"Generated \"{theme.Name}\" from {analysis.MatchedKeywords.Count} matched keywords.";
                _logger.LogInformation("Vibe generation succeeded via personalization pipeline. Generated theme: {ThemeName}, Source: {Source}", theme.Name, candidate.GenerationSource);

                // NOTE: Do NOT call SetActiveTheme here. Applying the generated
                // theme immediately re-skins the entire app (including the result
                // card itself), making the swatches, token details, and Save/Edit
                // buttons effectively invisible. The user applies the theme
                // explicitly via Save or Edit.
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Saves the current generated theme to the theme list.</summary>
    public async Task SaveGeneratedThemeAsync()
    {
        if (GeneratedTheme is null) return;
        try
        {
            App.Personalization.SubmitFeedback(new FeedbackAction
            {
                ItemId = GeneratedTheme.Id,
                IsWidget = false,
                Type = FeedbackType.ImplicitApplied,
                ThemeAccentColor = GeneratedTheme.AccentPrimary,
            });

            await _themeService.SaveThemeAsync(GeneratedTheme);
            _themeService.SetActiveTheme(GeneratedTheme);
            Status = $"\"{GeneratedTheme.Name}\" saved and applied to your widgets.";
            _logger.LogInformation("User saved and applied generated theme: {ThemeName}", GeneratedTheme.Name);
        }
        catch (IOException ex)
        {
            Status = $"Save failed: {ex.Message}";
            _logger.LogError(ex, "Failed to save generated theme: {ThemeName}", GeneratedTheme.Name);
        }
    }

    /// <summary>
    /// Copies the given hex string to the Windows clipboard and briefly
    /// shows a confirmation in the status bar.
    /// Called from VibePage code-behind on swatch click.
    /// </summary>
    public void CopyHexToClipboard(string hex)
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(hex);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        Status = $"Copied {hex} to clipboard.";
        _logger.LogInformation("User copied hex to clipboard: {Hex}", hex);
    }

    /// <summary>Regenerates with a randomised variation on the same text.</summary>
    public async Task RegenerateAsync()
    {
        // Appending a tiny variation seed doesn't change the meaning but
        // shuffles any tie-breaking randomness in future algorithm extensions.
        if (!string.IsNullOrWhiteSpace(VibeText))
            await GenerateAsync();
    }

    /// <summary>Sets the vibe text from a suggestion chip and triggers generation.</summary>
    public async Task UseChipAsync(string chip)
    {
        _logger.LogInformation("User selected vibe suggestion chip: {Chip}", chip);
        VibeText = chip;
        await GenerateAsync();
    }

    /// <summary>Resets the page back to the idle state.</summary>
    public void Reset()
    {
        HasResult  = false;
        HasNoMatch = false;
        Analysis   = null;
        GeneratedTheme = null;
        PreviewSwatches.Clear();
        Status = "Type a vibe, mood, place, or scene above.";
    }
}
