using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinsPage : Page
{
    public SkinsViewModel ViewModel { get; }

    public SkinsPage()
    {
        InitializeComponent();
        ViewModel = new SkinsViewModel(App.SkinManager);
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    private async void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var ts = (ToggleSwitch)sender;
        if (ts.FocusState == FocusState.Unfocused) return;
        var skin = ts.Tag as SkinDefinition;
        if (skin is null || skin.Enabled == ts.IsOn) return;
        await ViewModel.ToggleEnabledAsync(skin, ts.IsOn);
    }

    private async void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var slider = (Slider)sender;
        if (slider.FocusState == FocusState.Unfocused) return;
        var skin = slider.Tag as SkinDefinition;
        if (skin is null || Math.Abs(skin.Opacity - e.NewValue) < 0.001) return;
        await ViewModel.SetOpacityAsync(skin, e.NewValue);
    }

    private async void ClickThroughToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var ts = (ToggleSwitch)sender;
        if (ts.FocusState == FocusState.Unfocused) return;
        var skin = ts.Tag as SkinDefinition;
        if (skin is null || skin.ClickThrough == ts.IsOn) return;
        await ViewModel.ToggleClickThroughAsync(skin, ts.IsOn);
    }

    private async void AlwaysOnTopToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var ts = (ToggleSwitch)sender;
        if (ts.FocusState == FocusState.Unfocused) return;
        var skin = ts.Tag as SkinDefinition;
        if (skin is null || skin.AlwaysOnTop == ts.IsOn) return;
        await ViewModel.ToggleAlwaysOnTopAsync(skin, ts.IsOn);
    }

    private async void LockedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var ts = (ToggleSwitch)sender;
        if (ts.FocusState == FocusState.Unfocused) return;
        var skin = ts.Tag as SkinDefinition;
        if (skin is null || skin.Locked == ts.IsOn) return;
        await ViewModel.ToggleLockedAsync(skin, ts.IsOn);
    }

    private async void ResetPositionButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        await ViewModel.ResetPositionAsync(skin);
    }

    private void MasterVisibilityToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var ts = sender as ToggleSwitch;
        if (ts != null && ts.FocusState != FocusState.Unfocused)
            ViewModel.ToggleMasterVisibility();
    }

    private void GenerateWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindow?.NavigateToWidgetGenerator();
    }

    private bool _packageOperation;

    private async void ImportWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_packageOperation) return;
        _packageOperation = true;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add(".themedwidget");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            ViewModel.StatusMessage = "Importing widget and images…";
            await ViewModel.ImportPackageAsync(file.Path);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Could not import widget: {ex.Message}";
        }
        finally { _packageOperation = false; }
    }

    private async void ExportWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null || _packageOperation) return;
        _packageOperation = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Export widget",
                Content = "Images will be bundled. Weather, VibeFinder, Web/JSON and plugin connection settings are omitted. Review custom text, links and variables before sharing.",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "ThemedAI-widget",
            };
            picker.FileTypeChoices.Add("Themed.AI widget", new[] { ".themedwidget" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            ViewModel.StatusMessage = "Exporting widget and images…";
            await ViewModel.ExportPackageAsync(skin, file.Path);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Could not export widget: {ex.Message}";
        }
        finally { _packageOperation = false; }
    }

    private void NewWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        var names = ViewModel.Skins.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string name = "New Widget";
        for (int suffix = 2; names.Contains(name); suffix++) name = "New Widget " + suffix;
        var draft = new SkinDefinition { Name = name, Enabled = false, X = 60, Y = 60, Width = 200, Height = 100 };
        Frame.Navigate(typeof(SkinEditorPage), new SkinEditorDraft(draft));
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        Frame.Navigate(typeof(SkinEditorPage), skin);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete this widget?",
            Content = $"\"{skin.Name}\" will be removed for good — this can't be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteSkinAsync(skin);
    }
}
