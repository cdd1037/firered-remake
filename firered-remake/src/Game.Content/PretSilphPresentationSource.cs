using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>Pinned presentation inputs rebuilt from original Git blobs, never executed upstream.</summary>
public static class PretSilphPresentationSource
{
    public const string LockSha256 = "5e0b38acae2fbd5780363b1240329da8e0e3040217754f20068b08cf9cf04ea4";
    public const int LockByteLength = 23516;
    public const int SourceCount = 116;
    public const string RawDirectory = "content/raw/silph-presentation/";
    public static ImmutableDictionary<string, byte[]> Load(string root)
    {
        var bytes = SafariSource.Read(root, "scripts/silph-presentation.lock.json", LockSha256, LockByteLength);
        using var json = JsonDocument.Parse(bytes); var doc = json.RootElement;
        SafariSource.Require(doc.GetProperty("schemaVersion").GetInt32() == 1 &&
            doc.GetProperty("profile").GetString() == "firered-silph-presentation-v1" &&
            doc.GetProperty("repository").GetString() == "pret/pokefirered" &&
            doc.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            doc.GetProperty("tree").GetString() == "7515cd4067ea55f8cdbc73624478afd2736c58b9" &&
            doc.GetProperty("files").GetArrayLength() == SourceCount, "Silph presentation source identity differs.");
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in doc.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            var content = SafariSource.Read(root, RawDirectory + path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            SafariSource.Require(SafariSource.GitBlob(content) == row.GetProperty("gitBlobSha1").GetString(), "Silph presentation Git blob differs.");
            files.Add(path, content);
        }
        return files.ToImmutable();
    }
    internal static ImmutableDictionary<string, byte[]> Outputs(string root, string directory, ImmutableArray<SnorlaxArtOutput> outputs)
    {
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in outputs) files.Add(row.Path, SafariSource.Read(root, directory + row.Path, row.Sha256, row.ByteLength));
        return files.ToImmutable();
    }
    internal static byte[] Texture(string projectRelativePath, string directory, ImmutableDictionary<string, byte[]> files)
    {
        if (!projectRelativePath.StartsWith(directory, StringComparison.Ordinal) || !files.TryGetValue(projectRelativePath[directory.Length..], out var bytes))
            throw new InvalidDataException("Unknown Silph texture.");
        return [.. bytes];
    }
}
