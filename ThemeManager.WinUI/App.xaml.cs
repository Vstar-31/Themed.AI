using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ThemeManager.Core.Models;
using ThemeManager.Core.Personalization;
using ThemeManager.Core.Services;
using ThemeManager.Core.Utilities;
using ThemeManager.Integration;
using ThemeManager.WinUI.Services;
using Windows.UI;
using Windows.UI.ViewManagement;
using Serilog;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ThemeManager.WinUI;

/// <summary>
/// Application entry point.
/// Bootstraps all services, wires up the live-theming pipeline, and launches the main window.
/// </summary>
public partial class App : Application
{
    // ── Public service accessors (poor-man's DI; easy to replace with DI container) ──
    public static ThemeService ThemeService { get; private set; } = null!;
    public static ThemeRepository ThemeRepository { get; private set; } = null!;
    public static ISystemThemeIntegrator SystemIntegrator { get; private set; } = null!;

    /// <summary>Backs the Phase 7 weather-reactive automation rule — see
    /// ThemeManager.Integration.OpenWeatherMapConditionProvider and AppSettings.Schedule's
    /// Weather* fields.</summary>
    public static IWeatherConditionProvider WeatherProvider { get; private set; } = null!;

    /// <summary>Skins (desktop widgets) — see ThemeManager.WinUI.Services.SkinManagerService.</summary>
    public static SkinManagerService SkinManager { get; private set; } = null!;

    /// <summary>Ranks/learns from generated themes and widgets — see PersonalizationOrchestrator.
    /// Loaded synchronously on startup (it's one small JSON file); everything downstream of it
    /// is designed to degrade gracefully to an empty profile if that file doesn't exist yet.</summary>
    public static PersonalizationOrchestrator Personalization { get; private set; } = null!;

    /// <summary>System tray icon — see ThemeManager.Integration.TrayIcon.</summary>
    public static TrayIcon Tray { get; private set; } = null!;

    public static ILoggerFactory LoggerFactory { get; private set; } = null!;

    /// <summary>General app preferences, including the Phase 7 theme-automation schedule — see
    /// ThemeManager.Core.Services.AppSettings.</summary>
    public static AppSettings Settings { get; private set; } = null!;

    /// <summary>Watches the clock/Windows theme/battery-saver state and crossfades the active
    /// theme automatically per <see cref="AppSettings.Schedule"/> — see ThemeAutomationService.</summary>
    public static ThemeAutomationService Automation { get; private set; } = null!;

    /// <summary>Phase 8 Local Gallery — bundled community pack(s) of themes/widgets to browse and
    /// add to the person's own library. See ThemeManager.Core.Services.GalleryService.</summary>
    public static GalleryService Gallery { get; private set; } = null!;

    /// <summary>
    /// Set right before we deliberately let the main window actually close (from the tray's
    /// Exit command). While false, the AppWindow.Closing handler below intercepts every close
    /// request and hides the window instead — that's what makes "closing" the app really just
    /// minimize it to the tray.
    /// </summary>
    private static bool _isExiting;

