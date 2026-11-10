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
    private readonly SemaphoreSlim _writeGate = new(1, 1);

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
        await AddThemeAsync(theme);
        return theme;
    }

    public async Task<CozyTheme> DuplicateThemeAsync(CozyTheme source)
    {
        var clone = source.Duplicate();
        await AddThemeAsync(clone);
        return clone;
    }

    private async Task AddThemeAsync(CozyTheme theme)
    {
        await _writeGate.WaitAsync();
        try
        {
            // Stage membership privately; publish it only after the atomic file save succeeds.
            var pending = new List<CozyTheme>(_themes) { theme };
            await _repo.SaveAllAsync(pending);
            _themes = pending;
        }
        finally { _writeGate.Release(); }
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveThemeAsync(CozyTheme theme)
    {
        await _writeGate.WaitAsync();
        try
        {
            theme.BackgroundBase = CozyTheme.NormalizeHex(theme.BackgroundBase);
            theme.BackgroundAlt  = CozyTheme.NormalizeHex(theme.BackgroundAlt);
            theme.Surface        = CozyTheme.NormalizeHex(theme.Surface);
            theme.AccentPrimary  = CozyTheme.NormalizeHex(theme.AccentPrimary);
            theme.AccentStrong   = CozyTheme.NormalizeHex(theme.AccentStrong);
            theme.TextPrimary    = CozyTheme.NormalizeHex(theme.TextPrimary);
            theme.TextMuted      = CozyTheme.NormalizeHex(theme.TextMuted);
            theme.BorderSubtle   = CozyTheme.NormalizeHex(theme.BorderSubtle);
            var previousModified = theme.LastModified;
            theme.LastModified = DateTimeOffset.UtcNow;

            var pending = new List<CozyTheme>(_themes);
            var index = pending.FindIndex(t => t.Id == theme.Id);
            if (index >= 0) pending[index] = theme; else pending.Add(theme);
            try { await _repo.SaveAllAsync(pending); }
            catch
            {
                theme.LastModified = previousModified;
                throw;
            }
            _themes = pending;

            if (theme.Id == _activeTheme.Id)
            {
                if (ReferenceEquals(theme, _activeTheme)) NotifyThemeTokenChanged();
                else SetActiveTheme(theme);
            }
        }
        finally { _writeGate.Release(); }
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteThemeAsync(CozyTheme theme)
    {
        await _writeGate.WaitAsync();
        try
        {
            var stored = _themes.FirstOrDefault(t => t.Id == theme.Id);
            if (stored is null || stored.IsBuiltIn) return;
            var pending = _themes.Where(t => t.Id != stored.Id).ToList();
            await _repo.SaveAllAsync(pending);
            _themes = pending;
            // A failed delete must neither remove the theme nor broadcast a fallback palette.
            if (_activeTheme.Id == stored.Id)
                SetActiveTheme(pending.FirstOrDefault() ?? CozyDefaults.CreateDefault());
        }
        finally { _writeGate.Release(); }
        ThemeListChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ExportThemeAsync(CozyTheme theme, string filePath)
        => await _repo.ExportThemeAsync(theme, filePath);

    public async Task<CozyTheme?> ImportThemeAsync(string filePath)
    {
        var theme = await _repo.ImportThemeAsync(filePath);
        if (theme is null) return null;
        await AddThemeAsync(theme);
        return theme;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
