using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private List<SkinDefinition> _skins = new();
    public IReadOnlyList<SkinDefinition> Skins => _skins;
    public event EventHandler? SkinsChanged;
    private readonly Dictionary<string, (SkinHostWindow Window, SkinHostViewModel ViewModel)> _open = new();
    private DispatcherQueueTimer? _timer;
    private readonly Stopwatch _schedulerClock = Stopwatch.StartNew();
    private readonly WidgetTickScheduler _scheduler = new();
    private bool _widgetsHidden;
    private const int SchedulerQuantumMs = 50;

    public SkinManagerService(SkinRepository repository, ILoggerFactory? loggerFactory = null)
    {
        _repo = repository;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<SkinManagerService>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SkinManagerService>.Instance;
    }

    public async Task InitializeAsync()
    {
        _skins = await _repo.LoadAllAsync();
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
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(SchedulerQuantumMs);
        _timer.Tick += (_, _) => TickDueWidgets();
        _timer.Start();
    }

    private void TickDueWidgets()
    {
        var now = _schedulerClock.ElapsedMilliseconds;
        var dispatcher = DispatcherQueue.GetForCurrentThread();
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
        skin.Enabled = enabled;
        if (enabled) OpenWindowFor(skin); else await CloseWindowFor(skin);
        await PersistAsync(true);
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
            else OpenWindowFor(skin);
        }
        else await CloseWindowFor(skin);
        await PersistAsync(false);
    }

    private async void OnWindowMoved(SkinDefinition skin, double x, double y)
    {
        skin.X = x; skin.Y = y;
        await PersistAsync(false);
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

    public async Task SaveSkinAsync(SkinDefinition skin)
    {
        if (_open.ContainsKey(skin.Id)) { await CloseWindowFor(skin); if (skin.Enabled) OpenWindowFor(skin); }
        else if (skin.Enabled) OpenWindowFor(skin);
        await PersistAsync(true);
    }

    public async Task DeleteSkinAsync(SkinDefinition skin)
    {
        await CloseWindowFor(skin); _skins.RemoveAll(s => s.Id == skin.Id); await PersistAsync(true);
    }

    private void OpenWindowFor(SkinDefinition skin)
    {
        if (_open.ContainsKey(skin.Id)) return;
        var viewModel = new SkinHostViewModel(skin, _loggerFactory?.CreateLogger<SkinHostViewModel>(), App.ThemeService);
        var window = new SkinHostWindow(viewModel);
        window.PositionChanged += (x, y) => OnWindowMoved(skin, x, y);
        window.EditRequested += () => App.MainWindow.NavigateToSkinEditor(skin);
        window.LockToggleRequested += () => _ = SetLockedAsync(skin, !skin.Locked);
        window.ResetPositionRequested += () => _ = ResetPositionAsync(skin);
        window.DisableRequested += () => _ = SetEnabledAsync(skin, false);
        window.Closed += (_, _) => _open.Remove(skin.Id);
        _open[skin.Id] = (window, viewModel);
        _scheduler.RunImmediately(skin.Id, _schedulerClock.ElapsedMilliseconds);
        window.Activate();
        if (_widgetsHidden) window.AppWindow.Hide();
        viewModel.RefreshMeasures(); viewModel.UpdateMeters();
    }

    private async Task CloseWindowFor(SkinDefinition skin)
    {
        if (!_open.TryGetValue(skin.Id, out var entry)) return;
        _open.Remove(skin.Id); _scheduler.Remove(skin.Id); entry.ViewModel.IsClosed = true;
        entry.Window.AppWindow.Hide(); entry.Window.PrepareForClose();
        if (skin.DesktopLayer) await Task.Delay(150);
        try { entry.Window.Close(); } catch (Exception ex) { _logger.LogWarning(ex, "Widget window failed to close cleanly for skin {SkinId} ({SkinName})", skin.Id, skin.Name); }
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
        foreach (var (window, _) in _open.Values) { window.PrepareForClose(); try { window.Close(); } catch (Exception ex) { _logger.LogWarning(ex, "A widget window failed to close cleanly during shutdown"); } }
        _open.Clear(); _scheduler.Clear();
    }

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
                var enabled = skin.Enabled; var x = skin.X; var y = skin.Y; var opacity = skin.Opacity; var aot = skin.AlwaysOnTop; var locked = skin.Locked; var layer = skin.DesktopLayer;
                DefaultWidgetCatalog.RebuildVibeFinder(skin);
                skin.Enabled = enabled; skin.X = x; skin.Y = y; skin.Opacity = opacity; skin.AlwaysOnTop = aot; skin.Locked = locked; skin.DesktopLayer = layer;
                changed = true;
            }
        }
        if (changed) _ = PersistAsync(true);
    }
}
