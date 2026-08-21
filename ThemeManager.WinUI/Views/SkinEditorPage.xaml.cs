using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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
                Foreground = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"],
                Text = meter.PreviewText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
        }

        if (meter.Kind == MeterKind.Icon)
        {
            return new FontIcon
            {
                Width = meter.Width,
                Height = meter.Height,
                FontSize = Math.Max(8, Math.Min(meter.Width, meter.Height) * 0.8),
                Glyph = string.IsNullOrWhiteSpace(meter.IconGlyph) ? "\uE946" : meter.IconGlyph,
                Foreground = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        // Bar and Graph share the same simple "fill preview" visual here — the real scrolling
        // graph only exists where it matters, in the live SkinHostWindow; the editor just needs
        // to convey "roughly this full", which a static fill communicates just as well and is
        // far simpler to keep correct.
        var track = new Border
        {
            Width = meter.Width,
            Height = meter.Height,
            CornerRadius = new CornerRadius(meter.Kind == MeterKind.Bar ? meter.Height / 2 : 4),
            Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        };
        var fill = new Border
        {
            Width = meter.Width * meter.PreviewFraction,
            Height = meter.Height,
            CornerRadius = new CornerRadius(meter.Kind == MeterKind.Bar ? meter.Height / 2 : 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"],
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
