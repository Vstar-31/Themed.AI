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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref NativeMonitorInfo info);

    /// <summary>
    /// Reads the work area for the monitor containing the main window using Win32 monitor APIs.
    /// This avoids the WinRT DisplayArea projection in a path that runs immediately before
    /// generation; failures degrade to the generator's conservative default positioning.
    /// </summary>
    private (double Width, double Height)? GetScreenSizeDips()
    {
        try
        {
            var mainWindow = App.MainWindow;
            if (mainWindow is null) return null;

            var hwnd = WindowNative.GetWindowHandle(mainWindow);
            if (hwnd == IntPtr.Zero) return null;

            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return null;

            var info = new NativeMonitorInfo { Size = Marshal.SizeOf<NativeMonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return null;

            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (!double.IsFinite(scale) || scale <= 0) scale = 1.0;

            var width = (info.Work.Right - info.Work.Left) / scale;
            var height = (info.Work.Bottom - info.Work.Top) / scale;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                return null;

            _logger.LogDebug("Widget generation screen work area: {Width:0}×{Height:0} DIPs (scale {Scale:0.00})",
                width, height, scale);
            return (width, height);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read widget-generation work area; using default positioning");
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

        var stage = "prompt parsing";
        try
        {
            var text = PromptText.Trim();
            _logger.LogDebug("Widget generation stage: {Stage} (prompt length {Length})", stage, text.Length);

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
                stage = "monitor work-area lookup";
                var screen = GetScreenSizeDips();
                _logger.LogDebug("Widget generation work-area lookup completed (hasBounds={HasBounds})", screen is not null);

                stage = "candidate generation and ranking";
                var context = new GenerationContext { Prompt = text };
                _logger.LogDebug("Widget generation candidate engine starting");
                var candidate = await Task.Run(() =>
                    App.Personalization.GenerateBestWidget(context, screen?.Width, screen?.Height));
                _logger.LogDebug(
                    "Widget candidate selected: {Name}; measures={MeasureCount}; meters={MeterCount}; size={Width:0.##}×{Height:0.##}; source={Source}",
                    candidate.Skin.Name, candidate.Skin.Measures.Count, candidate.Skin.Meters.Count,
                    candidate.Skin.Width, candidate.Skin.Height, candidate.GenerationSource);

                stage = "validating generated widget";
                SanitizeGeneratedSkin(candidate.Skin, screen);

                stage = "applying generation defaults";
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

                stage = "updating generator result bindings";
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
            Status = $"Widget generation failed during {stage}: {ex.Message}";
            _logger.LogError(ex, "Failed to generate or refine widget during stage {Stage}", stage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void SanitizeGeneratedSkin(SkinDefinition skin, (double Width, double Height)? screen)
    {
        static double FiniteClamp(double value, double min, double max, double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

        skin.Width = FiniteClamp(skin.Width, 80, 1200, 240);
        skin.Height = FiniteClamp(skin.Height, 60, 1200, 120);
        skin.X = FiniteClamp(skin.X, 0, Math.Max(0, (screen?.Width ?? 1920) - skin.Width), 40);
        skin.Y = FiniteClamp(skin.Y, 0, Math.Max(0, (screen?.Height ?? 1080) - skin.Height), 40);
        skin.Opacity = FiniteClamp(skin.Opacity, 0, 1, 0.5);

        foreach (var meter in skin.Meters)
        {
            meter.Width = FiniteClamp(meter.Width, 1, 800, 100);
            meter.Height = FiniteClamp(meter.Height, 1, 800, 20);
            meter.X = FiniteClamp(meter.X, 0, Math.Max(0, skin.Width - meter.Width), 0);
            meter.Y = FiniteClamp(meter.Y, 0, Math.Max(0, skin.Height - meter.Height), 0);
            meter.FontSize = FiniteClamp(meter.FontSize, 8, 72, 13);
        }

        if (string.IsNullOrWhiteSpace(skin.Name))
            skin.Name = "Custom Widget";
        if (string.IsNullOrWhiteSpace(skin.Id))
            skin.Id = Guid.NewGuid().ToString();
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
