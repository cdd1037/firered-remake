using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Events;
namespace Game.Content;

/// <summary>Authenticated, finite inventory of static original script menu definitions.</summary>
public static class PretSourceGridMenus
{
    public const string Profile = "firered-source-grid-menus-v1";
    public const string LockSha256 = "a84d3995b57d4523a9e16bfa0f3bae6f0a208cc33d73c6e348b9d1a58ed386fb";
    public const string ManifestSha256 = "80bdf36db8811680be81e79329688b3c3d701f5498c2ee25e74c2dfa5cea07cb";
    private const string Tree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";

    public static ImmutableDictionary<string, StoryMenuDefinition> Load(string root)
    {
        byte[] Read(string path, string hash, int? length = null)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe source grid menu path.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null || file.Length is <= 0 or > 1_000_000 || length is { } size && file.Length != size)
                throw new InvalidDataException("Source grid menu size/type differs.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Source grid menu symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("Source grid menu bytes differ: " + path);
            return bytes;
        }
        using var locked = JsonDocument.Parse(Read("scripts/source-grid-menus.lock.json", LockSha256));
        using var document = JsonDocument.Parse(Read("content/generated/source-grid-menus/manifest.json", ManifestSha256));
        var source = locked.RootElement;
        var manifest = document.RootElement;
        if (source.GetProperty("schemaVersion").GetInt32() != 1 || source.GetProperty("profile").GetString() != Profile ||
            source.GetProperty("repository").GetString() != "pret/pokefirered" || source.GetProperty("commit").GetString() != PretWorldMap.SourceCommit ||
            source.GetProperty("tree").GetString() != Tree || source.GetProperty("files").GetArrayLength() != 8 ||
            manifest.GetProperty("schemaVersion").GetInt32() != 1 || manifest.GetProperty("profile").GetString() != Profile ||
            manifest.GetProperty("sourceCommit").GetString() != PretWorldMap.SourceCommit || manifest.GetProperty("sourceTree").GetString() != Tree ||
            manifest.GetProperty("lockSha256").GetString() != LockSha256 || manifest.GetProperty("menus").GetArrayLength() != 1)
            throw new InvalidDataException("Source grid menu identity differs.");
        foreach (var entry in source.GetProperty("files").EnumerateArray())
        {
            var bytes = Read("content/raw/source-grid-menus/" + entry.GetProperty("path").GetString(),
                entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != entry.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Source grid menu Git blob differs.");
        }
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
        };
        var menus = manifest.GetProperty("menus").Deserialize<ImmutableArray<StoryMenuDefinition>>(options);
        if (menus.IsDefaultOrEmpty || menus.Any(m => m.Options.IsDefaultOrEmpty)) throw new InvalidDataException("Source grid menu missing.");
        return menus.ToImmutableDictionary(m => m.Id, StringComparer.Ordinal);
    }
}
