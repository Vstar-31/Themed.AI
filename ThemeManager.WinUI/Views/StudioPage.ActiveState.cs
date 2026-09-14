using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private bool _activeStateUiInstalled;

    private void StudioPage_ActiveStateLoaded(object sender, RoutedEventArgs e)
    {
        if (_activeStateUiInstalled) return;
        _activeStateUiInstalled = true;
        UpdateSetActiveButtonState();
    }

    private void StudioPage_ActiveStateUnloaded(object sender, RoutedEventArgs e)
    {
        _activeStateUiInstalled = false;
    }

    private void UpdateSetActiveButtonState()
    {
        if (SetActiveWorldButton is null) return;

        var active = _selected is not null &&
                     ReferenceEquals(_selected, App.SceneService.ActiveScene);

        SetActiveWorldButton.Content = active ? "Active world ✓" : "Set selected world active";
        SetActiveWorldButton.IsEnabled = _selected is not null && !active;
    }

    private void SetSelectedWorldActive_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ApplyStatus.Text = "Select a world first.";
            return;
        }

        App.SceneService.SetActiveScene(_selected);
        UpdateSetActiveButtonState();
        ApplyStatus.Text = $"{_selected.Name} is now the active world ✓";
    }
}
