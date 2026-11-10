namespace ThemeManager.Core.Skins;

/// <summary>External links may open a browser or email client, never a file or arbitrary OS handler.</summary>
public static class WidgetActionPolicy
{
    public static bool CanOpenExternal(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeMailto)
        && !value!.Any(char.IsControl);
}
