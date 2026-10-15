using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>Reads a generated package and its source paths for script compilation.</summary>
internal sealed class SilphSourcePackage
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal ImmutableDictionary<string, byte[]> Raw { get; }
    internal ImmutableDictionary<string, byte[]> Outputs { get; }
    internal ImmutableArray<PretWorldSourceFile> Files { get; }
    internal JsonElement Manifest { get; }
    internal JsonElement Lock { get; }
    internal string Directory { get; }
    internal static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal SilphSourcePackage(string root, string name, string lockPath)
    {
        Directory = Path.Combine(root, "content/generated/" + name);
        Lock = Json(SafariSource.Read(root, lockPath));
        Manifest = Json(SafariSource.Read(Directory, "manifest.json"));
        var raw = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var row in Lock.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            var bytes = SafariSource.Read(root, PretSourcePaths.FireRedRawDirectory + "/" + path);
            raw.Add(path, bytes);
            // Existing source annotations remain readable; they are not loading credentials.
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        Raw = raw.ToImmutable(); Files = files.ToImmutable();
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        string prefix = "content/generated/" + name + "/";
        foreach (var row in Manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            if (path.StartsWith(prefix, StringComparison.Ordinal)) path = path[prefix.Length..];
            outputs.Add(path, SafariSource.Read(Directory, path));
        }
        Outputs = outputs.ToImmutable();
    }
    internal JsonElement Document(string path) => Json(Outputs[path]);
    internal T Parse<T>(string path) => SafariSource.Parse<T>(Outputs[path]);
    internal static JsonElement Json(byte[] bytes) => SafariSource.Parse<JsonElement>(bytes);
    internal static SilphSourcePackage Catalogs(string root) => new(root, "silph-catalogs", "scripts/silph-catalogs.lock.json");
}
