using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.ViewModels;

public sealed class SystemIntegrationViewModel : ViewModelBase, IDisposable
{
    private readonly ISystemThemeIntegrator _integrator;
    private readonly EventHandler<ThemeManager.Core.Models.CozyTheme> _themeChangedHandler;

    private string _currentAccentHex = "…";
    public string CurrentAccentHex
    {
        get => _currentAccentHex;
        set => SetProperty(ref _currentAccentHex, value);
    }

    private string _windowsBuild = "…";
    public string WindowsBuild
    {
        get => _windowsBuild;
        set => SetProperty(ref _windowsBuild, value);
    }

    private bool _isLightMode;
    public bool IsLightMode
    {
        get => _isLightMode;
        set => SetProperty(ref _isLightMode, value);
    }

    public bool ActiveThemeApplyWallpaper
    {
        get => App.ThemeService.ActiveTheme.ApplyToWallpaper;
        set
        {
            if (App.ThemeService.ActiveTheme.ApplyToWallpaper == value) return;
            App.ThemeService.ActiveTheme.ApplyToWallpaper = value;
            OnPropertyChanged();
        }
    }

    public bool ActiveThemeApplyWindowsApps
    {
        get => App.ThemeService.ActiveTheme.ApplyToWindowsApps;
        set
        {
            if (App.ThemeService.ActiveTheme.ApplyToWindowsApps == value) return;
            App.ThemeService.ActiveTheme.ApplyToWindowsApps = value;
            OnPropertyChanged();
            _ = PersistWindowsAppsPreferenceAsync();
        }
    }

    private async Task PersistWindowsAppsPreferenceAsync()
    {
        try
        {
            var theme = App.ThemeService.ActiveTheme;
            await App.ThemeService.SaveThemeAsync(theme);
            StatusMessage = theme.ApplyToWindowsApps
                ? "Windows app appearance sync enabled."
                : "Windows app appearance sync disabled for this theme.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save Windows app appearance preference: {ex.Message}";
        }
    }

    private bool _advancedEnabled;
    public bool AdvancedEnabled
    {
        get => _advancedEnabled;
        set
        {
            if (SetProperty(ref _advancedEnabled, value))
                OnPropertyChanged(nameof(AdvancedEnabledOpacity));
        }
    }

    public double AdvancedEnabledOpacity => _advancedEnabled ? 1.0 : 0.4;

    private string _statusMessage = "Ready.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public SystemIntegrationViewModel(ISystemThemeIntegrator integrator)
    {
        _integrator = integrator;
        _themeChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(ActiveThemeApplyWallpaper));
            OnPropertyChanged(nameof(ActiveThemeApplyWindowsApps));
        };
        App.ThemeService.ThemeChanged += _themeChangedHandler;
    }

    public async Task RefreshSystemInfoAsync()
    {
        IsBusy = true;
        try
        {
            var info = await _integrator.GetCurrentSystemThemeAsync();
            CurrentAccentHex = info.AccentHex;
            WindowsBuild = info.WindowsBuild;
            IsLightMode = info.IsLightMode;
        }
        finally { IsBusy = false; }
    }

    public async Task ApplyAccentColorAsync(string hexColor)
    {
        IsBusy = true;
        StatusMessage = "Applying accent color…";
        try
        {
            bool ok = await _integrator.ApplyAccentColorAsync(hexColor);
            StatusMessage = ok
                ? "Accent color applied."
                : "Failed to apply accent color — check permissions.";
        }
        finally { IsBusy = false; }
    }

    public async Task ApplyWallpaperAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "No wallpaper path set on the active theme.";
            return;
        }
        IsBusy = true;
        StatusMessage = "Setting wallpaper…";
        try
        {
            bool ok = await _integrator.ApplyWallpaperAsync(path);
            StatusMessage = ok ? "Wallpaper set." : "Failed to set wallpaper.";
        }
        finally { IsBusy = false; }
    }

    public async Task ResetAccentAsync()
    {
        if (!AdvancedEnabled) return;
        IsBusy = true;
        StatusMessage = "Resetting accent to Windows default…";
        try
        {
            bool ok = await _integrator.ResetAccentColorAsync();
            StatusMessage = ok ? "Accent color reset." : "Reset failed.";
        }
        finally { IsBusy = false; }
    }

    public void Dispose()
    {
        App.ThemeService.ThemeChanged -= _themeChangedHandler;
    }
}
