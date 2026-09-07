using ThemeManager.WinUI.Views;
using Microsoft.UI.Xaml;

namespace ThemeManager.WinUI;

public sealed partial class MainWindow
{
    private void NavStudio_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(StudioPage));
        SetActiveNav(NavStudio);
    }
}
