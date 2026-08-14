using System.Text.Json;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Personalization;

public class UserProfileManager
{
    private UserProfile _currentProfile;
    private readonly string _profileFilePath;

    public UserProfileManager(string profileFilePath)
    {
        _profileFilePath = profileFilePath;
        _currentProfile = new UserProfile();
    }

    public UserProfile GetProfile() => _currentProfile;

    public void Load()
    {
        if (File.Exists(_profileFilePath))
        {
            try
            {
                string json = File.ReadAllText(_profileFilePath);
                var profile = JsonSerializer.Deserialize<UserProfile>(json);
                if (profile != null)
                {
                    _currentProfile = profile;
                }
            }
            catch
            {
                // Fallback to new profile if corruption occurs
                _currentProfile = new UserProfile();
            }
        }
    }

    public void Save()
    {
        _currentProfile.LastUpdated = DateTime.UtcNow;
        string json = JsonSerializer.Serialize(_currentProfile, new JsonSerializerOptions { WriteIndented = true });
        
        var dir = Path.GetDirectoryName(_profileFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        File.WriteAllText(_profileFilePath, json);
    }

    public void ExportProfile(string targetPath)
    {
        _currentProfile.LastUpdated = DateTime.UtcNow;
        string json = JsonSerializer.Serialize(_currentProfile, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(targetPath, json);
    }

    public void ImportProfile(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            string json = File.ReadAllText(sourcePath);
            var profile = JsonSerializer.Deserialize<UserProfile>(json);
            if (profile != null)
            {
                _currentProfile = profile;
                Save(); // Overwrite local state
            }
        }
    }

    public void RecordFeedback(FeedbackAction action)
    {
        // Simple heuristic feedback ingestion
        if (action.Type == FeedbackType.ExplicitLike || action.Type == FeedbackType.ImplicitApplied)
        {
            if (!string.IsNullOrEmpty(action.ItemId) && !_currentProfile.LikedThemeIds.Contains(action.ItemId))
            {
                _currentProfile.LikedThemeIds.Add(action.ItemId);
            }
            
            // If we have context about what was generated, boost those preferences
            if (action.Context != null)
            {
                // In a real implementation, we would extract the specific measures/colors
                // that were actually in the generated item and boost those.
                // For now, we will add a placeholder for that logic.
            }
        }
        else if (action.Type == FeedbackType.ExplicitDislike || action.Type == FeedbackType.ImplicitDismissed)
        {
            if (!string.IsNullOrEmpty(action.ItemId) && !_currentProfile.DislikedThemeIds.Contains(action.ItemId))
            {
                _currentProfile.DislikedThemeIds.Add(action.ItemId);
            }
        }

        Save();
    }
}
