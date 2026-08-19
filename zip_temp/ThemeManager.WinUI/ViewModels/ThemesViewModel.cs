using System.Collections.ObjectModel;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed class ThemesViewModel : ViewModelBase, IDisposable
{
    private readonly ThemeService _themeService;
    private readonly EventHandler _themeListChangedHandler;

    // ── Observable theme list ───────────────────────────────────────────────────
    public ObservableCollection<CozyTheme> Themes { get; } = new();

    private CozyTheme? _selectedTheme;
    public CozyTheme? SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    // ── Status/feedback ────────────────────────────────────────────────────────
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ThemesViewModel(ThemeService themeService)
    {
        _themeService = themeService;

        // Store the handler so it can be unsubscribed in Dispose().
        // Previously an inline lambda was used here — it could never be
        // removed, so every new ThemesPage (created on each navigation)
        // added another permanent ghost listener to ThemeListChanged.
        _themeListChangedHandler = (_, _) => RefreshList();
        _themeService.ThemeListChanged += _themeListChangedHandler;

        RefreshList();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public async Task CreateThemeAsync()
    {
        var theme = await _themeService.CreateThemeAsync("New Theme");
        SelectedTheme = theme;
        StatusMessage = $"Created \"{theme.Name}\".";
    }

    public async Task DuplicateThemeAsync(CozyTheme theme)
    {
        var clone = await _themeService.DuplicateThemeAsync(theme);
        SelectedTheme = clone;
        StatusMessage = $"Duplicated as \"{clone.Name}\".";
    }

    public async Task DeleteThemeAsync(CozyTheme theme)
    {
        if (theme.IsBuiltIn)
        {
            StatusMessage = "Cannot delete the built-in Cozy Café theme.";
            return;
        }
        await _themeService.DeleteThemeAsync(theme);
        StatusMessage = $"Deleted \"{theme.Name}\".";
    }

    public void SetAsActive(CozyTheme theme)
    {
        _themeService.SetActiveTheme(theme);
        StatusMessage = $"\"{theme.Name}\" is now active.";
    }

    public CozyTheme? GetActiveTheme() => _themeService.ActiveTheme;

    public void RefreshThemesList()
    {
        RefreshList();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _themeService.ThemeListChanged -= _themeListChangedHandler;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        var source = _themeService.Themes;
        for (int i = 0; i < source.Count; i++)
        {
            if (Themes.Count > i)
                Themes[i] = source[i];
            else
                Themes.Add(source[i]);
        }
        while (Themes.Count > source.Count)
            Themes.RemoveAt(Themes.Count - 1);
    }
}
