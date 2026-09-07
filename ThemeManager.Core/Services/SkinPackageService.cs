using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Services;

/// <summary>
/// Portable widget-package format. A package is a normal ZIP containing a versioned skin.json and
/// optional asset files. Keeping this in Core makes gallery/import/export code independent of WinUI
/// and gives Themed.AI a clean interchange format instead of forcing users to copy internal JSON.
/// </summary>
public sealed class SkinPackageService
{
    private const string SkinFileName = "skin.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task ExportAsync(SkinDefinition skin, string packagePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skin);
        var issues = WidgetDefinitionValidator.Validate(skin).Where(i => i.IsError).ToList();
        if (issues.Count > 0)
            throw new InvalidDataException($"Widget '{skin.Name}' is invalid: {string.Join("; ", issues.Select(i => i.Message))}");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(packagePath))!);
        await using var file = File.Create(packagePath);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        var entry = archive.CreateEntry(SkinFileName, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, skin, JsonOptions, cancellationToken);
    }

    public async Task<SkinDefinition> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(packagePath);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
        var entry = archive.GetEntry(SkinFileName)
            ?? throw new InvalidDataException("The widget package does not contain skin.json.");

        await using var stream = entry.Open();
        var skin = await JsonSerializer.DeserializeAsync<SkinDefinition>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("skin.json is empty or invalid.");

        // Never trust ids from a package as global identity. Imported widgets receive a fresh id,
        // while measure/meter ids remain stable within the imported widget for editor references.
        skin.Id = Guid.NewGuid().ToString();
        skin.Enabled = false;
        skin.SchemaVersion = 2;

        foreach (var measure in skin.Measures)
            if (string.IsNullOrWhiteSpace(measure.Id)) measure.Id = Guid.NewGuid().ToString();
        foreach (var meter in skin.Meters)
            if (string.IsNullOrWhiteSpace(meter.Id)) meter.Id = Guid.NewGuid().ToString();

        var issues = WidgetDefinitionValidator.Validate(skin).Where(i => i.IsError).ToList();
        if (issues.Count > 0)
            throw new InvalidDataException($"Imported widget is invalid: {string.Join("; ", issues.Select(i => i.Message))}");

        return skin;
    }
}
