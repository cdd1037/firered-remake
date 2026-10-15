using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>Generated presentation file access shared by Silph-related packages.</summary>
public static class PretSilphPresentationSource
{
    internal static ImmutableDictionary<string, byte[]> LoadGenerated(string root, string directory)
    {
        using var manifest = JsonDocument.Parse(SafariSource.Read(root, directory + "manifest.json"));
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.RootElement.GetProperty("outputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            if (path.StartsWith(directory, StringComparison.Ordinal)) path = path[directory.Length..];
            files.Add(path, SafariSource.Read(root, path.StartsWith("content/generated/", StringComparison.Ordinal) ? path : directory + path));
        }
        return files.ToImmutable();
    }
    internal static ImmutableDictionary<string, byte[]> Outputs(string root, string directory, ImmutableArray<SnorlaxArtOutput> outputs)
    {
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in outputs) files.Add(row.Path, SafariSource.Read(root,row.Path.StartsWith("content/generated/", StringComparison.Ordinal) ? row.Path : directory + row.Path));
        return files.ToImmutable();
    }
    internal static byte[] Texture(string projectRelativePath, string directory, ImmutableDictionary<string, byte[]> files)
    {
        if (!projectRelativePath.StartsWith(directory, StringComparison.Ordinal) || !files.TryGetValue(projectRelativePath[directory.Length..], out var bytes))
            throw new InvalidDataException("Unknown Silph texture.");
        return [.. bytes];
    }
}
