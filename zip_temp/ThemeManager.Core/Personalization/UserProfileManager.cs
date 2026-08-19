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

        // Write to a temp file then rename over the real one — a plain WriteAllText can leave a
        // truncated/corrupt profile.json if the process dies mid-write; the rename is atomic so
        // there's no window where the file exists but is only half-written.
        var tempPath = _profileFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _profileFilePath, overwrite: true);
    }

    public void ExportProfile(string targetPath)
    {
        _currentProfile.LastUpdated = DateTime.UtcNow;
        string json = JsonSerializer.Serialize(_currentProfile, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(targetPath, json);
    }

    private const float BoostStep = 1f;
    private const float MinWeight = -5f;
    private const float MaxWeight = 5f;

    public void ImportProfile(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            try
            {
                string json = File.ReadAllText(sourcePath);
                var profile = JsonSerializer.Deserialize<UserProfile>(json);
                if (profile != null)
                {
                    _currentProfile = profile;
                    Save(); // Overwrite local state
                }
            }
            catch
            {
                // A hand-edited or corrupted import file shouldn't crash the caller — this is
                // untrusted external input (unlike Load(), which reads our own last-known-good
                // save), so it's at least as important to fail soft here. Existing profile is
                // left untouched on failure.
            }
        }
    }

    public void RecordFeedback(FeedbackAction action)
    {
        bool positive = action.Type is FeedbackType.ExplicitLike or FeedbackType.ImplicitApplied;
        bool negative = action.Type is FeedbackType.ExplicitDislike or FeedbackType.ImplicitDismissed;

        if (positive && !string.IsNullOrEmpty(action.ItemId) && !_currentProfile.LikedThemeIds.Contains(action.ItemId))
        {
            _currentProfile.LikedThemeIds.Add(action.ItemId);
        }
        else if (negative && !string.IsNullOrEmpty(action.ItemId) && !_currentProfile.DislikedThemeIds.Contains(action.ItemId))
        {
            _currentProfile.DislikedThemeIds.Add(action.ItemId);
        }

        // Boost (or penalize) the specific measures/color that were actually in the generated
        // item — this is the part that was a placeholder before. Clamped so spamming feedback
        // on the same measure/color can't grow a weight without bound.
        if (positive || negative)
        {
            float delta = positive ? BoostStep : -BoostStep;

            if (action.WidgetMeasures is { Count: > 0 })
            {
                foreach (var measure in action.WidgetMeasures)
                {
                    _currentProfile.WidgetPreferences.TryGetValue(measure, out float weight);
                    _currentProfile.WidgetPreferences[measure] = Math.Clamp(weight + delta, MinWeight, MaxWeight);
                }
            }

            if (!string.IsNullOrEmpty(action.ThemeAccentColor))
            {
                _currentProfile.ColorPreferences.TryGetValue(action.ThemeAccentColor, out float weight);
                _currentProfile.ColorPreferences[action.ThemeAccentColor] = Math.Clamp(weight + delta, MinWeight, MaxWeight);
            }
        }
        // FeedbackType.ImplicitEdited deliberately isn't scored yet — "the user changed it
        // after generating" is a real signal but an ambiguous one (was the original wrong, or
        // just not quite right?) and guessing wrong would train against the wrong thing. Left
        // as a no-op (matching the pre-existing behavior for this case) until there's a clearer
        // read on what it should mean.

        Save();
    }
}
