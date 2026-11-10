using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Services;
using ThemeManager.WinUI.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ThemeManager.WinUI.Views;

public sealed partial class SystemIntegrationPage : Page
{
    public SystemIntegrationViewModel ViewModel { get; }

    public SystemIntegrationPage()
    {
        InitializeComponent();
        ViewModel = new SystemIntegrationViewModel(App.SystemIntegrator);

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemIntegrationViewModel.IsBusy))
                BusyRing.IsActive = ViewModel.IsBusy;
        };

        Loaded += async (_, _) =>
        {
            await ViewModel.RefreshSystemInfoAsync();
            UpdateSystemInfoUI();
        };

        Unloaded += (_, _) => ViewModel.Dispose();
    }

    private void UpdateSystemInfoUI()
    {
        ModeText.Text = ViewModel.IsLightMode ? "Light" : "Dark";
        AccentHexText.Text = ViewModel.CurrentAccentHex;
        BuildText.Text = ViewModel.WindowsBuild;
        AccentSwatch.Background = new SolidColorBrush(App.HexToColor(ViewModel.CurrentAccentHex));
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshSystemInfoAsync();
        UpdateSystemInfoUI();
    }

    private async void BrowseWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            App.ThemeService.ActiveTheme.WallpaperPath = file.Path;
            ViewModel.StatusMessage = $"Wallpaper selected: {file.Name}";
        }
    }

    private async void ApplyThemeButton_Click(object sender, RoutedEventArgs e)
    {
        // Re-publish the current theme so every platform integration, including Windows
        // application/system appearance, re-applies even when Windows was changed externally.
        ThemeChangeHub.Publish(App.ThemeService.ActiveTheme);

        var theme = App.ThemeService.ActiveTheme;
        if (theme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(theme.WallpaperPath))
            await ViewModel.ApplyWallpaperAsync(theme.WallpaperPath);

        await ViewModel.RefreshSystemInfoAsync();
        UpdateSystemInfoUI();
        ViewModel.StatusMessage = theme.ApplyToWindowsApps
            ? "Themed.AI appearance re-applied to supported Windows apps."
            : "Windows app appearance sync is disabled for this theme.";
    }

    private async void ApplyAccentButton_Click(object sender, RoutedEventArgs e)
    {
        var accent = App.ThemeService.ActiveTheme.AccentPrimary;
        await ViewModel.ApplyAccentColorAsync(accent);
        await ViewModel.RefreshSystemInfoAsync();
        UpdateSystemInfoUI();
    }

    private async void ResetAccentButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ResetAccentAsync();
        await ViewModel.RefreshSystemInfoAsync();
        UpdateSystemInfoUI();
    }
}
