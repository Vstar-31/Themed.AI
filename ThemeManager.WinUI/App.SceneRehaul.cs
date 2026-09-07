using ThemeManager.Core.Services;

namespace ThemeManager.WinUI;

public partial class App
{
    public static DesktopSceneService SceneService { get; } = new(new DesktopSceneRepository());
}
