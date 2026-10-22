using System.Security.Cryptography;

namespace ThemeManager.Core.Services;

public sealed record WidgetAsset(string Path, string Name, long Bytes, string Status, bool Available);
public sealed record WidgetAssetListing(IReadOnlyList<WidgetAsset> Items, bool Truncated);

/// <summary>Managed raster imports and a bounded inventory of imported/package images.</summary>
public sealed class WidgetAssetLibrary
{
    public const int MaxListedAssets = 500;
    private static readonly SemaphoreSlim ImportGate = new(1, 1);
    private readonly string _root;
    public WidgetAssetLibrary(string? root = null) => _root = System.IO.Path.GetFullPath(root ??
        System.IO.Path.Combine(AppDataPaths.LocalRoot, "ThemedAI", "WidgetAssets"));

    public static WidgetAsset Inspect(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        try
        {
            if (!System.IO.Path.IsPathFullyQualified(path) || !SkinPackageService.IsSupportedImage(path))
                return new(path, name, 0, "Unsupported image reference", false);
            var file = new FileInfo(path);
            if (!file.Exists) return new(path, name, 0, "Missing file — import a replacement", false);
            if (file.Length == 0 || file.Length > SkinPackageService.MaxAssetBytes)
                return new(path, name, file.Length, "Image must be between 1 byte and 16 MiB", false);
            return new(path, name, file.Length, $"{file.Length / 1024.0:0.#} KB", true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(path, name, 0, "File unavailable — check access or import a replacement", false);
        }
    }

    public Task<WidgetAssetListing> ListAsync(IEnumerable<string>? references = null, CancellationToken cancellationToken = default)
    {
        var current = references?.ToArray() ?? Array.Empty<string>();
        return Task.Run(() =>
        {
            var items = new Dictionary<string, WidgetAsset>(StringComparer.OrdinalIgnoreCase);
            bool truncated = false;
            void Add(string path)
            {
                if (items.ContainsKey(path)) return;
                if (items.Count >= MaxListedAssets) { truncated = true; return; }
                items[path] = Inspect(path);
            }
            foreach (var reference in current.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(reference);
            }
            if (Directory.Exists(_root))
            {
                int visited = 0;
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true, IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 4
                };
                foreach (var path in Directory.EnumerateFiles(_root, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++visited > 5000 || items.Count >= MaxListedAssets) { truncated = true; break; }
                    if (SkinPackageService.IsSupportedImage(path)) Add(path);
                }
            }
            return new WidgetAssetListing(items.Values.OrderBy(a => a.Available).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray(), truncated);
        }, cancellationToken);
    }

    public async Task<WidgetAsset> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        await ImportGate.WaitAsync(cancellationToken);
        try { return await ImportCoreAsync(sourcePath, cancellationToken); }
        finally { ImportGate.Release(); }
    }

    private async Task<WidgetAsset> ImportCoreAsync(string sourcePath, CancellationToken cancellationToken)
    {
        if (!System.IO.Path.IsPathFullyQualified(sourcePath) || !SkinPackageService.IsSupportedImage(sourcePath))
            throw new InvalidDataException("Choose a local PNG, JPEG, GIF, BMP or ICO image.");
        cancellationToken.ThrowIfCancellationRequested();
        var library = System.IO.Path.Combine(_root, "Library");
        Directory.CreateDirectory(library);
        RejectLink(_root); RejectLink(library);
        var temporary = System.IO.Path.Combine(library, $".{Guid.NewGuid():N}.tmp");
        try
        {
            string hash;
            await using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                if (input.Length == 0 || input.Length > SkinPackageService.MaxAssetBytes)
                    throw new InvalidDataException("Images must be between 1 byte and 16 MiB.");
                byte[] header = new byte[16];
                int headerLength = await input.ReadAtLeastAsync(header, (int)Math.Min(header.Length, input.Length), false, cancellationToken);
                if (!MatchesImageHeader(header.AsSpan(0, headerLength), System.IO.Path.GetExtension(sourcePath)))
                    throw new InvalidDataException("The file content does not match its image format.");
                input.Position = 0;
                await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[81920];
                long size = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    size += count;
                    if (size > SkinPackageService.MaxAssetBytes) throw new InvalidDataException("Image exceeds 16 MiB.");
                    digest.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                hash = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            }
            cancellationToken.ThrowIfCancellationRequested();
            string extension = System.IO.Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension == ".jpeg") extension = ".jpg";
            var directory = System.IO.Path.Combine(library, hash);
            Directory.CreateDirectory(directory);
            RejectLink(directory);
            // The content hash and canonical extension identify one immutable imported asset.
            var existing = Directory.EnumerateFiles(directory, "*" + extension).FirstOrDefault();
            if (existing is not null)
            {
                RejectLink(existing);
                await using var existingStream = File.OpenRead(existing);
                var existingHash = Convert.ToHexString(await SHA256.HashDataAsync(existingStream, cancellationToken)).ToLowerInvariant();
                if (existingHash != hash) throw new InvalidDataException("The stored image changed. Refresh the library and choose another image.");
                return Inspect(existing);
            }
            var stem = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            if (stem.Length > 80) stem = stem[..80];
            var destination = System.IO.Path.Combine(directory, stem + extension);
            File.Move(temporary, destination, overwrite: false);
            return Inspect(destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The asset library location must not be a symbolic link or junction.");
    }

    private static bool MatchesImageHeader(ReadOnlySpan<byte> bytes, string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".jpg" or ".jpeg" => bytes.StartsWith(new byte[] { 255, 216, 255 }),
        ".gif" => bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8),
        ".bmp" => bytes.StartsWith("BM"u8),
        ".ico" => bytes.StartsWith(new byte[] { 0, 0, 1, 0 }),
        _ => false
    };
}
