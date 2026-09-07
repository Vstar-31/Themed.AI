using ThemeManager.Core.Services;

namespace ThemeManager.WinUI;

public partial class App
{
    /// <summary>Desktop-world orchestration service introduced by the desktop rehaul.</summary>
    public static DesktopSceneService SceneService { get; } = new(new DesktopSceneRepository());
}
