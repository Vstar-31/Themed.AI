using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ThemeManager.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private const string StartupKeyPath  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupAppName  = "ThemedAI";

    private static readonly string DataFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThemedAI");

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Active theme name
        ActiveThemeName.Text = App.ThemeService.ActiveTheme.Name;

        // Startup toggle (read from registry, silently fail if no permission)
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
            StartupToggle.IsOn = key?.GetValue(StartupAppName) is not null;
        }
        catch { StartupToggle.IsEnabled = false; }

        // Diagnostics
        DataFolderText.Text = DataFolder;
        RuntimeText.Text    = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, writable: true);
            if (StartupToggle.IsOn)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                key?.SetValue(StartupAppName, $"\"{exePath}\"");
                StatusText.Text = "Startup entry added.";
            }
            else
            {
                key?.DeleteValue(StartupAppName, throwOnMissingValue: false);
                StatusText.Text = "Startup entry removed.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not update startup: {ex.Message}";
        }
    }

    // ── Export ────────────────────────────────────────────────────────────────

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName      = "ThemedAI-themes",
        };
        picker.FileTypeChoices.Add("JSON file", new[] { ".json" });
        InitializeWithWindow.Initialize(picker, GetHwnd());

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            // Export first theme as a demo; a real app would export the whole list.
            var active = App.ThemeService.ActiveTheme;
            await App.ThemeRepository.ExportThemeAsync(active, file.Path);
            StatusText.Text = $"Exported "{active.Name}" to {file.Name}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    // ── Import ────────────────────────────────────────────────────────────────

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            ViewMode               = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, GetHwnd());

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var imported = await App.ThemeService.ImportThemeAsync(file.Path);
        StatusText.Text = imported is not null
            ? $"Imported "{imported.Name}" successfully."
            : "Import failed — not a valid Themed.AI JSON file.";
    }

    // ── Data folder ───────────────────────────────────────────────────────────

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DataFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName        = DataFolder,
            UseShellExecute = true,
        });
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static IntPtr GetHwnd()
    {
        // Walk the open windows to find the main window handle.
        // In a real app you'd store the hwnd centrally (e.g. App.MainWindowHandle).
        foreach (var window in Microsoft.UI.Xaml.Window.Current is { } w
                     ? new[] { w }
                     : Array.Empty<Microsoft.UI.Xaml.Window>())
        {
            return WindowNative.GetWindowHandle(window);
        }
        return IntPtr.Zero;
    }
}
