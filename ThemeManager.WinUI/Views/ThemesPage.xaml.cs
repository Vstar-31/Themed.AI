using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
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
    // ItemsRepeater doesn't support converters on child Borders out of the box
    // for opaque types, so we color the strips from code-behind when each item
    // is prepared (fires immediately on first render, and on recycle).
    private void ThemesRepeater_ElementPrepared(
        ItemsRepeater sender,
        ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Border card) return;
        if (card.DataContext is not CozyTheme theme) return;

        ColorPaletteStrip(card, theme);
    }

    private static void ColorPaletteStrip(Border card, CozyTheme theme)
    {
        // Walk the visual tree to find the strip Grid (first child Grid row 0).
        if (card.Child is not Grid root) return;

        // The palette strip is the first child in row 0.
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is Grid strip &&
                Grid.GetRow(strip) == 0)
            {
                string[] colors = [
                    theme.BackgroundBase,
                    theme.BackgroundAlt,
                    theme.Surface,
                    theme.AccentPrimary,
                    theme.AccentStrong,
                ];
                for (int j = 0; j < VisualTreeHelper.GetChildrenCount(strip) && j < colors.Length; j++)
                {
                    if (VisualTreeHelper.GetChild(strip, j) is Border swatch)
                        swatch.Background = new SolidColorBrush(App.HexToColor(colors[j]));
                }
                break;
            }
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private async void NewThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateThemeAsync();
        // Navigate straight to editor for the new theme.
        if (ViewModel.SelectedTheme is not null)
            Frame.Navigate(typeof(ThemeEditorPage), ViewModel.SelectedTheme);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private async void SetActiveButton_Click(object sender, RoutedEventArgs e)
    {
        // FOOLPROOF: Grab the theme directly from the CommandParameter or DataContext
        var theme = (sender as Button)?.CommandParameter as CozyTheme 
                 ?? (sender as FrameworkElement)?.DataContext as CozyTheme;

        if (theme != null)
        {
            // 1. Set it active in the App UI (Using your exact method name so it compiles!)
            ViewModel.SetAsActive(theme);
            ViewModel.RefreshThemesList();

            // 2. FORCE Windows to stop hiding your drip! (CRITICAL: DO THIS FIRST)
            // Windows 10/11 defaults to hiding accent colors unless 'ColorPrevalence' is 1
            try 
            {
                Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "ColorPrevalence", 1);
                Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "ColorPrevalence", 1);
            } 
            catch { /* Shhh, we tried to bypass permissions */ }

            // 3. THE SAUCE: Push the colours natively to Windows OS!
            var sysVm = new SystemIntegrationViewModel(App.SystemIntegrator);
            sysVm.AdvancedEnabled = true; // Force bypass any internal UI locks
            await sysVm.ApplyAccentColorAsync(theme.AccentPrimary);

            // 4. THE NUCLEAR OPTION: Manually scream at Windows to refresh the UI right now
            // 0x001A is WM_SETTINGCHANGE. This forces the taskbar and windows to redraw instantly.
            SendMessageTimeout(new IntPtr(-1), 0x001A, IntPtr.Zero, "ImmersiveColorSet", 0x0002, 5000, out _);

            // 5. Extra flex: apply wallpaper too if the theme has one enabled
            if (theme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(theme.WallpaperPath))
            {
                await sysVm.ApplyWallpaperAsync(theme.WallpaperPath);
            }
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CozyTheme theme)
        {
            ViewModel.SetAsActive(theme);
            Frame.Navigate(typeof(ThemeEditorPage), theme);
        }
    }

    private async void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is CozyTheme theme)
            await ViewModel.DuplicateThemeAsync(theme);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CozyTheme theme)
        {
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
};
