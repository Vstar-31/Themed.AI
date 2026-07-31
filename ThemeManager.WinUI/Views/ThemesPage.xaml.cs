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

        ThemesRepeater.ElementPrepared += ThemesRepeater_ElementPrepared;

        Unloaded += (_, _) =>
        {
            ViewModel.Dispose();
        };
    }

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

    private async void NewThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateThemeAsync();
        if (ViewModel.SelectedTheme is not null)
            Frame.Navigate(typeof(ThemeEditorPage), ViewModel.SelectedTheme);
    }

    private async void SetActiveButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (sender as FrameworkElement)?.Tag as CozyTheme;
        if (theme is null) return;

        ViewModel.SetAsActive(theme);
        ViewModel.RefreshThemesList();

        // AccentPrimary is the theme's main interactive color — now that
        // ColorizationColorBalance=100 renders it exactly, this is correct.
        string safeAccent = CozyTheme.NormalizeHex(theme.AccentPrimary);
        await App.SystemIntegrator.ApplyAccentColorAsync(safeAccent);

        if (theme.ApplyToWallpaper && !string.IsNullOrWhiteSpace(theme.WallpaperPath))
            await App.SystemIntegrator.ApplyWallpaperAsync(theme.WallpaperPath);
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
