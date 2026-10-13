using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.WinUI.Views;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow
{
    private bool _worldNavHooked;

    private void NavStudio_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(StudioPage));
        SetActiveNav(NavStudio);
    }

    private void NavWorld_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(DesktopWorldPage));
        SetActiveNav(NavMore);

        if (_worldNavHooked) return;
        _worldNavHooked = true;
        ContentFrame.Navigated += (_, _) =>
        {
            if (ContentFrame.Content is not DesktopWorldPage)
                SetActiveNavFromCurrentContent();
        };
    }

    private void SetActiveNavFromCurrentContent()
    {
        var button = ContentFrame.Content switch
        {
            StudioPage => NavStudio,
            ThemesPage => NavThemes,
            VibePage => NavThemes,
            ThemeEditorPage => NavThemes,
            SkinsPage => NavWidgets,
            SkinEditorPage => NavWidgets,
            WidgetGeneratorPage => NavWidgets,
            DesktopWorldPage => NavMore,
            SystemIntegrationPage => NavMore,
            GalleryPage => NavMore,
            VibeFinderAIPage => NavMore,
            SettingsPage => NavMore,
            _ => NavStudio
        };
        SetActiveNav(button);
    }
}
