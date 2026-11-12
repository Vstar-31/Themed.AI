using System.Text.Json;
using ThemeManager.Core.Services;

namespace ThemeManager.Tests;

public sealed class VibeFinderSiteTests
{
    [Fact]
    public async Task LoginScriptsExecuteSessionRenewalScenarios()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Fixtures", "vibefinder-login.cjs"));
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new
        {
            automatic = VibeFinderLoginScripts.Build("test'user", "test\\password"),
            forced = VibeFinderLoginScripts.Build("test'user", "test\\password", force: true),
            ready = VibeFinderLoginScripts.SessionReadyScript,
            renewal = VibeFinderLoginScripts.BuildRenewal("test'user", "test\\password", "11111111-1111-4111-8111-111111111111")
        }));
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await output + await errors);
    }

    [Theory]
    [InlineData("https://vfaithemed.netlify.app")]
    [InlineData("https://VFAITHEMED.netlify.app:443/app?embed=1#music")]
    public void AcceptsOnlyConfiguredHttpsOrigin(string source) => Assert.True(VibeFinderSite.IsTrusted(source));

    [Theory]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("http://vfaithemed.netlify.app/app")]
    [InlineData("https://vfaithemed.netlify.app:444/app")]
    [InlineData("https://vfaithemed.netlify.app.evil.example/app")]
    [InlineData("https://evil.example/vibefinderai")]
    [InlineData("https://evil.example/?next=https://vfaithemed.netlify.app")]
    [InlineData("https://vfaithemed.netlify.app@evil.example")]
    [InlineData("https://user@vfaithemed.netlify.app")]
    [InlineData("https://vibefinderai.netlify.app/app")]
    [InlineData("file:///vibefinderai.html")]
    [InlineData("about:blank")]
    public void RejectsUntrustedDocumentOrMessageSources(string? source) => Assert.False(VibeFinderSite.IsTrusted(source));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoginScriptsGuardExecutionAndPreserveCredentials(bool force)
    {
        const string user = "user'\\\nname";
        const string password = "'); fetch('https://evil.example'); //\r\n\u2028\"";
        var script = VibeFinderLoginScripts.Build(user, password, force);
        Assert.Contains($"fd.append('username', {JsonSerializer.Serialize(user)})", script);
        Assert.Contains($"fd.append('password', {JsonSerializer.Serialize(password)})", script);
        var guard = script.IndexOf($"if (location.origin !== {JsonSerializer.Serialize(VibeFinderSite.Origin)}) return;", StringComparison.Ordinal);
        Assert.True(guard >= 0 && guard < script.IndexOf("const postResult", StringComparison.Ordinal));
        Assert.Equal(!force, script.Contains("if (window._themed_auto_login) return;"));
    }
}
