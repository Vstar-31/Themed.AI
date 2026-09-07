using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.WinUI.Views;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow
{
    private void NavStudio_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(StudioPage));
        SetActiveNav(NavStudio);
    }

    private void NavWorld_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(DesktopWorldPage));
        SetActiveNav(NavWorld);
        NavWorld.Style = (Style)Application.Current.Resources["NavItemActiveStyle"];
    }
}
