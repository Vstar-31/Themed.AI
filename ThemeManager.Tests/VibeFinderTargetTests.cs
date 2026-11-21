using System.Security.Cryptography;
using System.Text;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class VibeFinderTargetTests
{
    private const string Dummy = "dummy-Pass#123";

    // Reversible stand-in for DPAPI; never contains the plain text.
    private sealed class FakeProtector : ISecretProtector
    {
        public bool FailProtect, FailUnprotect, WrongRoundTrip;
        public string Protect(string plain)
        {
            if (FailProtect) throw new CryptographicException("protect failed");
            return Convert.ToBase64String(Encoding.UTF8.GetBytes("X" + new string(plain.Reverse().ToArray())));
        }
        public bool TryUnprotect(string stored, out string plain)
        {
            plain = "";
            if (FailUnprotect) return false;
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(stored));
            plain = WrongRoundTrip ? "different" : new string(raw[1..].Reverse().ToArray());
            return true;
        }
    }

    [Fact]
    public void Build_StoresProtectedPasswordAndKeepsUserAndVibe()
    {
        var target = VibeFinderTarget.Build("alice", Dummy, "calm rain", new FakeProtector());
        Assert.DoesNotContain(Dummy, target);
        Assert.StartsWith("alice|" + VibeFinderTarget.ProtectedPrefix, target);
        Assert.EndsWith("|calm rain", target);
        Assert.Equal(Dummy, VibeFinderTarget.ResolvePassword(target.Split('|', 3)[1], new FakeProtector()));
    }

    [Fact]
    public void Build_ThrowsInsteadOfFallingBackToPlainText() =>
        Assert.Throws<CryptographicException>(() =>
            VibeFinderTarget.Build("alice", Dummy, "v", new FakeProtector { FailProtect = true }));

    [Fact]
    public void ResolvePassword_ReadsLegacyPlainTextAndFailsClosedWhenUndecryptable()
    {
        Assert.Equal("old-plain", VibeFinderTarget.ResolvePassword("old-plain", new FakeProtector()));
        var stored = VibeFinderTarget.Build("alice", Dummy, "v", new FakeProtector()).Split('|', 3)[1];
        var unreadable = VibeFinderTarget.ResolvePassword(stored, new FakeProtector { FailUnprotect = true });
        Assert.Equal("", unreadable);
        Assert.False(VibeFinderCredentials.IsConfigured("alice", unreadable));
    }

    [Theory]
    [InlineData("alice|dummy-Pass#123|calm rain")]
    [InlineData("|alice|dummy-Pass#123|calm rain")]
    public void TryProtect_MigratesLegacyTargetAndIsIdempotent(string legacy)
    {
        var p = new FakeProtector();
        Assert.True(VibeFinderTarget.TryProtect(legacy, out var migrated, p));
        Assert.DoesNotContain(Dummy, migrated);
        Assert.Equal(legacy.StartsWith('|'), migrated.StartsWith('|'));
        Assert.EndsWith("|calm rain", migrated);
        Assert.False(VibeFinderTarget.TryProtect(migrated, out var again, p));
        Assert.Equal(migrated, again);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TryProtect_KeepsOriginalWhenProtectionOrVerificationFails(bool failProtect, bool wrongRoundTrip)
    {
        const string legacy = "alice|dummy-Pass#123|v";
        var p = new FakeProtector { FailProtect = failProtect, WrongRoundTrip = wrongRoundTrip };
        Assert.False(VibeFinderTarget.TryProtect(legacy, out var result, p));
        Assert.Equal(legacy, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("||vibe only")]
    [InlineData("listener|hunter2|placeholder account")]
    public void TryProtect_SkipsEmptyAndPlaceholderTargets(string? target) =>
        Assert.False(VibeFinderTarget.TryProtect(target, out _, new FakeProtector()));

    [Fact]
    public void Equivalent_IgnoresBlobDifferencesButCatchesRealChanges()
    {
        var p = new FakeProtector();
        var a = VibeFinderTarget.Build("alice", Dummy, "v", p);
        Assert.True(VibeFinderTarget.Equivalent(a, "alice|" + Dummy + "|v", p));
        Assert.False(VibeFinderTarget.Equivalent(a, VibeFinderTarget.Build("alice", "other", "v", p), p));
        Assert.False(VibeFinderTarget.Equivalent(a, VibeFinderTarget.Build("bob", Dummy, "v", p), p));
        Assert.False(VibeFinderTarget.Equivalent(a, VibeFinderTarget.Build("alice", Dummy, "w", p), p));
        Assert.False(VibeFinderTarget.Equivalent(a, "||v", p));
        Assert.False(VibeFinderTarget.Equivalent(a, "alice|dpapi1:QUJD|v", new FakeProtector { FailUnprotect = true }));
    }

    [Theory]
    [InlineData("alice|dummy-Pass#123|v")]
    [InlineData("alice|dpapi1:QUJD|v")]
    public async Task PackageExport_NeverContainsCredentialBytes(string target)
    {
        var root = Path.Combine(Path.GetTempPath(), "ThemedAI-cred", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var skin = new SkinDefinition { Measures = new() { new() { Name = "Mood", Type = MeasureType.VibeMood, Target = target } } };
            var package = Path.Combine(root, "w.themedwidget");
            await new SkinPackageService(Path.Combine(root, "installed")).ExportAsync(skin, package);
            // Package entries may be compressed; check the imported definition and the raw bytes.
            var imported = await new SkinPackageService(Path.Combine(root, "installed")).ImportAsync(package);
            Assert.Null(imported.Measures.Single().Target);
            Assert.DoesNotContain(Dummy, Encoding.Latin1.GetString(await File.ReadAllBytesAsync(package)));
            Assert.Equal(target, skin.Measures[0].Target); // original not mutated
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
