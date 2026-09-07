using Microsoft.UI.Dispatching;
using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Services;
using ThemeManager.Integration.Skins;

namespace ThemeManager.WinUI.Services;

public sealed class DesktopWorldRuntime : IDisposable
{
    private readonly DesktopSceneService _scenes;
    private readonly DispatcherQueue _dispatcher;
    private readonly Dictionary<string, double> _baseOpacity = new(StringComparer.OrdinalIgnoreCase);
    private DesktopAtmosphereHost? _atmosphere;
    private DispatcherQueueTimer? _timer;
    private bool _started;
    private bool _transitioning;

    public DesktopWorldRuntime(DesktopSceneService scenes, DispatcherQueue dispatcher) { _scenes = scenes; _dispatcher = dispatcher; }
    public DesktopVibeAdapter.Atmosphere CurrentAtmosphere { get; private set; } = DesktopVibeAdapter.From("Neutral", 0.08, 0.5);
    public DesktopVibeAdapter.Atmosphere TargetAtmosphere { get; private set; } = DesktopVibeAdapter.From("Neutral", 0.08, 0.5);

    public void Start()
    {
        if (_started) return;
        _started = true;
        VibeSnapshotHub.SnapshotChanged += OnVibeChanged;
        VibeFinderWebState.StateChanged += OnVibeFinderStateChanged;
        _scenes.ActiveSceneChanged += OnSceneChanged;
        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        ApplySnapshot(VibeSnapshotHub.Current);
        PullVibeFinderState();
        _ = StartAtmosphereAsync();
    }

    private async Task StartAtmosphereAsync()
    {
        try
        {
            _atmosphere ??= new DesktopAtmosphereHost(App.LoggerFactory.CreateLogger<DesktopAtmosphereHost>());
            await _atmosphere.StartAsync(_scenes.ActiveScene, CurrentAtmosphere);
            _atmosphere.ApplyScene(_scenes.ActiveScene, CurrentAtmosphere);
        }
        catch (Exception ex)
        {
            App.LoggerFactory.CreateLogger<DesktopWorldRuntime>().LogWarning(ex, "Desktop atmosphere startup failed; widgets will continue normally");
        }
    }

    private void OnVibeFinderStateChanged(object? sender, EventArgs e) { if (_dispatcher.HasThreadAccess) PullVibeFinderState(); else _dispatcher.TryEnqueue(PullVibeFinderState); }
    private static void PullVibeFinderState()
    {
        if (!VibeFinderWebState.IsActive) return;
        var signal = VibeAnalyzer.Analyze($"{VibeFinderWebState.Title} {VibeFinderWebState.Artist}");
        var atmosphere = DesktopVibeAdapter.From(signal);
        var energy = VibeFinderWebState.IsPlaying ? atmosphere.Energy : atmosphere.Energy * 0.22;
        var mood = signal.HasSignal ? atmosphere.Mood : (VibeFinderWebState.IsPlaying ? "Atmospheric" : "Neutral");
        var warmth = signal.HasSignal ? atmosphere.Warmth : 0.5;
        VibeSnapshotHub.Publish(new VibeSnapshot(mood, Math.Clamp(energy, 0, 1), Math.Clamp(warmth, 0, 1), "VibeFinder", VibeFinderWebState.Title == "—" ? null : VibeFinderWebState.Title, VibeFinderWebState.Artist == "—" ? null : VibeFinderWebState.Artist));
    }

    private void OnVibeChanged(object? sender, VibeSnapshot snapshot)
    {
        if (_dispatcher.HasThreadAccess) ApplySnapshot(snapshot); else _dispatcher.TryEnqueue(() => ApplySnapshot(snapshot));
    }

    private void OnSceneChanged(object? sender, DesktopScene? scene)
    {
        _baseOpacity.Clear();
        if (_dispatcher.HasThreadAccess) RecalculateTarget(VibeSnapshotHub.Current); else _dispatcher.TryEnqueue(() => RecalculateTarget(VibeSnapshotHub.Current));
    }

    private void ApplySnapshot(VibeSnapshot snapshot) { RecalculateTarget(snapshot); TryAutoSwitch(snapshot); }

    private void RecalculateTarget(VibeSnapshot snapshot)
    {
        var behavior = _scenes.ActiveScene?.Behavior;
        if (behavior is null)
        {
            TargetAtmosphere = DesktopVibeAdapter.From(snapshot.Mood, snapshot.Energy, snapshot.Warmth);
            _transitioning = true;
            return;
        }

        if (!behavior.ReactToVibeFinder && !behavior.ReactToMedia)
        {
            TargetAtmosphere = ApplySceneEffects(DesktopVibeAdapter.From("Static", 0.05, snapshot.Warmth, behavior.TransitionSeconds));
            _transitioning = true;
            return;
        }

        var energy = behavior.ReactToVibeFinder ? snapshot.Energy : 0.05;
        if (behavior.ReactToMedia && snapshot.Source == "VibeFinder")
            energy = Math.Clamp(energy * Math.Max(0.35, behavior.AudioSensitivity), 0, 1);

        TargetAtmosphere = ApplySceneEffects(DesktopVibeAdapter.From(snapshot.Mood, energy, snapshot.Warmth, behavior.TransitionSeconds));
        _transitioning = true;
    }

