namespace ThemeManager.Core.Services;

/// <summary>The only web origin allowed to receive desktop credentials and bridge messages.</summary>
public static class VibeFinderSite
{
    public const string Origin = "https://vfaithemed.netlify.app";
    public const string AppUrl = Origin + "/app";

    public static bool IsTrusted(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IdnHost.Equals("vfaithemed.netlify.app", StringComparison.OrdinalIgnoreCase)
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo);
}
