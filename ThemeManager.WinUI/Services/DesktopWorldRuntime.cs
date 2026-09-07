using Microsoft.UI.Dispatching;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Integration.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Live bridge between scene behavior, VibeFinder playback and desktop widgets.
/// The runtime deliberately owns only orchestration: platform-specific rendering stays in the
/// existing SkinManager/SystemIntegrator services.
/// </summary>
public sealed class DesktopWorldRuntime : IDisposable
{
    private readonly DesktopSceneService _scenes;
    private readonly DispatcherQueue _dispatcher;
    private DispatcherQueueTimer? _timer;
    private bool _started;
    private bool _transitioning;

    public DesktopWorldRuntime(DesktopSceneService scenes, DispatcherQueue dispatcher)
    {
        _scenes = scenes;
        _dispatcher = dispatcher;
    }

    public Atmosphere CurrentAtmosphere { get; private set; } = DesktopVibeAdapter.From("Neutral", 0.08, 0.5);
    public Atmosphere TargetAtmosphere { get; private set; } = DesktopVibeAdapter.From("Neutral", 0.08, 0.5);

    public void Start()
    {
        if (_started) return;
        _started = true;
        VibeSnapshotHub.SnapshotChanged += OnVibeChanged;
        _scenes.ActiveSceneChanged += OnSceneChanged;

        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        ApplySnapshot(VibeSnapshotHub.Current);
    }

    private void OnVibeChanged(object? sender, VibeSnapshot snapshot)
    {
        if (_dispatcher.HasThreadAccess) ApplySnapshot(snapshot);
        else _dispatcher.TryEnqueue(() => ApplySnapshot(snapshot));
    }

    private void OnSceneChanged(object? sender, DesktopScene? scene)
    {
        if (_dispatcher.HasThreadAccess) RecalculateTarget(VibeSnapshotHub.Current);
        else _dispatcher.TryEnqueue(() => RecalculateTarget(VibeSnapshotHub.Current));
    }

    private void ApplySnapshot(VibeSnapshot snapshot)
    {
        RecalculateTarget(snapshot);
        TryAutoSwitch(snapshot);
    }

    private void RecalculateTarget(VibeSnapshot snapshot)
    {
        var scene = _scenes.ActiveScene;
        var behavior = scene?.Behavior;
        if (behavior is null)
        {
            TargetAtmosphere = DesktopVibeAdapter.From(snapshot.Mood, snapshot.Energy, snapshot.Warmth);
            return;
        }

        if (!behavior.ReactToVibeFinder && !behavior.ReactToMedia)
        {
            TargetAtmosphere = DesktopVibeAdapter.From("Static", 0.05, snapshot.Warmth, behavior.TransitionSeconds);
            return;
        }

        var energy = behavior.ReactToVibeFinder ? snapshot.Energy : 0.05;
        if (behavior.ReactToMedia && snapshot.Source == "VibeFinder" && snapshot.Energy > 0)
            energy = Math.Clamp(energy * Math.Max(0.35, behavior.AudioSensitivity), 0, 1);

        TargetAtmosphere = DesktopVibeAdapter.From(snapshot.Mood, energy, snapshot.Warmth, behavior.TransitionSeconds);
        _transitioning = true;
    }

    private void Tick()
    {
        var scene = _scenes.ActiveScene;
        if (scene is null) return;

        var amount = _transitioning
            ? Math.Clamp(0.25 / Math.Max(0.15, scene.Behavior.TransitionSeconds), 0.04, 0.35)
            : 0.08;
        CurrentAtmosphere = DesktopVibeAdapter.Blend(CurrentAtmosphere, TargetAtmosphere, amount);
        if (Math.Abs(CurrentAtmosphere.Energy - TargetAtmosphere.Energy) < 0.01 &&
            Math.Abs(CurrentAtmosphere.Warmth - TargetAtmosphere.Warmth) < 0.01)
            _transitioning = false;

        // Modulate enabled widgets rather than rebuilding them every frame. This keeps the
        // expensive widget lifecycle stable while still making the desktop feel alive.
        var motion = Math.Clamp(CurrentAtmosphere.Motion * scene.Behavior.MotionIntensity, 0, 1);
        foreach (var widget in App.SkinManager.Skins)
        {
            if (!widget.Enabled) continue;
            var baseOpacity = Math.Clamp(widget.Opacity, 0.08, 1.0);
            var targetOpacity = Math.Clamp(baseOpacity * (0.92 + motion * 0.08), 0.05, 1.0);
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
        if (active is null || !active.Behavior.AutoSwitch || !active.Behavior.ReactToVibeFinder)
            return;

        var desiredTag = snapshot.Mood.ToLowerInvariant() switch
        {
            "nocturnal" => "nocturnal",
            "cozy" => "cozy",
            "euphoric" or "intense" => "neon",
            "bright" => "minimal",
            "melancholic" => "nocturnal",
            _ => null
        };
        if (desiredTag is null || active.Tags.Any(t => t.Equals(desiredTag, StringComparison.OrdinalIgnoreCase)))
            return;

        var candidate = _scenes.Scenes.FirstOrDefault(s =>
            s.Tags.Any(t => t.Equals(desiredTag, StringComparison.OrdinalIgnoreCase)) &&
            s.Behavior.ReactToVibeFinder);
        if (candidate is not null) _scenes.SetActiveScene(candidate);
    }

    public void Dispose()
    {
        if (!_started) return;
        _started = false;
        VibeSnapshotHub.SnapshotChanged -= OnVibeChanged;
        _scenes.ActiveSceneChanged -= OnSceneChanged;
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }
    }
}
