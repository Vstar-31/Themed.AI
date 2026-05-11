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

    private void SetActiveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is CozyTheme theme)
            ViewModel.SetAsActive(theme);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is CozyTheme theme)
            Frame.Navigate(typeof(ThemeEditorPage), theme);
    }

    private async void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is CozyTheme theme)
            await ViewModel.DuplicateThemeAsync(theme);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CozyTheme theme) return;

        var dialog = new ContentDialog
        {
            Title             = "Delete theme?",
            Content           = $"\"{theme.Name}\" will be permanently deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText   = "Cancel",
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = XamlRoot,
            RequestedTheme    = ElementTheme.Default,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ViewModel.DeleteThemeAsync(theme);
    }
}
