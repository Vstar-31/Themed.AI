using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;
using ThemeManager.WinUI.Services;

namespace ThemeManager.WinUI.Views;

public sealed partial class StudioPage
{
    private void ScenesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScenesList.SelectedItem is not DesktopScene scene) return;

        Select(scene);
        ActiveSceneName.Text = scene.Name;
        var vibe = VibeSnapshotHub.Current;
        var track = string.IsNullOrWhiteSpace(vibe.TrackTitle) ? "No track" : $"{vibe.TrackTitle} · {vibe.Artist}";
        ActiveSceneMeta.Text = $"{scene.Widgets.Count} widgets · {scene.Effects.Count} effects · {vibe.Mood} · {track}";
    }
}
