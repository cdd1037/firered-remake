using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record FossilLabDexDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PokedexMenuEntry> Entries);
public sealed record FossilLabPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Four source fossil species, source dex pages and menus, and authenticated normal cry resources.</summary>
public static class PretFossilLabPresentation
{
    public const string Base = "content/generated/fossil-lab-presentation/";
    public const string Profile = "firered-fossil-lab-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "4784dcbe4b602f56dcc33b72ca4b747d3558e418112be42a0ff52c5bb0db0fd8";
    public const string ManifestSha256 = "451ba9aed1242c23207bf525168eff03430d6054b866a0c27f4888515a6d0fb1";

    public static FossilLabPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int length)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || length is <= 0 or > 4_000_000)
                throw new InvalidDataException("Unsafe Fossil Lab presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fossil Lab resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Fossil Lab directory symlink rejected.");
            if (file.Length != length) throw new InvalidDataException("Fossil Lab presentation size differs: " + path);
            var bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Fossil Lab presentation bytes differ: " + path);
            return bytes;
        }

        using var locked = JsonDocument.Parse(Read("scripts/fossil-lab-presentation.lock.json", LockSha256, 14542));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 45856));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != Profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != Profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 46 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 4 ||
            manifest.GetProperty("outputs").GetArrayLength() != 50)
            throw new InvalidDataException("Fossil Lab presentation identity differs.");
        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
        {
            var bytes = Read("content/raw/fossil-lab-presentation/" + row.GetProperty("path").GetString(),
                row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) !=
                row.GetProperty("gitBlobSha1").GetString()) throw new InvalidDataException("Fossil Lab source Git blob differs.");
        }
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in sourceLock.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (string kind in new[] { "sprites", "pokemonIcons", "footprints" })
        foreach (var asset in manifest.GetProperty(kind).EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("rgbaPath").GetString()!;
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var rgba) ||
                rgba.Length != width * height * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != asset.GetProperty("rgbaSha256").GetString())
                throw new InvalidDataException("Fossil Lab pixels differ: " + path);
        }
        var dex = JsonSerializer.Deserialize<FossilLabDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Fossil Lab dex document missing.");
        if (dex.SchemaVersion != 1 || dex.Profile != Profile || dex.SourceCommit != SourceCommit ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 138, 139, 140, 141 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 138, 139, 140, 141 }) ||
            manifest.GetProperty("sprites").GetArrayLength() != 16 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 4 ||
            manifest.GetProperty("footprints").GetArrayLength() != 4 || manifest.GetProperty("menus").GetArrayLength() != 2 ||
            manifest.GetProperty("audio").GetProperty("newCueCount").GetInt32() != 4)
            throw new InvalidDataException("Fossil Lab presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!, e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != texture.Sha256)
                throw new InvalidDataException("Fossil Lab dex texture differs.");
        foreach (var menu in manifest.GetProperty("menus").EnumerateArray())
        {
            string id = menu.GetProperty("menuId").GetString()!;
            string fossil = id switch { "MULTICHOICE_HELIX" => "HELIX FOSSIL", "MULTICHOICE_DOME" => "DOME FOSSIL",
                _ => throw new InvalidDataException("Unadmitted fossil menu.") };
            if (!menu.GetProperty("rows").EnumerateArray().Select(r => r.GetString()).SequenceEqual(new[] { fossil, "EXIT" }) ||
                menu.GetProperty("left").GetInt32() != 0 || menu.GetProperty("top").GetInt32() != 0 ||
                menu.GetProperty("rowHeight").GetInt32() != 14 || menu.GetProperty("interiorHeightTiles").GetInt32() != 4 ||
                menu.GetProperty("ignoreB").GetBoolean() || menu.GetProperty("wrap").GetBoolean() || menu.GetProperty("cancelResult").GetInt32() != 127)
                throw new InvalidDataException("Fossil source menu differs.");
        }
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
