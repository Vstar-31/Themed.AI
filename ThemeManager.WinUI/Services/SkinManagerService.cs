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
    private bool _widgetsHidden;

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

        bool changed = false;
        foreach (var skin in _skins.Where(s => s.Name.StartsWith("VibeFinder")))
        {
            foreach (var meter in skin.Meters.Where(m => m.Kind == MeterKind.String && 
                (m.MeasureName == "VibeTitle" || m.MeasureName == "VibeArtist" || m.MeasureName == "VibeMood")))
            {
                if (string.IsNullOrEmpty(meter.Format) || meter.Format == "{0:F0}")
                {
                    meter.Format = "{1}";
                    changed = true;
                }
            }
        }
        if (changed) await PersistAsync(false);

        foreach (var skin in _skins.Where(s => s.Enabled))
            OpenWindowFor(skin);

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000);
        _timer.Tick += (_, _) => TickAll();
        _timer.Start();
    }

    private void TickAll()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        var snapshot = _open.Values.ToList();
        foreach (var (_, viewModel) in snapshot)
        {
            if (viewModel.IsClosed) continue;
            Task.Run(() =>
            {
                if (viewModel.IsClosed) return;
                try { viewModel.RefreshMeasures(); }
                catch (Exception ex) { _logger.LogWarning(ex, "A widget failed to refresh its measures this tick"); }
                
                dispatcher.TryEnqueue(() =>
                {
                    if (viewModel.IsClosed) return;
                    try { viewModel.UpdateMeters(); }
                    catch (Exception ex) { _logger.LogWarning(ex, "A widget failed to update its meters this tick"); }
                });
            });
        }
    }

    // ── Per-skin settings (each mutates in-memory, applies live, then persists) ──────

    public async Task SetEnabledAsync(SkinDefinition skin, bool enabled)
    {
        skin.Enabled = enabled;

        if (enabled) OpenWindowFor(skin);
        else await CloseWindowFor(skin);

        await PersistAsync(false);
    }

    public async Task SetOpacityAsync(SkinDefinition skin, double opacity)
    {
        skin.Opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyOpacity(skin.Opacity);
        await PersistAsync(false);
    }

    public async Task SetClickThroughAsync(SkinDefinition skin, bool enabled)
    {
        skin.ClickThrough = enabled;
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyClickThrough(skin.ClickThrough);
        await PersistAsync(false);
    }

    public async Task SetLockedAsync(SkinDefinition skin, bool locked)
    {
        skin.Locked = locked;
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyLocked(skin.Locked);
        await PersistAsync(false);
    }

    /// <summary>
    /// Experimental — see <c>DesktopLayerInterop</c>. Returns whether attachment actually
    /// succeeded so the UI can say so; <see cref="SkinDefinition.DesktopLayer"/> still gets set
    /// to the requested value either way (it's what gets *retried* on next launch), but a
    /// failed attach leaves the widget visibly in its normal always-on-top mode regardless of
    /// what the toggle says.
    /// </summary>
    public async Task<bool> SetDesktopLayerAsync(SkinDefinition skin, bool enabled)
    {
        skin.DesktopLayer = enabled;
        bool succeeded = true;

        if (_open.TryGetValue(skin.Id, out var entry))
            succeeded = await entry.Window.ApplyDesktopLayerAsync(enabled);

        await PersistAsync(false);
        return succeeded;
    }

    public async Task ResetPositionAsync(SkinDefinition skin)
    {
        skin.X = 60;
        skin.Y = 60;
        if (_open.TryGetValue(skin.Id, out var entry))
            entry.Window.ApplyPosition(skin.X, skin.Y);
        await PersistAsync(false);
    }

    private async void OnWindowMoved(SkinDefinition skin, double x, double y)
    {
        skin.X = x;
        skin.Y = y;
        await PersistAsync(false);
    }

    public void ToggleAllWidgetsVisibility()
    {
        _widgetsHidden = !_widgetsHidden;
        foreach (var (window, _) in _open.Values)
        {
            if (_widgetsHidden)
                window.AppWindow.Hide();
            else
                window.AppWindow.Show();
        }
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
        await PersistAsync(true);
        return skin;
    }

    /// <summary>
    /// Same "create now, refine in the editor" pattern as <see cref="CreateNewSkinAsync"/>, but
    /// for a fully-built <see cref="SkinDefinition"/> — used by the prompt-based widget
    /// generator, which already knows the measures/meters/layout and just needs it added to the
    /// list and persisted before handing off to the editor for final customization.
    /// </summary>
    public async Task AddGeneratedSkinAsync(SkinDefinition skin)
    {
        _skins.Add(skin);
        await PersistAsync(true);
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
            await CloseWindowFor(skin);
            if (skin.Enabled) OpenWindowFor(skin);
        }
        else if (skin.Enabled)
        {
            OpenWindowFor(skin);
        }

        await PersistAsync(true);
    }

    public async Task DeleteSkinAsync(SkinDefinition skin)
    {
        await CloseWindowFor(skin);
        _skins.RemoveAll(s => s.Id == skin.Id);
        await PersistAsync(true);
    }

    // ── Window lifecycle ──────────────────────────────────────────────────────────

    private void OpenWindowFor(SkinDefinition skin)
    {
        if (_open.ContainsKey(skin.Id)) return; // already open

        // App.ThemeService (same static-service pattern already used for App.MainWindow below)
        // is how a VibeFinderAI measure targeting "$theme" resolves the active theme — see
        // SkinHostViewModel's activeThemeProvider param and phases.md, Phase 6.
        var viewModel = new SkinHostViewModel(skin, _loggerFactory?.CreateLogger<SkinHostViewModel>(), App.ThemeService);
        var window = new SkinHostWindow(viewModel);

        window.PositionChanged += (x, y) => OnWindowMoved(skin, x, y);
        window.EditRequested += () => App.MainWindow.NavigateToSkinEditor(skin);
        window.LockToggleRequested += () => _ = SetLockedAsync(skin, !skin.Locked);
        window.ResetPositionRequested += () => _ = ResetPositionAsync(skin);
        window.DisableRequested += () => _ = SetEnabledAsync(skin, false);
        window.Closed += (_, _) =>
        {
            // Covers the unlikely case the OS closes it for us (e.g. explorer restart) —
            // keep in-memory state consistent rather than pointing at a dead window.
            _open.Remove(skin.Id);
        };

        _open[skin.Id] = (window, viewModel);
        window.Activate();
        if (_widgetsHidden) window.AppWindow.Hide(); // stay consistent with a global hide from the hotkey
        viewModel.RefreshMeasures(); // safe to call synchronously on first open since there's no data yet
        viewModel.UpdateMeters(); // paint real values immediately instead of waiting up to 1s for the first tick
    }

    private async Task CloseWindowFor(SkinDefinition skin)
    {
        if (!_open.TryGetValue(skin.Id, out var entry)) return;
        _open.Remove(skin.Id);
        entry.ViewModel.IsClosed = true;
        entry.Window.PrepareForClose();

        // Yield to the WinUI compositor so it can process the reparenting/backdrop changes
        // before we destroy the HWND. Without this, WinUI throws STATUS_STOWED_EXCEPTION.
        await Task.Delay(150);

        try
        {
            entry.Window.Close();
        }
        catch (Exception ex)
        {
            // Window.Close() is a WinRT-projected call, and PrepareForClose() above already
            // catches everything it does — so if native window teardown still throws here
            // despite that plus the delay, it's an OS/compositor-level failure one widget
            // hit, not something the rest of the app should die for. Log and move on.
            _logger.LogWarning(ex, "Widget window failed to close cleanly for skin {SkinId} ({SkinName})", skin.Id, skin.Name);
        }
    }

    private async Task PersistAsync(bool notifyListChanged)
    {
        try
        {
            await _repo.SaveAllAsync(_skins);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to persist skins.json (file locked). It will be saved on the next edit.");
            SaveFailed?.Invoke(this, "Widget changes couldn't be saved (file was locked) — they'll be saved with the next edit.");
        }
        
        if (notifyListChanged)
            SkinsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when a save fails so the UI can show a transient warning.</summary>
    public event EventHandler<string>? SaveFailed;

    public void Dispose()
    {
        _timer?.Stop();
        foreach (var (window, _) in _open.Values)
        {
            window.PrepareForClose();
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A widget window failed to close cleanly during shutdown");
            }
        }
        _open.Clear();
    }

    public void EnsureVibeFinderSkinsExist()
    {
        bool changed = false;

        if (!_skins.Any(s => s.Name == "VibeFinder Primary"))
        {
            var primary = new SkinDefinition { Id = Guid.NewGuid().ToString(), Name = "VibeFinder Primary", Enabled = true, X = 50, Y = 50, Width = 300, Height = 120 };
            primary.Measures.Add(new MeasureDefinition { Name = "VibeTitle", Type = MeasureType.VibeTrackTitle, Target = "listener|hunter2|$theme" });
            primary.Measures.Add(new MeasureDefinition { Name = "VibeArtist", Type = MeasureType.VibeTrackArtist, Target = "listener|hunter2|$theme" });
            
            primary.Meters.Add(new MeterDefinition { Kind = MeterKind.Icon, MeasureName = "VibeTitle", X = 10, Y = 10, Width = 100, Height = 100 });
            primary.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "VibeTitle", X = 120, Y = 15, Width = 170, Height = 30, FontSize = 16, Bold = true });
            primary.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "VibeArtist", X = 120, Y = 45, Width = 170, Height = 25, FontSize = 14 });
            primary.Meters.Add(new MeterDefinition { Kind = MeterKind.Icon, StaticText = "⏯", ActionUrl = "themed://media/playpause", X = 120, Y = 75, Width = 30, Height = 30, FontSize = 16, IconGlyph = "\uE768" });
            primary.Meters.Add(new MeterDefinition { Kind = MeterKind.Icon, StaticText = "⏩", ActionUrl = "themed://media/next", X = 160, Y = 75, Width = 30, Height = 30, FontSize = 16, IconGlyph = "\uE893" });
            
            _skins.Add(primary);
            changed = true;
        }

        if (!_skins.Any(s => s.Name == "VibeFinder Minimal"))
        {
            var minimal = new SkinDefinition { Id = Guid.NewGuid().ToString(), Name = "VibeFinder Minimal", Enabled = false, X = 50, Y = 200, Width = 200, Height = 60 };
            minimal.Measures.Add(new MeasureDefinition { Name = "VibeTitle", Type = MeasureType.VibeTrackTitle, Target = "listener|hunter2|$theme" });
            
            minimal.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "VibeTitle", X = 10, Y = 10, Width = 140, Height = 40, FontSize = 14, Bold = true });
            minimal.Meters.Add(new MeterDefinition { Kind = MeterKind.Icon, StaticText = "⏯", ActionUrl = "themed://media/playpause", X = 160, Y = 15, Width = 30, Height = 30, FontSize = 14, IconGlyph = "\uE768" });
            
            _skins.Add(minimal);
            changed = true;
        }

        if (!_skins.Any(s => s.Name == "VibeFinder Playlist"))
        {
            var playlist = new SkinDefinition { Id = Guid.NewGuid().ToString(), Name = "VibeFinder Playlist", Enabled = false, X = 400, Y = 50, Width = 250, Height = 300 };
            playlist.Measures.Add(new MeasureDefinition { Name = "VibeMood", Type = MeasureType.VibeMood, Target = "listener|hunter2|$theme" });
            playlist.Measures.Add(new MeasureDefinition { Name = "VibeTitle", Type = MeasureType.VibeTrackTitle, Target = "listener|hunter2|$theme" });
            playlist.Measures.Add(new MeasureDefinition { Name = "VibeArtist", Type = MeasureType.VibeTrackArtist, Target = "listener|hunter2|$theme" });
            
            playlist.Meters.Add(new MeterDefinition { Kind = MeterKind.String, StaticText = "Now Playing from Vibe", X = 10, Y = 10, Width = 230, Height = 20, FontSize = 12 });
            playlist.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "VibeMood", X = 10, Y = 30, Width = 230, Height = 40, FontSize = 24, Bold = true });
            playlist.Meters.Add(new MeterDefinition { Kind = MeterKind.Icon, MeasureName = "VibeTitle", X = 10, Y = 80, Width = 230, Height = 150 });
            playlist.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "VibeTitle", X = 10, Y = 240, Width = 230, Height = 25, FontSize = 16, Bold = true });
            playlist.Meters.Add(new MeterDefinition { Kind = MeterKind.String, MeasureName = "VibeArtist", X = 10, Y = 265, Width = 230, Height = 20, FontSize = 14 });
            
            _skins.Add(playlist);
            changed = true;
        }

        foreach (var skin in _skins.Where(s => s.Name.StartsWith("VibeFinder")))
        {
            foreach (var meter in skin.Meters.Where(m => m.Kind == MeterKind.String && 
                (m.MeasureName == "VibeTitle" || m.MeasureName == "VibeArtist" || m.MeasureName == "VibeMood")))
            {
                if (string.IsNullOrEmpty(meter.Format) || meter.Format == "{0:F0}")
                {
                    meter.Format = "{1}";
                    changed = true;
                }
            }
        }

        if (changed)
        {
            _ = PersistAsync(true);
        }
    }

    public void UpdateVibeFinderCredentials(string target)
    {
        bool changed = false;
        foreach (var skin in _skins)
        {
            if (skin.Name.StartsWith("VibeFinder"))
            {
                foreach (var measure in skin.Measures)
                {
                    if (measure.Type == MeasureType.VibeTrackTitle || 
                        measure.Type == MeasureType.VibeTrackArtist || 
                        measure.Type == MeasureType.VibeMood)
                    {
                        measure.Target = target;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            foreach (var skin in _skins.Where(s => s.Name.StartsWith("VibeFinder")))
            {
                _ = SaveSkinAsync(skin);
            }
        }
    }
}
