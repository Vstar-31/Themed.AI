using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI;

public partial class App
{
    public static DesktopWorldRuntime WorldRuntime { get; private set; } = null!;

    public static async Task StartWorldRuntimeAsync()
    {
        await SceneService.InitializeAsync();
        if (MainWindow is null) return;
        WorldRuntime ??= new DesktopWorldRuntime(SceneService, MainWindow.DispatcherQueue);
        WorldRuntime.Start();
    }
}
