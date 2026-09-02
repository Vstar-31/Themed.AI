using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// The preview canvas is built and redrawn entirely in code-behind rather than via XAML
/// DataTemplates — same reasoning as <see cref="SkinHostWindow"/>: it's much easier to
/// hand-verify a straight-line C# rebuild loop than a template selector + attached-property
/// Canvas positioning combination, and this page can't be test-run before you build it either.
/// </summary>
public sealed partial class SkinEditorPage : Page
{
    public SkinEditorViewModel ViewModel { get; }

    private readonly Dictionary<MeterEditorItem, Border> _previewElements = new();
    private bool _dragging;
    private MeterEditorItem? _dragTarget;
    private Windows.Foundation.Point _dragAnchor;
    private double _dragStartMeterX;
    private double _dragStartMeterY;

    public SkinEditorPage()
    {
        InitializeComponent();
        ViewModel = new SkinEditorViewModel(App.SkinManager);

        ViewModel.Meters.CollectionChanged += (_, _) => RebuildPreview();
        ViewModel.Measures.CollectionChanged += (_, _) => RefreshMeasureComboItems();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SkinDefinition skin)
        {
            ViewModel.LoadSkin(skin);
            RebuildPreview();
            RefreshMeasureComboItems();
            SyncMeasureComboSelection();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkinEditorViewModel.SelectedMeter))
        {
            HighlightSelection();
            SyncMeasureComboSelection();
        }
    }

    // ── Header buttons ───────────────────────────────────────────────────────────

    private void BackButton_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete this widget?",
            Content = $"\"{ViewModel.Name}\" will be removed for good — this can't be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteAsync();
            Frame.GoBack();
        }
    }

    // ── Measures ─────────────────────────────────────────────────────────────────

    private void AddMeasureButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeasure();

    private void RemoveMeasureButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag as MeasureEditorItem is { } item)
            ViewModel.RemoveMeasure(item);
    }

    /// <summary>
    /// Quick-fills a WebJson measure's Target from the chosen preset. A plain SelectionChanged
    /// rather than an x:Bind TwoWay on SelectedItem — the ComboBox is picking from a *different*
    /// list (presets) than what it writes to (the measure's free-text Target), so there's no
    /// single bindable property that means both "what's selected" and "what Target should become".
    /// </summary>
    private void WebJsonPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag as MeasureEditorItem is not { } item) return;
        if ((sender as ComboBox)?.SelectedItem as WebJsonPreset is not { } preset) return;
        item.Target = preset.Target;
    }

    private bool _isSyncingMeasureCombo;

    private void RefreshMeasureComboItems()
    {
        _isSyncingMeasureCombo = true;
        var names = new List<string> { "(static text)" };
        names.AddRange(ViewModel.Measures.Select(m => m.Name));
        MeasureCombo.ItemsSource = names;
        _isSyncingMeasureCombo = false;
        SyncMeasureComboSelection();
    }

    private void SyncMeasureComboSelection()
    {
        _isSyncingMeasureCombo = true;
        var name = ViewModel.SelectedMeter?.MeasureName;
        MeasureCombo.SelectedItem = string.IsNullOrEmpty(name) ? "(static text)" : name;
        _isSyncingMeasureCombo = false;
    }

    private void MeasureCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingMeasureCombo) return;
        if (ViewModel.SelectedMeter is null) return;
        var selected = MeasureCombo.SelectedItem as string;
        ViewModel.SelectedMeter.MeasureName = (selected is null or "(static text)") ? "" : selected;
    }

    // ── Meters ───────────────────────────────────────────────────────────────────

    private void AddStringMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.String);
    private void AddBarMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Bar);
    private void AddGraphMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Graph);
    private void AddIconMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Icon);
    private void AddRingMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Ring);
    private void AddWebEmbedMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.WebEmbed);

    private void RemoveMeterButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag as MeterEditorItem is { } item)
            ViewModel.RemoveMeter(item);
    }

    // ── Preview canvas: build once per Meters-collection change, then just restyle/redraw ──

    private void RebuildPreview()
    {
        // Unsubscribe from old elements before clearing to prevent memory leaks and redundant layout cycles
        foreach (var meter in _previewElements.Keys)
        {
            meter.PropertyChanged -= Meter_PreviewPropertyChangedHandler;
        }

        PreviewCanvas.Children.Clear();
        _previewElements.Clear();

        foreach (var meter in ViewModel.Meters)
        {
            var content = BuildPreviewContent(meter);
            var container = new Border
            {
                Child = content,
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)), // set for real by HighlightSelection()
                CornerRadius = new CornerRadius(4),
            };

            container.PointerPressed += (s, e) => PreviewElement_PointerPressed(meter, container, e);
            container.PointerMoved += (s, e) => PreviewElement_PointerMoved(meter, container, e);
            container.PointerReleased += (s, e) => PreviewElement_PointerReleased(container, e);

            Canvas.SetLeft(container, meter.X);
            Canvas.SetTop(container, meter.Y);
            PreviewCanvas.Children.Add(container);
            _previewElements[meter] = container;

            // Use a dedicated handler method so we can unsubscribe later
            meter.PropertyChanged += Meter_PreviewPropertyChangedHandler;
        }

        HighlightSelection();
    }

    private void Meter_PreviewPropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is MeterEditorItem meter && _previewElements.TryGetValue(meter, out var container))
        {
            Meter_PreviewPropertyChanged(meter, container, args.PropertyName);
        }
    }

    private static FrameworkElement BuildPreviewContent(MeterEditorItem meter)
    {
        if (meter.Kind == MeterKind.String)
        {
            return new TextBlock
            {
                Width = meter.Width,
                Height = meter.Height,
                FontSize = meter.FontSize,
                FontWeight = meter.Bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                TextAlignment = meter.CenterText ? TextAlignment.Center : TextAlignment.Left,
                Foreground = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"],
                Text = meter.PreviewText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
        }

        if (meter.Kind == MeterKind.Icon)
        {
            var iconAccent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
            var chip = new Border
            {
                Width = meter.Width,
                Height = meter.Height,
                CornerRadius = new CornerRadius(Math.Min(meter.Width, meter.Height) * 0.25),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, iconAccent.Color.R, iconAccent.Color.G, iconAccent.Color.B)),
                Child = new FontIcon
                {
                    Width = meter.Width,
                    Height = meter.Height,
                    FontSize = Math.Max(8, Math.Min(meter.Width, meter.Height) * 0.55),
                    Glyph = string.IsNullOrWhiteSpace(meter.IconGlyph) ? "\uE946" : meter.IconGlyph,
                    Foreground = iconAccent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            return chip;
        }

        if (meter.Kind == MeterKind.WebEmbed)
        {
            // No live WebView2 in the editor canvas — same "roughly this, not pixel-perfect"
            // philosophy as the Bar/Graph static-fill preview further down. An outlined
            // placeholder box reads as "something lives here" without pretending to render the
            // actual page.
            var placeholder = new Border
            {
                Width = meter.Width,
                Height = meter.Height,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = "\uE774  Web embed",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons, Segoe UI"),
                    FontSize = 13,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = meter.Width - 12,
                },
            };
            return placeholder;
        }

        if (meter.Kind == MeterKind.Ring)
        {
            // Same geometry as SkinHostWindow.BuildRingVisual, just computed once from
            // meter.PreviewFraction rather than redrawn from a ticking measure — this preview
            // has no live data to tick from, same reasoning as Bar/Graph's static fill below.
            var ringAccent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
            var trackBrush = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"];

            double thickness = Math.Max(3, Math.Min(meter.Width, meter.Height) * 0.12);
            double cx = meter.Width / 2, cy = meter.Height / 2;
            double radius = Math.Min(meter.Width, meter.Height) / 2 - thickness / 2;

            var ringTrack = new Ellipse
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
                Width = meter.Width,
                Height = meter.Height,
                Stroke = ringAccent,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            if (meter.PreviewFraction > 0.001)
            {
                double sweepDeg = Math.Min(meter.PreviewFraction, 0.999) * 360.0;

                Windows.Foundation.Point PointOnCircle(double angleDeg)
                {
                    double rad = (angleDeg - 90) * Math.PI / 180.0;
                    return new Windows.Foundation.Point(cx + radius * Math.Cos(rad), cy + radius * Math.Sin(rad));
                }

                var figure = new PathFigure { StartPoint = PointOnCircle(0), IsClosed = false };
                figure.Segments.Add(new ArcSegment
                {
                    Point = PointOnCircle(sweepDeg),
                    Size = new Windows.Foundation.Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = sweepDeg > 180.0,
                });
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                arc.Data = geometry;
            }

            var ringGrid = new Grid { Width = meter.Width, Height = meter.Height };
            ringGrid.Children.Add(ringTrack);
            ringGrid.Children.Add(arc);
            return ringGrid;
        }

        // Bar and Graph share the same simple "fill preview" visual here — the real scrolling
        // graph only exists where it matters, in the live SkinHostWindow; the editor just needs
        // to convey "roughly this full", which a static fill communicates just as well and is
        // far simpler to keep correct. The gradient below matches SkinHostWindow's Bar fill so
        // the "roughly this" preview is at least color-accurate even where the shape isn't.
        var track = new Border
        {
            Width = meter.Width,
            Height = meter.Height,
            CornerRadius = new CornerRadius(meter.Kind == MeterKind.Bar ? meter.Height / 2 : 4),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };
        var fillAccent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var fillStrong = (SolidColorBrush)Application.Current.Resources["StrongAccentBrush"];
        var fill = new Border
        {
            Width = meter.Width * meter.PreviewFraction,
            Height = meter.Height,
            CornerRadius = new CornerRadius(meter.Kind == MeterKind.Bar ? meter.Height / 2 : 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Color = fillAccent.Color, Offset = 0 },
                    new GradientStop { Color = fillStrong.Color, Offset = 1 },
                },
            },
        };
        var grid = new Grid { Width = meter.Width, Height = meter.Height };
        grid.Children.Add(track);
        grid.Children.Add(fill);
        return grid;
    }

    /// <summary>Keeps an existing preview element in sync as its meter's properties change,
    /// without tearing down and rebuilding the whole canvas for every keystroke.</summary>
    private void Meter_PreviewPropertyChanged(MeterEditorItem meter, Border container, string? propertyName)
    {
        if (propertyName is nameof(MeterEditorItem.X) or nameof(MeterEditorItem.Y))
        {
            Canvas.SetLeft(container, meter.X);
            Canvas.SetTop(container, meter.Y);
            return;
        }

        // Anything else (size, text, fraction, font, ...) — simplest correct approach is to
        // rebuild just this one element's content rather than trying to patch every possible
        // field individually.
        container.Child = BuildPreviewContent(meter);
    }

    private void HighlightSelection()
    {
        var accent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var none = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        foreach (var (meter, container) in _previewElements)
            container.BorderBrush = meter == ViewModel.SelectedMeter ? accent : none;
    }

    // ── Drag to reposition a meter within the preview (local Canvas coords — no AppWindow involved) ──

    private void PreviewElement_PointerPressed(MeterEditorItem meter, Border container, PointerRoutedEventArgs e)
    {
        ViewModel.SelectedMeter = meter;
        _dragging = true;
        _dragTarget = meter;
        _dragStartMeterX = meter.X;
        _dragStartMeterY = meter.Y;
        _dragAnchor = e.GetCurrentPoint(PreviewCanvas).Position;
        container.CapturePointer(e.Pointer);
    }

    private void PreviewElement_PointerMoved(MeterEditorItem meter, Border container, PointerRoutedEventArgs e)
    {
        if (!_dragging || !ReferenceEquals(_dragTarget, meter)) return;

        var current = e.GetCurrentPoint(PreviewCanvas).Position;
        double deltaX = current.X - _dragAnchor.X;
        double deltaY = current.Y - _dragAnchor.Y;
        if (deltaX == 0 && deltaY == 0) return;

        double targetX = _dragStartMeterX + deltaX;
        double targetY = _dragStartMeterY + deltaY;

        if (ViewModel.SnapToGrid)
        {
            targetX = Math.Round(targetX / 10.0) * 10.0;
            targetY = Math.Round(targetY / 10.0) * 10.0;
        }

        meter.MoveTo(targetX, targetY);
    }

    private void PreviewElement_PointerReleased(Border container, PointerRoutedEventArgs e)
    {
        _dragging = false;
        _dragTarget = null;
        container.ReleasePointerCapture(e.Pointer);
    }
}
