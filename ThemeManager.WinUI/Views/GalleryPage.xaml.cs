using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

/// <summary>
/// Phase 8's Local Gallery — browse bundled community packs (themes + widgets) and add whichever
/// ones look good to your own library. No code-behind color-strip logic like
/// <see cref="ThemesPage"/> has (see <c>ElementPrepared</c> there): packs render as nested
/// ItemsRepeaters (one pack section per <see cref="CommunityPack"/>, each with its own inner
/// theme/widget repeater), and a nested repeater has no single stable name code-behind could grab
/// the way <see cref="ThemesPage"/>'s single top-level one does — so the palette swatches bind
/// their color directly via <c>HexToBrushConverter</c> in XAML instead, which works uniformly
/// regardless of nesting and needed no code-behind at all.
/// </summary>
public sealed partial class GalleryPage : Page
{
    public GalleryViewModel ViewModel { get; }

    public GalleryPage()
    {
        InitializeComponent();
        ViewModel = new GalleryViewModel(App.Gallery, App.ThemeService, App.SkinManager);
    }

    private async void AddThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (sender as FrameworkElement)?.Tag as CozyTheme;
        if (theme is not null)
            await ViewModel.AddThemeAsync(theme);
    }

    private async void AddWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        var widget = (sender as FrameworkElement)?.Tag as SkinDefinition;
        if (widget is not null)
            await ViewModel.AddWidgetAsync(widget);
    }
}
