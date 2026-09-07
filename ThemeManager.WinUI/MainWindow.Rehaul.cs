using Microsoft.UI.Xaml;
using ThemeManager.WinUI.Views;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow
{
    private void NavStudio_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(StudioPage));
        NavStudio.Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["NavItemActiveStyle"];
        SetActiveNav(NavStudio);
    }
}
