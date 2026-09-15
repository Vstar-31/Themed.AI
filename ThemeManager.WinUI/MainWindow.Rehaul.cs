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
        SetActiveNav(NavWorld);

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
            DesktopWorldPage => NavWorld,
            ThemesPage => NavThemes,
            VibePage => NavVibe,
            ThemeEditorPage => NavPreview,
            SystemIntegrationPage => NavSystem,
            SkinsPage => NavWidgets,
            WidgetGeneratorPage => NavWidgetVibe,
            GalleryPage => NavGallery,
            VibeFinderAIPage => NavVibeFinderAI,
            SettingsPage => NavSettings,
            _ => NavThemes
        };
        SetActiveNav(button);
    }
}
