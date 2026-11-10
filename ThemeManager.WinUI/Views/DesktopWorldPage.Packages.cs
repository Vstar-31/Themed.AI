using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ThemeManager.Core.Models;
using ThemeManager.Core.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ThemeManager.WinUI.Views;

public sealed partial class DesktopWorldPage
{
    private bool _worldPackageBusy;
    private async void ImportWorld_Click(object sender, RoutedEventArgs e)
    {
        if (_worldPackageBusy) return;
        _worldPackageBusy = true;
        try
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".themedworld");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSingleFileAsync(); if (file is null) return;
            PackageStatus.Text = "Importing world…";
            await new WorldPackageService().ImportToLibraryAsync(file.Path, App.SceneService);
            PackageStatus.Text = "Imported as a new world. Select it in Studio when you are ready to apply it.";
        }
        catch (Exception ex) { PackageStatus.Text = "Could not import world: " + ex.Message; }
        finally { _worldPackageBusy = false; }
    }

    private async void ExportWorld_Click(object sender, RoutedEventArgs e)
    {
        if (_worldPackageBusy || sender is not Button { Tag: DesktopScene scene }) return;
        _worldPackageBusy = true;
        try
        {
            var confirm = new ContentDialog { Title = "Export world", Content = "Widget images, wallpaper and the palette are bundled. Widget connection settings are omitted. Review custom text, links and variables before sharing.", PrimaryButtonText = "Export", CloseButtonText = "Cancel", XamlRoot = XamlRoot };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            var picker = new FileSavePicker { SuggestedFileName = "ThemedAI-world" };
            picker.FileTypeChoices.Add("Themed.AI world", new[] { ".themedworld" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var file = await picker.PickSaveFileAsync(); if (file is null) return;
            PackageStatus.Text = "Exporting world…";
            var theme = App.ThemeService.Themes.FirstOrDefault(t => t.Id == scene.ThemeId);
            await new WorldPackageService().ExportAsync(scene, App.SkinManager.Skins, theme, file.Path);
            PackageStatus.Text = "World exported.";
        }
        catch (Exception ex) { PackageStatus.Text = "Could not export world: " + ex.Message; }
        finally { _worldPackageBusy = false; }
    }
}
