using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ThemeManager.Core.Services;

/// <summary>Turns a secret into text that is safe to store, and back.</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    bool TryUnprotect(string stored, out string plain);
}

/// <summary>
/// Windows DPAPI (CurrentUser scope) via crypt32. Only the same Windows user on the same
/// machine can decrypt, so a copied settings file or package never carries a usable password.
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ThemedAI.VibeFinder.v1");
    private const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int cbData; public IntPtr pbData; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, ref DataBlob entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    public string Protect(string plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        var input = Encoding.UTF8.GetBytes(plain);
        try { return Convert.ToBase64String(Run(input, protect: true)); }
        finally { Array.Clear(input); }
    }

    public bool TryUnprotect(string stored, out string plain)
    {
        plain = "";
        byte[]? output = null;
        try
        {
            output = Run(Convert.FromBase64String(stored), protect: false);
            plain = Encoding.UTF8.GetString(output);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
        finally { if (output is not null) Array.Clear(output); }
    }

    private static byte[] Run(byte[] data, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI needs Windows.");
        var input = new DataBlob { cbData = data.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, data.Length)) };
        var entropy = new DataBlob { cbData = Entropy.Length, pbData = Marshal.AllocHGlobal(Entropy.Length) };
        var output = new DataBlob();
        try
        {
            Marshal.Copy(data, 0, input.pbData, data.Length);
            Marshal.Copy(Entropy, 0, entropy.pbData, Entropy.Length);
            bool ok = protect
                ? CryptProtectData(ref input, null, ref entropy, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output);
            if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, output.cbData);
            return result;
        }
        finally
        {
            if (data.Length > 0) Marshal.Copy(new byte[data.Length], 0, input.pbData, data.Length);
            Marshal.FreeHGlobal(input.pbData);
            Marshal.FreeHGlobal(entropy.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }
}

/// <summary>
/// Helpers for the "user|password|vibe" VibeFinder target. The password segment is stored as
/// "dpapi1:&lt;base64&gt;" instead of plain text. Legacy plain-text targets still read fine until migrated.
/// </summary>
public static class VibeFinderTarget
{
    public const string ProtectedPrefix = "dpapi1:";
    private static readonly Lazy<ISecretProtector> DefaultProtector = new(() => new DpapiSecretProtector());

    public static bool IsProtected(string? segment) =>
        segment is not null && segment.StartsWith(ProtectedPrefix, StringComparison.Ordinal);

    /// <summary>Builds a new target with the password protected. Throws if protection fails, so plain text is never saved by accident.</summary>
    public static string Build(string user, string password, string vibe, ISecretProtector? protector = null) =>
        $"{user}|{ProtectedPrefix}{(protector ?? DefaultProtector.Value).Protect(password)}|{vibe}";

    /// <summary>Returns the usable password for a stored segment. Empty when it cannot be unprotected (the widget then asks to sign in).</summary>
    public static string ResolvePassword(string segment, ISecretProtector? protector = null)
    {
        if (!IsProtected(segment)) return segment;
        return (protector ?? DefaultProtector.Value).TryUnprotect(segment[ProtectedPrefix.Length..], out var plain) ? plain : "";
    }

    /// <summary>
    /// Migrates a legacy plain-text target. Returns true only when <paramref name="migrated"/> holds a verified
    /// protected copy. On any failure it returns false and the caller must keep the original untouched.
    /// </summary>
    public static bool TryProtect(string? target, out string migrated, ISecretProtector? protector = null)
    {
        migrated = target ?? "";
        if (string.IsNullOrWhiteSpace(target)) return false;
        try
        {
            protector ??= DefaultProtector.Value;
            string lead = target.StartsWith('|') && !target.StartsWith("||", StringComparison.Ordinal) ? "|" : "";
            var parts = target[lead.Length..].Split('|', 3);
            if (parts.Length < 2 || IsProtected(parts[1]) || !VibeFinderCredentials.IsConfigured(parts[0], parts[1]))
                return false;

            string stored = protector.Protect(parts[1]);
            if (!protector.TryUnprotect(stored, out var roundTrip) || !string.Equals(roundTrip, parts[1], StringComparison.Ordinal))
                return false;

            migrated = lead + parts[0] + "|" + ProtectedPrefix + stored + (parts.Length == 3 ? "|" + parts[2] : "");
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or ArgumentException)
        {
            migrated = target;
            return false;
        }
    }
}
