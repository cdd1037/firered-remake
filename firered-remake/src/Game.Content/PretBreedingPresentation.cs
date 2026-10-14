using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record BreedingDexDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PokedexMenuEntry> Entries);
public sealed record BreedingPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Eight authenticated breeding species reuse eight battlers, all geometry and cry samples; only missing source assets and cues are added.</summary>
public static class PretBreedingPresentation
{
    public const string Base = "content/generated/breeding-presentation/";
    public const string Profile = "firered-breeding-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "25afb4e1e9c5b8a1d8e9d26ebf3ab1df19d58a05f35d56df2bc1d9a509521103";
    public const string ManifestSha256 = "c2da0772a36c0eaf6cf1caae328175104c3731ad1cdd8ce2ca0e5191daab951c";

    public static readonly ImmutableArray<int> Species = [172, 173, 174, 236, 238, 239, 360, 237];

    public static readonly ImmutableArray<string> EggAssetIds = ["egg-front", "egg-icon-0", "egg-icon-1",
        "egg-hatch-0", "egg-hatch-1", "egg-hatch-2", "egg-hatch-3", "egg-shard-0", "egg-shard-1", "egg-shard-2", "egg-shard-3"];

    public static BreedingPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path, string hash, int length)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || length is <= 0 or > 4_000_000)
                throw new InvalidDataException("Unsafe Breeding presentation resource.");
            var file = new FileInfo(Path.Combine(path.StartsWith(Base, StringComparison.Ordinal) || path.StartsWith("content/raw/breeding-presentation/", StringComparison.Ordinal) || path == "scripts/breeding-presentation.lock.json" ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Breeding resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Breeding directory symlink rejected.");
            if (file.Length != length) throw new InvalidDataException("Breeding presentation size differs: " + path);
            var bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Breeding presentation bytes differ: " + path);
            return bytes;
        }

        using var locked = JsonDocument.Parse(Read("scripts/breeding-presentation.lock.json", LockSha256, 24498));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 94956));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        if (sourceLock.GetProperty("schemaVersion").GetInt32() != 1 || sourceLock.GetProperty("profile").GetString() != Profile ||
            sourceLock.GetProperty("repository").GetString() != "pret/pokefirered" || sourceLock.GetProperty("commit").GetString() != SourceCommit ||
            sourceLock.GetProperty("tree").GetString() != SourceTree || manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
            manifest.GetProperty("profile").GetString() != Profile || manifest.GetProperty("sourceCommit").GetString() != SourceCommit ||
            manifest.GetProperty("sourceTree").GetString() != SourceTree || manifest.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(manifest.GetProperty("inputs"), sourceLock.GetProperty("files")) ||
            !JsonElement.DeepEquals(manifest.GetProperty("inheritedInputs"), sourceLock.GetProperty("inheritedInputs")) ||
            sourceLock.GetProperty("files").GetArrayLength() != 72 || sourceLock.GetProperty("inheritedInputs").GetArrayLength() != 25 ||
            manifest.GetProperty("outputs").GetArrayLength() != 104)
            throw new InvalidDataException("Breeding presentation identity differs.");
        foreach (var row in sourceLock.GetProperty("files").EnumerateArray())
        {
            var bytes = Read(row.GetProperty("rawPath").GetString()!,
                row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) !=
                row.GetProperty("gitBlobSha1").GetString()) throw new InvalidDataException("Breeding source Git blob differs.");
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
        foreach (var asset in new[] { "sprites", "pokemonIcons", "footprints", "eggAssets" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("rgbaPath").GetString()!;
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var rgba) ||
                rgba.Length != width * height * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != asset.GetProperty("rgbaSha256").GetString())
                throw new InvalidDataException("Breeding pixels differ: " + path);
            if (!files.TryGetValue(asset.GetProperty("texturePath").GetString()!, out var png) ||
                Convert.ToHexStringLower(SHA256.HashData(png)) != asset.GetProperty("sha256").GetString())
                throw new InvalidDataException("Breeding texture differs: " + path);
        }
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
        };
        var dex = JsonSerializer.Deserialize<BreedingDexDocument>(files[Base + "dex-entries.json"], options)
            ?? throw new InvalidDataException("Breeding dex document missing.");
        if (dex.SchemaVersion != 1 || dex.Profile != Profile || dex.SourceCommit != SourceCommit ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(Species) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(Species) ||
            manifest.GetProperty("sprites").GetArrayLength() != 32 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 8 ||
            manifest.GetProperty("footprints").GetArrayLength() != 8 ||
            manifest.GetProperty("audio").GetProperty("newCueCount").GetInt32() != 6 ||
            manifest.GetProperty("audio").GetProperty("inheritedCueCount").GetInt32() != 2)
            throw new InvalidDataException("Breeding presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!, e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png) || Convert.ToHexStringLower(SHA256.HashData(png)) != texture.Sha256)
                throw new InvalidDataException("Breeding dex texture differs.");
        using var inheritedDex = JsonDocument.Parse(files["content/generated/pokedex-menu/pokedex-menu.json"]);
        foreach (var entry in dex.Entries)
        {
            var original = inheritedDex.RootElement.GetProperty("entries").EnumerateArray()
                .Single(e => e.GetProperty("sourceSpeciesId").GetInt32() == entry.SourceSpeciesId).Deserialize<PokedexMenuEntry>(options);
            if (entry with { Portrait = null, Footprint = null } != original || entry.Name != entry.Name.ToUpperInvariant())
                throw new InvalidDataException("Breeding source dex metadata differs.");
        }
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(), s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!))
            .SequenceEqual(Species.SelectMany(s => new[] { (s, "front", "normal"), (s, "front", "shiny"), (s, "back", "normal"), (s, "back", "shiny") })))
            throw new InvalidDataException("Breeding battler variants differ.");
        foreach (var kind in new[] { "sprites", "pokemonIcons", "footprints" })
        {
            var rows = manifest.GetProperty(kind).EnumerateArray().ToArray();
            if (kind != "sprites" && !rows.Select(r => r.GetProperty("sourceSpeciesId").GetInt32()).SequenceEqual(Species))
                throw new InvalidDataException("Breeding menu asset inventory differs.");
            foreach (var asset in rows)
            {
                int species = asset.GetProperty("sourceSpeciesId").GetInt32();
                var entry = dex.Entries.Single(e => e.SourceSpeciesId == species);
                if (asset.GetProperty("speciesId").GetString() != "pret:SPECIES_" + entry.Name)
                    throw new InvalidDataException("Breeding asset species differs.");
                if (asset.GetProperty("inherited").GetBoolean())
                {
                    using var inherited = JsonDocument.Parse(files[asset.GetProperty("inheritedManifest").GetString()!]);
                    var original = asset.GetProperty("inheritedAsset");
                    if (!inherited.RootElement.GetProperty(asset.GetProperty("inheritedCollection").GetString()!).EnumerateArray().Any(old => JsonElement.DeepEquals(old, original)))
                        throw new InvalidDataException("Breeding inherited binding differs.");
                    foreach (var property in original.EnumerateObject())
                    {
                        if (property.Name is "rgbaPath" or "texturePath")
                        {
                            string oldPath = property.Value.GetString()!;
                            if (!oldPath.StartsWith("content/", StringComparison.Ordinal))
                                oldPath = asset.GetProperty("inheritedManifest").GetString()![..(asset.GetProperty("inheritedManifest").GetString()!.LastIndexOf('/') + 1)] + oldPath;
                            if (asset.GetProperty(property.Name).GetString() != oldPath)
                                throw new InvalidDataException("Breeding inherited asset path differs.");
                        }
                        else if (!JsonElement.DeepEquals(asset.GetProperty(property.Name), property.Value))
                            throw new InvalidDataException("Breeding inherited asset identity differs.");
                    }
                    using var outputManifest = JsonDocument.Parse(files[asset.GetProperty("inheritedOutputManifest").GetString()!]);
                    foreach (var (field, hashField) in new[] { ("rgbaPath", "rgbaSha256"), ("texturePath", "sha256") })
                    {
                        string path = asset.GetProperty(field).GetString()!;
                        string directory = asset.GetProperty("inheritedManifest").GetString()![..(asset.GetProperty("inheritedManifest").GetString()!.LastIndexOf('/') + 1)];
                        var output = outputManifest.RootElement.GetProperty("outputs").EnumerateArray().Single(row => row.GetProperty("path").GetString() == path[directory.Length..]);
                        if (output.GetProperty("sha256").GetString() != asset.GetProperty(hashField).GetString())
                            throw new InvalidDataException("Breeding inherited output hash differs.");
                    }
                }
            }
        }
        var icons = manifest.GetProperty("pokemonIcons").EnumerateArray().ToArray();
        var footprints = manifest.GetProperty("footprints").EnumerateArray().ToArray();
        if (icons.Any(i => i.GetProperty("inherited").GetBoolean()) || footprints.Any(i => i.GetProperty("inherited").GetBoolean()) ||
            sprites.Count(i => i.GetProperty("inherited").GetBoolean()) != 8 ||
            icons.Any(i => i.GetProperty("width").GetInt32() != 32 || i.GetProperty("height").GetInt32() != 32 ||
                i.GetProperty("sourceFrame").GetInt32() != 0 || i.GetProperty("sourceFrameCount").GetInt32() != 2 || i.GetProperty("paletteIndex").GetInt32() is < 0 or > 2) ||
            footprints.Any(i => i.GetProperty("width").GetInt32() != 16 || i.GetProperty("height").GetInt32() != 16))
            throw new InvalidDataException("Breeding menu geometry differs.");
        using var inheritedGeometry = JsonDocument.Parse(files[manifest.GetProperty("moveAnimationGeometry").GetProperty("inheritedManifest").GetString()!]);
        var geometry = manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray().ToArray();
        if (!geometry.Select(r => (r.GetProperty("species").GetInt32(), r.GetProperty("side").GetString()!))
            .SequenceEqual(Species.SelectMany(s => new[] { (s, "front"), (s, "back") })))
            throw new InvalidDataException("Breeding move geometry inventory differs.");
        foreach (var row in geometry)
        {
            int species = row.GetProperty("species").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(old => JsonElement.DeepEquals(old, row)))
                throw new InvalidDataException("Breeding inherited move geometry differs.");
            foreach (var sprite in sprites.Where(s => s.GetProperty("sourceSpeciesId").GetInt32() == species && s.GetProperty("side").GetString() == side))
                if (sprite.GetProperty("width").GetInt32() != 64 || sprite.GetProperty("height").GetInt32() != 64 ||
                    sprite.GetProperty("x").GetInt32() != (side == "front" ? 144 : 40) ||
                    sprite.GetProperty("y").GetInt32() != (side == "front" ? 8 + row.GetProperty("yOffset").GetInt32() - row.GetProperty("elevation").GetInt32() : 48 + row.GetProperty("yOffset").GetInt32()) ||
                    sprite.GetProperty("sourceYOffset").GetInt32() != row.GetProperty("yOffset").GetInt32() ||
                    sprite.GetProperty("sourceEnemyElevation").GetInt32() != row.GetProperty("elevation").GetInt32() ||
                    sprite.GetProperty("sourceWidth").GetInt32() != row.GetProperty("sourceWidth").GetInt32() ||
                    sprite.GetProperty("sourceHeight").GetInt32() != row.GetProperty("sourceHeight").GetInt32())
                    throw new InvalidDataException("Breeding source battler geometry differs.");
        }
        var eggAssets = manifest.GetProperty("eggAssets").EnumerateArray().ToArray();
        if (!eggAssets.Select(a => a.GetProperty("id").GetString()).SequenceEqual(EggAssetIds))
            throw new InvalidDataException("Breeding Egg source asset inventory differs.");
        foreach (var asset in eggAssets)
        {
            string id = asset.GetProperty("id").GetString()!, role = asset.GetProperty("role").GetString()!;
            int frame = asset.GetProperty("sourceFrame").GetInt32();
            int size = role == "shell" ? 64 : role == "shard" ? 8 : 32;
            int frames = role == "shell" ? 1 : role == "icon" ? 2 : 4;
            if (asset.GetProperty("sourceSpeciesId").GetInt32() != 412 || asset.GetProperty("speciesId").GetString() != "pret:SPECIES_EGG" ||
                asset.GetProperty("inherited").GetBoolean() || asset.GetProperty("transparentIndex").GetInt32() != 0 ||
                role is not ("shell" or "icon" or "hatch" or "shard") || frame < 0 || frame >= frames ||
                id != (role == "shell" ? "egg-front" : "egg-" + role + "-" + frame) ||
                asset.GetProperty("width").GetInt32() != size || asset.GetProperty("height").GetInt32() != size ||
                asset.GetProperty("sourceFrameCount").GetInt32() != frames ||
                asset.GetProperty("paletteExpansion").GetString() != (role == "icon" ? "(rgb5 << 3) | (rgb5 >> 2)" : "floor(rgb5 * 255 / 31)"))
                throw new InvalidDataException("Breeding Egg-only texture binding differs.");
            if (role == "shell" && (asset.GetProperty("sourceWidth").GetInt32() != 24 || asset.GetProperty("sourceHeight").GetInt32() != 24 ||
                    asset.GetProperty("sourceYOffset").GetInt32() != 20) ||
                role == "icon" && asset.GetProperty("paletteIndex").GetInt32() != 1 ||
                role is "hatch" or "shard" && (asset.GetProperty("sourceTileOffset").GetInt32() != frame * (role == "hatch" ? 16 : 1) ||
                    asset.GetProperty("sourceDuration").GetInt32() != 5))
                throw new InvalidDataException("Breeding Egg source geometry differs.");
        }
        var cues = manifest.GetProperty("audio").GetProperty("cues").EnumerateArray().ToArray();
        if (!cues.Select(c => c.GetProperty("cue").GetProperty("species").GetInt32()).SequenceEqual(Species))
            throw new InvalidDataException("Breeding inherited cry inventory differs.");
        using var extension = JsonDocument.Parse(files[Base + "synth-manifest.json"]);
        var addedCues = extension.RootElement.GetProperty("cues").EnumerateArray().ToArray();
        if (extension.RootElement.GetProperty("schemaVersion").GetInt32() != 1 || extension.RootElement.GetProperty("sourceCommit").GetString() != SourceCommit ||
            !addedCues.Select(c => c.GetProperty("species").GetInt32()).SequenceEqual(Species.Where(s => s is not (237 or 238))) ||
            cues.Count(c => c.GetProperty("inherited").GetBoolean()) != 2 ||
            manifest.GetProperty("audio").GetProperty("inheritedSampleCount").GetInt32() != 8 ||
            !manifest.GetProperty("audio").GetProperty("originalPackUnchanged").GetBoolean())
            throw new InvalidDataException("Breeding cry extension inventory differs.");
        foreach (var row in cues)
        {
            var cue = row.GetProperty("cue");
            int species = cue.GetProperty("species").GetInt32();
            string name = dex.Entries.Single(e => e.SourceSpeciesId == species).Name;
            if (cue.GetProperty("id").GetString() != "CRY_" + name + "_NORMAL" || cue.GetProperty("sourcePlayer").GetInt32() != 4 ||
                cue.GetProperty("priority").GetInt32() != 10 || cue.GetProperty("sourceCommit").GetString() != SourceCommit ||
                cue.GetProperty("sourceSymbol").GetString() != "SPECIES_" + name || cue.GetProperty("looping").GetBoolean() ||
                cue.GetProperty("cryMode").GetInt32() != 0 || cue.GetProperty("songAddress").GetInt32() != 0 ||
                cue.GetProperty("sourceCounter").ValueKind != JsonValueKind.Null ||
                cue.GetProperty("sourcePath").GetString() != "sound/direct_sound_samples/cries/" + name.ToLowerInvariant() + ".wav")
                throw new InvalidDataException("Breeding normal cry source binding differs.");
            if (row.GetProperty("inherited").GetBoolean())
            {
                using var synth = JsonDocument.Parse(files[row.GetProperty("inheritedManifest").GetString()!]);
                if (!synth.RootElement.GetProperty("cues").EnumerateArray().Any(old => JsonElement.DeepEquals(old, cue)))
                    throw new InvalidDataException("Breeding inherited normal cry differs.");
            }
            else if (species is 237 or 238 || !addedCues.Any(added => JsonElement.DeepEquals(added, cue)))
                throw new InvalidDataException("Breeding added cry cue differs.");
        }
        var cryBytes = files["content/generated/synth-audio/normal-cries.bin"];
        var cryRows = manifest.GetProperty("audio").GetProperty("normalCries").EnumerateArray().ToArray();
        if (!cryRows.Select(r => r.GetProperty("species").GetInt32()).SequenceEqual(Species))
            throw new InvalidDataException("Breeding existing cry sample inventory differs.");
        foreach (var row in cryRows)
        {
            int species = row.GetProperty("species").GetInt32();
            var record = Enumerable.Range(0, (cryBytes.Length - 12) / 16).Select(i => 12 + i * 16)
                .Single(i => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(cryBytes.AsSpan(i)) == species);
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(cryBytes.AsSpan(record + 2)) != row.GetProperty("cryIndex").GetInt32() ||
                row.GetProperty("cryIndex").GetInt32() != (species == 360 ? 258 : species - 1) ||
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(cryBytes.AsSpan(record + 4)) != row.GetProperty("sampleCount").GetUInt32() ||
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(cryBytes.AsSpan(record + 8)) != row.GetProperty("payloadBytes").GetUInt32() ||
                (cryBytes[record + 12] != 0) != row.GetProperty("guardStored").GetBoolean())
                throw new InvalidDataException("Breeding existing cry sample identity differs.");
        }
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
