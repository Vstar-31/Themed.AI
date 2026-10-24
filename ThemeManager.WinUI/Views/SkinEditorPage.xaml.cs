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
    private readonly Dictionary<GroupEditorItem, Canvas> _previewGroups = new();
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
        ViewModel.Groups.CollectionChanged += (_, _) => RebuildPreview();
        ViewModel.Measures.CollectionChanged += (_, _) => RefreshMeasureComboItems();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.BehaviorPreviewChanged += RefreshBehaviorPreview;
        ViewModel.BehaviorPreviewChanged += RefreshLayerTree;
        ViewModel.BehaviorStructureChanged += RebuildPreview;
        ViewModel.DraftRestored += () =>
        {
            RebuildPreview();
            RefreshMeasureComboItems();
            RefreshGroupSelection();
            _layerSignature = "";
            RefreshLayerTree();
            SelectLayerNode(ViewModel.SelectedMeter is { } meter ? "meter:" + meter.Definition.Id :
                ViewModel.SelectedGroup is { } group ? "group:" + group.Definition.Id : null);
        };
        Loaded += (_, _) => { _behaviorPreviewVisible = true; RefreshBehaviorPreview(); };
        Unloaded += (_, _) => StopBehaviorPreview();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var skin = e.Parameter is SkinEditorDraft draft ? draft.Definition : e.Parameter as SkinDefinition;
        if (skin is not null)
        {
            ViewModel.LoadSkin(skin, e.Parameter is SkinEditorDraft);
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
            if (ViewModel.SelectedMeter is { } meter) SelectLayerNode("meter:" + meter.Definition.Id);
        }
        else if (e.PropertyName == nameof(SkinEditorViewModel.SelectedGroup) && ViewModel.SelectedMeter is null)
        {
            SelectLayerNode(ViewModel.SelectedGroup is { } group ? "group:" + group.Definition.Id : null);
        }
    }

    // ── Header buttons ───────────────────────────────────────────────────────────

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Dirty)
        {
            var dialog = new ContentDialog
            {
                Title = "Discard unsaved changes?",
                Content = "Your running widget will keep its last saved settings.",
                PrimaryButtonText = "Discard changes",
                CloseButtonText = "Keep editing",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private bool _saving;

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_saving) return;
        _saving = true;
        try
        {
            if (await ViewModel.SaveAsync() && !ViewModel.Dirty && Frame.CanGoBack) Frame.GoBack();
        }
        finally { _saving = false; }
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
        if (ViewModel.IsRestoringDraft) return;
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
        if (ViewModel.IsRestoringDraft) return;
        if (_isSyncingMeasureCombo) return;
        if (ViewModel.SelectedMeter is null) return;
        var selected = MeasureCombo.SelectedItem as string;
        ViewModel.SelectedMeter.MeasureName = (selected is null or "(static text)") ? "" : selected;
    }


    private void MetersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MetersList.SelectedItem is MeterEditorItem item)
            ViewModel.SelectedMeter = item;
    }

    private void GroupSelectedMeters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.GroupMeters(MetersList.SelectedItems.OfType<MeterEditorItem>());
        RefreshGroupSelection();
    }

    private void UngroupSelectedMeters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UngroupMeters(MetersList.SelectedItems.OfType<MeterEditorItem>());
        RefreshGroupSelection();
    }

    private void RemoveGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GroupEditorItem group)
            ViewModel.RemoveGroup(group);
        RefreshGroupSelection();
    }

    private void RefreshGroupSelection()
    {
        GroupsList.SelectedItem = ViewModel.SelectedGroup;
        HighlightSelection();
    }

    // ── Meters ───────────────────────────────────────────────────────────────────

    private void AddStringMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.String);
    private void AddBarMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Bar);
    private void AddGraphMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Graph);
    private void AddIconMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Icon);
    private void AddRingMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Ring);
    private void AddImageMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.Image);

    private void AddWebEmbedMeterButton_Click(object sender, RoutedEventArgs e) => ViewModel.AddMeter(MeterKind.WebEmbed);

    private void RemoveMeterButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag as MeterEditorItem is { } item)
            ViewModel.RemoveMeter(item);
    }

    // ── Preview canvas: build once per Meters-collection change, then just restyle/redraw ──

    private void RebuildPreview()
    {
        if (ViewModel.IsRestoringDraft) return;
        foreach (var meter in _previewElements.Keys)
            meter.PropertyChanged -= Meter_PreviewPropertyChangedHandler;
        foreach (var group in _previewGroups.Keys)
            group.PropertyChanged -= Group_PreviewPropertyChangedHandler;

        _previewStateTimer?.Stop();
        _previewStates.Clear();
        PreviewCanvas.Children.Clear();
        _previewElements.Clear();
        _previewGroups.Clear();

        var groups = ViewModel.Groups.ToList();
        var groupByMeterId = new Dictionary<string, GroupEditorItem>(StringComparer.OrdinalIgnoreCase);

        // Groups are real preview containers too, so the editor uses the same local-coordinate
        // semantics as the desktop host.
        foreach (var group in groups.OrderBy(g => g.Definition.ZIndexForOrdering(ViewModel.Meters.Select(m => m.Definition).ToList())))
        {
            foreach (var meterId in group.Definition.MeterIds)
                if (!groupByMeterId.ContainsKey(meterId))
                    groupByMeterId[meterId] = group;

            var canvas = new Canvas
            {
                Width = Math.Max(1, group.Width),
                Height = Math.Max(1, group.Height),
                Opacity = Math.Clamp(group.Opacity, 0, 1),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            };

            if (group.Clip)
            {
                canvas.Clip = new RectangleGeometry
                {
                    Rect = new Windows.Foundation.Rect(0, 0, Math.Max(1, group.Width), Math.Max(1, group.Height))
                };
            }

            var transforms = new TransformGroup();
            if (Math.Abs(group.ScaleX - 1) > 0.0001 || Math.Abs(group.ScaleY - 1) > 0.0001)
                transforms.Children.Add(new ScaleTransform { ScaleX = group.ScaleX, ScaleY = group.ScaleY });
            if (Math.Abs(group.Rotation) > 0.0001)
                transforms.Children.Add(new RotateTransform
                {
                    Angle = group.Rotation,
                    CenterX = group.Width / 2,
                    CenterY = group.Height / 2
                });
            if (transforms.Children.Count > 0)
                canvas.RenderTransform = transforms;

            Canvas.SetLeft(canvas, group.X);
            Canvas.SetTop(canvas, group.Y);
            Canvas.SetZIndex(canvas, group.Definition.MeterIds
                .Select(id => ViewModel.Meters.FirstOrDefault(m => m.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Definition.ZIndex ?? 0)
                .DefaultIfEmpty(0).Max());
            PreviewCanvas.Children.Add(canvas);
            _previewGroups[group] = canvas;
            group.PropertyChanged += Group_PreviewPropertyChangedHandler;
        }

        foreach (var meter in ViewModel.Meters.OrderBy(m => m.Definition.ZIndex).ThenBy(m => m.Definition.Id))
        {
            var content = BuildPreviewContent(meter);
            var container = new Border
            {
                Width = meter.Width,
                Height = meter.Height,
                Child = content,
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
            };

            AttachStatePreview(meter, container);
            container.PointerEntered += (_, _) => SetPreviewPointer(meter, true, null);
            container.PointerExited += (_, _) => SetPreviewPointer(meter, false, false);
            container.PointerCanceled += (_, _) => { FinishDrag(); SetPreviewPointer(meter, null, false); };
            container.PointerCaptureLost += (_, _) => { FinishDrag(); SetPreviewPointer(meter, null, false); };
            container.PointerPressed += (s, e) => PreviewElement_PointerPressed(meter, container, e);
            container.PointerMoved += (s, e) => PreviewElement_PointerMoved(meter, container, e);
            container.PointerReleased += (s, e) => PreviewElement_PointerReleased(container, e);

            Canvas.SetLeft(container, meter.X);
            Canvas.SetTop(container, meter.Y);
            Canvas.SetZIndex(container, meter.Definition.ZIndex);

            var group = groupByMeterId.GetValueOrDefault(meter.Definition.Id);
            if (group is not null && _previewGroups.TryGetValue(group, out var groupCanvas))
                groupCanvas.Children.Add(container);
            else
                PreviewCanvas.Children.Add(container);

            _previewElements[meter] = container;
            meter.PropertyChanged += Meter_PreviewPropertyChangedHandler;
        }

        HighlightSelection();
        RefreshBehaviorPreview();
    }

    private void Meter_PreviewPropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is MeterEditorItem meter && _previewElements.TryGetValue(meter, out var container))
        {
            Meter_PreviewPropertyChanged(meter, container, args.PropertyName);
        }
    }

    private void Group_PreviewPropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is not GroupEditorItem group || !_previewGroups.TryGetValue(group, out var canvas))
            return;

        Canvas.SetLeft(canvas, group.X);
        Canvas.SetTop(canvas, group.Y);
        canvas.Width = Math.Max(1, group.Width);
        canvas.Height = Math.Max(1, group.Height);
        canvas.Opacity = Math.Clamp(group.Opacity, 0, 1);
        canvas.Clip = group.Clip
            ? new RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, Math.Max(1, group.Width), Math.Max(1, group.Height))
            }
            : null;

        var transforms = new TransformGroup();
        if (Math.Abs(group.ScaleX - 1) > 0.0001 || Math.Abs(group.ScaleY - 1) > 0.0001)
            transforms.Children.Add(new ScaleTransform { ScaleX = group.ScaleX, ScaleY = group.ScaleY });
        if (Math.Abs(group.Rotation) > 0.0001)
            transforms.Children.Add(new RotateTransform
            {
                Angle = group.Rotation,
                CenterX = group.Width / 2,
                CenterY = group.Height / 2
            });
        canvas.RenderTransform = transforms.Children.Count == 0 ? null : transforms;
        HighlightSelection();
    }

    private static FrameworkElement BuildPreviewContent(MeterEditorItem meter) => MeterPreviewVisual.Build(meter);

    /// <summary>Keeps an existing preview element in sync as its meter's properties change,
    /// without tearing down and rebuilding the whole canvas for every keystroke.</summary>
    private void Meter_PreviewPropertyChanged(MeterEditorItem meter, Border container, string? propertyName)
    {
        if (propertyName is nameof(MeterEditorItem.X) or nameof(MeterEditorItem.Y))
        {
            Canvas.SetLeft(container, meter.X);
            Canvas.SetTop(container, meter.Y);
            Canvas.SetZIndex(container, meter.Definition.ZIndex);
            return;
        }

        // Anything else (size, text, fraction, font, ...) — simplest correct approach is to
        // rebuild just this one element's content rather than trying to patch every possible
        // field individually.
        container.Width = meter.Width;
        container.Height = meter.Height;
        container.Child = BuildPreviewContent(meter);
        AttachStatePreview(meter, container);
        RefreshBehaviorPreview();
    }

    private void HighlightSelection()
    {
        var accent = (SolidColorBrush)Application.Current.Resources["PrimaryAccentBrush"];
        var none = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        foreach (var (meter, container) in _previewElements)
            container.BorderBrush = meter == ViewModel.SelectedMeter ? accent : none;

        foreach (var (group, canvas) in _previewGroups)
            canvas.Opacity = Math.Clamp(group.Opacity, 0, 1) * (group == ViewModel.SelectedGroup ? 0.98 : 1.0);
    }

    // ── Drag to reposition a meter within the preview (local Canvas coords — no AppWindow involved) ──

    private void PreviewElement_PointerPressed(MeterEditorItem meter, Border container, PointerRoutedEventArgs e)
    {
        ViewModel.SelectedMeter = meter;
        if (ViewModel.PreviewInteractions)
        {
            if (e.GetCurrentPoint(container).Properties.IsLeftButtonPressed)
            {
                SetPreviewPointer(meter, true, true);
                container.CapturePointer(e.Pointer);
            }
            return;
        }
        if (!e.GetCurrentPoint(container).Properties.IsLeftButtonPressed) return;
        FinishDrag();
        _dragEdit = ViewModel.BeginEdit();
        _dragging = true;
        _dragTarget = meter;
        _dragStartMeterX = meter.X;
        _dragStartMeterY = meter.Y;
        _dragAnchor = e.GetCurrentPoint((UIElement)container.Parent).Position;
        if (!container.CapturePointer(e.Pointer)) FinishDrag();
    }

    private void PreviewElement_PointerMoved(MeterEditorItem meter, Border container, PointerRoutedEventArgs e)
    {
        if (!_dragging || !ReferenceEquals(_dragTarget, meter)) return;

        var current = e.GetCurrentPoint((UIElement)container.Parent).Position;
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
        FinishDrag();
        foreach (var pair in _previewElements.Where(p => ReferenceEquals(p.Value, container)))
            SetPreviewPointer(pair.Key, null, false);
        container.ReleasePointerCapture(e.Pointer);
    }
}
