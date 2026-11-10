using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed class EditorLayerItem
{
    public MeterEditorItem? Meter { get; init; }
    public GroupEditorItem? Group { get; init; }
    public List<EditorLayerItem> Children { get; init; } = new();
    public string Key => Meter is { } meter ? "meter:" + meter.Definition.Id : "group:" + Group!.Definition.Id;
    public int ZIndex => Meter?.Definition.ZIndex ?? Children.Select(c => c.ZIndex).DefaultIfEmpty(0).Max();
    public string Label
    {
        get
        {
            if (Group is { } group) return $"{group.Name} · {Children.Count} meters";
            var name = Meter!.IsBoundToMeasure ? Meter.MeasureName : Meter.Definition.StaticText;
            name = (name ?? "").Replace('\r', ' ').Replace('\n', ' ');
            if (name.Length > 48) name = name[..48] + "…";
            return string.IsNullOrWhiteSpace(name) ? Meter.Kind.ToString() : $"{Meter.Kind} · {name}";
        }
    }
}

public sealed partial class SkinEditorViewModel
{
    /// <summary>Back-to-front order, including the host's group-before-meter tie ordering.</summary>
    public List<EditorLayerItem> GetRootLayers()
    {
        var roots = new List<EditorLayerItem>();
        foreach (var group in Groups.OrderBy(g => g.Definition.MeterIds
            .Select(id => Meters.FirstOrDefault(m => m.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Definition.ZIndex ?? 0)
            .DefaultIfEmpty(0).Min()))
        {
            roots.Add(new EditorLayerItem
            {
                Group = group,
                Children = Meters.Where(m => ParentOf(m) == group).OrderBy(m => m.Definition.ZIndex)
                    .ThenBy(m => m.Definition.Id).Select(m => new EditorLayerItem { Meter = m }).ToList()
            });
        }
        roots.AddRange(Meters.Where(m => ParentOf(m) is null).OrderBy(m => m.Definition.ZIndex)
            .ThenBy(m => m.Definition.Id).Select(m => new EditorLayerItem { Meter = m }));
        return roots.OrderBy(layer => layer.ZIndex).ToList();
    }

    private static void WriteLayerOrder(List<EditorLayerItem> roots)
    {
        // Give every root block a disjoint range. Group Z is the maximum of its meters in the host.
        // This avoids integer overflow and preserves both internal order and root stacking.
        int z = 0;
        foreach (var layer in roots)
        {
            if (layer.Meter is { } meter) meter.Definition.ZIndex = z++;
            else foreach (var child in layer.Children) child.Meter!.Definition.ZIndex = z++;
        }
    }

    public void ArrangeLayer(string key, string action)
    {
        var roots = GetRootLayers();
        var siblings = roots.Any(layer => layer.Key == key) ? roots :
            roots.FirstOrDefault(layer => layer.Children.Any(child => child.Key == key))?.Children;
        if (siblings is null) return;
        // Empty groups have no rendered stacking value in schema v6.
        if (ReferenceEquals(siblings, roots)) siblings = roots = roots.Where(l => l.Meter is not null || l.Children.Count > 0).ToList();
        int index = siblings.FindIndex(layer => layer.Key == key);
        if (index < 0) { StatusMessage = "Add meters to this group before arranging it."; return; }
        int destination = action switch
        {
            "Front" => siblings.Count - 1, "Back" => 0,
            "Forward" => Math.Min(siblings.Count - 1, index + 1),
            "Backward" => Math.Max(0, index - 1), _ => index
        };
        if (index == destination) { StatusMessage = "This layer is already at that boundary."; return; }
        using var edit = BeginEdit();
        var layer = siblings[index];
        siblings.RemoveAt(index);
        siblings.Insert(destination, layer);
        WriteLayerOrder(roots);
        MarkDirty();
        BehaviorStructureChanged?.Invoke();
        StatusMessage = "Updated layer order. Preview only until Save.";
    }

    private static MeterDefinition CopyMeter(MeterDefinition original)
    {
        var copy = new SkinDefinition { Meters = new() { original } }.Clone().Meters[0];
        copy.Id = Guid.NewGuid().ToString();
        foreach (var state in copy.States) state.Id = Guid.NewGuid().ToString();
        return copy;
    }

    private static double CopyOffset(double value) => double.IsFinite(value + 16) ? value + 16 : value;

    public string? DuplicateLayer(string key)
    {
        var roots = GetRootLayers();
        var source = roots.Concat(roots.SelectMany(layer => layer.Children)).FirstOrDefault(layer => layer.Key == key);
        if (source is null) return null;
        if (source.Group is not null && source.Children.Count == 0)
        {
            StatusMessage = "Add meters to this group before duplicating it.";
            return null;
        }
        using var edit = BeginEdit();
        EditorLayerItem copyLayer;
        if (source.Meter is { } meter)
        {
            var definition = CopyMeter(meter.Definition);
            definition.X = CopyOffset(definition.X);
            definition.Y = CopyOffset(definition.Y);
            var parent = ParentOf(meter);
            parent?.Definition.MeterIds.Add(definition.Id);
            parent?.RefreshCount(parent.Definition.MeterIds.Count);
            _working.Meters.Add(definition);
            var copy = new MeterEditorItem(definition, MarkDirty);
            Meters.Add(copy);
            copyLayer = new EditorLayerItem { Meter = copy };
            var siblings = parent is null ? roots : roots.First(layer => layer.Group == parent).Children;
            siblings.Insert(siblings.FindIndex(layer => layer.Key == key) + 1, copyLayer);
            SelectedMeter = copy;
            SelectedGroup = parent;
        }
        else
        {
            var definition = source.Group!.Definition.Clone(Guid.NewGuid().ToString());
            string stem = source.Group.Name + " copy";
            definition.Name = stem;
            for (int suffix = 2; Groups.Any(g => g.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)); suffix++)
                definition.Name = stem + " " + suffix;
            definition.X = CopyOffset(definition.X);
            definition.Y = CopyOffset(definition.Y);
            definition.MeterIds.Clear();
            var copies = source.Children.Select(child => CopyMeter(child.Meter!.Definition)).ToList();
            definition.MeterIds.AddRange(copies.Select(m => m.Id));
            _working.Groups.Add(definition);
            var group = new GroupEditorItem(definition, MarkDirty);
            Groups.Add(group);
            copyLayer = new EditorLayerItem { Group = group };
            foreach (var copy in copies)
            {
                _working.Meters.Add(copy);
                var item = new MeterEditorItem(copy, MarkDirty);
                Meters.Add(item);
                copyLayer.Children.Add(new EditorLayerItem { Meter = item });
            }
            roots.Insert(roots.FindIndex(layer => layer.Key == key) + 1, copyLayer);
            SelectedMeter = null;
            SelectedGroup = group;
        }
        WriteLayerOrder(roots);
        MarkDirty();
        BehaviorStructureChanged?.Invoke();
        StatusMessage = "Duplicated layer with independent properties and states. Measure bindings are shared.";
        return copyLayer.Key;
    }
}
