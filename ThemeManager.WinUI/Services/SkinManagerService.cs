using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Services;
using ThemeManager.WinUI.ViewModels;
using ThemeManager.WinUI.Views;

namespace ThemeManager.WinUI.Services;

public sealed class SkinManagerService : IDisposable
{
    private readonly SkinRepository _repo;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;
    private readonly DispatcherQueue _dispatcher;
    private readonly WidgetPluginRegistry? _pluginRegistry;
    private List<SkinDefinition> _skins = new();
    public IReadOnlyList<SkinDefinition> Skins => _skins;
    public event EventHandler? SkinsChanged;
    private readonly Dictionary<string, (SkinHostWindow Window, SkinHostViewModel ViewModel)> _open = new();
    private DispatcherQueueTimer? _timer;
    private readonly Stopwatch _schedulerClock = Stopwatch.StartNew();
    private readonly WidgetTickScheduler _scheduler = new();
    private bool _widgetsHidden;
    private readonly SemaphoreSlim _windowMutationGate = new(1, 1);
    private const int SchedulerQuantumMs = 50;
    private static readonly IReadOnlyDictionary<string, SkinDefinition> CanonicalDefaults =
        SkinDefaults.CreateAllDefaults().ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

    public SkinManagerService(SkinRepository repository, ILoggerFactory? loggerFactory = null, WidgetPluginRegistry? pluginRegistry = null)
    {
        _repo = repository;
        _loggerFactory = loggerFactory;
        _pluginRegistry = pluginRegistry;
        _logger = loggerFactory?.CreateLogger<SkinManagerService>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SkinManagerService>.Instance;
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("SkinManagerService must be created on the WinUI dispatcher thread.");
    }

    public async Task InitializeAsync()
    {
        _skins = await _repo.LoadAllAsync();

        // Studio worlds own VibeFinder visibility. Resolve the persisted active world before
        // opening any windows so legacy global Enabled flags cannot briefly resurrect widgets
        // that are disabled in the current world.
        await App.SceneService.InitializeAsync();
        if (App.SceneService.ActiveScene is { } activeScene)
        {
            foreach (var vibeSkin in _skins.Where(IsVibeFinderSkin))
            {
                var placement = activeScene.Widgets.FirstOrDefault(p =>
                    p.WidgetId.Equals(vibeSkin.Id, StringComparison.OrdinalIgnoreCase));

                if (placement is not null)
                {
                    vibeSkin.X = placement.X;
                    vibeSkin.Y = placement.Y;
                    vibeSkin.Opacity = Math.Clamp(placement.Opacity, 0, 1);
                    vibeSkin.Enabled = placement.Visible;
                }
                else
                {
                    vibeSkin.Enabled = false;
                }
            }
        }

        bool changed = false;
        foreach (var skin in _skins.Where(s => s.Name.StartsWith("VibeFinder")))
        {
            int removed = skin.Meters.RemoveAll(m => m.Kind == MeterKind.WebEmbed);
            if (removed > 0) { if (skin.Name == "VibeFinder Playlist" && skin.Height > 400) skin.Height -= 230; changed = true; }
            foreach (var meter in skin.Meters.Where(m => m.Kind == MeterKind.String && (m.MeasureName == "VibeTitle" || m.MeasureName == "VibeArtist" || m.MeasureName == "VibeMood")))
                if (string.IsNullOrEmpty(meter.Format) || meter.Format == "{0:F0}") { meter.Format = "{1}"; changed = true; }
        }
        if (changed) await PersistAsync(false);
        foreach (var skin in _skins.Where(s => s.Enabled)) OpenWindowFor(skin);
        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(SchedulerQuantumMs);
        _timer.Tick += (_, _) => TickDueWidgets();
        _timer.Start();
    }

