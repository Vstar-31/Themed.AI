using Microsoft.Extensions.Logging;

namespace ThemeManager.Core.Skins;

/// <summary>
/// Metadata and entry point for a Themed.AI extension. Plugins register capabilities with the host
/// instead of reaching into internal runtime classes, which keeps the widget format stable while the
/// built-in implementation evolves.
/// </summary>
public interface IThemedPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
    void Register(IThemedPluginHost host);
}

/// <summary>Capability registration surface exposed to trusted in-process plugins.</summary>
public interface IThemedPluginHost
{
    void RegisterMeasure(IMeasurePlugin plugin);
    void RegisterMeter(IMeterPlugin plugin);
    void RegisterAction(IWidgetActionPlugin plugin);
}

/// <summary>Factory contract for a custom measure type such as "network.latency".</summary>
public interface IMeasurePlugin
{
    string TypeId { get; }
    IMeasure Create(MeasureDefinition definition, ILogger? logger = null);
}

/// <summary>
/// UI-independent runtime contract for a custom meter. The WinUI layer can later map the runtime's
/// state to any visual surface without forcing plugin authors to depend on WinUI in their data logic.
/// </summary>
public interface IMeterPlugin
{
    string TypeId { get; }
    IMeterPluginRuntime Create(MeterDefinition definition, ILogger? logger = null);
}

/// <summary>Live state surface produced by an <see cref="IMeterPlugin"/>.</summary>
public interface IMeterPluginRuntime
{
    string TypeId { get; }
    MeterDefinition Definition { get; }
    IReadOnlyDictionary<string, object?> State { get; }
    void Tick(IReadOnlyDictionary<string, IMeasure> measuresByName);
}

/// <summary>
/// Context passed to action plugins. Variable lookup is supplied by the widget host so action
/// handlers can resolve the same widget-local values as a normal meter/formula.
/// </summary>
public sealed record WidgetActionContext(
    string WidgetId,
    string? MeterId,
    string ActionUri,
    Func<string, string?> ResolveVariable);

/// <summary>Handles a custom action URI such as "plugin://spotify/like".</summary>
public interface IWidgetActionPlugin
{
    string Scheme { get; }
    bool CanHandle(Uri actionUri);
    ValueTask<bool> ExecuteAsync(
        WidgetActionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe registry for trusted in-process Themed.AI plugins. Duplicate capability identifiers
/// are rejected so a plugin cannot silently replace an installed provider.
/// </summary>
public sealed class WidgetPluginRegistry : IThemedPluginHost
{
    private readonly Dictionary<string, IMeasurePlugin> _measures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMeterPlugin> _meters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IWidgetActionPlugin> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public IReadOnlyCollection<string> MeasureTypes
    {
        get { lock (_gate) return _measures.Keys.ToArray(); }
    }

    public IReadOnlyCollection<string> MeterTypes
    {
        get { lock (_gate) return _meters.Keys.ToArray(); }
    }

    public IReadOnlyCollection<string> ActionSchemes
    {
        get { lock (_gate) return _actions.Keys.ToArray(); }
    }

    public void RegisterPlugin(IThemedPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        lock (_gate)
        {
            plugin.Register(this);
        }
    }

    public void RegisterMeasure(IMeasurePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var typeId = Normalize(plugin.TypeId, "measure type id");
        lock (_gate)
        {
            if (!_measures.TryAdd(typeId, plugin))
                throw new InvalidOperationException($"A measure plugin is already registered for '{typeId}'.");
        }
    }

    public void RegisterMeter(IMeterPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var typeId = Normalize(plugin.TypeId, "meter type id");
        lock (_gate)
        {
            if (!_meters.TryAdd(typeId, plugin))
                throw new InvalidOperationException($"A meter plugin is already registered for '{typeId}'.");
        }
    }

    public void RegisterAction(IWidgetActionPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        var scheme = Normalize(plugin.Scheme, "action scheme");
        lock (_gate)
        {
            if (!_actions.TryAdd(scheme, plugin))
                throw new InvalidOperationException($"An action plugin is already registered for '{scheme}'.");
        }
    }

    public bool TryCreateMeasure(
        string typeId,
        MeasureDefinition definition,
        ILogger? logger,
        out IMeasure measure)
    {
        measure = null!;
        if (string.IsNullOrWhiteSpace(typeId)) return false;

        IMeasurePlugin? plugin;
        lock (_gate)
            _measures.TryGetValue(typeId.Trim(), out plugin);

        if (plugin is null) return false;
        measure = plugin.Create(definition, logger);
        return measure is not null;
    }

    public bool TryCreateMeterRuntime(
        string typeId,
        MeterDefinition definition,
        ILogger? logger,
        out IMeterPluginRuntime runtime)
    {
        runtime = null!;
        if (string.IsNullOrWhiteSpace(typeId)) return false;

        IMeterPlugin? plugin;
        lock (_gate)
            _meters.TryGetValue(typeId.Trim(), out plugin);

        if (plugin is null) return false;
        runtime = plugin.Create(definition, logger);
        return runtime is not null;
    }

    public bool TryGetActionPlugin(string scheme, out IWidgetActionPlugin? plugin)
    {
        lock (_gate)
            return _actions.TryGetValue(scheme.Trim(), out plugin);
    }

    private static string Normalize(string value, string label)
    {
        var normalized = value?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{label} cannot be empty.", label)
            : normalized;
    }
}
