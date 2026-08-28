using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
    private readonly double _scaleFactor;

    private bool _dragging;
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private POINT _dragAnchorScreen;
    private PointInt32 _dragWindowStart;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>Raised when the user finishes dragging the widget to a new spot (screen coordinates).</summary>
    public event Action<double, double>? PositionChanged;

    /// <summary>Raised from the widget's own right-click menu. No payload needed — the owner
    /// (SkinManagerService) already knows which SkinDefinition this window belongs to via its
    /// own subscription closure, same as PositionChanged above.</summary>
    public event Action? EditRequested;
    public event Action? LockToggleRequested;
    public event Action? ResetPositionRequested;
    public event Action? DisableRequested;

    public SkinHostWindow(SkinHostViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Title = viewModel.Definition.Name;

        ConfigurePresenter();
        EnableTransparency();
        SkinWindowInterop.HideFromTaskbarAndAltTab(_hwnd);

        // Query the window's DPI so we can convert between DIPs (used in SkinDefinition,
        // pointer events, and all user-facing coordinates) and physical pixels (used by
        // AppWindow.Move / Resize). Most Windows 11 laptops run at 125–150% scaling.
        try { _scaleFactor = GetDpiForWindow(_hwnd) / 96.0; }
        catch { _scaleFactor = 1.0; }
        if (_scaleFactor <= 0) _scaleFactor = 1.0;

        var def = viewModel.Definition;
        ApplyPosition(def.X, def.Y);
        AppWindow.Resize(new SizeInt32(
            (int)(def.Width * _scaleFactor),
            (int)(def.Height * _scaleFactor)));
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

    public void PrepareForClose()
    {
        try
        {
            // If the widget is currently attached to the desktop layer, it MUST be detached 
            // (reparented back to a normal top-level window) before the HWND is destroyed,
            // otherwise WinUI throws a STATUS_STOWED_EXCEPTION (0xc000027b).
            if (_viewModel.Definition.DesktopLayer)
            {
                DesktopLayerInterop.Detach(_hwnd);
            }

            this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>().SystemBackdrop = null;
        }
        catch { }
    }

    // ── Applied live by SkinManagerService when a setting changes ─────────────

    public void ApplyPosition(double x, double y) =>
        AppWindow.Move(new PointInt32((int)(x * _scaleFactor), (int)(y * _scaleFactor)));

    public void ApplyOpacity(double opacity)
    {
        // Opacity controls the card background plate only — meters always stay fully opaque.
        // At 0 the card is invisible ("modular" mode, meters float directly on the desktop).
        // Above 0 the translucent card shows, giving a visible background plate like Rainmeter
        // skins that use a background image or tinted glass.
        if (opacity <= 0.001)
        {
            CardBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            CardBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            var cardBrush = (SolidColorBrush)Application.Current.Resources["CardBackgroundBrush"];
            var borderBrush = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"];
            var color = cardBrush.Color;
            CardBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(
                (byte)(opacity * 255), color.R, color.G, color.B));
            CardBorder.BorderBrush = borderBrush;
            CardBorder.BorderThickness = new Thickness(1);
        }
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
                IconMeterViewModel icon => BuildIconVisual(icon),
                RingMeterViewModel ring => BuildRingVisual(ring),
                StringMeterViewModel str => BuildStringVisual(str),
                _ => new TextBlock(), // defensive: an unrecognized meter kind renders as an empty label, not a crash
            };

            element.PointerPressed += (s, e) =>
            {
                if (meter.ActionUrl is not null)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(meter.ActionUrl) { UseShellExecute = true });
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        App.LoggerFactory.CreateLogger<SkinHostWindow>()
                            .LogWarning(ex, "Failed to launch action URL: {Url}", meter.ActionUrl);
                    }
                }
            };
            element.PointerEntered += (s, e) =>
            {
                if (meter.ActionUrl is not null)
                {
                    typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                        .SetValue(element, Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand));
                }
            };
            element.PointerExited += (s, e) =>
            {
                typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                    .SetValue(element, null);
            };

            Canvas.SetLeft(element, meter.X);
            Canvas.SetTop(element, meter.Y);
            RootCanvas.Children.Add(element);
        }
    }

    private static TextBlock BuildStringVisual(StringMeterViewModel vm)
    {
        var normalBrush = GetWidgetTextBrush();
        var text = new TextBlock
        {
            Width = vm.Width,
            Height = vm.Height,
            FontSize = vm.FontSize,
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["AppFontFamily"],
            FontWeight = vm.Bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            TextAlignment = vm.CenterText ? TextAlignment.Center : TextAlignment.Left,
            Foreground = normalBrush,
            Text = vm.DisplayText,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        SolidColorBrush? thresholdBrush = vm.HasThreshold ? ParseHexBrush(vm.ThresholdColorHex) : null;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StringMeterViewModel.DisplayText))
                text.Text = vm.DisplayText;
            if (e.PropertyName == nameof(StringMeterViewModel.IsThresholdCrossed) && thresholdBrush is not null)
                text.Foreground = vm.IsThresholdCrossed ? thresholdBrush : normalBrush;
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

    /// <summary>Parses a "#RRGGBB" or "#AARRGGBB" hex string into a <see cref="SolidColorBrush"/>.
    /// Returns a red fallback brush if the string is malformed — threshold colors are user-editable
    /// via the skin editor and hand-editable in the JSON file, so invalid input is plausible.</summary>
    private static SolidColorBrush ParseHexBrush(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            byte a = 0xFF, r, g, b;
            if (hex.Length == 8)
            {
                a = Convert.ToByte(hex[..2], 16);
                r = Convert.ToByte(hex[2..4], 16);
                g = Convert.ToByte(hex[4..6], 16);
                b = Convert.ToByte(hex[6..8], 16);
            }
            else if (hex.Length == 6)
            {
                r = Convert.ToByte(hex[..2], 16);
                g = Convert.ToByte(hex[2..4], 16);
                b = Convert.ToByte(hex[4..6], 16);
            }
            else return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x44, 0x44));

            return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
        }
        catch
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x44, 0x44));
        }
    }

    /// <summary>A track + fill pair wrapped in one Grid, so it can be positioned as a single element.</summary>
    private static Grid BuildBarVisual(BarMeterViewModel vm)
    {
        var normalBrush = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var track = new Border
        {
            Width = vm.Width,
            Height = vm.Height,
            CornerRadius = new CornerRadius(vm.Height / 2),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };

        // A soft light-to-dark sweep along the fill using the theme's own two accent tokens
        // (not a hardcoded color pair) so it reads as "a nicer bar" under any palette, not just
        // the Cozy Café defaults — same reasoning as every other brush lookup in this file.
        var strongBrush = (SolidColorBrush)Application.Current.Resources["StrongAccentBrush"];
        var gradientFill = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 0),
            GradientStops =
            {
                new GradientStop { Color = normalBrush.Color, Offset = 0 },
                new GradientStop { Color = strongBrush.Color, Offset = 1 },
            },
        };

        var fill = new Border
        {
            Width = vm.Width * vm.FillFraction,
            Height = vm.Height,
            CornerRadius = new CornerRadius(vm.Height / 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = gradientFill,
        };

        SolidColorBrush? thresholdBrush = vm.HasThreshold ? ParseHexBrush(vm.ThresholdColorHex) : null;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BarMeterViewModel.FillFraction))
                fill.Width = vm.Width * vm.FillFraction;
            if (e.PropertyName == nameof(BarMeterViewModel.IsThresholdCrossed) && thresholdBrush is not null)
                fill.Background = vm.IsThresholdCrossed ? thresholdBrush : gradientFill;
        };

        var grid = new Grid { Width = vm.Width, Height = vm.Height };
        grid.Children.Add(track);
        grid.Children.Add(fill);
        return grid;
    }

    /// <summary>A font-glyph icon on a softly-tinted rounded chip — same "small rounded container"
    /// language as ColorChipStyle elsewhere in the app, so it reads as a badge rather than a bare
    /// character floating on the transparent canvas. Size follows whichever of Width/Height is
    /// smaller (icons are square regardless of the bounding box the editor gives them), and —
    /// like Bar and Graph — swaps to the threshold color when one's configured and crossed.</summary>
    private static Border BuildIconVisual(IconMeterViewModel vm)
    {
        var normalBrush = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];

        // Tint is a low-alpha cut of the accent color itself, not a separate token, so it's
        // correct for any theme rather than just the Cozy Café defaults.
        var chip = new Border
        {
            Width = vm.Width,
            Height = vm.Height,
            CornerRadius = new CornerRadius(Math.Min(vm.Width, vm.Height) * 0.25),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, normalBrush.Color.R, normalBrush.Color.G, normalBrush.Color.B)),
        };

        var icon = new FontIcon
        {
            Glyph = vm.Glyph,
            Width = vm.Width,
            Height = vm.Height,
            // Was 0.8 (nearly edge-to-edge) when the glyph had no chip behind it to give it
            // context; smaller now so the chip's own padding actually reads as padding.
            FontSize = Math.Max(8, Math.Min(vm.Width, vm.Height) * 0.55),
            Foreground = normalBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        SolidColorBrush? thresholdBrush = vm.HasThreshold ? ParseHexBrush(vm.ThresholdColorHex) : null;

        if (thresholdBrush is not null)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IconMeterViewModel.IsThresholdCrossed))
                    icon.Foreground = vm.IsThresholdCrossed ? thresholdBrush : normalBrush;
            };
        }

        chip.Child = icon;
        return chip;
    }

    /// <summary>A circular percentage gauge: a full pale "track" ring behind, with a colored arc
    /// swept clockwise from the top proportional to FillFraction drawn on top of it — the classic
    /// Rainmeter/iOS-style ring gauge, and the shape Bar/Graph/Icon couldn't cover. Built with
    /// Path + ArcSegment (the standard WinUI technique for a partial ring) rather than the
    /// Ellipse+StrokeDashArray trick — dash-array units are relative to StrokeThickness in
    /// WinUI/UWP, and getting that unit conversion subtly wrong felt like a worse risk to take
    /// blind than the extra trigonometry below.</summary>
    private static Grid BuildRingVisual(RingMeterViewModel vm)
    {
        var normalBrush = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var trackBrush = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"];

        double thickness = Math.Max(3, Math.Min(vm.Width, vm.Height) * 0.12);
        double cx = vm.Width / 2, cy = vm.Height / 2;
        double radius = Math.Min(vm.Width, vm.Height) / 2 - thickness / 2;

        var track = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = trackBrush,
            StrokeThickness = thickness,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var arc = new Microsoft.UI.Xaml.Shapes.Path
        {
            Width = vm.Width,
            Height = vm.Height,
            Stroke = normalBrush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };

        SolidColorBrush? thresholdBrush = vm.HasThreshold ? ParseHexBrush(vm.ThresholdColorHex) : null;

        Windows.Foundation.Point PointOnCircle(double angleDeg)
        {
            // -90 so an angle of 0 sits at 12 o'clock, matching every real ring gauge, rather
            // than the mathematical convention of 0 degrees = 3 o'clock.
            double rad = (angleDeg - 90) * Math.PI / 180.0;
            return new Windows.Foundation.Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
        }

        void Redraw()
        {
            if (vm.FillFraction <= 0.001)
            {
                arc.Data = null; // nothing to draw at 0% — sidesteps relying on how a zero-length ArcSegment renders
                return;
            }

            // An ArcSegment can't represent a full closed circle (start == end at 360° is
            // geometrically degenerate), so this clamps just shy of it — at 99.9% the gap is
            // under half a degree, invisible at any size this app draws widgets.
            double sweepDeg = Math.Min(vm.FillFraction, 0.999) * 360.0;
            var start = PointOnCircle(0);
            var end = PointOnCircle(sweepDeg);

            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Windows.Foundation.Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweepDeg > 180.0,
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            arc.Data = geometry;
        }

        Redraw();

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RingMeterViewModel.FillFraction))
                Redraw();
            if (e.PropertyName == nameof(RingMeterViewModel.IsThresholdCrossed) && thresholdBrush is not null)
                arc.Stroke = vm.IsThresholdCrossed ? thresholdBrush : normalBrush;
        };

        var grid = new Grid { Width = vm.Width, Height = vm.Height };
        grid.Children.Add(track);
        grid.Children.Add(arc);
        return grid;
    }

    /// <summary>A rounded background plate + a soft fading area fill + a Polyline on top, all
    /// redrawn every time the meter gets a new sample. The trio lives inside the background
    /// Border's Child (rather than as three Grid siblings) specifically so WinUI's automatic
    /// child-clipping-to-CornerRadius keeps the area fill's square bottom corners from poking out
    /// past the plate's rounded ones.</summary>
    private static Border BuildGraphVisual(GraphMeterViewModel vm)
    {
        var normalBrush = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var background = new Border
        {
            Width = vm.Width,
            Height = vm.Height,
            CornerRadius = new CornerRadius(6),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };

        // Fades from a translucent cut of the line's own color down to nothing, so it stays
        // correct for any accent color rather than a separately-maintained fill token.
        var areaFill = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = Windows.UI.Color.FromArgb(0x55, normalBrush.Color.R, normalBrush.Color.G, normalBrush.Color.B), Offset = 0 },
                new GradientStop { Color = Windows.UI.Color.FromArgb(0x00, normalBrush.Color.R, normalBrush.Color.G, normalBrush.Color.B), Offset = 1 },
            },
        };
        var area = new Polygon { Fill = areaFill };

        var line = new Polyline
        {
            Stroke = normalBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
        };

        SolidColorBrush? thresholdBrush = vm.HasThreshold ? ParseHexBrush(vm.ThresholdColorHex) : null;

        void Redraw()
        {
            var samples = vm.Snapshot();
            var points = new PointCollection();
            var areaPoints = new PointCollection();
            if (samples.Length > 1)
            {
                double stepX = vm.Width / (samples.Length - 1);
                for (int i = 0; i < samples.Length; i++)
                {
                    double x = i * stepX;
                    double y = vm.Height - (samples[i] * vm.Height);
                    points.Add(new Windows.Foundation.Point(x, y));
                    areaPoints.Add(new Windows.Foundation.Point(x, y));
                }
                // Same points as the line, plus the two bottom corners, to close the outline into
                // a fillable region down to the baseline.
                areaPoints.Add(new Windows.Foundation.Point(vm.Width, vm.Height));
                areaPoints.Add(new Windows.Foundation.Point(0, vm.Height));
            }
            line.Points = points;
            area.Points = areaPoints;
        }

        vm.HistoryUpdated += Redraw;

        if (thresholdBrush is not null)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GraphMeterViewModel.IsThresholdCrossed))
                    line.Stroke = vm.IsThresholdCrossed ? thresholdBrush : normalBrush;
                // Area fill deliberately stays the accent gradient even past the threshold —
                // swapping it to a solid threshold color too would compete with the line for
                // attention; the stroke color change alone is signal enough.
            };
        }

        var overlay = new Grid { Width = vm.Width, Height = vm.Height };
        overlay.Children.Add(area);
        overlay.Children.Add(line);
        background.Child = overlay;
        return background;
    }

    // ── Drag to move (disabled while locked; naturally inert while click-through is on,
    //     since a click-through window never receives pointer events in the first place) ──
    //     Right-click shows a context menu instead of dragging — see ShowContextMenu below.

    private void RootCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootCanvas);

        if (point.Properties.IsRightButtonPressed)
        {
            ShowContextMenu(point.Position);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return; // ignore middle-click etc.
        if (_viewModel.Definition.Locked) return;
        _dragging = true;
        GetCursorPos(out _dragAnchorScreen);
        _dragWindowStart = AppWindow.Position;
        RootCanvas.CapturePointer(e.Pointer);
    }

    private const int SnapThresholdPhysicalPx = 24; // ~16 DIPs at 150% scaling

    private void RootCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging) return;

        GetCursorPos(out var currentScreen);
        int deltaX = currentScreen.X - _dragAnchorScreen.X;
        int deltaY = currentScreen.Y - _dragAnchorScreen.Y;
        if (deltaX == 0 && deltaY == 0) return;

        var (newX, newY) = SnapToScreenEdges(_dragWindowStart.X + deltaX, _dragWindowStart.Y + deltaY);
        AppWindow.Move(new PointInt32(newX, newY));
    }

    /// <summary>Pulls the widget flush against a screen edge once it's within a few DIPs of one,
    /// using the work area (excludes the taskbar) of whichever monitor the widget is actually on
    /// right now — not always the primary display. Snapping is a nicety, never worth breaking
    /// the drag over, so any lookup failure just falls back to the unsnapped position.</summary>
    private (int X, int Y) SnapToScreenEdges(int x, int y)
    {
        try
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            if (displayArea is null) return (x, y);

            var work = displayArea.WorkArea;
            int width = AppWindow.Size.Width;
            int height = AppWindow.Size.Height;

            if (Math.Abs(x - work.X) <= SnapThresholdPhysicalPx)
                x = work.X;
            else if (Math.Abs((x + width) - (work.X + work.Width)) <= SnapThresholdPhysicalPx)
                x = work.X + work.Width - width;

            if (Math.Abs(y - work.Y) <= SnapThresholdPhysicalPx)
                y = work.Y;
            else if (Math.Abs((y + height) - (work.Y + work.Height)) <= SnapThresholdPhysicalPx)
                y = work.Y + work.Height - height;

            return (x, y);
        }
        catch
        {
            return (x, y);
        }
    }

    private void RootCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        RootCanvas.ReleasePointerCapture(e.Pointer);

        // Convert physical-pixel position back to DIPs for storage in SkinDefinition.
        var pos = AppWindow.Position;
        PositionChanged?.Invoke(pos.X / _scaleFactor, pos.Y / _scaleFactor);
    }

    // ── Right-click menu ──────────────────────────────────────────────────────

    /// <summary>Built fresh on every right-click (not cached) so the Lock/Unlock label always
    /// reflects the widget's current state, including changes made elsewhere (e.g. the Widgets
    /// page) while this window has stayed open.</summary>
    private void ShowContextMenu(Windows.Foundation.Point position)
    {
        var menu = new MenuFlyout();

        var edit = new MenuFlyoutItem { Text = "Edit" };
        edit.Click += (_, _) => EditRequested?.Invoke();
        menu.Items.Add(edit);

        var lockItem = new MenuFlyoutItem { Text = _viewModel.Definition.Locked ? "Unlock" : "Lock" };
        lockItem.Click += (_, _) => LockToggleRequested?.Invoke();
        menu.Items.Add(lockItem);

        var reset = new MenuFlyoutItem { Text = "Reset Position" };
        reset.Click += (_, _) => ResetPositionRequested?.Invoke();
        menu.Items.Add(reset);

        menu.Items.Add(new MenuFlyoutSeparator());

        var disable = new MenuFlyoutItem { Text = "Disable" };
        disable.Click += (_, _) => DisableRequested?.Invoke();
        menu.Items.Add(disable);

        menu.ShowAt(RootCanvas, new FlyoutShowOptions { Position = position });
    }
}
