using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.WinUI.ViewModels;
using Windows.Storage.Pickers;

namespace ThemeManager.WinUI.Views;

public sealed partial class SystemIntegrationPage : Page
{
    public SystemIntegrationViewModel ViewModel { get; }

    public SystemIntegrationPage()
    {
        InitializeComponent();
        ViewModel = new SystemIntegrationViewModel(App.SystemIntegrator);

        // Extend ViewModel with wallpaper toggle proxy to the active theme.
        // (Simpler than a full binding to a nested property via converter.)
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
    }

    // ── System info UI update ─────────────────────────────────────────────────

    private void UpdateSystemInfoUI()
    {
        ModeText.Text    = ViewModel.IsLightMode ? "Light" : "Dark";
        AccentHexText.Text = ViewModel.CurrentAccentHex;
        BuildText.Text   = ViewModel.WindowsBuild;

        if (App.HexToColor(ViewModel.CurrentAccentHex) is var c)
            AccentSwatch.Background = new SolidColorBrush(c);
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

        // Associate picker with the window handle (required on Windows App SDK).
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            (Application.Current as App) is not null
                ? (Microsoft.UI.Xaml.Window)App.Current.Resources["MainWindow"]!
                : throw new InvalidOperationException("No main window"));

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            // Store path on the active theme.
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
