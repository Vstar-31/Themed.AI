namespace ThemeManager.Core.Skins;

/// <summary>Creates an independent local authoring draft without changing or persisting its source.</summary>
public static class WidgetRemix
{
    public static SkinDefinition Create(SkinDefinition source, IEnumerable<string>? existingNames = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.Clone(Guid.NewGuid().ToString());
        copy.SchemaVersion = SkinDefinition.CurrentSchemaVersion;
        copy.Enabled = false;
        copy.Locked = false;
        copy.ClickThrough = false;
        copy.DesktopLayer = false;
        copy.AlwaysOnTop = false;
        var names = new HashSet<string>(existingNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        string stem = string.IsNullOrWhiteSpace(source.Name) ? "Widget remix" : source.Name.Trim() + " remix";
        copy.Name = stem;
        for (int suffix = 2; names.Contains(copy.Name); suffix++) copy.Name = stem + " " + suffix;
        copy.Tags.RemoveAll(tag => tag.Equals("built-in", StringComparison.OrdinalIgnoreCase));
        if (!copy.Tags.Contains("remix", StringComparer.OrdinalIgnoreCase)) copy.Tags.Add("remix");
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var meter in copy.Meters)
        {
            string id = Guid.NewGuid().ToString();
            if (!ids.TryAdd(meter.Id, id)) throw new ArgumentException("The source has duplicate meter IDs.", nameof(source));
            meter.Id = id;
            foreach (var state in meter.States) state.Id = Guid.NewGuid().ToString();
        }
        foreach (var measure in copy.Measures) measure.Id = Guid.NewGuid().ToString();
        foreach (var group in copy.Groups)
        {
            group.Id = Guid.NewGuid().ToString();
            group.MeterIds = group.MeterIds.Select(id => ids.TryGetValue(id, out var mapped) ? mapped :
                throw new ArgumentException("The source group references a missing meter.", nameof(source))).ToList();
        }
        return copy;
    }
}