    private void TickDueWidgets()
    {
        var now = _schedulerClock.ElapsedMilliseconds;
        var dispatcher = _dispatcher;
        var snapshot = _open.ToList();
        var dueIds = _scheduler.GetDue(snapshot.Select(x => x.Value.ViewModel.Definition), now).Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (skinId, entry) in snapshot)
        {
            var viewModel = entry.ViewModel;
            if (viewModel.IsClosed || !dueIds.Contains(skinId)) continue;
            Task.Run(() =>
            {
                if (viewModel.IsClosed) return;
                try { viewModel.RefreshMeasures(); } catch (Exception ex) { _logger.LogWarning(ex, "Skin {SkinId} ({SkinName}): failed to refresh its measures", viewModel.Definition.Id, viewModel.Definition.Name); }
                dispatcher.TryEnqueue(() =>
                {
                    if (viewModel.IsClosed) return;
                    try { viewModel.UpdateMeters(); } catch (Exception ex) { _logger.LogWarning(ex, "Skin {SkinId} ({SkinName}): failed to update its meters", viewModel.Definition.Id, viewModel.Definition.Name); }
                });
            });
        }
    }

    public async Task SetEnabledAsync(SkinDefinition skin, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(skin);

        await _windowMutationGate.WaitAsync();
        try
        {
            if (skin.Enabled == enabled)
            {
                await SyncActiveScenePlacementAsync(skin);
                return;
            }

            skin.Enabled = enabled;
            if (enabled) OpenWindowFor(skin);
            else await CloseWindowFor(skin);

            await PersistAsync(true);
            await SyncActiveScenePlacementAsync(skin);
        }
        finally
        {
            _windowMutationGate.Release();
        }
    }

    public async Task SetOpacityAsync(SkinDefinition skin, double opacity)
    {
        skin.Opacity = Math.Clamp(opacity, 0.0, 1.0);
        if (_open.TryGetValue(skin.Id, out var entry)) entry.Window.ApplyOpacity(skin.Opacity);
        await PersistAsync(false);
    }

    public async Task SetClickThroughAsync(SkinDefinition skin, bool enabled)
    {
        skin.ClickThrough = enabled;
        if (_open.TryGetValue(skin.Id, out var entry)) entry.Window.ApplyClickThrough(skin.ClickThrough);
        await PersistAsync(false);
    }

    public async Task SetLockedAsync(SkinDefinition skin, bool locked)
    {
        skin.Locked = locked;
        if (_open.TryGetValue(skin.Id, out var entry)) entry.Window.ApplyLocked(skin.Locked);
        await PersistAsync(false);
    }

    public async Task SetAlwaysOnTopAsync(SkinDefinition skin, bool enabled)
    {
        skin.AlwaysOnTop = enabled;
        if (_open.TryGetValue(skin.Id, out var entry) && entry.Window.AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = enabled;
        await PersistAsync(true);
    }

    public async Task SetUpdateIntervalAsync(SkinDefinition skin, int intervalMs)
    {
        skin.UpdateIntervalMs = Math.Clamp(intervalMs, 50, 60_000);
        _scheduler.RunImmediately(skin.Id, _schedulerClock.ElapsedMilliseconds);
        await PersistAsync(false);
    }

    public async Task<bool> SetDesktopLayerAsync(SkinDefinition skin, bool enabled)
    {
        skin.DesktopLayer = enabled;
        bool succeeded = true;
        if (_open.TryGetValue(skin.Id, out var entry)) succeeded = await entry.Window.ApplyDesktopLayerAsync(enabled);
        await PersistAsync(false);
        return succeeded;
    }

    public async Task ResetPositionAsync(SkinDefinition skin)
    {
        skin.X = 60; skin.Y = 60;
        if (_open.TryGetValue(skin.Id, out var entry)) entry.Window.ApplyPosition(skin.X, skin.Y);
        await PersistAsync(false);
    }

    public async Task ApplyScenePlacementAsync(SkinDefinition skin, double x, double y, double opacity, bool enabled)
    {
        await ApplyScenePlacementCoreAsync(skin, x, y, opacity, enabled);
        await PersistAsync(false);
    }

    /// <summary>
    /// Reconciles the complete widget composition of a desktop world in one pass. A world placement
    /// may carry a serialized widget definition; when its id is no longer present in skins.json we
    /// materialize that definition back into the live widget library instead of silently dropping the
    /// placement. All window changes are performed before one persistence operation, which also avoids
    /// the repeated file churn that the old Studio loop caused for every widget.
    /// </summary>
    public async Task<(int Applied, int Missing)> ApplySceneAsync(IEnumerable<SceneWidgetPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        await _windowMutationGate.WaitAsync();
        try
        {
            var scenePlacements = placements
            .Where(p => !string.IsNullOrWhiteSpace(p.WidgetId))
            .OrderBy(p => p.ZIndex)
            .ToList();

        // Worlds should contain one placement per widget. Older versions could leave behind
        // orphaned GUIDs or duplicate VibeFinder placements during provisioning/rebuilds.
        // Normalize those records before opening any windows so a world can never resurrect
        // two copies of the same widget.
        var cleanedPlacements = new List<SceneWidgetPlacement>(scenePlacements.Count);
        var seenPlacementKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in scenePlacements)
        {
            var key = !string.IsNullOrWhiteSpace(placement.Definition?.Name) &&
                      placement.Definition!.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase)
                ? $"name:{placement.Definition.Name}"
                : $"id:{placement.WidgetId}";

            if (!seenPlacementKeys.Add(key))
            {
                _logger.LogWarning(
                    "Desktop world ignored duplicate widget placement {WidgetId} ({WidgetName})",
                    placement.WidgetId,
                    placement.Definition?.Name ?? "(unknown)");
                continue;
            }

            cleanedPlacements.Add(placement);
        }

        scenePlacements = cleanedPlacements;

        var known = _skins.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        var sceneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var successfulPlacements = new List<SceneWidgetPlacement>(scenePlacements.Count);
        var applied = 0;
        var missing = 0;

        foreach (var placement in scenePlacements)
        {
            if (!known.TryGetValue(placement.WidgetId, out var skin))
            {
                if (placement.Definition is not null)
                {
                    // Legacy worlds may contain an old random WidgetId while the snapshot still
                    // identifies the canonical VibeFinder widget by name. Rebind it to the live
                    // definition instead of creating a second native widget instance.
                    var snapshotName = placement.Definition.Name?.Trim();
                    var liveVibe = !string.IsNullOrWhiteSpace(snapshotName) &&
                                   IsVibeFinderSkin(placement.Definition)
                        ? _skins.FirstOrDefault(s =>
                            s.Name.Equals(snapshotName, StringComparison.OrdinalIgnoreCase))
                        : null;

                    if (liveVibe is not null)
                    {
                        var oldWidgetId = placement.WidgetId;
                        skin = liveVibe;
                        placement.WidgetId = liveVibe.Id;
                        _logger.LogInformation(
                            "Desktop world rebound legacy widget placement {OldWidgetId} to canonical {WidgetId} ({WidgetName})",
                            oldWidgetId, skin.Id, skin.Name);
                    }
                    else
                    {
                        skin = placement.Definition.Clone(placement.WidgetId);
                        _logger.LogInformation(
                            "Desktop world restored missing widget {WidgetId} ({WidgetName}) from its saved placement snapshot",
                            skin.Id, skin.Name);
                    }
                }
                else if (CanonicalDefaults.TryGetValue(placement.WidgetId, out var canonical))
                {
                    // Built-in widget ids are stable, so pre-snapshot worlds can still be repaired
                    // even when a local skins.json reset removed the corresponding definition.
                    skin = canonical.Clone(placement.WidgetId);
                    _logger.LogInformation(
                        "Desktop world restored canonical built-in widget {WidgetId} ({WidgetName})",
                        skin.Id, skin.Name);
                }
                else
                {
                    missing++;
                    _logger.LogWarning(
                        "Desktop world removed orphaned placement {WidgetId} with no live or recoverable widget definition",
                        placement.WidgetId);
                    continue;
                }

                skin.Enabled = false;
                _skins.Add(skin);
                known[skin.Id] = skin;
            }
            else if (sceneIds.Contains(skin.Id))
            {
                // Duplicate placements were normalized before this point; never materialize an
                // automatic second widget instance.
                missing++;
                continue;
            }

            sceneIds.Add(skin.Id);
            await ApplyScenePlacementCoreAsync(
                skin,
                placement.X,
                placement.Y,
                placement.Opacity,
                placement.Visible);
            successfulPlacements.Add(placement);
            applied++;

            // World application can create several WinUI top-level windows in one dispatcher turn.
            // Give WinUI a dispatcher turn between placements so each HWND gets created, shown and
            // registered before the next widget is activated. This is especially important for a
            // world containing many widgets: without the yield, Windows can visually settle only the
            // last activation even though the composition model and _open registry contain every skin.
            await YieldToDispatcherAsync();
        }

        // Anything currently visible but not part of this world is hidden. This is deliberately
        // based on the resolved ids so an unresolvable/corrupt placement cannot accidentally keep a
        // stale widget around forever.
        foreach (var skin in _skins.Where(s => s.Enabled && !sceneIds.Contains(s.Id)).ToList())
            await ApplyScenePlacementCoreAsync(skin, skin.X, skin.Y, skin.Opacity, false);

        // Persist a cleaned active-world composition when the caller passed the actual scene list.
        if (placements is List<SceneWidgetPlacement> sourcePlacements &&
            App.SceneService is not null)
        {
            var owner = App.SceneService.Scenes.FirstOrDefault(s =>
                ReferenceEquals(s.Widgets, sourcePlacements));
            if (owner is not null && sourcePlacements.Count != successfulPlacements.Count)
            {
                sourcePlacements.Clear();
                sourcePlacements.AddRange(successfulPlacements);
                await App.SceneService.UpsertAsync(owner);
            }
        }

        await PersistAsync(true);

        _logger.LogInformation(
            "Desktop world applied: {AppliedCount} widget placements resolved, {MissingCount} missing definitions, {PlacementCount} placements in scene",
            applied,
            missing,
            scenePlacements.Count);

            return (applied, missing);
        }
        finally
        {
            _windowMutationGate.Release();
        }
    }

    private async Task ApplyScenePlacementCoreAsync(SkinDefinition skin, double x, double y, double opacity, bool enabled)
    {
        skin.X = x;
        skin.Y = y;
        skin.Opacity = Math.Clamp(opacity, 0, 1);
        skin.Enabled = enabled;

        if (enabled)
        {
            if (_open.TryGetValue(skin.Id, out var existing))
            {
                existing.Window.ApplyPosition(skin.X, skin.Y);
                existing.Window.ApplyOpacity(skin.Opacity);
            }
            else
            {
                OpenWindowFor(skin);
            }
        }
        else
        {
            await CloseWindowFor(skin);
        }
    }

    private async void OnWindowMoved(SkinDefinition skin, double x, double y)
    {
        try
        {
            skin.X = x;
            skin.Y = y;
            await PersistAsync(false);
            await SyncActiveScenePlacementAsync(skin);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to remember widget {SkinId} position in the active desktop world", skin.Id);
        }
    }

    private async Task SyncActiveScenePlacementAsync(SkinDefinition skin)
    {
        var scene = App.SceneService?.ActiveScene;
        if (scene is null) return;

        var placement = scene.Widgets.FirstOrDefault(
            p => p.WidgetId.Equals(skin.Id, StringComparison.OrdinalIgnoreCase));
        if (placement is null) return;

        placement.X = skin.X;
        placement.Y = skin.Y;
        placement.Opacity = Math.Clamp(skin.Opacity, 0, 1);
        placement.Visible = skin.Enabled;
        placement.Definition = skin.Clone(skin.Id);

        await App.SceneService.UpsertAsync(scene);
    }

    public void ToggleAllWidgetsVisibility()
    {
        _widgetsHidden = !_widgetsHidden;
        foreach (var (window, _) in _open.Values) { if (_widgetsHidden) window.AppWindow.Hide(); else window.AppWindow.Show(); }
    }

    public async Task<SkinDefinition> CreateNewSkinAsync()
    {
        var skin = new SkinDefinition { Name = "New Widget", Enabled = false, X = 60, Y = 60, Width = 200, Height = 100 };
        _skins.Add(skin); await PersistAsync(true); return skin;
    }

    public async Task AddGeneratedSkinAsync(SkinDefinition skin) { _skins.Add(skin); await PersistAsync(true); }

    /// <summary>
    /// Persists measure/target changes without destroying the existing widget window.
    /// </summary>
    public async Task PersistSkinDataAsync(SkinDefinition skin)
    {
        ArgumentNullException.ThrowIfNull(skin);
        await _windowMutationGate.WaitAsync();
        try
        {
            if (_open.TryGetValue(skin.Id, out var entry))
            {
                entry.ViewModel.ReloadMeasures();
                entry.ViewModel.RefreshMeasures();
                _dispatcher.TryEnqueue(entry.ViewModel.UpdateMeters);
            }

            await PersistAsync(true);
            await SyncActiveScenePlacementAsync(skin);
        }
        finally
        {
            _windowMutationGate.Release();
        }
    }

    public async Task SaveSkinAsync(SkinDefinition skin)
    {
        ArgumentNullException.ThrowIfNull(skin);
        await _windowMutationGate.WaitAsync();
        try
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
            await SyncActiveScenePlacementAsync(skin);
        }
        finally
        {
            _windowMutationGate.Release();
        }
    }

    public async Task DeleteSkinAsync(SkinDefinition skin)
    {
        ArgumentNullException.ThrowIfNull(skin);
        await _windowMutationGate.WaitAsync();
        try
        {
            await CloseWindowFor(skin);
            _skins.RemoveAll(s => s.Id == skin.Id);

            var scene = App.SceneService.ActiveScene;
            var placement = scene?.Widgets.FirstOrDefault(p => p.WidgetId.Equals(skin.Id, StringComparison.OrdinalIgnoreCase));
            if (placement is not null)
                scene!.Widgets.Remove(placement);
            if (scene is not null)
                await App.SceneService.UpsertAsync(scene);

            await PersistAsync(true);
        }
        finally
        {
            _windowMutationGate.Release();
        }
    }

    private void OpenWindowFor(SkinDefinition skin)
    {
        if (_open.ContainsKey(skin.Id)) return;
        var viewModel = new SkinHostViewModel(skin, _loggerFactory?.CreateLogger<SkinHostViewModel>(), App.ThemeService, _pluginRegistry);
        var window = new SkinHostWindow(viewModel);
        window.PositionChanged += (x, y) => OnWindowMoved(skin, x, y);
        window.EditRequested += () => App.MainWindow.NavigateToSkinEditor(skin);
        window.LockToggleRequested += () => _ = SetLockedAsync(skin, !skin.Locked);
        window.ResetPositionRequested += () => _ = ResetPositionAsync(skin);
        window.DisableRequested += () => _ = SetEnabledAsync(skin, false);
        window.Closed += (_, _) =>
        {
            if (_open.TryGetValue(skin.Id, out var current) && ReferenceEquals(current.Window, window))
            {
                _open.Remove(skin.Id);
                _scheduler.Remove(skin.Id);
            }
        };
        _open[skin.Id] = (window, viewModel);
        _scheduler.RunImmediately(skin.Id, _schedulerClock.ElapsedMilliseconds);

        try
        {
            window.Activate();
            // Explicit Show is intentional: unlike Activate(), it does not make the newly-created
            // widget depend on activation/focus state to remain visible while a world is being
            // composed. It is harmless for normal individual widget toggles and makes batch world
            // application deterministic.
            window.AppWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Widget {WidgetId} ({WidgetName}) was created but could not be shown during activation",
                skin.Id,
                skin.Name);
        }

        _logger.LogDebug(
            "Widget window opened: {WidgetId} ({WidgetName}) at ({X:0.##},{Y:0.##}), size {Width:0.##}×{Height:0.##}; open windows={OpenCount}",
            skin.Id,
            skin.Name,
            skin.X,
            skin.Y,
            skin.Width,
            skin.Height,
            _open.Count);

        if (_widgetsHidden) window.AppWindow.Hide();
        viewModel.RefreshMeasures(); viewModel.UpdateMeters();
    }

    private Task YieldToDispatcherAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() => completion.TrySetResult()))
            completion.TrySetResult();
        return completion.Task;
    }

    private async Task CloseWindowFor(SkinDefinition skin)
    {
        if (!_open.TryGetValue(skin.Id, out var entry)) return;

        _open.Remove(skin.Id);
        _scheduler.Remove(skin.Id);

        // Stop measure-owned background work before destroying the HWND. This is particularly
        // important for VibeFinder widgets, whose recommendation/login tasks outlive a single
        // visual frame. Closing one of several VibeFinder instances must not leave its measure
        // objects racing the remaining windows.
        entry.ViewModel.DisposeMeasures();

        try { entry.Window.AppWindow.Hide(); }
        catch (Exception ex) { _logger.LogDebug(ex, "Widget {SkinId}: hide during teardown failed", skin.Id); }

        entry.Window.PrepareForClose();

        // Desktop-layer windows need a small native settle time after detachment before Close().
        if (skin.DesktopLayer)
            await Task.Delay(150);

        await YieldToDispatcherAsync();

        try
        {
            entry.Window.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Widget window failed to close cleanly for skin {SkinId} ({SkinName})",
                skin.Id,
                skin.Name);
        }
    }

    private async Task PersistAsync(bool notifyListChanged)
    {
        try { await _repo.SaveAllAsync(_skins); }
        catch (IOException ex) { _logger.LogWarning(ex, "Failed to persist skins.json (file locked). It will be saved on the next edit."); SaveFailed?.Invoke(this, "Widget changes couldn't be saved (file was locked) — they'll be saved with the next edit."); }
        if (notifyListChanged) SkinsChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<string>? SaveFailed;

    public void Dispose()
    {
        _timer?.Stop();

        foreach (var (_, entry) in _open.Values)
            entry.ViewModel.DisposeMeasures();

        foreach (var (window, _) in _open.Values)
        {
            try { window.PrepareForClose(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Widget teardown preparation failed during shutdown"); }

            try { window.Close(); }
            catch (Exception ex) { _logger.LogWarning(ex, "A widget window failed to close cleanly during shutdown"); }
        }

        _open.Clear();
        _scheduler.Clear();
    }

    private static bool IsVibeFinderSkin(SkinDefinition skin) =>
        skin.Name.StartsWith("VibeFinder", StringComparison.OrdinalIgnoreCase);

    public void EnsureVibeFinderSkinsExist()
    {
        bool changed = false;
        const string vibeTarget = "listener|hunter2|$theme";
        foreach (var vibe in new[] { "VibeFinder Primary", "VibeFinder Minimal", "VibeFinder Playlist" })
        {
            var skin = _skins.FirstOrDefault(s => s.Name.Equals(vibe, StringComparison.Ordinal));
            if (skin is null)
            {
                skin = vibe switch
                {
                    "VibeFinder Primary" => DefaultWidgetCatalog.CreateVibeFinderPrimary(vibeTarget),
                    "VibeFinder Minimal" => DefaultWidgetCatalog.CreateVibeFinderMinimal(vibeTarget),
                    _ => DefaultWidgetCatalog.CreateVibeFinderPlaylist(vibeTarget)
                };
                _skins.Add(skin); changed = true;
            }
            else
            {
                var needsRebuild = !skin.Variables.TryGetValue("designVersion", out var version) ||
                                   !string.Equals(version, DefaultWidgetCatalog.DesignVersion, StringComparison.Ordinal);
                if (!needsRebuild) continue;

                var enabled = skin.Enabled;
                var x = skin.X;
                var y = skin.Y;
                var opacity = skin.Opacity;
                var aot = skin.AlwaysOnTop;
                var locked = skin.Locked;
                var layer = skin.DesktopLayer;

                DefaultWidgetCatalog.RebuildVibeFinder(skin);

                skin.Enabled = enabled;
                skin.X = x;
                skin.Y = y;
                skin.Opacity = opacity;
                skin.AlwaysOnTop = aot;
                skin.Locked = locked;
                skin.DesktopLayer = layer;
                changed = true;
            }
        }
        if (changed) _ = PersistAsync(true);
    }
}
