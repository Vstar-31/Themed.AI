using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

        // Dispose the VM when navigating away so its ThemeChanged subscription
        // doesn't linger and trigger stale accent applies.
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    // ── System info UI update ─────────────────────────────────────────────────

    private void UpdateSystemInfoUI()
    {
        ModeText.Text = ViewModel.IsLightMode ? "Light" : "Dark";
        AccentHexText.Text = ViewModel.CurrentAccentHex;
        BuildText.Text = ViewModel.WindowsBuild;

        AccentSwatch.Background = new SolidColorBrush(App.HexToColor(ViewModel.CurrentAccentHex));
    }

    // ── Button handlers ───────────────────────────────────────────────────────

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

        // Use the static App.MainWindow — Window.Current is always null
        // in packaged WinUI 3 apps and must never be used for HWND retrieval.
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
        var theme = App.ThemeService.ActiveTheme;
        if (theme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(theme.WallpaperPath))
            await ViewModel.ApplyWallpaperAsync(theme.WallpaperPath);
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
