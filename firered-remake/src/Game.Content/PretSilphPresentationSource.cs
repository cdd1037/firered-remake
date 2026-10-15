using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>Pinned presentation inputs rebuilt from original Git blobs, never executed upstream.</summary>
public static class PretSilphPresentationSource
{
    public const int SourceCount = 116;
    public const string RawDirectory = PretSourcePaths.FireRedRawDirectory + "/";
    public static ImmutableDictionary<string, byte[]> Load(string root)
    {
        var bytes = SafariSource.Read(root,"scripts/silph-presentation.lock.json");
        using var json = JsonDocument.Parse(bytes); var doc = json.RootElement;
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in doc.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            var content = SafariSource.Read(root,RawDirectory + path);
            files.Add(path, content);
        }
        return files.ToImmutable();
    }
    internal static ImmutableDictionary<string, byte[]> Outputs(string root, string directory, ImmutableArray<SnorlaxArtOutput> outputs)
    {
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in outputs) files.Add(row.Path, SafariSource.Read(root,directory + row.Path));
        return files.ToImmutable();
    }
    internal static byte[] Texture(string projectRelativePath, string directory, ImmutableDictionary<string, byte[]> files)
    {
        if (!projectRelativePath.StartsWith(directory, StringComparison.Ordinal) || !files.TryGetValue(projectRelativePath[directory.Length..], out var bytes))
            throw new InvalidDataException("Unknown Silph texture.");
        return [.. bytes];
    }
}
