using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.WinUI.ViewModels;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private readonly Dictionary<TreeViewNode, EditorLayerItem> _layerNodes = new();
    private bool _syncingLayers;
    private string _layerSignature = "";
    private string? _selectedLayerKey;

    private void RefreshLayerTree()
    {
        if (ViewModel.IsRestoringDraft || _syncingLayers) return;
        var roots = ViewModel.GetRootLayers();
        // Geometry and animation changes do not rebuild the tree or interrupt its keyboard focus.
        string signature = System.Text.Json.JsonSerializer.Serialize(roots.Select(layer => new
        {
            layer.Key, layer.Label,
            Children = layer.Children.Select(child => new { child.Key, child.Label })
        }));
        if (_layerSignature == signature) return;
        _layerSignature = signature;
        var collapsed = _layerNodes.Where(pair => pair.Value.Group is not null && !pair.Key.IsExpanded)
            .Select(pair => pair.Value.Key).ToHashSet();
        _syncingLayers = true;
        try
        {
            LayersTree.RootNodes.Clear();
            _layerNodes.Clear();
            foreach (var layer in roots.AsEnumerable().Reverse())
            {
                var node = MakeLayerNode(layer);
                foreach (var child in layer.Children.AsEnumerable().Reverse()) node.Children.Add(MakeLayerNode(child));
                node.IsExpanded = !collapsed.Contains(layer.Key);
                LayersTree.RootNodes.Add(node);
            }
        }
        finally { _syncingLayers = false; }
        var selectionKey = _selectedLayerKey;
        if (!_layerNodes.Values.Any(layer => layer.Key == selectionKey))
            selectionKey = ViewModel.SelectedMeter is { } meter ? "meter:" + meter.Definition.Id :
                ViewModel.SelectedGroup is { } group ? "group:" + group.Definition.Id : null;
        SelectLayerNode(selectionKey);
    }

    private TreeViewNode MakeLayerNode(EditorLayerItem layer)
    {
        var node = new TreeViewNode { Content = (layer.Group is null ? "" : "Group · ") + layer.Label };
        _layerNodes[node] = layer;
        return node;
    }

    private void SelectLayerNode(string? key)
    {
        if (_syncingLayers || ViewModel.IsRestoringDraft) return;
        _syncingLayers = true;
        try
        {
            var node = _layerNodes.FirstOrDefault(pair => pair.Value.Key == key).Key;
            LayersTree.SelectedNode = node;
            _selectedLayerKey = node is null ? null : key;
            LayerActions.IsEnabled = node is not null;
            LayerSelectionLabel.Text = node is null ? "Select a layer to arrange or duplicate." : _layerNodes[node].Label;
        }
        finally { _syncingLayers = false; }
    }

    private void LayersTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_syncingLayers || ViewModel.IsRestoringDraft) return;
        if (sender.SelectedNode is not { } node || !_layerNodes.TryGetValue(node, out var layer)) return;
        _syncingLayers = true;
        try
        {
            _selectedLayerKey = layer.Key;
            ViewModel.SelectedMeter = layer.Meter;
            ViewModel.SelectedGroup = layer.Group ?? ViewModel.Groups.FirstOrDefault(group =>
                group.Definition.MeterIds.Contains(layer.Meter!.Definition.Id, StringComparer.OrdinalIgnoreCase));
            MetersList.SelectedItems.Clear();
            if (layer.Meter is not null) MetersList.SelectedItems.Add(layer.Meter);
            GroupsList.SelectedItem = ViewModel.SelectedGroup;
            HighlightSelection();
        }
        finally { _syncingLayers = false; }
        SelectLayerNode(layer.Key);
    }

    private void ArrangeTreeLayer_Click(object sender, RoutedEventArgs args)
    {
        FinishDrag();
        if (_selectedLayerKey is { } key && (sender as FrameworkElement)?.Tag is string action)
            ViewModel.ArrangeLayer(key, action);
    }

    private void DuplicateTreeLayer_Click(object sender, RoutedEventArgs args)
    {
        FinishDrag();
        if (_selectedLayerKey is { } key && ViewModel.DuplicateLayer(key) is { } copyKey)
        {
            RefreshLayerTree();
            SelectLayerNode(copyKey);
        }
    }
}
