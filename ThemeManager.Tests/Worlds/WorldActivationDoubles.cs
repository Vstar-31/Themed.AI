using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI
{
    internal static class App
    {
        public static Services.SkinManagerService SkinManager { get; set; } = null!;
        public static ThemeRepository ThemeRepository { get; set; } = null!;
        public static ThemeService ThemeService { get; set; } = null!;
        public static DesktopSceneService SceneService { get; set; } = null!;
        public static ISystemThemeIntegrator SystemIntegrator { get; set; } = null!;
        public static ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
    }
}

namespace ThemeManager.WinUI.Services
{
    public sealed partial class SkinManagerService
    {
        public List<SkinDefinition> Skins { get; } = [];
        public Task<(int Applied, int Missing)> ApplySceneAsync(IEnumerable<SceneWidgetPlacement> placements)
        {
            var visible = placements.Where(p => p.Visible).Select(p => p.WidgetId).ToHashSet();
            foreach (var skin in Skins) skin.Enabled = visible.Contains(skin.Id);
            return Task.FromResult((Skins.Count(s => s.Enabled), 0));
        }
    }
}
