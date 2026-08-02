using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Services;
using ThemeManager.WinUI.ViewModels;
using ThemeManager.WinUI.Views;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Central service for skin (desktop widget) state management — the counterpart to
/// <see cref="ThemeService"/>, but living in the WinUI project because it has to create
/// real <see cref="SkinHostWindow"/> instances, which <c>ThemeManager.Core</c> can't reference.
///
/// - Holds the in-memory skin list and delegates persistence to <see cref="SkinRepository"/>.
/// - Owns one <see cref="SkinHostWindow"/> per *enabled* skin.
/// - Runs a single shared 1-second timer that ticks every open widget (see the class remarks
///   on <see cref="SkinDefinition.UpdateIntervalMs"/> for why this is intentionally simple for now).
/// </summary>
public sealed class SkinManagerService : IDisposable
{
    private readonly SkinRepository _repo;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;

    private List<SkinDefinition> _skins = new();
    public IReadOnlyList<SkinDefinition> Skins => _skins;

    /// <summary>Fired whenever a skin is added, removed, or has a setting changed.</summary>
    public event EventHandler? SkinsChanged;

    private readonly Dictionary<string, (SkinHostWindow Window, SkinHostViewModel ViewModel)> _open = new();
    private DispatcherQueueTimer? _timer;

    public SkinManagerService(SkinRepository repository, ILoggerFactory? loggerFactory = null)
    {
        _repo = repository;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<SkinManagerService>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SkinManagerService>.Instance;
    }

    /// <summary>Must be called once on startup, after the main window exists (needs a DispatcherQueue).</summary>
    public async Task InitializeAsync()
    {
        _skins = await _repo.LoadAllAsync();

        foreach (var skin in _skins.Where(s => s.Enabled))
            OpenWindowFor(skin);

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000);
        _timer.Tick += (_, _) => TickAll();
        _timer.Start();
    }

    private void TickAll()
    {
        foreach (var (_, viewModel) in _open.Values)
        {
            try { viewModel.Tick(); }
            catch (Exception ex) { _logger.LogWarning(ex, "A widget failed to refresh this tick; skipping it until next tick"); }
        }
    }

    // ── Per-skin settings (each mutates in-memory, applies live, then persists) ──────

    public async Task SetEnabledAsync(SkinDefinition skin, bool enabled)
    {
        skin.Enabled = enabled;

        if (enabled) OpenWindowFor(skin);
        else CloseWindowFor(skin);

        await PersistAsync();
    }

    public async Task SetOpacityAsync(SkinDefinition skin, double opacity)
    {
        skin.Opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyOpacity(skin.Opacity);
        await PersistAsync();
    }

    public async Task SetClickThroughAsync(SkinDefinition skin, bool enabled)
    {
        skin.ClickThrough = enabled;
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyClickThrough(skin.ClickThrough);
        await PersistAsync();
    }

    public async Task SetLockedAsync(SkinDefinition skin, bool locked)
    {
        skin.Locked = locked;
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyLocked(skin.Locked);
        await PersistAsync();
    }

    public async Task ResetPositionAsync(SkinDefinition skin)
    {
        skin.X = 60;
        skin.Y = 60;
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyPosition(skin.X, skin.Y);
        await PersistAsync();
    }

    /// <summary>Called by a <see cref="SkinHostWindow"/> when the user finishes dragging it.</summary>
    private async void OnWindowMoved(SkinDefinition skin, double x, double y)
    {
        skin.X = x;
        skin.Y = y;
        await PersistAsync();
    }

    // ── Editor support (create / save / delete a whole widget) ──────────────────────

    /// <summary>
    /// Creates a small blank widget, adds it to the list, and persists it immediately —
    /// mirrors <c>ThemesViewModel.CreateThemeAsync</c>'s "create now, refine in the editor"
    /// pattern, so there's never a half-created widget floating around unsaved.
    /// Starts disabled: an empty widget with zero meters has nothing worth showing yet.
    /// </summary>
    public async Task<SkinDefinition> CreateNewSkinAsync()
    {
        var skin = new SkinDefinition
        {
            Name = "New Widget",
            Enabled = false,
            X = 60,
            Y = 60,
            Width = 200,
            Height = 100,
        };

        _skins.Add(skin);
        await PersistAsync();
        return skin;
    }

    /// <summary>
    /// Call after editing a skin's meters/measures/name/size in place. The editor mutates the
    /// same <see cref="SkinDefinition"/> instance that's already in <see cref="Skins"/> (same
    /// approach <c>ThemeEditorViewModel</c> uses for themes), so this just needs to persist and,
    /// if the widget is currently showing, rebuild its window so the new layout actually appears —
    /// simpler and far more robust than trying to hot-patch a running window's meter list.
    /// </summary>
    public async Task SaveSkinAsync(SkinDefinition skin)
    {
        if (_open.ContainsKey(skin.Id))
        {
            CloseWindowFor(skin);
            if (skin.Enabled) OpenWindowFor(skin);
        }
        else if (skin.Enabled)
        {
            OpenWindowFor(skin);
        }

        await PersistAsync();
    }

    /// <summary>Removes a widget entirely — closes its window first if it's open.</summary>
    public async Task DeleteSkinAsync(SkinDefinition skin)
    {
        CloseWindowFor(skin);
        _skins.RemoveAll(s => s.Id == skin.Id);
        await PersistAsync();
    }

    // ── Window lifecycle ──────────────────────────────────────────────────────────

    private void OpenWindowFor(SkinDefinition skin)
    {
        if (_open.ContainsKey(skin.Id)) return; // already open

        var viewModel = new SkinHostViewModel(skin, _loggerFactory?.CreateLogger<SkinHostViewModel>());
        var window = new SkinHostWindow(viewModel);

        window.PositionChanged += (x, y) => OnWindowMoved(skin, x, y);
        window.Closed += (_, _) =>
        {
            // Covers the unlikely case the OS closes it for us (e.g. explorer restart) —
            // keep in-memory state consistent rather than pointing at a dead window.
            _open.Remove(skin.Id);
        };

        _open[skin.Id] = (window, viewModel);
        window.Activate();
        viewModel.Tick(); // paint real values immediately instead of waiting up to 1s for the first tick
    }

    private void CloseWindowFor(SkinDefinition skin)
    {
        if (!_open.TryGetValue(skin.Id, out var entry)) return;
        _open.Remove(skin.Id);
        entry.Window.Close();
    }

    private async Task PersistAsync()
    {
        await _repo.SaveAllAsync(_skins);
        SkinsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer?.Stop();
        foreach (var (window, _) in _open.Values)
            window.Close();
        _open.Clear();
    }
}
