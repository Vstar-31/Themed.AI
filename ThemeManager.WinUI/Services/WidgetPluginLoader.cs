using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.Services;

/// <summary>
/// Discovers trusted Themed.AI plugins from the local Plugins folder. Loaded assemblies stay in the
/// default load context for the lifetime of the process so any plugin-owned measures/meters can keep
/// their types and resources alive while widgets are running.
/// </summary>
public sealed class WidgetPluginLoader
{
    private readonly WidgetPluginRegistry _registry;
    private readonly ILogger? _logger;

    public WidgetPluginLoader(WidgetPluginRegistry registry, ILogger? logger = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger;
    }

    /// <summary>
    /// Loads every DLL in <paramref name="directory"/> and registers public, concrete
    /// <see cref="IThemedPlugin"/> implementations with parameterless constructors.
    /// Invalid assemblies/plugins are skipped individually so one bad DLL cannot block app startup.
    /// </summary>
    public int LoadFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return 0;

        var loadedPlugins = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                var candidates = assembly.GetExportedTypes()
                    .Where(t => typeof(IThemedPlugin).IsAssignableFrom(t))
                    .Where(t => t is { IsAbstract: false, IsInterface: false })
                    .Where(t => t.GetConstructor(Type.EmptyTypes) is not null);

                foreach (var type in candidates)
                {
                    try
                    {
                        var plugin = (IThemedPlugin)Activator.CreateInstance(type)!;
                        _registry.RegisterPlugin(plugin);
                        loadedPlugins++;
                        _logger?.LogInformation(
                            "Widget plugin loaded: {PluginId} ({PluginName}) v{Version} from {Path}",
                            plugin.Id,
                            plugin.Name,
                            plugin.Version,
                            path);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to register widget plugin type {PluginType} from {Path}", type.FullName, path);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var loaderException in ex.LoaderExceptions.Where(e => e is not null))
                    _logger?.LogWarning(loaderException, "A plugin assembly type could not be loaded from {Path}", path);
                _logger?.LogWarning(ex, "Plugin assembly {Path} was only partially loadable; usable types were skipped", path);
            }
            catch (BadImageFormatException ex)
            {
                _logger?.LogWarning(ex, "Ignoring non-.NET or incompatible plugin DLL {Path}", path);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load widget plugin assembly {Path}", path);
            }
        }

        return loadedPlugins;
    }
}
