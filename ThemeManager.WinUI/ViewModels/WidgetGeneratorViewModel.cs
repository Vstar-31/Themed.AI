using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Personalization;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.Services;
using Microsoft.Extensions.Logging;
using WinRT.Interop;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Backs <see cref="Views.WidgetGeneratorPage"/> — the prompt-to-widget sibling of
/// <see cref="VibeGeneratorViewModel"/>. Where that one generates a color theme from a mood,
/// this one generates a widget's measures/meters/layout from a plain description, then hands
/// the draft to <see cref="SkinManagerService.AddGeneratedSkinAsync"/> and on to the full
/// editor for whatever customization is still wanted.
/// </summary>
public sealed class WidgetGeneratorViewModel : ViewModelBase
{
    private readonly SkinManagerService _manager;
    private readonly WidgetVibeGenerator _generator = new();
    private readonly ILogger<WidgetGeneratorViewModel> _logger;

    // ── Prompt text ───────────────────────────────────────────────────────────
    private string _promptText = "";
    public string PromptText
    {
        get => _promptText;
        set
        {
            if (SetProperty(ref _promptText, value))
                OnPropertyChanged(nameof(CanGenerate));
        }
    }

    public bool CanGenerate => !string.IsNullOrWhiteSpace(PromptText) && !IsBusy;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(CanGenerate)); }
    }

    private bool _hasResult;
    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    private SkinDefinition? _generatedSkin;
    public SkinDefinition? GeneratedSkin
    {
        get => _generatedSkin;
        set => SetProperty(ref _generatedSkin, value);
    }

    private WidgetAnalysisResult? _analysis;
    public WidgetAnalysisResult? Analysis
    {
        get => _analysis;
        set
        {
            if (!SetProperty(ref _analysis, value)) return;
            OnPropertyChanged(nameof(MatchedWordsDisplay));
            OnPropertyChanged(nameof(HasFuzzyCorrections));
            OnPropertyChanged(nameof(FuzzyDisplay));
            OnPropertyChanged(nameof(MeasuresDisplay));
            OnPropertyChanged(nameof(UsedFallbackDisplay));
        }
    }

    // ── Display strings for the insights panel ──────────────────────────────
    public string MatchedWordsDisplay => Analysis is null || Analysis.MatchedKeywords.Count == 0
        ? "—" : string.Join(", ", Analysis.MatchedKeywords);

    public bool HasFuzzyCorrections => Analysis?.FuzzyCorrections.Count > 0;
    public string FuzzyDisplay => HasFuzzyCorrections
        ? string.Join(", ", Analysis!.FuzzyCorrections.Select(kv => $"{kv.Key} → {kv.Value}"))
        : "—";

    public string MeasuresDisplay => Analysis is null || Analysis.Measures.Count == 0
        ? "—" : string.Join(", ", Analysis.Measures);

    public string UsedFallbackDisplay => Analysis?.UsedFallback == true
        ? "Couldn't find any specific measures in that text, so this defaults to a simple clock — add words like \"cpu\", \"battery\", or \"network\" to get something more specific."
        : "";

    // ── Suggestion chips ──────────────────────────────────────────────────────
    public ObservableCollection<string> SuggestionChips { get; } = new()
    {
        "a big CPU and memory graph in the top right",
        "minimal clock",
        "compact battery and network bar",
        "huge uptime tracker",
        "disk space in the bottom left",
        "cpu memory and disk monitor",
    };

    // ── Status message ────────────────────────────────────────────────────────
    private string _status = "Describe the widget you want — which stats, how big, roughly where.";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public WidgetGeneratorViewModel(SkinManagerService manager)
    {
        _manager = manager;
        _logger = App.LoggerFactory.CreateLogger<WidgetGeneratorViewModel>();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>
    /// Work-area size (in DIPs) of the monitor the main window is currently on, so a prompt like
    /// "top right" lands on the screen the user is actually looking at instead of an assumed
    /// 1920x1080. Same DPI-conversion approach as SkinHostWindow's _scaleFactor. Returns null on
    /// any failure — WidgetVibeGenerator falls back to its own default in that case, so a bad
    /// lookup here can never block generation, only make positioning less precise.
    /// </summary>
    private static (double Width, double Height)? GetScreenSizeDips()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea is null) return null;

            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0) scale = 1.0;

            return (displayArea.WorkArea.Width / scale, displayArea.WorkArea.Height / scale);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Sets the prompt text from a suggestion chip and generates immediately — same UX as VibeGeneratorViewModel.UseChipAsync.</summary>
    public async Task UseChipAsync(string chip)
    {
        _logger.LogInformation("User selected widget suggestion chip: {Chip}", chip);
        PromptText = chip;
        await GenerateAsync();
    }

    /// <summary>Runs the offline NLP pipeline on the current PromptText. Uses Task.Run so lexicon
    /// traversal doesn't block the UI thread, same as VibeGeneratorViewModel.GenerateAsync.</summary>
    public async Task GenerateAsync()
    {
        if (!CanGenerate) return;

        IsBusy = true;
        Status = "Reading your description…";
        _logger.LogInformation("Generating/Refining widget for text: {PromptText}", PromptText);

        try
        {
            var text = PromptText.Trim();

            // Phase 5: Conversational Refinement
            var promptWords = VibeTokenizer.TokenizeFull(text).Raw;
            bool isRefinement = promptWords.Any(w => w is
                "bigger" or "larger" or "large" or
                "smaller" or "small" or "tiny" or
                "remove" or "delete" or "hide");
            
            if (HasResult && GeneratedSkin != null && isRefinement)
            {
                var skin = await Task.Run(() => _generator.Refine(GeneratedSkin, text));
                // Force a property change notification
                var temp = GeneratedSkin;
                GeneratedSkin = null;
                GeneratedSkin = skin;
                
                Status = $"Refined \"{skin.Name}\" based on conversational input.";
                _logger.LogInformation("Widget conversational refinement succeeded. Refined skin: {SkinName}", skin.Name);
            }
            else
            {
                // A previous result that was generated but never accepted is a real, if soft,
                // negative signal — the user saw it and moved on. Worth recording before it's
                // overwritten and lost.
                if (HasResult && GeneratedSkin != null)
                {
                    App.Personalization.SubmitFeedback(new FeedbackAction
                    {
                        ItemId = GeneratedSkin.Id,
                        IsWidget = true,
                        Type = FeedbackType.ImplicitDismissed,
                        WidgetMeasures = GeneratedSkin.Measures.Select(m => m.Type).ToList(),
                    });
                }

                HasResult = false;
                var screen = GetScreenSizeDips();
                var context = new GenerationContext { Prompt = text };
                var candidate = await Task.Run(() =>
                    App.Personalization.GenerateBestWidget(context, screen?.Width, screen?.Height));

                // Autofill weather target from settings if available
                var schedule = App.Settings?.Schedule;
                string? weatherCity = schedule?.WeatherUseDynamicLocation == true ? "AUTO" : schedule?.WeatherCity;
                if (!string.IsNullOrWhiteSpace(weatherCity) && !string.IsNullOrWhiteSpace(schedule?.WeatherApiKey))
                {
                    foreach (var measure in candidate.Skin.Measures)
                    {
                        if ((measure.Type == MeasureType.WeatherTemp || measure.Type == MeasureType.WeatherDesc || measure.Type == MeasureType.WeatherCity) && string.IsNullOrEmpty(measure.Target))
                        {
                            measure.Target = $"{weatherCity}|{schedule!.WeatherApiKey}";
                        }
                    }
                }

                Analysis = candidate.Analysis;
                GeneratedSkin = candidate.Skin;
                HasResult = true;

                Status = candidate.Analysis?.UsedFallback == true
                    ? $"Generated \"{candidate.Skin.Name}\" — no specific measures detected, so this is a simple starting point."
                    : $"Generated \"{candidate.Skin.Name}\" from {candidate.Analysis?.MatchedKeywords.Count ?? 0} matched word(s).";
                _logger.LogInformation("Widget generation succeeded via personalization pipeline. Generated skin: {SkinName}, Source: {Source}", candidate.Skin.Name, candidate.GenerationSource);
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            _logger.LogError(ex, "Failed to generate or refine widget.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Adds the generated widget to the list and returns it, ready to hand to the editor.</summary>
    public async Task<SkinDefinition?> AcceptAndOpenEditorAsync()
    {
        if (GeneratedSkin is null) return null;
        _logger.LogInformation("User opening generated skin in editor: {SkinName}", GeneratedSkin.Name);

        App.Personalization.SubmitFeedback(new FeedbackAction
        {
            ItemId = GeneratedSkin.Id,
            IsWidget = true,
            Type = FeedbackType.ImplicitApplied,
            WidgetMeasures = GeneratedSkin.Measures.Select(m => m.Type).ToList(),
        });

        await _manager.AddGeneratedSkinAsync(GeneratedSkin);
        return GeneratedSkin;
    }
}
