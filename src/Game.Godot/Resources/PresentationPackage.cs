using System.Text.Json;
using Game.Content;

/// <summary>Shared generated manifest/file loading. PNG validation belongs to the native upload boundary.</summary>
internal sealed record PresentationPackage(JsonElement Manifest, Dictionary<string, byte[]> Files)
{
    internal static JsonElement ReadManifest(string root, string directory)
    {
        using var document = JsonDocument.Parse(GeneratedResourceFile.Read(root, directory + "manifest.json"));
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported presentation manifest: " + directory);
        return document.RootElement.Clone();
    }

    internal static PresentationPackage Load(string root, string directory, bool relativeOutputKeys = false,
        params string[] assetGroups)
    {
        var manifest = ReadManifest(root, directory);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in manifest.GetProperty("outputs").EnumerateArray())
        {
            string name = output.GetProperty("path").GetString()!;
            files.Add(relativeOutputKeys ? name : directory + name, GeneratedResourceFile.Read(root, directory + name));
        }
        foreach (string group in assetGroups)
        foreach (var asset in manifest.GetProperty(group).EnumerateArray())
        {
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, GeneratedResourceFile.Read(root, path));
            if (asset.TryGetProperty("width", out var width) && width.GetInt32() <= 0 ||
                asset.TryGetProperty("height", out var height) && height.GetInt32() <= 0)
                throw new InvalidDataException("Presentation image dimensions must be positive: " + path);
        }
        return new(manifest, files);
    }
}
