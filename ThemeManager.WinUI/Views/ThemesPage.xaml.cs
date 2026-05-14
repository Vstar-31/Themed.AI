using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Models;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class ThemesPage : Page
{
    public ThemesViewModel ViewModel { get; }

    public ThemesPage()
    {
        InitializeComponent();
        ViewModel = new ThemesViewModel(App.ThemeService);

        // After ItemsRepeater renders, colour the palette strips.
        ThemesRepeater.ElementPrepared += ThemesRepeater_ElementPrepared;
    }

    // ── Palette strip coloring ────────────────────────────────────────────────
    // ItemsRepeater does NOT set DataContext on its children (unlike ListView),
    // so we retrieve the theme directly from ViewModel.Themes via args.Index.
    private void ThemesRepeater_ElementPrepared(
        ItemsRepeater sender,
        ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Border card) return;
        if (args.Index < 0 || args.Index >= ViewModel.Themes.Count) return;

        var theme = ViewModel.Themes[args.Index];
        ColorPaletteStrip(card, theme);
    }

    private static void ColorPaletteStrip(Border card, CozyTheme theme)
    {
        if (card.Child is not Grid root) return;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is Grid strip &&
                Grid.GetRow(strip) == 0)
            {
                string[] colors =
                [
                    theme.BackgroundBase,
                    theme.BackgroundAlt,
                    theme.Surface,
                    theme.AccentPrimary,
                    theme.AccentStrong,
                ];

                for (int j = 0; j < VisualTreeHelper.GetChildrenCount(strip) && j < colors.Length; j++)
                {
                    if (VisualTreeHelper.GetChild(strip, j) is Border swatch)
                        swatch.Background = new SolidColorBrush(
                            App.HexToColor(CozyTheme.NormalizeHex(colors[j])));
                }
                break;
            }
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private async void NewThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateThemeAsync();
        if (ViewModel.SelectedTheme is not null)
            Frame.Navigate(typeof(ThemeEditorPage), ViewModel.SelectedTheme);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private async void SetActiveButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (sender as FrameworkElement)?.Tag as CozyTheme;
        if (theme is null) return;

        // 1. Apply theme inside the app UI.
        ViewModel.SetAsActive(theme);
        ViewModel.RefreshThemesList();

        // 2. Write accent/colorization prefs to registry and flush immediately.
        try
        {
            using (var pKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (pKey != null)
                {
                    pKey.SetValue("SystemUsesLightTheme", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    pKey.SetValue("ColorPrevalence", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    pKey.Flush();
                }
            }

            using (var dKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\DWM"))
            {
                if (dKey != null)
                {
                    dKey.SetValue("ColorPrevalence", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    dKey.Flush();
                }
            }
        }
        catch { /* Registry write failed silently — non-critical. */ }

        // 3. Normalize the hex BEFORE pushing to the registry.
        //    This ensures ColorPicker-derived, harmony-generated, or
        //    unsaved values are all clean #RRGGBB before hitting DWM.
        var sysVm = new SystemIntegrationViewModel(App.SystemIntegrator);
        sysVm.AdvancedEnabled = true;
        string safeAccent = CozyTheme.NormalizeHex(theme.AccentPrimary);
        await sysVm.ApplyAccentColorAsync(safeAccent);

        // 4. Notify shell to reload theme settings without killing Explorer.
        SendMessageTimeout(new IntPtr(-1), 0x001A, IntPtr.Zero, "ImmersiveColorSet",
            0x0002, 5000, out _);

        // 5. Apply wallpaper if the theme has one configured.
        if (theme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(theme.WallpaperPath))
            await sysVm.ApplyWallpaperAsync(theme.WallpaperPath);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (sender as FrameworkElement)?.Tag as CozyTheme;
        if (theme is null) return;
        Frame.Navigate(typeof(ThemeEditorPage), theme);
    }

    private async void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (sender as FrameworkElement)?.Tag as CozyTheme;
        if (theme is not null)
            await ViewModel.DuplicateThemeAsync(theme);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (sender as FrameworkElement)?.Tag as CozyTheme;
        if (theme is null) return;

        var dialog = new ContentDialog
        {
            Title = "Delete theme?",
            Content = $"\"{theme.Name}\" will be permanently deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Default,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ViewModel.DeleteThemeAsync(theme);
    }
}
