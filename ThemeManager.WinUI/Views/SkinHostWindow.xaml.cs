using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Extensions.Logging;
using Windows.Graphics;
using WinRT;
using ThemeManager.Core.Models;
using ThemeManager.Integration.Skins;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// A single floating Rainmeter-style widget: borderless, always-on-top, optionally click-through,
/// with a translucent Cozy-themed card background that stays true-transparent (not just faded to
/// black) thanks to <see cref="EnableTransparency"/>.
///
/// EXPERIMENTAL NOTE (read this before assuming a rendering glitch is a code bug):
/// True per-pixel window transparency is one of the few things WinUI3 still doesn't have a fully
/// turnkey API for — this class uses the technique Microsoft's own docs and forum answers point to
/// (a fully-transparent Composition color brush as the window's SystemBackdrop), which is different
/// from the classic WPF/Win32 DwmExtendFrameIntoClientArea trick because WinUI3 renders through a
/// DirectX swap chain rather than classic GDI. It's been verified against current documentation, not
/// against a live Windows 11 machine (this was built in a Linux sandbox). If a widget renders with a
/// solid card instead of a see-through one, transparency simply failed to apply — <see cref="EnableTransparency"/>
/// already catches and logs that case instead of crashing, so the widget stays fully functional either way.
/// Meters are built in code (not XAML DataTemplates) for the same reason: it's easier to verify by
/// reading than a template selector would be.
/// </summary>
public sealed partial class SkinHostWindow : Window
{
    private readonly SkinHostViewModel _viewModel;
    private readonly IntPtr _hwnd;

    private bool _dragging;
    private Windows.Foundation.Point _dragAnchorLocal;

    /// <summary>Raised when the user finishes dragging the widget to a new spot (screen coordinates).</summary>
    public event Action<double, double>? PositionChanged;

    public SkinHostWindow(SkinHostViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = viewModel.Definition.Name;

        ConfigurePresenter();
        EnableTransparency();
        SkinWindowInterop.HideFromTaskbarAndAltTab(_hwnd);

        var def = viewModel.Definition;
        ApplyPosition(def.X, def.Y);
        AppWindow.Resize(new SizeInt32((int)def.Width, (int)def.Height));
        ApplyOpacity(def.Opacity);
        ApplyClickThrough(def.ClickThrough);

        BuildMeterVisuals();

        RootCanvas.PointerPressed += RootCanvas_PointerPressed;
        RootCanvas.PointerMoved += RootCanvas_PointerMoved;
        RootCanvas.PointerReleased += RootCanvas_PointerReleased;
        RootCanvas.PointerCaptureLost += (_, _) => _dragging = false;

        // Keep the card's tint in step with live theme switches from the Themes page.
        App.ThemeService.ThemeChanged += ThemeService_ThemeChanged;
        Closed += (_, _) => App.ThemeService.ThemeChanged -= ThemeService_ThemeChanged;
    }

    // ── Window setup ─────────────────────────────────────────────────────────

    private void ConfigurePresenter()
    {
        // Same "no title bar" technique MainWindow already uses successfully in this app.
        ExtendsContentIntoTitleBar = true;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    // ── DispatcherQueue Helper for System Compositor ─────────────────────────

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [System.Runtime.InteropServices.DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        [System.Runtime.InteropServices.In] DispatcherQueueOptions options,
        [System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] ref object dispatcherQueueController);

    private object? _dispatcherQueueController = null;

    private void EnsureDispatcherQueue()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null) return;
        if (_dispatcherQueueController != null) return;

