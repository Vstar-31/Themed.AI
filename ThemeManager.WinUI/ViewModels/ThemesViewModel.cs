using System.Collections.ObjectModel;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed class ThemesViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;

    // ── Observable theme list ─────────────────────────────────────────────────
    public ObservableCollection<CozyTheme> Themes { get; } = new();

    private CozyTheme? _selectedTheme;
    public CozyTheme? SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    // ── Status/feedback ───────────────────────────────────────────────────────
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ThemesViewModel(ThemeService themeService)
    {
        _themeService = themeService;
        _themeService.ThemeListChanged += (_, _) => RefreshList();
        RefreshList();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task CreateThemeAsync()
    {
        var theme = await _themeService.CreateThemeAsync("New Theme");
        SelectedTheme = theme;
        StatusMessage = $"Created "{theme.Name}".";
    }

    public async Task DuplicateThemeAsync(CozyTheme theme)
    {
        var clone = await _themeService.DuplicateThemeAsync(theme);
        SelectedTheme = clone;
        StatusMessage = $"Duplicated as "{clone.Name}".";
    }

    public async Task DeleteThemeAsync(CozyTheme theme)
    {
        if (theme.IsBuiltIn)
        {
            StatusMessage = "Cannot delete the built-in Cozy Café theme.";
            return;
        }
        await _themeService.DeleteThemeAsync(theme);
        StatusMessage = $"Deleted "{theme.Name}".";
    }

    public void SetAsActive(CozyTheme theme)
    {
        _themeService.SetActiveTheme(theme);
        StatusMessage = $""{theme.Name}" is now active.";
    }

    public CozyTheme? GetActiveTheme() => _themeService.ActiveTheme;

    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        Themes.Clear();
        foreach (var t in _themeService.Themes)
            Themes.Add(t);
    }
}
