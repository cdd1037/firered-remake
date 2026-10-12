using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Bounded, pinned files with Git-blob identity, complete parent-path symlink rejection and strict JSON.</summary>
internal sealed class SilphSourcePackage
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal ImmutableDictionary<string, byte[]> Raw { get; }
    internal ImmutableDictionary<string, byte[]> Outputs { get; }
    internal ImmutableArray<PretWorldSourceFile> Files { get; }
    internal JsonElement Manifest { get; }
    internal JsonElement Lock { get; }
    internal string Directory { get; }
    private readonly ImmutableDictionary<string, int> _lines;
    internal static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal SilphSourcePackage(string root, string name, string profile, string lockPath, string lockHash, int lockLength,
        string manifestHash, int manifestLength, int inputCount, int outputCount)
    {
        root = Path.GetFullPath(root); Directory = Path.Combine(root, "content/generated/" + name);
        Lock = Json(SafariSource.Read(root, lockPath, lockHash, lockLength));
        Manifest = Json(SafariSource.Read(root, "content/generated/" + name + "/manifest.json", manifestHash, manifestLength));
        Need(Lock.GetProperty("schemaVersion").GetInt32() == 1 && Lock.GetProperty("repository").GetString() == "pret/pokefirered" &&
            Lock.GetProperty("commit").GetString() == SourceCommit && Lock.GetProperty("tree").GetString() == SourceTree &&
            Lock.GetProperty("profile").GetString() == profile && Lock.GetProperty("files").GetArrayLength() == inputCount &&
            Manifest.GetProperty("schemaVersion").GetInt32() == 1 && Manifest.GetProperty("profile").GetString() == profile &&
            Manifest.GetProperty("sourceCommit").GetString() == SourceCommit && Manifest.GetProperty("sourceTree").GetString() == SourceTree &&
            Manifest.GetProperty("lockSha256").GetString() == lockHash && Manifest.GetProperty("outputs").GetArrayLength() == outputCount,
            "Silph source package scope differs.");
        var raw = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lines = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var row in Lock.GetProperty("files").EnumerateArray())
        {
            string p = row.GetProperty("path").GetString()!, h = row.GetProperty("sha256").GetString()!, blob = row.GetProperty("gitBlobSha1").GetString()!;
            int length = row.GetProperty("byteLength").GetInt32();
            var bytes = SafariSource.Read(root, "content/raw/" + name + "/" + p, h, length);
            Need(SafariSource.GitBlob(bytes) == blob, "Silph source Git blob differs: " + p);
            raw.Add(p, bytes); files.Add(new(p, blob, length, h, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{p}"));
            if (!p.EndsWith(".bin", StringComparison.Ordinal) && !p.EndsWith(".png", StringComparison.Ordinal) && !p.EndsWith(".wav", StringComparison.Ordinal))
                lines.Add(p, new UTF8Encoding(false, true).GetString(bytes).Count(c => c == '\n') + 1);
        }
        Raw = raw.ToImmutable(); Files = files.ToImmutable(); _lines = lines.ToImmutable();
        Need(SafariSource.Hash(Encoding.UTF8.GetBytes(string.Concat(Files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n")))) ==
            Manifest.GetProperty("fullSourceHash").GetString(), "Silph full source inventory hash differs.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in Manifest.GetProperty("outputs").EnumerateArray())
        {
            string p = row.GetProperty("path").GetString()!;
            string relative = p.StartsWith("content/generated/", StringComparison.Ordinal) ? p : "content/generated/" + name + "/" + p;
            Need(relative.StartsWith("content/generated/" + name + "/", StringComparison.Ordinal), "Silph output escaped package.");
            outputs.Add(relative[("content/generated/" + name + "/").Length..], SafariSource.Read(root, relative, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        Outputs = outputs.ToImmutable();
    }
    internal JsonElement Document(string path) => Json(Outputs[path]);
    internal T Parse<T>(string path) => SafariSource.Parse<T>(Outputs[path]);
    internal static JsonElement Json(byte[] bytes)
    {
        // Strict parser also rejects duplicate object properties recursively.
        return SafariSource.Parse<JsonElement>(bytes);
    }
    internal void Reference(string path, string hash, int start, int end, string symbol) => Need(
        Files.Any(f => f.Path == path && f.Sha256 == hash) && _lines.TryGetValue(path, out int count) &&
        start > 0 && end >= start && end <= count && !string.IsNullOrWhiteSpace(symbol), "Silph source reference differs.");
    internal void Reference(PokemonSourceRef r) => Reference(r.Path, r.Sha256, r.StartLine, r.EndLine, r.Symbol);
    internal void Reference(OpeningItemSourceRef r) => Reference(r.Path, r.Sha256, r.StartLine, r.EndLine, r.Symbol);
    internal void Reference(TrainerSourceRef r) => Reference(r.Path, r.Sha256, r.StartLine, r.EndLine, r.Symbol);
    internal static SilphSourcePackage Catalogs(string root) => new(root, "silph-catalogs", "firered-silph-catalogs-v1",
        "scripts/silph-catalogs.lock.json", "c89452508b53d251a0c3ad382b514fb7cb4e86009dc1f54ee91d91304ad19d28", 11220,
        "18d32aeb40285c49ae7bfd9eb74819768735fe3703750d09704ebbaaa648c1fd", 1126, 38, 4);
}
