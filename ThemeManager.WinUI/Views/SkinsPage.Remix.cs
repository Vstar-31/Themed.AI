using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinsPage
{
    private void OpenRemix(SkinDefinition source)
    {
        try
        {
            var draft = WidgetRemix.Create(source, ViewModel.Skins.Select(s => s.Name));
            Frame.Navigate(typeof(SkinEditorPage), new SkinEditorDraft(draft));
        }
        catch (ArgumentException ex) { ViewModel.StatusMessage = $"Could not remix this widget: {ex.Message}"; }
    }

    private void RemixWidget_Click(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.Tag is SkinDefinition source) OpenRemix(source);
    }

    private bool _templatePickerOpen;
    private async void BrowseTemplates_Click(object sender, RoutedEventArgs args)
    {
        if (_templatePickerOpen) return;
        _templatePickerOpen = true;
        try
        {
            var templates = WidgetStarterCatalog.Create();
            var search = new TextBox { PlaceholderText = "Search name, data source or meter type", MaxLength = 120 };
            AutomationProperties.SetName(search, "Search starter widgets");
            var list = new ListView { ItemsSource = templates, DisplayMemberPath = "Name", Height = 152, SelectionMode = ListViewSelectionMode.Single };
            AutomationProperties.SetName(list, "Starter widgets");
            var results = new TextBlock { TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetLiveSetting(results, AutomationLiveSetting.Polite);
            var detail = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var preview = new Border
            {
                Height = 184, Padding = new Thickness(12), CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"], BorderThickness = new Thickness(1)
            };
            var caption = new TextBlock
            {
                Text = "Sample data · colors follow your current theme. Nothing runs until you save your copy.",
                TextWrapping = TextWrapping.Wrap, FontSize = 12
            };
            var content = new StackPanel { Spacing = 10, MinWidth = 260, MaxWidth = 480 };
            content.Children.Add(search); content.Children.Add(results); content.Children.Add(list);
            content.Children.Add(preview); content.Children.Add(detail); content.Children.Add(caption);
            var scroll = new ScrollViewer { Content = content, MaxHeight = 520,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var dialog = new ContentDialog { Title = "Remix a starter widget", Content = scroll, XamlRoot = XamlRoot,
                PrimaryButtonText = "Open in editor", CloseButtonText = "Cancel", IsPrimaryButtonEnabled = false };
            void UpdateSelection()
            {
                dialog.IsPrimaryButtonEnabled = list.SelectedItem is WidgetStarter;
                if (list.SelectedItem is WidgetStarter selected)
                {
                    var skin = selected.Definition;
                    detail.Text = $"{selected.Summary}\n{skin.Width:0} × {skin.Height:0} DIP · {skin.Meters.Count} meters · {skin.Measures.Count} data sources";
                    preview.Child = BuildStarterPreview(skin);
                    AutomationProperties.SetName(preview, $"Sample preview of {selected.Name}");
                }
                else
                {
                    detail.Text = "Select a starter to preview and customize.";
                    preview.Child = new TextBlock { Text = "Choose a starter to see its design", TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    AutomationProperties.SetName(preview, "No starter selected");
                }
            }
            list.SelectionChanged += (_, _) => UpdateSelection();
            search.TextChanged += (_, _) =>
            {
                var selected = list.SelectedItem as WidgetStarter;
                var matches = WidgetStarterCatalog.Search(templates, search.Text);
                list.ItemsSource = matches;
                list.SelectedItem = selected is not null && matches.Contains(selected) ? selected : null;
                results.Text = matches.Count == 0 ? "No matching starters. Try clock, CPU, network or ring." :
                    $"{matches.Count} of {templates.Count} starters";
                UpdateSelection();
            };
            results.Text = $"{templates.Count} starters";
            UpdateSelection();
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && IsLoaded && list.SelectedItem is WidgetStarter chosenStarter)
                OpenRemix(chosenStarter.Definition);
        }
        finally { _templatePickerOpen = false; }
    }

    private static FrameworkElement BuildStarterPreview(SkinDefinition skin)
    {
        // This lightweight renderer is intentionally limited to bundled, ungrouped primitive designs.
        // New complex starter types fall back to a label instead of loading files or embedded pages.
        if (skin.Groups.Count > 0 || skin.Meters.Any(m => m.Kind is not (MeterKind.String or MeterKind.Bar or MeterKind.Graph or MeterKind.Ring)))
            return new TextBlock { Text = "Open this starter in the editor to preview its design.", TextWrapping = TextWrapping.Wrap };
        var measures = skin.Measures.Select(m => new MeasureEditorItem(m, () => { })).ToArray();
        var canvas = new Canvas { Width = skin.Width, Height = skin.Height, IsHitTestVisible = false,
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, skin.Width, skin.Height) } };
        foreach (var definition in skin.Meters.OrderBy(m => m.ZIndex).ThenBy(m => m.Id))
        {
            var meter = new MeterEditorItem(definition, () => { });
            var sample = MeterPreview.Compute(meter, measures);
            meter.ApplyPreview(sample.Text, sample.Fraction);
            var visual = MeterPreviewVisual.Build(meter);
            visual.Opacity = Math.Clamp(definition.Opacity, 0, 1);
            visual.RenderTransform = new RotateTransform { Angle = definition.Rotation, CenterX = meter.Width / 2, CenterY = meter.Height / 2 };
            Canvas.SetLeft(visual, meter.X); Canvas.SetTop(visual, meter.Y); Canvas.SetZIndex(visual, definition.ZIndex);
            canvas.Children.Add(visual);
        }
        return new Viewbox { Child = canvas, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    }
}
