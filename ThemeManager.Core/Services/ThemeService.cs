using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>
/// Central service for theme state management.
/// - Holds the active <see cref="CozyTheme"/> and raises <see cref="PropertyChanged"/> on change.
/// - Exposes an event so the WinUI layer can update its ResourceDictionary.
/// - Owns the in-memory theme list and delegates persistence to <see cref="ThemeRepository"/>.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    private readonly ThemeRepository _repo;

    // ── Observable list of all themes ────────────────────────────────────────
    private List<CozyTheme> _themes = new();
    public IReadOnlyList<CozyTheme> Themes => _themes;

    // ── Active theme ─────────────────────────────────────────────────────────
    private CozyTheme _activeTheme = CozyDefaults.CreateDefault();
    public CozyTheme ActiveTheme
    {
        get => _activeTheme;
        private set
        {
            if (_activeTheme == value) return;
            _activeTheme = value;
            OnPropertyChanged();
            ThemeChanged?.Invoke(this, value);
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired whenever the active theme changes (palette or geometry).</summary>
    public event EventHandler<CozyTheme>? ThemeChanged;

    /// <summary>Fired whenever the theme list is mutated (add/remove/rename).</summary>
    public event EventHandler? ThemeListChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Constructor ───────────────────────────────────────────────────────────
    public ThemeService(ThemeRepository repository)
    {
        _repo = repository;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>Must be called once on startup (await before showing UI).</summary>
    public async Task InitializeAsync()
    {
        _themes = await _repo.LoadAllAsync();

        // Default to Cozy Café on first run.
        var defaultTheme = _themes.Find(t => t.Id == "cozy-default")
                           ?? _themes.FirstOrDefault()
                           ?? CozyDefaults.CreateDefault();

        // Don't use the property setter here to avoid firing events before UI is ready.
        _activeTheme = defaultTheme;
    }

    // ── Active theme management ───────────────────────────────────────────────

    /// <summary>Sets the active theme and fires <see cref="ThemeChanged"/>.</summary>
    public void SetActiveTheme(CozyTheme theme)
    {
        ActiveTheme = theme;
    }

    /// <summary>
    /// Applies a live palette token change to the active theme and fires <see cref="ThemeChanged"/>.
    /// Useful for real-time color picker updates without saving.
    /// </summary>
    public void NotifyThemeTokenChanged()
    {
        ThemeChanged?.Invoke(this, _activeTheme);
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<CozyTheme> CreateThemeAsync(string name)
    {
        var theme = CozyDefaults.CreateDefault();
        theme.Id       = Guid.NewGuid().ToString();
        theme.Name     = name;
        theme.IsBuiltIn = false;
        _themes.Add(theme);
        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
        return theme;
    }

    public async Task<CozyTheme> DuplicateThemeAsync(CozyTheme source)
    {
        var clone = source.Duplicate();
        _themes.Add(clone);
        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
        return clone;
    }

    public async Task SaveThemeAsync(CozyTheme theme)
    {
        theme.LastModified = DateTimeOffset.UtcNow;

        if (!_themes.Contains(theme))
            _themes.Add(theme);

        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);

        // If we just saved the active theme, re-broadcast so the UI re-applies.
        if (theme.Id == _activeTheme.Id)
            ThemeChanged?.Invoke(this, theme);
    }

    public async Task DeleteThemeAsync(CozyTheme theme)
    {
        if (theme.IsBuiltIn) return; // Never delete the built-in theme.
        _themes.Remove(theme);

        // Fall back to Cozy Café if the active theme was deleted.
        if (_activeTheme.Id == theme.Id)
            SetActiveTheme(_themes.First());

        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Import / Export ───────────────────────────────────────────────────────

    public async Task ExportThemeAsync(CozyTheme theme, string filePath)
        => await _repo.ExportThemeAsync(theme, filePath);

    public async Task<CozyTheme?> ImportThemeAsync(string filePath)
    {
        var theme = await _repo.ImportThemeAsync(filePath);
        if (theme is null) return null;
        _themes.Add(theme);
        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
        return theme;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task PersistAsync() => _repo.SaveAllAsync(_themes);

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
