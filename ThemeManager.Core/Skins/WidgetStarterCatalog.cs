namespace ThemeManager.Core.Skins;

public sealed record WidgetStarter(SkinDefinition Definition, string Summary)
{
    public string Name => Definition.Name;
}

/// <summary>Local starter discovery only. Creates definitions without starting measures or hosts.</summary>
public static class WidgetStarterCatalog
{
    public static IReadOnlyList<WidgetStarter> Create() => SkinDefaults.CreateAllDefaults()
        .Select(skin => new WidgetStarter(skin, skin.Id switch
        {
            "builtin-clock" => "A clear digital clock and date for a calm desktop.",
            "builtin-system-monitor" => "CPU, RAM and free disk space with compact progress bars.",
            "builtin-uptime" => "A minimal counter showing how long your computer has been running.",
            "builtin-network" => "Download and upload speeds with a network history graph.",
            "builtin-system-rings" => "CPU, memory and disk usage in three circular gauges.",
            _ => skin.Description
        })).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<WidgetStarter> Search(IEnumerable<WidgetStarter> starters, string? query)
    {
        var terms = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return starters.Where(item =>
        {
            var skin = item.Definition;
            var text = string.Join(" ", new[] { item.Name, item.Summary, skin.Description }
                .Concat(skin.Tags)
                .Concat(skin.Measures.Select(m => m.Type.ToString()))
                .Concat(skin.Meters.Select(m => m.Kind.ToString()))
                .Concat(skin.Meters.Select(m => m.StaticText)));
            // Word prefixes support partial typing without "ring" matching the String meter kind.
            var words = new string(text.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return terms.All(term => words.Any(word => word.StartsWith(term, StringComparison.OrdinalIgnoreCase)));
        }).ToArray();
    }
}
