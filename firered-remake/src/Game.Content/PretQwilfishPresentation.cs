using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record QwilfishDexDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PokedexMenuEntry> Entries);
public sealed record QwilfishPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Qwilfish owned icon/footprint and exact reuse of existing Tower battlers, geometry and normal cry.</summary>
public static class PretQwilfishPresentation
{
    public const string Base = "content/generated/qwilfish-presentation/";
    public const string Profile = "firered-qwilfish-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "467213de86a5e34ca87442f88debdfc26c712326df04d61ed2f620bee76379af";
    public const string ManifestSha256 = "19d7426efe763d7eb3de24c755f21dc218d66608f6a88fc5d415af389df08bff";

    public static QwilfishPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path, string hash, int length)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || length is <= 0 or > 4_000_000)
                throw new InvalidDataException("Unsafe Qwilfish presentation resource.");
            var file = new FileInfo(Path.Combine(path.StartsWith(Base, StringComparison.Ordinal) || path.StartsWith("content/raw/qwilfish-presentation/", StringComparison.Ordinal) || path == "scripts/qwilfish-presentation.lock.json" ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Qwilfish resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Qwilfish directory symlink rejected.");
            if (file.Length != length) throw new InvalidDataException("Qwilfish presentation size differs: " + path);
            var bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Qwilfish presentation bytes differ: " + path);
            return bytes;
        }

        using var locked = JsonDocument.Parse(Read("scripts/qwilfish-presentation.lock.json", LockSha256, 8103));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 14513));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != Profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != Profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 20 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 13 ||
            manifest.GetProperty("outputs").GetArrayLength() != 5)
            throw new InvalidDataException("Qwilfish presentation identity differs.");
        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
        {
            var bytes = Read(row.GetProperty("rawPath").GetString()!,
                row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) !=
                row.GetProperty("gitBlobSha1").GetString()) throw new InvalidDataException("Qwilfish source Git blob differs.");
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
        foreach (var asset in new[] { "inheritedSprites", "pokemonIcons", "footprints" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("rgbaPath").GetString()!;
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var rgba) ||
                rgba.Length != width * height * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != asset.GetProperty("rgbaSha256").GetString())
                throw new InvalidDataException("Qwilfish pixels differ: " + path);
            if (!files.TryGetValue(asset.GetProperty("texturePath").GetString()!, out var png) ||
                Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString())
                throw new InvalidDataException("Qwilfish texture differs: " + path);
        }
        var dex = JsonSerializer.Deserialize<QwilfishDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Qwilfish dex document missing.");
        if (dex.SchemaVersion != 1 || dex.Profile != Profile || dex.SourceCommit != SourceCommit ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 211 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 211 }) ||
            manifest.GetProperty("inheritedSprites").GetArrayLength() != 4 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 1 ||
            manifest.GetProperty("footprints").GetArrayLength() != 1 ||
            manifest.GetProperty("audio").GetProperty("newCueCount").GetInt32() != 0 ||
            manifest.GetProperty("audio").GetProperty("inheritedCueCount").GetInt32() != 1)
            throw new InvalidDataException("Qwilfish presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!, e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != texture.Sha256)
                throw new InvalidDataException("Qwilfish dex texture differs.");
        var sprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        if (!sprites.Select(s => (s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!))
            .SequenceEqual(new[] { ("front", "normal"), ("front", "shiny"), ("back", "normal"), ("back", "shiny") }))
            throw new InvalidDataException("Qwilfish battler variant inventory differs.");
        foreach (var asset in new[] { "inheritedSprites", "pokemonIcons", "footprints" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
            if (asset.GetProperty("sourceSpeciesId").GetInt32() != 211 || asset.GetProperty("speciesId").GetString() != "pret:SPECIES_QWILFISH")
                throw new InvalidDataException("Qwilfish asset species differs.");
        foreach (var sprite in sprites)
        {
            bool front = sprite.GetProperty("side").GetString() == "front";
            if (sprite.GetProperty("width").GetInt32() != 64 || sprite.GetProperty("height").GetInt32() != 64 ||
                sprite.GetProperty("x").GetInt32() != (front ? 144 : 40) || sprite.GetProperty("y").GetInt32() != (front ? 18 : 55) ||
                sprite.GetProperty("sourceYOffset").GetInt32() != (front ? 10 : 7) ||
                sprite.GetProperty("sourceEnemyElevation").GetInt32() != 0 ||
                sprite.GetProperty("sourceWidth").GetInt32() != (front ? 40 : 56) ||
                sprite.GetProperty("sourceHeight").GetInt32() != (front ? 48 : 56))
                throw new InvalidDataException("Qwilfish battler geometry differs.");
        }
        var icon = manifest.GetProperty("pokemonIcons").EnumerateArray().Single();
        var foot = manifest.GetProperty("footprints").EnumerateArray().Single();
        if (icon.GetProperty("width").GetInt32() != 32 || icon.GetProperty("height").GetInt32() != 32 ||
            icon.GetProperty("paletteIndex").GetInt32() != 0 || icon.GetProperty("sourceFrame").GetInt32() != 0 ||
            icon.GetProperty("sourceFrameCount").GetInt32() != 2 ||
            foot.GetProperty("width").GetInt32() != 16 || foot.GetProperty("height").GetInt32() != 16)
            throw new InvalidDataException("Qwilfish icon/footprint geometry differs.");
        using var inherited = JsonDocument.Parse(files["content/generated/trainer-tower-presentation/catalog.json"]);
        foreach (var row in sprites)
            if (!inherited.RootElement.GetProperty("sprites").EnumerateArray().Any(old => JsonElement.DeepEquals(old, row)))
                throw new InvalidDataException("Qwilfish Tower sprite binding differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray().ToArray();
        if (geometry.Length != 2 || !geometry.Select(r => r.GetProperty("side").GetString()).SequenceEqual(new[] { "front", "back" }))
            throw new InvalidDataException("Qwilfish move geometry inventory differs.");
        foreach (var row in geometry)
        {
            bool front = row.GetProperty("side").GetString() == "front";
            if (row.GetProperty("species").GetInt32() != 211 || row.GetProperty("yOffset").GetInt32() != (front ? 10 : 7) ||
                row.GetProperty("elevation").GetInt32() != 0 || row.GetProperty("sourceWidth").GetInt32() != (front ? 40 : 56) ||
                row.GetProperty("sourceHeight").GetInt32() != (front ? 48 : 56))
                throw new InvalidDataException("Qwilfish move geometry differs.");
        }
        using var synth = JsonDocument.Parse(files["content/generated/trainer-tower-audio/synth-manifest.json"]);
        var cue = manifest.GetProperty("audio").GetProperty("cues").EnumerateArray().Single();
        if (cue.GetProperty("id").GetString() != "CRY_QWILFISH_NORMAL" || cue.GetProperty("species").GetInt32() != 211 ||
            cue.GetProperty("cryMode").GetInt32() != 0 || cue.GetProperty("sourcePlayer").GetInt32() != 4 ||
            !synth.RootElement.GetProperty("cues").EnumerateArray().Any(old => JsonElement.DeepEquals(old, cue)))
            throw new InvalidDataException("Qwilfish inherited normal cry differs.");
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
