using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.WinUI.Services;
using System.Linq;

namespace ThemeManager.WinUI.Views;

public sealed partial class VibeFinderAIPage : Page
{
    private readonly SkinManagerService _skinManager = App.SkinManager;
    private bool _isInitializing = true;

    public VibeFinderAIPage()
    {
        this.InitializeComponent();

        _skinManager.EnsureVibeFinderSkinsExist();

        // Load current toggle states
        var skins = _skinManager.Skins;
        var primary = skins.FirstOrDefault(s => s.Name == "VibeFinder Primary");
        if (primary != null) TogglePrimary.IsOn = primary.Enabled;

        var minimal = skins.FirstOrDefault(s => s.Name == "VibeFinder Minimal");
        if (minimal != null) ToggleMinimal.IsOn = minimal.Enabled;

        var playlist = skins.FirstOrDefault(s => s.Name == "VibeFinder Playlist");
        if (playlist != null) TogglePlaylist.IsOn = playlist.Enabled;

        // Load saved credentials
        var vibeSkin = skins.FirstOrDefault(s => s.Name.StartsWith("VibeFinder"));
        if (vibeSkin != null)
        {
            var measure = vibeSkin.Measures.FirstOrDefault(m => 
                m.Type == ThemeManager.Core.Skins.MeasureType.VibeTrackTitle || 
                m.Type == ThemeManager.Core.Skins.MeasureType.VibeTrackArtist || 
                m.Type == ThemeManager.Core.Skins.MeasureType.VibeMood);
            
            if (measure != null && !string.IsNullOrWhiteSpace(measure.Target))
            {
                var targetStr = measure.Target;
                if (targetStr.StartsWith("|")) targetStr = targetStr.Substring(1);
                var parts = targetStr.Split('|', 3);
                if (parts.Length >= 1) UsernameBox.Text = parts[0];
                if (parts.Length >= 2) PasswordBox.Password = parts[1];
                if (parts.Length >= 3) PromptBox.Text = parts[2];
            }
        }

        _isInitializing = false;
    }

    private void WidgetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (sender is ToggleSwitch toggle && toggle.Tag is string skinName)
        {
            var skin = _skinManager.Skins.FirstOrDefault(s => s.Name == skinName);
            if (skin != null && skin.Enabled != toggle.IsOn)
            {
                _ = _skinManager.SetEnabledAsync(skin, toggle.IsOn);
            }
        }
    }

    private void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        string user = UsernameBox.Text;
        string pass = PasswordBox.Password;
        string prompt = PromptBox.Text;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(prompt))
        {
            StatusText.Text = "Please fill all fields.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            StatusText.Visibility = Visibility.Visible;
            return;
        }

        string target = $"{user}|{pass}|{prompt}";
        _skinManager.UpdateVibeFinderCredentials(target);

        StatusText.Text = "Saved!";
        StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        StatusText.Visibility = Visibility.Visible;
    }
}
