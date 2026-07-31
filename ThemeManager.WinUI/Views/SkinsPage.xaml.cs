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
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        await ViewModel.ToggleEnabledAsync(skin, ((ToggleSwitch)sender).IsOn);
    }

    private async void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        await ViewModel.SetOpacityAsync(skin, e.NewValue);
    }

    private async void ClickThroughToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        await ViewModel.ToggleClickThroughAsync(skin, ((ToggleSwitch)sender).IsOn);
    }

    private async void LockedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        await ViewModel.ToggleLockedAsync(skin, ((ToggleSwitch)sender).IsOn);
    }

    private async void ResetPositionButton_Click(object sender, RoutedEventArgs e)
    {
        var skin = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (skin is null) return;
        await ViewModel.ResetPositionAsync(skin);
    }
}
