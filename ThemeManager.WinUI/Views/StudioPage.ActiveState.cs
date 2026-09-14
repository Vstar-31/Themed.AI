using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private Button? _setActiveWorldButton;
    private bool _activeStateUiInstalled;

    private readonly object _activeStateUiHook = AttachActiveStateUiHooks();

    private object AttachActiveStateUiHooks()
    {
        Loaded += StudioPage_ActiveStateLoaded;
        Unloaded += StudioPage_ActiveStateUnloaded;
        return new object();
    }

    private void StudioPage_ActiveStateLoaded(object sender, RoutedEventArgs e)
    {
        if (_activeStateUiInstalled) return;
        _activeStateUiInstalled = true;

        // Selecting a world in Studio is a preview operation. Activation is explicit.
        ScenesList.SelectionMode = ListViewSelectionMode.None;
        ScenesList.IsItemClickEnabled = true;
        ScenesList.ItemClick += ScenesList_ItemClickForPreview;

        AddSetActiveButton();
    }

    private void StudioPage_ActiveStateUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_activeStateUiInstalled) return;
        ScenesList.ItemClick -= ScenesList_ItemClickForPreview;
        _activeStateUiInstalled = false;
    }

    private void ScenesList_ItemClickForPreview(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WorldListItem item)
            Select(item.Scene, false);
    }

    private void AddSetActiveButton()
    {
        if (_setActiveWorldButton is not null) return;
        if (ScenesList.Parent is not Grid worldsGrid) return;

        worldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _setActiveWorldButton = new Button
        {
            Content = "Set selected world active",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(14, 10, 14, 10)
        };
        _setActiveWorldButton.Click += SetSelectedWorldActive_Click;
        Grid.SetRow(_setActiveWorldButton, 2);
        worldsGrid.Children.Add(_setActiveWorldButton);
    }

    private void SetSelectedWorldActive_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ApplyStatus.Text = "Select a world first.";
            return;
        }

        App.SceneService.SetActiveScene(_selected);
        ApplyStatus.Text = $"{_selected.Name} is now the active world ✓";
    }
}