    private DesktopVibeAdapter.Atmosphere ApplySceneEffects(DesktopVibeAdapter.Atmosphere atmosphere)
    {
        var scene = _scenes.ActiveScene;
        if (scene is null) return atmosphere;

        var glow = scene.Effects.Where(e => e.Enabled && e.Type.Equals("Glow", StringComparison.OrdinalIgnoreCase)).Select(e => e.Intensity).DefaultIfEmpty(0.0).Max();
        var ambient = scene.Effects.Where(e => e.Enabled && e.Type.Equals("AmbientMotion", StringComparison.OrdinalIgnoreCase)).Select(e => e.Intensity).DefaultIfEmpty(0.0).Max();
        var reactive = scene.Effects.Where(e => e.Enabled && (e.Type.Equals("Spectrum", StringComparison.OrdinalIgnoreCase) || e.Type.Equals("Visualizer", StringComparison.OrdinalIgnoreCase))).Select(e => e.Intensity).DefaultIfEmpty(0.0).Max();

        return atmosphere with
        {
            Glow = Math.Clamp(atmosphere.Glow * (0.7 + glow), 0, 1),
            Motion = Math.Clamp(atmosphere.Motion * (0.55 + ambient), 0, 1),
            AudioReactivity = Math.Clamp(atmosphere.AudioReactivity * (0.7 + reactive), 0, 1)
        };
    }

    private void Tick()
    {
        var scene = _scenes.ActiveScene;
        if (scene is null) return;

        var amount = _transitioning ? Math.Clamp(0.25 / Math.Max(0.15, scene.Behavior.TransitionSeconds), 0.04, 0.35) : 0.08;
        CurrentAtmosphere = DesktopVibeAdapter.Blend(CurrentAtmosphere, TargetAtmosphere, amount);
        if (Math.Abs(CurrentAtmosphere.Energy - TargetAtmosphere.Energy) < 0.01 && Math.Abs(CurrentAtmosphere.Warmth - TargetAtmosphere.Warmth) < 0.01) _transitioning = false;

        _atmosphere?.ApplyScene(scene, CurrentAtmosphere);

        if (App.SkinManager is null) return;
        var motion = Math.Clamp(CurrentAtmosphere.Motion * Math.Max(0, scene.Behavior.MotionIntensity), 0, 1);
        foreach (var widget in App.SkinManager.Skins)
        {
            if (!widget.Enabled) continue;
            if (!_baseOpacity.ContainsKey(widget.Id)) _baseOpacity[widget.Id] = Math.Clamp(widget.Opacity, 0.05, 1.0);
            var targetOpacity = Math.Clamp(_baseOpacity[widget.Id] * (0.92 + motion * 0.08), 0.05, 1.0);
            if (Math.Abs(widget.Opacity - targetOpacity) > 0.015)
            {
                widget.Opacity = targetOpacity;
                _ = App.SkinManager.SetOpacityAsync(widget, targetOpacity);
            }
        }
    }

    private void TryAutoSwitch(VibeSnapshot snapshot)
    {
        var active = _scenes.ActiveScene;
        if (active is null || !active.Behavior.AutoSwitch || !active.Behavior.ReactToVibeFinder) return;
        var desiredTag = snapshot.Mood.ToLowerInvariant() switch { "nocturnal" => "nocturnal", "cozy" => "cozy", "euphoric" or "intense" => "neon", "bright" => "minimal", "melancholic" => "nocturnal", _ => null };
        if (desiredTag is null || active.Tags.Any(t => t.Equals(desiredTag, StringComparison.OrdinalIgnoreCase))) return;
        var candidate = _scenes.Scenes.FirstOrDefault(s => s.Tags.Any(t => t.Equals(desiredTag, StringComparison.OrdinalIgnoreCase)) && s.Behavior.ReactToVibeFinder);
        if (candidate is not null) _scenes.SetActiveScene(candidate);
    }

    public void Dispose()
    {
        if (!_started) return;
        _started = false;
        VibeSnapshotHub.SnapshotChanged -= OnVibeChanged;
        VibeFinderWebState.StateChanged -= OnVibeFinderStateChanged;
        _scenes.ActiveSceneChanged -= OnSceneChanged;
        _timer?.Stop();
        _timer = null;
        _baseOpacity.Clear();
        _atmosphere?.Dispose();
        _atmosphere = null;
    }
}
