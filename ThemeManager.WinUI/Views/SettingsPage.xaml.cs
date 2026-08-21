using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ThemeManager.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private const string StartupKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupAppName = "ThemedAI";

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
        RuntimeText.Text = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        LoadAutomationSettings();
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
            SuggestedFileName = "ThemedAI-themes",
        };
        picker.FileTypeChoices.Add("JSON file", new[] { ".json" });
        InitializeWithWindow.Initialize(picker, GetHwnd());

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            var active = App.ThemeService.ActiveTheme;
            await App.ThemeRepository.ExportThemeAsync(active, file.Path);
            StatusText.Text = $"Exported \"{active.Name}\" to {file.Name}.";
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
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, GetHwnd());

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var imported = await App.ThemeService.ImportThemeAsync(file.Path);
        StatusText.Text = imported is not null
            ? $"Imported \"{imported.Name}\" successfully."
            : "Import failed — not a valid Themed.AI JSON file.";
    }

    // ── Theme automation (Phase 7) ───────────────────────────────────────────────

    /// <summary>Display wrapper for a ComboBox entry — a plain Id/Name pair plus a leading
    /// "— Not set —" sentinel (empty Id) so every automation slot can be left unconfigured
    /// without needing a nullable-item-in-ItemsSource workaround.</summary>
    private sealed record ThemeOption(string Id, string Name);

    private void LoadAutomationSettings()
    {
        var schedule = App.Settings.Schedule;

        AutomationEnabledToggle.IsOn = schedule.Enabled;
        FollowLightDarkToggle.IsOn = schedule.FollowSystemLightDark;
        BatterySaverToggle.IsOn = schedule.BatterySaverEnabled;
        CrossfadeSecondsBox.Value = schedule.CrossfadeMs / 1000.0;

        SunriseTimePicker.Time = TimeSpan.FromMinutes(schedule.SunriseMinutes);
        NoonTimePicker.Time = TimeSpan.FromMinutes(schedule.NoonMinutes);
        DuskTimePicker.Time = TimeSpan.FromMinutes(schedule.DuskMinutes);
        MidnightTimePicker.Time = TimeSpan.FromMinutes(schedule.MidnightMinutes);

        SetUpThemeCombo(SunriseThemeCombo, schedule.SunriseThemeId);
        SetUpThemeCombo(NoonThemeCombo, schedule.NoonThemeId);
        SetUpThemeCombo(DuskThemeCombo, schedule.DuskThemeId);
        SetUpThemeCombo(MidnightThemeCombo, schedule.MidnightThemeId);
        SetUpThemeCombo(LightThemeCombo, schedule.LightThemeId);
        SetUpThemeCombo(DarkThemeCombo, schedule.DarkThemeId);
        SetUpThemeCombo(BatterySaverThemeCombo, schedule.BatterySaverThemeId);

        WeatherReactiveToggle.IsOn = schedule.WeatherReactiveEnabled;
        WeatherCityBox.Text = schedule.WeatherCity ?? "";
        WeatherApiKeyBox.Password = schedule.WeatherApiKey ?? "";

        SetUpThemeCombo(WeatherClearThemeCombo, schedule.WeatherClearThemeId);
        SetUpThemeCombo(WeatherCloudsThemeCombo, schedule.WeatherCloudsThemeId);
        SetUpThemeCombo(WeatherRainThemeCombo, schedule.WeatherRainThemeId);
        SetUpThemeCombo(WeatherThunderstormThemeCombo, schedule.WeatherThunderstormThemeId);
        SetUpThemeCombo(WeatherSnowThemeCombo, schedule.WeatherSnowThemeId);
        SetUpThemeCombo(WeatherFogThemeCombo, schedule.WeatherFogThemeId);
    }

    private static List<ThemeOption> BuildThemeOptions() =>
        new List<ThemeOption> { new("", "— Not set —") }
            .Concat(App.ThemeService.Themes.Select(t => new ThemeOption(t.Id, t.Name)))
            .ToList();

    private static void SetUpThemeCombo(ComboBox combo, string? currentId)
    {
        var options = BuildThemeOptions();
        combo.ItemsSource = options;
        combo.DisplayMemberPath = nameof(ThemeOption.Name);
        combo.SelectedItem = options.FirstOrDefault(o => o.Id == currentId) ?? options[0];
    }

    private static string? ReadThemeId(ComboBox combo) =>
        combo.SelectedItem is ThemeOption { Id.Length: > 0 } opt ? opt.Id : null;

    private async void SaveAutomationButton_Click(object sender, RoutedEventArgs e)
    {
        var schedule = App.Settings.Schedule;

        schedule.Enabled = AutomationEnabledToggle.IsOn;
        schedule.FollowSystemLightDark = FollowLightDarkToggle.IsOn;
        schedule.BatterySaverEnabled = BatterySaverToggle.IsOn;
        schedule.CrossfadeMs = (int)Math.Round(Math.Max(0, CrossfadeSecondsBox.Value) * 1000);

        schedule.SunriseMinutes = (int)SunriseTimePicker.Time.TotalMinutes;
        schedule.NoonMinutes = (int)NoonTimePicker.Time.TotalMinutes;
        schedule.DuskMinutes = (int)DuskTimePicker.Time.TotalMinutes;
        schedule.MidnightMinutes = (int)MidnightTimePicker.Time.TotalMinutes;

        schedule.SunriseThemeId = ReadThemeId(SunriseThemeCombo);
        schedule.NoonThemeId = ReadThemeId(NoonThemeCombo);
        schedule.DuskThemeId = ReadThemeId(DuskThemeCombo);
        schedule.MidnightThemeId = ReadThemeId(MidnightThemeCombo);
        schedule.LightThemeId = ReadThemeId(LightThemeCombo);
        schedule.DarkThemeId = ReadThemeId(DarkThemeCombo);
        schedule.BatterySaverThemeId = ReadThemeId(BatterySaverThemeCombo);

        schedule.WeatherReactiveEnabled = WeatherReactiveToggle.IsOn;
        schedule.WeatherCity = string.IsNullOrWhiteSpace(WeatherCityBox.Text) ? null : WeatherCityBox.Text.Trim();
        schedule.WeatherApiKey = string.IsNullOrWhiteSpace(WeatherApiKeyBox.Password) ? null : WeatherApiKeyBox.Password;

        schedule.WeatherClearThemeId = ReadThemeId(WeatherClearThemeCombo);
        schedule.WeatherCloudsThemeId = ReadThemeId(WeatherCloudsThemeCombo);
        schedule.WeatherRainThemeId = ReadThemeId(WeatherRainThemeCombo);
        schedule.WeatherThunderstormThemeId = ReadThemeId(WeatherThunderstormThemeCombo);
        schedule.WeatherSnowThemeId = ReadThemeId(WeatherSnowThemeCombo);
        schedule.WeatherFogThemeId = ReadThemeId(WeatherFogThemeCombo);

        await App.Settings.SaveAsync();
        StatusText.Text = "Automation settings saved.";
    }

    // ── Data folder ───────────────────────────────────────────────────────────

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DataFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = DataFolder,
            UseShellExecute = true,
        });
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the HWND of the main window using the static App.MainWindow property.
    /// Window.Current is always null in packaged WinUI 3 apps — do not use it.
    /// </summary>
    private static IntPtr GetHwnd()
        => WindowNative.GetWindowHandle(App.MainWindow);
}