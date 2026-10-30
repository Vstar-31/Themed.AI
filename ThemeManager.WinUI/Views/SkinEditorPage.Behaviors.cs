using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private readonly Dictionary<MeterEditorItem, MeterStateController> _previewStates = new();
    private readonly System.Diagnostics.Stopwatch _previewClock = System.Diagnostics.Stopwatch.StartNew();
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _previewStateTimer;
    private bool _behaviorPreviewVisible;

    private void MoveStateEarlier_Click(object sender, RoutedEventArgs e) => ViewModel.MoveState(-1);
    private void MoveStateLater_Click(object sender, RoutedEventArgs e) => ViewModel.MoveState(1);
    private void AddMeterState_Click(object sender, RoutedEventArgs e) => ViewModel.AddState();
    private void DuplicateMeterState_Click(object sender, RoutedEventArgs e) => ViewModel.AddState(duplicate: true);
    private void RemoveMeterState_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveState();
    private void AddResponsiveRule_Click(object sender, RoutedEventArgs e) => ViewModel.AnchorEditor?.AddRule();
    private void RemoveResponsiveRule_Click(object sender, RoutedEventArgs e) => ViewModel.AnchorEditor?.RemoveRule();
    private void ResetAnchorMargins_Click(object sender, RoutedEventArgs e) => ViewModel.AnchorEditor?.ResetMargins();

    private void AttachStatePreview(MeterEditorItem meter, Border container)
    {
        _previewStates.Remove(meter);
        if (container.Child is not FrameworkElement content) return;
        if (meter.Definition.States is { Count: > 0 } states)
        {
            _previewStates[meter] = new MeterStateController(content, meter.Definition, states, ViewModel.PreviewMeasure);
        }
        else
        {
            content.Opacity = Math.Clamp(meter.Definition.Opacity, 0, 1);
            content.RenderTransform = new RotateTransform
            {
                Angle = meter.Definition.Rotation,
                CenterX = meter.Width / 2,
                CenterY = meter.Height / 2
            };
        }
    }

    private void SetPreviewPointer(MeterEditorItem meter, bool? hover, bool? pressed)
    {
        if (!_previewStates.TryGetValue(meter, out var controller)) return;
        if (hover is { } h) controller.Hovered = ViewModel.PreviewInteractions && h;
        if (pressed is { } p) controller.Pressed = ViewModel.PreviewInteractions && p;
        EvaluatePreviewStates();
    }

    private void RefreshBehaviorPreview()
    {
        DrawAnchorPreview();
        EvaluatePreviewStates();
    }

    private void EvaluatePreviewStates()
    {
        if (!_behaviorPreviewVisible) return;
        bool animating = false;
        foreach (var controller in _previewStates.Values)
        {
            if (!ViewModel.PreviewInteractions) controller.Hovered = controller.Pressed = false;
            controller.WidgetActive = ViewModel.PreviewActive;
            controller.Evaluate(_previewClock.ElapsedMilliseconds);
            animating |= controller.IsAnimating;
        }
        if (animating)
        {
            if (_previewStateTimer is null)
            {
                _previewStateTimer = DispatcherQueue.CreateTimer();
                _previewStateTimer.Interval = TimeSpan.FromMilliseconds(16);
                _previewStateTimer.Tick += (_, _) => EvaluatePreviewStates();
            }
            if (!_previewStateTimer.IsRunning) _previewStateTimer.Start();
        }
        else _previewStateTimer?.Stop();
    }

    private void StopBehaviorPreview()
    {
        FinishDrag();
        _behaviorPreviewVisible = false;
        _previewStateTimer?.Stop();
        _dragging = false;
        _dragTarget = null;
        foreach (var controller in _previewStates.Values) controller.Hovered = controller.Pressed = false;
    }

    private void DrawAnchorPreview()
    {
        if (AnchorPreviewCanvas is null) return;
        var layout = ViewModel.ResolveAnchorPreview();
        double factor = Math.Min(240 / ViewModel.PreviewAreaWidth, 135 / ViewModel.PreviewAreaHeight);
        double areaWidth = ViewModel.PreviewAreaWidth * factor, areaHeight = ViewModel.PreviewAreaHeight * factor;
        double left = (240 - areaWidth) / 2, top = (135 - areaHeight) / 2;
        AnchorPreviewCanvas.Children.Clear();
        AnchorPreviewCanvas.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 240, 135) };
        var area = new Rectangle
        {
            Width = areaWidth, Height = areaHeight,
            Stroke = (Brush)Application.Current.Resources["BorderSubtleBrush"], StrokeThickness = 1
        };
        Canvas.SetLeft(area, left); Canvas.SetTop(area, top);
        AnchorPreviewCanvas.Children.Add(area);
        var widget = new Rectangle
        {
            Width = Math.Max(2, ViewModel.Width * layout.Scale * factor),
            Height = Math.Max(2, ViewModel.Height * layout.Scale * factor),
            Fill = (Brush)Application.Current.Resources["PrimaryAccentBrush"],
            Opacity = layout.Hidden ? 0.2 : 0.85,
            RadiusX = 2, RadiusY = 2
        };
        Canvas.SetLeft(widget, left + layout.X * factor);
        Canvas.SetTop(widget, top + layout.Y * factor);
        AnchorPreviewCanvas.Children.Add(widget);
        AnchorPreviewDescription.Text = layout.Hidden
            ? "Hidden at this sample size. Increase the sample area to check other rules."
            : $"{layout.Edge}: ({layout.X:0}, {layout.Y:0}) DIP · {layout.Scale:0.##}× scale · {(layout.ActiveRule is null ? "base placement" : "responsive rule")}.";
    }
}
