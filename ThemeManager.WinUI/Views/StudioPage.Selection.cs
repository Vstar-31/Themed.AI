using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private void ScenesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScenesList.SelectedItem is DesktopScene scene)
            Select(scene);
    }
}