    /// <summary>
    /// Exposed so any page can retrieve the HWND for file pickers without
    /// using the broken Window.Current / Resources["MainWindow"] pattern.
    /// </summary>
    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        ThemeRepository = new ThemeRepository();
        ThemeService = new ThemeService(ThemeRepository);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj} (Thread:{ThreadId}){NewLine}{Exception}")
            .WriteTo.File(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "ThemeManager.AI", "logs", "app-.log"), 
                          rollingInterval: RollingInterval.Day,
                          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj} (Thread:{ThreadId}){NewLine}{Exception}")
            .CreateLogger();

        LoggerFactory = new LoggerFactory().AddSerilog(Log.Logger);
        var logger = LoggerFactory.CreateLogger<App>();
        logger.LogInformation("Application Starting...");

        // ── Global Exception Handling ──
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            logger.LogCritical((Exception)e.ExceptionObject, "AppDomain Unhandled Exception. Terminating: {IsTerminating}", e.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            logger.LogCritical(e.Exception, "Unobserved Task Exception");
            e.SetObserved();
        };

        this.UnhandledException += (s, e) =>
        {
            logger.LogCritical(e.Exception, "WinUI Unhandled Exception: {Message}", e.Message);
            e.Handled = true; // Attempt to keep running if possible
        };

        SystemIntegrator = new SystemThemeIntegrator(LoggerFactory.CreateLogger<SystemThemeIntegrator>());
        WeatherProvider = new OpenWeatherMapConditionProvider(LoggerFactory.CreateLogger<OpenWeatherMapConditionProvider>());

        // Initialise (loads JSON, sets Cozy Café as active) before the window appears.
        await ThemeService.InitializeAsync();

        // General preferences (startup toggle's actual source of truth is the registry itself —
        // see SettingsPage — but the Phase 7 automation schedule below lives here).
        Settings = await AppSettings.LoadAsync();

        // Apply the loaded theme to ResourceDictionary immediately.
        ApplyThemeToResources(ThemeService.ActiveTheme);

        // Subscribe to future theme changes for live updating.
        ThemeService.ThemeChanged += (_, theme) => ApplyThemeToResources(theme);

        // Assign the static property BEFORE Activate so pickers can grab the HWND.
        MainWindow = new MainWindow();
        MainWindow.Activate();

        // Widgets start up after the main window so its DispatcherQueue is definitely
        // running (SkinManagerService's tick timer needs one). A widget that was left
        // enabled last session reappears on the desktop right away, same as Rainmeter.
        SkinManager = new SkinManagerService(new SkinRepository(), LoggerFactory);

        Personalization = new PersonalizationOrchestrator(System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "ThemedAI", "profile.json"));
        await SkinManager.InitializeAsync();

        // ── Phase 8 Local Gallery: bundled starter pack(s), read-only, no network involved ──
        // AppContext.BaseDirectory rather than ApplicationData.Current — this app is unpackaged
        // (WindowsPackageType=None; see SettingsPage's startup-toggle comments for the full
        // history of why ApplicationData throws without an MSIX identity), so bundled content
        // ships as plain Content files copied next to the exe, not packaged app assets. A missing
        // or empty folder just means an empty gallery (GalleryService.LoadAsync handles that),
        // not a startup failure — nothing downstream depends on a pack existing.
        Gallery = new GalleryService(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "CommunityPacks"));
        await Gallery.LoadAsync();

        // ── Theme automation: time-of-day schedule, Windows light/dark following, battery saver,
        // weather-reactive ──
        // Off by default (Settings.Schedule.Enabled starts false) — see SettingsPage's "Theme
        // Automation" section. AutomationTriggered fires on a background timer thread (see
        // ThemeAutomationService), so — same pattern as Tray.GlobalHotkeyActivated below — it gets
        // marshalled onto the UI thread before touching anything visual.
        Automation = new ThemeAutomationService(ThemeService, SystemIntegrator, () => Settings.Schedule, WeatherProvider);
        Automation.AutomationTriggered += theme =>
        {
            MainWindow.DispatcherQueue.TryEnqueue(async () =>
                await CrossfadeToThemeAsync(theme, TimeSpan.FromMilliseconds(Math.Max(0, Settings.Schedule.CrossfadeMs))));
        };

        // ── System tray: closing the main window now minimizes to tray instead of quitting ──
        Tray = new TrayIcon(LoggerFactory.CreateLogger<TrayIcon>());
        Tray.OpenRequested += () =>
        {
            MainWindow.AppWindow.Show();
            MainWindow.Activate();
        };
        Tray.ExitRequested += () =>
        {
            _isExiting = true;
            MainWindow.Close();
        };
        Tray.GlobalHotkeyActivated += () =>
        {
            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                SkinManager.ToggleAllWidgetsVisibility();
            });
        };
        Tray.Show();

        // AppWindow.Closing (not the older Window.Closed) is the one that can actually be
        // cancelled — this is the documented way to turn "the X button" into "hide to tray"
        // instead of "quit". Must stay synchronous: it decides Cancel before returning.
        MainWindow.AppWindow.Closing += (_, closingArgs) =>
        {
            if (_isExiting) return; // Exit was chosen from the tray — let the real close proceed
            closingArgs.Cancel = true;
            MainWindow.AppWindow.Hide();
        };

        // Only reached once a close was actually allowed through (i.e. after Exit) —
        // final cleanup so widgets and the tray icon don't outlive the main process.
        MainWindow.Closed += (_, _) =>
        {
            SkinManager.Dispose();
            Tray.Dispose();
            Automation.Dispose();
            // Deliberately NOT saving Settings here. Window.Closed isn't awaited by the
            // framework — an async handler returns (and lets teardown proceed) at its first
            // await, while the save is still in flight, which is exactly what caused a crash
            // on exit here before. Blocking on it synchronously instead would deadlock:
            // AppSettings.SaveAsync() has no ConfigureAwait(false), so it tries to resume on
            // the UI thread's captured context — the same thread that would be sitting there
            // blocked waiting for it. Not needed anyway: every Settings mutation (see
            // SettingsPage's Save Automation Settings button) already persists immediately on
            // its own, so there's nothing left unsaved by the time a user gets to closing the app.
            logger.LogInformation("Application Shutting Down...");
            Log.CloseAndFlush();
        };
    }

    // ── Live theming ──────────────────────────────────────────────────────────

    /// <summary>
    /// Translates a <see cref="CozyTheme"/> into XAML resource entries and overwrites
    /// the running ResourceDictionary so every bound control updates immediately.
    /// Must be called on the UI thread.
    /// </summary>
    public static void ApplyThemeToResources(CozyTheme theme)
    {
        var resources = Current.Resources;

        // Helper: mutate the existing brush (so StaticResource bindings update instantly)
        // AND explicitly place it in the top-level dictionary (so it survives WinUI 3's
        // automatic dictionary wipe during a system WM_SETTINGCHANGE broadcast).
        void UpdateBrush(string key, Color color)
        {
            if (resources.TryGetValue(key, out var obj) && obj is SolidColorBrush b)
            {
                b.Color = color;
                resources[key] = b;
            }
            else
            {
                resources[key] = new SolidColorBrush(color);
            }
        }

        // Colors (these are boxed structs, so just overwriting them is fine)
        resources["ColorBackgroundBase"] = HexToColor(theme.BackgroundBase);
        resources["ColorBackgroundAlt"] = HexToColor(theme.BackgroundAlt);
        resources["ColorSurface"] = HexToColor(theme.Surface);
        resources["ColorAccentPrimary"] = HexToColor(theme.AccentPrimary);
        resources["ColorAccentStrong"] = HexToColor(theme.AccentStrong);
        resources["ColorTextPrimary"] = HexToColor(theme.TextPrimary);
        resources["ColorTextMuted"] = HexToColor(theme.TextMuted);
        resources["ColorBorderSubtle"] = HexToColor(theme.BorderSubtle);

        // Brushes (the binding targets throughout the visual tree)
        UpdateBrush("AppBackgroundBrush", HexToColor(theme.BackgroundBase));
        UpdateBrush("SidebarBackgroundBrush", HexToColor(theme.BackgroundAlt));
        UpdateBrush("CardBackgroundBrush", HexToColor(theme.BackgroundBase));
        UpdateBrush("SurfaceBrush", HexToColor(theme.Surface));
        UpdateBrush("PrimaryAccentBrush", HexToColor(theme.AccentPrimary));
        UpdateBrush("StrongAccentBrush", HexToColor(theme.AccentStrong));
        UpdateBrush("TextPrimaryBrush", HexToColor(theme.TextPrimary));
        UpdateBrush("TextMutedBrush", HexToColor(theme.TextMuted));
        UpdateBrush("BorderSubtleBrush", HexToColor(theme.BorderSubtle));

        // Hover/pressed variants (auto-computed from base surface color).
        UpdateBrush("SurfaceHoverBrush", HexToColor(LightenHex(theme.Surface, 0.15)));
        UpdateBrush("SurfacePressedBrush", HexToColor(DarkenHex(theme.Surface, 0.10)));

        // Corner radius tokens (scaled by theme preference)
        double s = Math.Clamp(theme.CornerRadiusScale, 0.25, 2.0);
        resources["CornerRadiusLarge"] = new CornerRadius(16 * s);
        resources["CornerRadiusMedium"] = new CornerRadius(12 * s);
        resources["CornerRadiusSmall"] = new CornerRadius(8 * s);

        // Spacing tokens (scaled by density)
        double d = Math.Clamp(theme.DensityScale, 0.5, 2.0);
        resources["Space1"] = 4 * d;
        resources["Space2"] = 8 * d;
        resources["Space3"] = 12 * d;
        resources["Space4"] = 16 * d;
        resources["Space5"] = 24 * d;
        resources["Space6"] = 32 * d;
        resources["Space7"] = 48 * d;
    }

    /// <summary>
    /// Smoothly steps the live ResourceDictionary from whatever it currently shows to
    /// <paramref name="target"/> over <paramref name="duration"/>, using <see cref="ThemeInterpolator"/>
    /// for the in-between color snapshots, then calls <see cref="Services.ThemeService.SetActiveTheme"/>
    /// once at the end to commit the real theme object — rather than one of the many interpolated
    /// snapshots — so the Themes page selection, persistence, and every other <c>ThemeChanged</c>
    /// subscriber all see the actual target theme.
    ///
    /// Must be called on the UI thread (every frame touches the live Resources dictionary) —
    /// callers coming from a background thread (like <see cref="ThemeAutomationService"/>) need to
    /// dispatch first, same as every other cross-thread UI touch in this app.
    /// </summary>
    public static async Task CrossfadeToThemeAsync(CozyTheme target, TimeSpan duration)
    {
        var start = ThemeService.ActiveTheme;
        if (start.Id == target.Id || duration <= TimeSpan.Zero)
        {
            ThemeService.SetActiveTheme(target);
            return;
        }

        const int steps = 24;
        var frameDelay = duration / steps;
        for (int i = 1; i <= steps; i++)
        {
            ApplyThemeToResources(ThemeInterpolator.Lerp(start, target, (double)i / steps));
            if (i < steps) await Task.Delay(frameDelay);
        }

        ThemeService.SetActiveTheme(target);
    }

    // ── Color utilities ───────────────────────────────────────────────────────

    public static Color HexToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length switch
        {
            6 => Color.FromArgb(0xFF,
                     Convert.ToByte(hex[..2], 16),
                     Convert.ToByte(hex[2..4], 16),
                     Convert.ToByte(hex[4..6], 16)),
            8 => Color.FromArgb(
                     Convert.ToByte(hex[..2], 16),
                     Convert.ToByte(hex[2..4], 16),
                     Convert.ToByte(hex[4..6], 16),
                     Convert.ToByte(hex[6..8], 16)),
            _ => Color.FromArgb(0xFF, 0x7D, 0x5A, 0x44) // Cocoa fallback
        };
    }

    public static string ColorToHex(Color c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string LightenHex(string hex, double amount)
    {
        var c = HexToColor(hex);
        return ColorToHex(Color.FromArgb(c.A,
            (byte)Math.Min(255, c.R + 255 * amount),
            (byte)Math.Min(255, c.G + 255 * amount),
            (byte)Math.Min(255, c.B + 255 * amount)));
    }

    private static string DarkenHex(string hex, double amount)
    {
        var c = HexToColor(hex);
        return ColorToHex(Color.FromArgb(c.A,
            (byte)Math.Max(0, c.R - 255 * amount),
            (byte)Math.Max(0, c.G - 255 * amount),
            (byte)Math.Max(0, c.B - 255 * amount)));
    }
}