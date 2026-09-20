using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>
/// Central service for theme state management.
/// - Holds the active theme and raises <see cref="ThemeChanged"/> on change.
/// - Exposes an event so the WinUI layer can update its ResourceDictionary.
/// - Publishes the same change through <see cref="ThemeChangeHub"/> so platform integration
///   layers can synchronize without a dependency from Core on platform-specific code.
/// - Owns the in-memory theme list and delegates persistence to <see cref="ThemeRepository"/>.
/// - Implements <see cref="IActiveThemeProvider"/> so <c>ThemeManager.Integration</c> code can
///   read the active theme without depending on this CRUD/event/persistence surface.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged, IActiveThemeProvider
{
    private readonly ThemeRepository _repo;

    private List<CozyTheme> _themes = new();
    public IReadOnlyList<CozyTheme> Themes => _themes;

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
            ThemeChangeHub.Publish(value);
        }
    }

    public event EventHandler<CozyTheme>? ThemeChanged;
    public event EventHandler? ThemeListChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemeService(ThemeRepository repository)
    {
        _repo = repository;
    }

    public async Task InitializeAsync()
    {
        _themes = await _repo.LoadAllAsync();

        var defaultTheme = _themes.Find(t => t.Id == "cozy-default")
                        ?? _themes.FirstOrDefault()
                        ?? CozyDefaults.CreateDefault();

        _activeTheme = defaultTheme;
        ThemeChangeHub.Publish(_activeTheme);
    }

    /// <summary>Sets the active theme, normalizes all color fields, and broadcasts the change to
    /// both the WinUI resource pipeline and cross-layer platform integrations.</summary>
    public void SetActiveTheme(CozyTheme theme)
    {
        theme.BackgroundBase = CozyTheme.NormalizeHex(theme.BackgroundBase);
        theme.BackgroundAlt  = CozyTheme.NormalizeHex(theme.BackgroundAlt);
        theme.Surface        = CozyTheme.NormalizeHex(theme.Surface);
        theme.AccentPrimary  = CozyTheme.NormalizeHex(theme.AccentPrimary);
        theme.AccentStrong   = CozyTheme.NormalizeHex(theme.AccentStrong);
        theme.TextPrimary    = CozyTheme.NormalizeHex(theme.TextPrimary);
        theme.TextMuted      = CozyTheme.NormalizeHex(theme.TextMuted);
        theme.BorderSubtle   = CozyTheme.NormalizeHex(theme.BorderSubtle);

        ActiveTheme = theme;
    }

    /// <summary>Applies a live palette token change to the active theme and broadcasts it.</summary>
    public void NotifyThemeTokenChanged()
    {
        ThemeChanged?.Invoke(this, _activeTheme);
        ThemeChangeHub.Publish(_activeTheme);
    }

    public async Task<CozyTheme> CreateThemeAsync(string name)
    {
        var theme = CozyDefaults.CreateDefault();
        theme.Id = Guid.NewGuid().ToString();
        theme.Name = name;
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
        theme.BackgroundBase = CozyTheme.NormalizeHex(theme.BackgroundBase);
        theme.BackgroundAlt  = CozyTheme.NormalizeHex(theme.BackgroundAlt);
        theme.Surface        = CozyTheme.NormalizeHex(theme.Surface);
        theme.AccentPrimary  = CozyTheme.NormalizeHex(theme.AccentPrimary);
        theme.AccentStrong   = CozyTheme.NormalizeHex(theme.AccentStrong);
        theme.TextPrimary    = CozyTheme.NormalizeHex(theme.TextPrimary);
        theme.TextMuted      = CozyTheme.NormalizeHex(theme.TextMuted);
        theme.BorderSubtle   = CozyTheme.NormalizeHex(theme.BorderSubtle);
        theme.LastModified = DateTimeOffset.UtcNow;

        if (!_themes.Contains(theme))
            _themes.Add(theme);

        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);

        if (theme.Id == _activeTheme.Id)
        {
            ThemeChanged?.Invoke(this, theme);
            ThemeChangeHub.Publish(theme);
        }
    }

    public async Task DeleteThemeAsync(CozyTheme theme)
    {
        if (theme.IsBuiltIn) return;
        _themes.Remove(theme);

        if (_activeTheme.Id == theme.Id)
            SetActiveTheme(_themes.First());

        await PersistAsync();
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
    }

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

    private Task PersistAsync() => _repo.SaveAllAsync(_themes);

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
