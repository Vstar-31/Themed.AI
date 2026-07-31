using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Services;
using ThemeManager.Core.Utilities;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Phase 3 upgrades:
///   • Undo/redo (PaletteHistory, Ctrl+Z / Ctrl+Y)
///   • Harmony lock  — changing AccentPrimary re-derives the whole palette
///   • Live WCAG contrast results for every text/bg pair
/// </summary>
public sealed class ThemeEditorViewModel : ViewModelBase
{
    private readonly ThemeService    _themeService;
    private readonly PaletteHistory  _history = new();
    private CozyTheme _working = CozyDefaults.CreateDefault();
    private readonly EventHandler _historyChangedHandler;

    public ThemeEditorViewModel(ThemeService themeService)
    {
        _themeService = themeService;
        _historyChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoDepthLabel));
        };
        _history.HistoryChanged += _historyChangedHandler;
        LoadTheme(themeService.ActiveTheme);
    }

    public void Dispose()
    {
        _history.HistoryChanged -= _historyChangedHandler;
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) { _working.Name = value; Dirty = true; } }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set { if (SetProperty(ref _description, value)) { _working.Description = value; Dirty = true; } }
    }

    // ── Harmony lock ──────────────────────────────────────────────────────────

    private bool _harmonyLocked;
    public bool HarmonyLocked
    {
        get => _harmonyLocked;
        set => SetProperty(ref _harmonyLocked, value);
    }

    // ── Palette hex tokens ────────────────────────────────────────────────────

    private string _backgroundBase = CozyDefaults.Linen;
    public string BackgroundBase
    {
        get => _backgroundBase;
        set { if (SetProperty(ref _backgroundBase, value)) ApplyToken(t => t.BackgroundBase = value); }
    }

    private string _backgroundAlt = CozyDefaults.Khaki;
    public string BackgroundAlt
    {
        get => _backgroundAlt;
        set { if (SetProperty(ref _backgroundAlt, value)) ApplyToken(t => t.BackgroundAlt = value); }
    }

    private string _surface = CozyDefaults.Camel;
    public string Surface
    {
        get => _surface;
        set { if (SetProperty(ref _surface, value)) ApplyToken(t => t.Surface = value); }
    }

    private string _accentPrimary = CozyDefaults.Cocoa;
    public string AccentPrimary
    {
        get => _accentPrimary;
        set
        {
            if (!SetProperty(ref _accentPrimary, value)) return;
            if (_harmonyLocked)
                ApplyTokenWithHarmony(value);   // re-derive whole palette
            else
                ApplyToken(t => t.AccentPrimary = value);
        }
    }

    private string _accentStrong = CozyDefaults.Espresso;
    public string AccentStrong
    {
        get => _accentStrong;
        set { if (SetProperty(ref _accentStrong, value)) ApplyToken(t => t.AccentStrong = value); }
    }

    private string _textPrimary = "#3B2A20";
    public string TextPrimary
    {
        get => _textPrimary;
        set { if (SetProperty(ref _textPrimary, value)) ApplyToken(t => t.TextPrimary = value); }
    }

    private string _textMuted = "#7F7065";
    public string TextMuted
    {
        get => _textMuted;
        set { if (SetProperty(ref _textMuted, value)) ApplyToken(t => t.TextMuted = value); }
    }

    private string _borderSubtle = "#E0D5C7";
    public string BorderSubtle
    {
        get => _borderSubtle;
        set { if (SetProperty(ref _borderSubtle, value)) ApplyToken(t => t.BorderSubtle = value); }
    }

    // ── Scale sliders ─────────────────────────────────────────────────────────

    private double _cornerRadiusScale = 1.0;
    public double CornerRadiusScale
    {
        get => _cornerRadiusScale;
        set { if (SetProperty(ref _cornerRadiusScale, value)) ApplyToken(t => t.CornerRadiusScale = value); }
    }

    private double _densityScale = 1.0;
    public double DensityScale
    {
        get => _densityScale;
        set { if (SetProperty(ref _densityScale, value)) ApplyToken(t => t.DensityScale = value); }
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _dirty;
    public bool Dirty
    {
        get => _dirty;
        set => SetProperty(ref _dirty, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public string UndoDepthLabel => _history.CanUndo
        ? $"Undo ({_history.UndoDepth})"
        : "Undo";

    public void Undo()
    {
        var snap = _history.Undo(_working);
        if (snap is null) return;
        snap.ApplyTo(_working);
        SyncPropertiesFromWorking();
        _themeService.NotifyThemeTokenChanged();
        StatusMessage = "Undone.";
        Dirty = true;
    }

    public void Redo()
    {
        var snap = _history.Redo(_working);
        if (snap is null) return;
        snap.ApplyTo(_working);
        SyncPropertiesFromWorking();
        _themeService.NotifyThemeTokenChanged();
        StatusMessage = "Redone.";
        Dirty = true;
    }

    // ── Contrast results ──────────────────────────────────────────────────────

    private IReadOnlyList<ContrastChecker.ContrastResult> _contrastResults = Array.Empty<ContrastChecker.ContrastResult>();
    public IReadOnlyList<ContrastChecker.ContrastResult> ContrastResults
    {
        get => _contrastResults;
        private set { SetProperty(ref _contrastResults, value); OnPropertyChanged(nameof(ContrastSummary)); }
    }

    public string ContrastSummary
    {
        get
        {
            var (pass, total) = ContrastChecker.Summary(_working);
            return $"{pass}/{total} pairs pass WCAG AA";
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public void LoadTheme(CozyTheme theme)
    {
        _working = theme;
        _history.Clear();
        SyncPropertiesFromWorking();
        RefreshContrast();
        Dirty = false;
    }

    public async Task SaveAsync()
    {
        await _themeService.SaveThemeAsync(_working);
        Dirty = false;
        StatusMessage = $"\"{_working.Name}\" saved.";
    }

    public void RevertToDefault()
    {
        _history.Push(_working); // allow undo back past the revert
        _working.ResetToDefault();
        SyncPropertiesFromWorking();
        _themeService.NotifyThemeTokenChanged();
        RefreshContrast();
        StatusMessage = "Reverted to Cozy Café defaults.";
        Dirty = true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Standard single-token mutation. Pushes undo snapshot before applying.
    /// </summary>
    private void ApplyToken(Action<CozyTheme> mutate)
    {
        _history.Push(_working);
        mutate(_working);
        Dirty = true;
        RefreshContrast();
        _themeService.NotifyThemeTokenChanged();
    }

    /// <summary>
    /// Harmony lock path: extract HSL from the new accent, re-derive all 8 slots.
    /// Uses PaletteHarmonizer.FromAccentHex so the algorithm stays consistent.
    /// </summary>
    private void ApplyTokenWithHarmony(string newAccentHex)
    {
        _history.Push(_working);

        var palette = PaletteHarmonizer.FromAccentHex(newAccentHex);
        palette.ApplyTo(_working);

        // Sync all VM fields from the freshly derived palette.
        SyncPropertiesFromWorking();
        Dirty = true;
        RefreshContrast();
        _themeService.NotifyThemeTokenChanged();
    }

    private void SyncPropertiesFromWorking()
    {
        _name              = _working.Name;
        _description       = _working.Description;
        _backgroundBase    = _working.BackgroundBase;
        _backgroundAlt     = _working.BackgroundAlt;
        _surface           = _working.Surface;
        _accentPrimary     = _working.AccentPrimary;
        _accentStrong      = _working.AccentStrong;
        _textPrimary       = _working.TextPrimary;
        _textMuted         = _working.TextMuted;
        _borderSubtle      = _working.BorderSubtle;
        _cornerRadiusScale = _working.CornerRadiusScale;
        _densityScale      = _working.DensityScale;

        // Broadcast all changes in one shot.
        OnPropertyChanged(string.Empty);
    }

    private void RefreshContrast()
        => ContrastResults = ContrastChecker.Check(_working);
}
