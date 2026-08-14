using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Personalization;

public class UserProfile
{
    public string UserId { get; set; } = "default";
    
    // Weights for color families or specific hex codes. Higher = more preferred
    public Dictionary<string, float> ColorPreferences { get; set; } = new();
    
    // Weights for measure types (e.g. CPU, Time, etc.). Higher = more preferred
    public Dictionary<MeasureType, float> WidgetPreferences { get; set; } = new();
    
    // History
    public List<string> LikedThemeIds { get; set; } = new();
    public List<string> DislikedThemeIds { get; set; } = new();
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