        var options = new DispatcherQueueOptions
        {
            dwSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DispatcherQueueOptions)),
            threadType = 2,    // DQTYPE_THREAD_CURRENT
            apartmentType = 2  // DQTAT_COM_STA
        };
        CreateDispatcherQueueController(options, ref _dispatcherQueueController!);
    }

    /// <summary>
    /// Enables true per-pixel transparency via a fully-transparent Composition color brush as the
    /// window's SystemBackdrop — the WinUI3-native technique (see the class remarks above for why
    /// this differs from WPF/Win32). Failure here degrades gracefully to a solid card, never a crash.
    /// </summary>
    private void EnableTransparency()
    {
        try
        {
            EnsureDispatcherQueue();
            var winCompositor = new Windows.UI.Composition.Compositor();
            var transparentBrush = winCompositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>().SystemBackdrop = transparentBrush;
        }
        catch (Exception ex)
        {
            App.LoggerFactory.CreateLogger<SkinHostWindow>()
                .LogWarning(ex, "True transparency failed to apply; widget will render on a solid card instead");
        }
    }

    // ── Applied live by SkinManagerService when a setting changes ─────────────

    public void ApplyPosition(double x, double y) =>
        AppWindow.Move(new PointInt32((int)x, (int)y));

    public void ApplyOpacity(double opacity)
    {
        CardBorder.Opacity = opacity;
    }

    public void ApplyClickThrough(bool enabled) =>
        SkinWindowInterop.SetClickThrough(_hwnd, enabled);

    public Task<bool> ApplyDesktopLayerAsync(bool enabled)
    {
        return Task.Run(() =>
        {
            if (enabled) return DesktopLayerInterop.TryAttach(_hwnd);
            DesktopLayerInterop.Detach(_hwnd);
            return true;
        });
    }

    public void ApplyLocked(bool locked)
    {
        // Nothing to apply to the OS here — RootCanvas_PointerPressed reads
        // _viewModel.Definition.Locked directly at drag-start time.
    }

    private void ThemeService_ThemeChanged(object? sender, CozyTheme theme) =>
        ApplyOpacity(_viewModel.Definition.Opacity);

    // ── Meter rendering (built in code, refreshed via each meter VM's PropertyChanged) ─────

    private void BuildMeterVisuals()
    {
        foreach (var meter in _viewModel.Meters)
        {
            FrameworkElement element = meter switch
            {
                BarMeterViewModel bar => BuildBarVisual(bar),
                GraphMeterViewModel graph => BuildGraphVisual(graph),
                StringMeterViewModel str => BuildStringVisual(str),
                _ => new TextBlock(), // defensive: an unrecognized meter kind renders as an empty label, not a crash
            };

            Canvas.SetLeft(element, meter.X);
            Canvas.SetTop(element, meter.Y);
            RootCanvas.Children.Add(element);
        }
    }

    private static TextBlock BuildStringVisual(StringMeterViewModel vm)
    {
        var text = new TextBlock
        {
            Width = vm.Width,
            Height = vm.Height,
            FontSize = vm.FontSize,
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["AppFontFamily"],
            FontWeight = vm.Bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = GetWidgetTextBrush(),
            Text = vm.DisplayText,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StringMeterViewModel.DisplayText))
                text.Text = vm.DisplayText;
        };

        return text;
    }

    /// <summary>
    /// Returns a foreground brush that is guaranteed to be readable against the current
    /// theme's surface color. If TextPrimaryBrush and SurfaceBrush have insufficient contrast,
    /// falls back to pure white or black depending on which contrasts better.
    /// </summary>
    private static SolidColorBrush GetWidgetTextBrush()
    {
        var surfaceBrush = Application.Current.Resources["SurfaceBrush"] as SolidColorBrush;
        var textBrush = Application.Current.Resources["TextPrimaryBrush"] as SolidColorBrush;

        if (surfaceBrush is null || textBrush is null)
            return textBrush ?? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x00, 0x00));

        double surfaceLum = RelativeLuminance(surfaceBrush.Color);
        double textLum = RelativeLuminance(textBrush.Color);

        // WCAG contrast ratio: (L1 + 0.05) / (L2 + 0.05) where L1 > L2
        double lighter = Math.Max(surfaceLum, textLum);
        double darker = Math.Min(surfaceLum, textLum);
        double contrast = (lighter + 0.05) / (darker + 0.05);

        // If contrast is acceptable (≥3:1 for large text), use the theme's own text color
        if (contrast >= 3.0) return textBrush;

        // Otherwise pick white or black, whichever has higher contrast against the surface
        return surfaceLum > 0.4
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A))  // dark text on light surface
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5)); // light text on dark surface
    }

    private static double RelativeLuminance(Windows.UI.Color c)
    {
        double r = SrgbToLinear(c.R / 255.0);
        double g = SrgbToLinear(c.G / 255.0);
        double b = SrgbToLinear(c.B / 255.0);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double SrgbToLinear(double v) =>
        v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    /// <summary>A track + fill pair wrapped in one Grid, so it can be positioned as a single element.</summary>
    private static Grid BuildBarVisual(BarMeterViewModel vm)
    {
        var track = new Border
        {
            Width = vm.Width,
            Height = vm.Height,
            CornerRadius = new CornerRadius(vm.Height / 2),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };

        var fill = new Border
        {
            Width = vm.Width * vm.FillFraction,
            Height = vm.Height,
            CornerRadius = new CornerRadius(vm.Height / 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"],
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BarMeterViewModel.FillFraction))
                fill.Width = vm.Width * vm.FillFraction;
        };

        var grid = new Grid { Width = vm.Width, Height = vm.Height };
        grid.Children.Add(track);
        grid.Children.Add(fill);
        return grid;
    }

    /// <summary>A rounded background plate + a Polyline redrawn every time the meter gets a new sample.</summary>
    private static Grid BuildGraphVisual(GraphMeterViewModel vm)
    {
        var background = new Border
        {
            Width = vm.Width,
            Height = vm.Height,
            CornerRadius = new CornerRadius(6),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };

        var line = new Polyline
        {
            Stroke = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"],
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        };

        void Redraw()
        {
            var samples = vm.Snapshot();
            var points = new PointCollection();
            if (samples.Length > 1)
            {
                double stepX = vm.Width / (samples.Length - 1);
                for (int i = 0; i < samples.Length; i++)
                {
                    double x = i * stepX;
                    double y = vm.Height - (samples[i] * vm.Height);
                    points.Add(new Windows.Foundation.Point(x, y));
                }
            }
            line.Points = points;
        }

        vm.HistoryUpdated += Redraw;

        var grid = new Grid { Width = vm.Width, Height = vm.Height };
        grid.Children.Add(background);
        grid.Children.Add(line);
        return grid;
    }

    // ── Drag to move (disabled while locked; naturally inert while click-through is on,
    //     since a click-through window never receives pointer events in the first place) ──

    private void RootCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_viewModel.Definition.Locked) return;
        _dragging = true;
        _dragAnchorLocal = e.GetCurrentPoint(RootCanvas).Position;
        RootCanvas.CapturePointer(e.Pointer);
    }

    private void RootCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging) return;

        var current = e.GetCurrentPoint(RootCanvas).Position;
        int deltaX = (int)Math.Round(current.X - _dragAnchorLocal.X);
        int deltaY = (int)Math.Round(current.Y - _dragAnchorLocal.Y);
        if (deltaX == 0 && deltaY == 0) return;

        var pos = AppWindow.Position;
        AppWindow.Move(new PointInt32(pos.X + deltaX, pos.Y + deltaY));
    }

    private void RootCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        RootCanvas.ReleasePointerCapture(e.Pointer);

        var pos = AppWindow.Position;
        PositionChanged?.Invoke(pos.X, pos.Y);
    }
}
