using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

/// <summary>Portable widgets: skin.json plus referenced raster images under assets/.</summary>
public sealed class SkinPackageService
{
    public const int MaxEntries = 128;
    public const long MaxAssetBytes = 16 * 1024 * 1024;
    public const long MaxExpandedBytes = 64 * 1024 * 1024;
    public const long MaxManifestBytes = 1024 * 1024;
    private const string SkinFileName = "skin.json";
    private readonly string _assetRoot;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico" };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SkinPackageService(string? assetRoot = null)
    {
        _assetRoot = Path.GetFullPath(assetRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThemedAI", "WidgetAssets"));
    }

    public static bool IsSupportedImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    public async Task ExportAsync(SkinDefinition skin, string packagePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skin);
        Validate(skin);
        var portable = skin.Clone();
        portable.Enabled = false;
        // These targets contain account credentials/API keys, not portable widget design.
        // Custom text, variables and arbitrary URLs are preserved and still need author review.
        foreach (var measure in portable.Measures)
        {
            if (measure.Type is MeasureType.WeatherTemp or MeasureType.WeatherDesc or MeasureType.WeatherCity
                or MeasureType.VibeTrackTitle or MeasureType.VibeTrackArtist or MeasureType.VibeMood
                or MeasureType.VibePlaybackState or MeasureType.VibeTrackProgress or MeasureType.WebJson
                || !string.IsNullOrWhiteSpace(measure.PluginType))
                measure.Target = null;
        }

        var assets = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var meter in portable.Meters)
        {
            if (string.IsNullOrWhiteSpace(meter.ImagePath)) continue;
            if (!Path.IsPathFullyQualified(meter.ImagePath) || !IsSupportedImage(meter.ImagePath))
                throw new InvalidDataException("Images must be local PNG, JPEG, GIF, BMP or ICO files.");
            var source = Path.GetFullPath(meter.ImagePath);
            if (source.Equals(Path.GetFullPath(packagePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The package cannot replace one of its own images.");
            if (!assets.TryGetValue(source, out var name))
            {
                name = $"assets/{assets.Count + 1:D3}{Path.GetExtension(source).ToLowerInvariant()}";
                assets.Add(source, name);
            }
            meter.ImagePath = name;
        }
        if (assets.Count + 1 > MaxEntries)
            throw new InvalidDataException($"A package can contain at most {MaxEntries - 1} images.");

        var manifest = JsonSerializer.SerializeToUtf8Bytes(portable, JsonOptions);
        if (manifest.LongLength > MaxManifestBytes)
            throw new InvalidDataException("The widget definition exceeds 1 MiB.");
        var target = Path.GetFullPath(packagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
                await using (var stream = archive.CreateEntry(SkinFileName).Open())
                    await stream.WriteAsync(manifest, cancellationToken);
                long total = manifest.LongLength;
                foreach (var (source, name) in assets)
                {
                    await using var input = File.OpenRead(source);
                    await using var output = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
                    total += await CopyLimitedAsync(input, output, Math.Min(MaxAssetBytes, MaxExpandedBytes - total), cancellationToken);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<SkinDefinition> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(packagePath);
        if (file.Length > MaxExpandedBytes + MaxManifestBytes)
            throw new InvalidDataException("The widget package is too large.");
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var entries = ValidateEntries(archive);
        if (!entries.TryGetValue(SkinFileName, out var entry))
            throw new InvalidDataException("The widget package does not contain skin.json.");

        SkinDefinition skin;
        try
        {
            await using var input = entry.Open();
            using var json = new MemoryStream();
            await CopyLimitedAsync(input, json, MaxManifestBytes, cancellationToken);
            skin = JsonSerializer.Deserialize<SkinDefinition>(json.ToArray(), JsonOptions)
                ?? throw new InvalidDataException("skin.json is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("skin.json is not a valid widget definition.", ex);
        }
        Validate(skin);
        // Normalize only supported legacy versions; never silently downgrade future packages.
        if (skin.SchemaVersion < 4) skin.AlwaysOnTop = false;
        foreach (var meter in skin.Meters) meter.States ??= new List<MeterStateDefinition>();
        if (skin.Anchor is not null) skin.Anchor.Responsive ??= new List<WidgetResponsiveRule>();
        skin.SchemaVersion = SkinDefinition.CurrentSchemaVersion;
        skin.Id = Guid.NewGuid().ToString();
        skin.Enabled = false;
        // A package from another monitor should be easy to find when first enabled.
        skin.X = 40;
        skin.Y = 40;

        var references = skin.Meters.Where(m => !string.IsNullOrWhiteSpace(m.ImagePath))
            .Select(m => m.ImagePath!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var reference in references)
        {
            ValidateAssetName(reference);
            if (!entries.ContainsKey(reference))
                throw new InvalidDataException($"The package is missing image '{reference}'.");
        }
        if (references.Count == 0) return skin;

        Directory.CreateDirectory(_assetRoot);
        var staging = Path.Combine(_assetRoot, $".import-{Guid.NewGuid():N}");
        var installed = Path.Combine(_assetRoot, skin.Id);
        Directory.CreateDirectory(staging);
        try
        {
            long total = 0;
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in references)
            {
                // Generate filenames ourselves. Archive paths are never used as filesystem paths.
                var localName = $"{paths.Count + 1:D3}{Path.GetExtension(reference).ToLowerInvariant()}";
                await using var input = entries[reference].Open();
                await using var output = new FileStream(Path.Combine(staging, localName), FileMode.CreateNew, FileAccess.Write);
                total += await CopyLimitedAsync(input, output, Math.Min(MaxAssetBytes, MaxExpandedBytes - total), cancellationToken);
                paths.Add(reference, Path.Combine(installed, localName));
            }
            foreach (var meter in skin.Meters)
                if (!string.IsNullOrWhiteSpace(meter.ImagePath)) meter.ImagePath = paths[meter.ImagePath];
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, installed);
            return skin;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static Dictionary<string, ZipArchiveEntry> ValidateEntries(ZipArchive archive)
    {
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidDataException($"The package exceeds {MaxEntries} entries.");
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Symbolic links are not supported in widget packages.");
            if (entry.FullName == "assets/" && entry.Length == 0) continue;
            bool manifest = entry.FullName.Equals(SkinFileName, StringComparison.OrdinalIgnoreCase);
            if (!manifest) ValidateAssetName(entry.FullName);
            if (!entries.TryAdd(entry.FullName, entry))
                throw new InvalidDataException($"Duplicate package entry '{entry.FullName}'.");
            var limit = manifest ? MaxManifestBytes : MaxAssetBytes;
            if (entry.Length > limit || entry.Length < 0 || total > MaxExpandedBytes - entry.Length)
                throw new InvalidDataException("The package exceeds the expanded size limits.");
            total += entry.Length;
        }
        return entries;
    }

    private static void ValidateAssetName(string name)
    {
        // Use portable Windows filename rules even when validating on Linux.
        var parts = name.Split('/');
        if (parts.Length != 2 || parts[0] != "assets" || !IsSupportedImage(name)
            || parts[1].Length == 0 || parts[1].Length > 160 || parts[1].EndsWith('.')
            || parts[1].Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            || parts[1].StartsWith('.'))
            throw new InvalidDataException("Package images must use simple filenames inside assets/.");
    }

    private static void Validate(SkinDefinition skin)
    {
        if (skin.SchemaVersion < 1 || skin.SchemaVersion > SkinDefinition.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported widget schema version {skin.SchemaVersion}.");
        if (skin.Measures is null || skin.Meters is null || skin.Groups is null
            || skin.Measures.Any(m => m is null) || skin.Meters.Any(m => m is null) || skin.Groups.Any(g => g is null))
            throw new InvalidDataException("Widget measures, meters and groups must be arrays without null items.");
        var issues = WidgetDefinitionValidator.Validate(skin).Where(i => i.IsError).ToList();
        if (issues.Count > 0)
            throw new InvalidDataException($"Invalid widget: {string.Join("; ", issues.Select(i => i.Message))}");
    }

    private static async Task<long> CopyLimitedAsync(Stream input, Stream output, long limit, CancellationToken token)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            if (total > limit - read) throw new InvalidDataException("An image or definition exceeds the package size limits.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            total += read;
        }
        return total;
    }
}
