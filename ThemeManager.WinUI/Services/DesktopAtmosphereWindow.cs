using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Integration.Skins;
using Windows.Graphics;
using Windows.UI;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Transparent, click-through visual layer rendered behind the desktop icons.
/// A scene controls the palette and effect intensity; this window turns those abstract
/// properties into a lightweight animated WinUI visual field.
/// </summary>
public sealed class DesktopAtmosphereWindow : Window, IDisposable
{
    private readonly Canvas _canvas = new();
    private readonly Grid _root = new();
    private readonly Border _wash = new();
    private readonly Border _vignette = new();
    private readonly List<Ellipse> _orbs = new();
    private readonly List<Particle> _particles = new();
    private readonly Random _random;
    private readonly IntPtr _hwnd;
    private readonly ILogger _logger;
    private readonly DispatcherQueueTimer _timer;
    private bool _attached;
    private bool _disposed;
    private double _time;
    private DesktopVibeAdapter.Atmosphere _atmosphere = DesktopVibeAdapter.From("Neutral", 0.08, 0.5);
    private DesktopScene? _scene;

    private sealed record Particle(Ellipse Element, double X, double Y, double Speed, double Phase);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
    }

    public DesktopAtmosphereWindow(IntPtr monitorHandle, int index, ILogger logger)
    {
        _logger = logger;
        _random = new Random(unchecked(Environment.TickCount * 31 + index));
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = "Themed.AI Atmosphere";

        ConfigureWindow();
        EnableTransparency();
        SkinWindowInterop.HideFromTaskbarAndAltTab(_hwnd, _logger);
        SkinWindowInterop.SetClickThrough(_hwnd, true, _logger);

        _root.Children.Add(_wash);
        _root.Children.Add(_canvas);
        _root.Children.Add(_vignette);
        _root.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        Content = _root;

        BuildVisuals();
        PositionToMonitor(monitorHandle);

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(50);
        _timer.Tick += (_, _) => Tick();
    }

    private void ConfigureWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = false;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    private void EnableTransparency()
    {
        try
        {
            var compositor = new Windows.UI.Composition.Compositor();
            var transparentBrush = compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
            this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>().SystemBackdrop = transparentBrush;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Atmosphere transparency could not be enabled");
        }
    }

    private void PositionToMonitor(IntPtr monitorHandle)
    {
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitorHandle, ref info)) return;
        var r = info.rcMonitor;
        AppWindow.Move(new PointInt32(r.Left, r.Top));
        AppWindow.Resize(new SizeInt32(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top)));
    }

    private void BuildVisuals()
    {
        _wash.HorizontalAlignment = HorizontalAlignment.Stretch;
        _wash.VerticalAlignment = VerticalAlignment.Stretch;

        _vignette.HorizontalAlignment = HorizontalAlignment.Stretch;
        _vignette.VerticalAlignment = VerticalAlignment.Stretch;
        var vignette = new RadialGradientBrush
        {
            GradientOrigin = new Windows.Foundation.Point(0.5, 0.5),
            Center = new Windows.Foundation.Point(0.5, 0.5),
            RadiusX = 0.82,
            RadiusY = 0.82
        };
        vignette.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0.42 });
        vignette.GradientStops.Add(new GradientStop { Color = Color.FromArgb(72, 0, 0, 0), Offset = 1.0 });
        _vignette.Background = vignette;

        for (var i = 0; i < 4; i++)
        {
            var orb = new Ellipse
            {
                Width = 520 + i * 90,
                Height = 520 + i * 90,
                Opacity = 0.08,
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush()
            };
            var brush = (RadialGradientBrush)orb.Fill;
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(180, 255, 255, 255), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 });
            _orbs.Add(orb);
            _canvas.Children.Add(orb);
        }

        for (var i = 0; i < 22; i++)
        {
            var dot = new Ellipse
            {
                Width = 1.5 + _random.NextDouble() * 4,
                Height = 1.5 + _random.NextDouble() * 4,
                Opacity = 0.12 + _random.NextDouble() * 0.35,
                Fill = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
                IsHitTestVisible = false
            };
            _particles.Add(new Particle(dot, _random.NextDouble(), _random.NextDouble(), 0.015 + _random.NextDouble() * 0.045, _random.NextDouble() * Math.PI * 2));
            _canvas.Children.Add(dot);
        }
    }

    public async Task AttachAsync()
    {
        if (_attached || _disposed) return;
        Activate();
        await Task.Yield();
        var success = await Task.Run(() => DesktopLayerInterop.TryAttach(_hwnd, _logger));
        if (!success)
        {
            AppWindow.Hide();
            _logger.LogWarning("Atmosphere window was not attached to the desktop layer; hiding it to avoid covering applications");
            return;
        }
        _attached = true;
        _timer.Start();
    }

    public void ApplyScene(DesktopScene? scene, DesktopVibeAdapter.Atmosphere atmosphere)
    {
        _scene = scene;
        _atmosphere = atmosphere;
        RenderPalette();
    }

    private void RenderPalette()
    {
        var (a, b, accent) = PaletteFor(_atmosphere.Mood);
        var energy = Math.Clamp(_atmosphere.Energy, 0, 1);
        var warmth = Math.Clamp(_atmosphere.Warmth, 0, 1);
        var glow = Math.Clamp(_atmosphere.Glow, 0, 1);

        var wash = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };
        wash.GradientStops.Add(new GradientStop { Color = Color.FromArgb((byte)(18 + energy * 34), a.R, a.G, a.B), Offset = 0 });
        wash.GradientStops.Add(new GradientStop { Color = Color.FromArgb((byte)(10 + warmth * 28), b.R, b.G, b.B), Offset = 0.55 });
        wash.GradientStops.Add(new GradientStop { Color = Color.FromArgb((byte)(12 + glow * 26), accent.R, accent.G, accent.B), Offset = 1 });
        _wash.Background = wash;

        for (var i = 0; i < _orbs.Count; i++)
        {
            var color = i % 2 == 0 ? accent : a;
            var brush = (RadialGradientBrush)_orbs[i].Fill;
            brush.GradientStops[0].Color = Color.FromArgb((byte)Math.Clamp(70 + glow * 150, 0, 255), color.R, color.G, color.B);
            brush.GradientStops[1].Color = Color.FromArgb(0, color.R, color.G, color.B);
            _orbs[i].Opacity = 0.035 + glow * 0.13;
        }
    }

    private void Tick()
    {
        if (_disposed) return;
        _time += 0.05;
        var motion = Math.Clamp(_atmosphere.Motion, 0, 1);
        var audio = Math.Clamp(_atmosphere.AudioReactivity, 0, 1);
        var ambientEnabled = _scene?.Effects.Any(e => e.Enabled && e.Type.Equals("AmbientMotion", StringComparison.OrdinalIgnoreCase)) ?? true;
        var intensity = ambientEnabled ? motion : 0;
        var width = Math.Max(1, _root.ActualWidth);
        var height = Math.Max(1, _root.ActualHeight);

        for (var i = 0; i < _orbs.Count; i++)
        {
            var orb = _orbs[i];
            var x = (0.08 + i * 0.25) * width + Math.Sin(_time * (0.25 + i * 0.07)) * (50 + 90 * intensity);
            var y = (0.15 + (i % 3) * 0.32) * height + Math.Cos(_time * (0.21 + i * 0.05)) * (35 + 80 * intensity);
            Canvas.SetLeft(orb, x - orb.Width / 2);
            Canvas.SetTop(orb, y - orb.Height / 2);
            var scale = 0.98 + Math.Sin(_time * 0.9 + i) * 0.025 + audio * Math.Sin(_time * 5 + i) * 0.025;
            orb.RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale, CenterX = orb.Width / 2, CenterY = orb.Height / 2 };
        }

        foreach (var p in _particles)
        {
            var x = (p.X + Math.Sin(_time * 0.22 + p.Phase) * 0.025 * intensity + 1) % 1;
            var y = (p.Y - _time * p.Speed * (0.4 + intensity) + 10) % 1;
            Canvas.SetLeft(p.Element, x * width);
            Canvas.SetTop(p.Element, y * height);
            p.Element.Opacity = Math.Clamp((0.08 + Math.Sin(_time * 1.6 + p.Phase) * 0.08) * (0.6 + intensity), 0, 0.5);
        }
    }

    private static (Color A, Color B, Color Accent) PaletteFor(string? mood) =>
        mood?.ToLowerInvariant() switch
        {
            "cozy" => (Color.FromArgb(255, 126, 77, 54), Color.FromArgb(255, 68, 42, 31), Color.FromArgb(255, 239, 174, 102)),
            "neon" or "euphoric" => (Color.FromArgb(255, 55, 15, 105), Color.FromArgb(255, 9, 55, 87), Color.FromArgb(255, 255, 65, 181)),
            "intense" => (Color.FromArgb(255, 100, 20, 45), Color.FromArgb(255, 38, 12, 30), Color.FromArgb(255, 255, 74, 74)),
            "nocturnal" or "melancholic" => (Color.FromArgb(255, 19, 29, 67), Color.FromArgb(255, 8, 14, 34), Color.FromArgb(255, 91, 111, 255)),
            "bright" or "minimal" => (Color.FromArgb(255, 75, 125, 160), Color.FromArgb(255, 220, 235, 242), Color.FromArgb(255, 110, 210, 196)),
            "hud" => (Color.FromArgb(255, 5, 55, 72), Color.FromArgb(255, 4, 17, 25), Color.FromArgb(255, 51, 220, 202)),
            _ => (Color.FromArgb(255, 25, 30, 45), Color.FromArgb(255, 10, 12, 20), Color.FromArgb(255, 130, 150, 255))
        };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        try
        {
            if (_attached) DesktopLayerInterop.Detach(_hwnd, _logger);
            AppWindow.Hide();
            this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>().SystemBackdrop = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Atmosphere window cleanup failed");
        }
    }
}
