namespace ThemeManager.Core.Services;

/// <summary>Process-scoped storage override for isolated desktop testing.</summary>
public static class AppDataPaths
{
    public static string LocalRoot => ResolveLocalRoot(Environment.GetEnvironmentVariable("THEMEDAI_LOCAL_DATA_ROOT"));

    public static string ResolveLocalRoot(string? configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!Path.IsPathFullyQualified(configuredRoot))
            throw new ArgumentException("THEMEDAI_LOCAL_DATA_ROOT must be an absolute path.", nameof(configuredRoot));
        return Path.GetFullPath(configuredRoot);
    }
}
