using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

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
        {
            ViewModel.ToggleMasterVisibility();
        }
    }

    private async void NewWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateSkinAsync();
        if (ViewModel.SelectedSkin is not null)
            Frame.Navigate(typeof(SkinEditorPage), ViewModel.SelectedSkin);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        Frame.Navigate(typeof(SkinEditorPage), skin);
    }
}
