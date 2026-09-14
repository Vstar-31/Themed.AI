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

        // Selecting a world previews it without changing the global active world.
        ScenesList.SelectionMode = ListViewSelectionMode.None;
        ScenesList.IsItemClickEnabled = true;
        ScenesList.ItemClick += ScenesList_ItemClickForPreview;
        App.SceneService.ActiveSceneChanged += ActiveStateSceneChanged;
        UpdateSetActiveButtonState();
    }

    private void StudioPage_ActiveStateUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_activeStateUiInstalled) return;
        ScenesList.ItemClick -= ScenesList_ItemClickForPreview;
        App.SceneService.ActiveSceneChanged -= ActiveStateSceneChanged;
        _activeStateUiInstalled = false;
    }

    private void ActiveStateSceneChanged(object? sender, ThemeManager.Core.Models.DesktopScene? scene)
    {
        if (DispatcherQueue.HasThreadAccess) UpdateSetActiveButtonState();
        else DispatcherQueue.TryEnqueue(UpdateSetActiveButtonState);
    }

    private void ScenesList_ItemClickForPreview(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WorldListItem item)
        {
            Select(item.Scene, false);
            UpdateSetActiveButtonState();
        }
    }

    private void UpdateSetActiveButtonState()
    {
        if (SetActiveWorldButton is null) return;

        var active = _selected is not null && ReferenceEquals(_selected, App.SceneService.ActiveScene);
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
