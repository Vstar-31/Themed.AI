using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>
/// Process-wide notification point for active-theme changes.
/// Integration layers can observe theme changes without creating a dependency from Core on
/// platform-specific implementations such as ThemeManager.Integration.
/// </summary>
public static class ThemeChangeHub
{
    public static event Action<CozyTheme>? ThemeChanged;

    public static CozyTheme? Current { get; private set; }

    public static void Publish(CozyTheme theme)
    {
        Current = theme;
        ThemeChanged?.Invoke(theme);
    }
}
