using ThemeManager.Core.Models;
using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class ThemeAutomationRegressionTests
{
    [Fact]
    public async Task WeatherRuleRunsWithoutAnApiKey()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ThemedAI_weather_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new ThemeRepository(folder);
            await repository.SaveAllAsync(new[] { new CozyTheme { Id = "sunny" } });
            var service = new ThemeService(repository);
            await service.InitializeAsync();
            var schedule = new ThemeSchedule { Enabled = true, WeatherReactiveEnabled = true,
                WeatherCity = "Jaipur", WeatherClearThemeId = "sunny" };
            var provider = new KeyFreeWeather();
            using var automation = new ThemeAutomationService(service, new BatterySystem(), () => schedule, provider);
            var applied = new List<string>();
            automation.AutomationTriggered += theme => applied.Add(theme.Id);
            await automation.CheckNowAsync();
            Assert.Equal(new[] { "sunny" }, applied);
            Assert.True(provider.Called);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private sealed class KeyFreeWeather : IWeatherConditionProvider
    {
        public bool Called;
        public Task<WeatherCondition?> GetCurrentConditionAsync(string city, string apiKey)
        {
            Assert.Equal("Jaipur", city);
            Assert.Empty(apiKey);
            Called = true;
            return Task.FromResult<WeatherCondition?>(WeatherCondition.Clear);
        }
    }

    [Fact]
    public async Task ChangingThemeDuringBatterySaver_AppliesNewSelectionOnce()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ThemedAI_automation_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new ThemeRepository(folder);
            await repository.SaveAllAsync(new[] { new CozyTheme { Id = "one" }, new CozyTheme { Id = "two" } });
            var service = new ThemeService(repository);
            await service.InitializeAsync();
            var schedule = new ThemeSchedule { Enabled = true, BatterySaverEnabled = true, BatterySaverThemeId = "one" };
            using var automation = new ThemeAutomationService(service, new BatterySystem(), () => schedule);
            var applied = new List<string>();
            automation.AutomationTriggered += theme => applied.Add(theme.Id);
            await automation.CheckNowAsync();
            await automation.CheckNowAsync();
            schedule.BatterySaverThemeId = "two";
            await automation.CheckNowAsync();
            Assert.Equal(new[] { "one", "two" }, applied);
            schedule.Enabled = false;
            await automation.CheckNowAsync();
            schedule.Enabled = true;
            await automation.CheckNowAsync();
            Assert.Equal(new[] { "one", "two", "two" }, applied);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private sealed class BatterySystem : ISystemThemeIntegrator
    {
        public Task<bool> IsBatterySaverActiveAsync() => Task.FromResult(true);
        public Task<string> GetCurrentAccentColorAsync() => Task.FromResult("#000000");
        public Task<bool> ApplyAccentColorAsync(string color) => Task.FromResult(true);
        public Task<bool> ApplyWindowsThemeAsync(bool light) => Task.FromResult(true);
        public Task<bool> ApplyWallpaperAsync(string path) => Task.FromResult(true);
        public Task<SystemThemeInfo> GetCurrentSystemThemeAsync() => Task.FromResult(new SystemThemeInfo(true, "#000000", "test"));
        public Task<bool> ResetAccentColorAsync() => Task.FromResult(true);
    }
}
