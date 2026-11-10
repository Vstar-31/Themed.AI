namespace ThemeManager.Core.Services;

/// <summary>Distinguishes saved sign-in details from the old bundled example.</summary>
public static class VibeFinderCredentials
{
    public static bool IsConfigured(string? user, string? password) =>
        !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password)
        && !(user.Trim().Equals("listener", StringComparison.OrdinalIgnoreCase)
             && password == "hunter2");
}
