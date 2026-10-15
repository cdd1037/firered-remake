using System.Collections.Immutable;
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

    public static readonly ImmutableArray<int> Species = [172, 173, 174, 236, 238, 239, 360, 237];

    public static readonly ImmutableArray<string> EggAssetIds = ["egg-front", "egg-icon-0", "egg-icon-1",
        "egg-hatch-0", "egg-hatch-1", "egg-hatch-2", "egg-hatch-3", "egg-shard-0", "egg-shard-1", "egg-shard-2", "egg-shard-3"];

    public static BreedingPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Breeding presentation resource.");
            var file = new FileInfo(Path.Combine(path.StartsWith(Base, StringComparison.Ordinal) ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Breeding resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Breeding directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }

        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Breeding presentation schema differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "sprites","pokemonIcons","footprints","eggAssets" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            int width = asset.GetProperty("width").GetInt32(),height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
            string texturePath = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(texturePath)) files.Add(texturePath, Read(texturePath));
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var rgba) ||
                PngRgba8.Decode(rgba, width, height).Length != width * height * 4)
                throw new InvalidDataException("Breeding pixels differ: " + path);
            if (!files.TryGetValue(asset.GetProperty("texturePath").GetString()!, out var png))
                throw new InvalidDataException("Breeding texture differs: " + path);
        }
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        };
        var dex = JsonSerializer.Deserialize<BreedingDexDocument>(files[Base + "dex-entries.json"], options)
            ?? throw new InvalidDataException("Breeding dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(Species) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(Species) ||
            manifest.GetProperty("sprites").GetArrayLength() != 32 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 8 ||
            manifest.GetProperty("footprints").GetArrayLength() != 8 )
            throw new InvalidDataException("Breeding presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!,e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png))
                throw new InvalidDataException("Breeding dex texture differs.");
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(), s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!))
            .SequenceEqual(Species.SelectMany(s => new[] { (s, "front", "normal"),(s, "front", "shiny"),(s, "back", "normal"),(s, "back", "shiny") })))
            throw new InvalidDataException("Breeding battler variants differ.");
        foreach (var kind in new[] { "sprites","pokemonIcons","footprints" })
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
            }
        }
        var footprints = manifest.GetProperty("footprints").EnumerateArray().ToArray();
        if (footprints.Any(i => i.GetProperty("width").GetInt32() != 16 || i.GetProperty("height").GetInt32() != 16))
            throw new InvalidDataException("Breeding menu geometry differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray().ToArray();
        if (!geometry.Select(r => (r.GetProperty("species").GetInt32(), r.GetProperty("side").GetString()!))
            .SequenceEqual(Species.SelectMany(s => new[] { (s, "front"),(s, "back") })))
            throw new InvalidDataException("Breeding move geometry inventory differs.");
        foreach (var row in geometry)
        {
            int species = row.GetProperty("species").GetInt32();
            string side = row.GetProperty("side").GetString()!;
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
            string id = asset.GetProperty("id").GetString()!,role = asset.GetProperty("role").GetString()!;
            int frame = asset.GetProperty("sourceFrame").GetInt32();
            int size = role == "shell" ? 64 : role == "shard" ? 8 : 32;
            int frames = role == "shell" ? 1 : role == "icon" ? 2 : 4;
            if (asset.GetProperty("sourceSpeciesId").GetInt32() != 412 || asset.GetProperty("speciesId").GetString() != "pret:SPECIES_EGG" ||
                asset.GetProperty("transparentIndex").GetInt32() != 0 ||
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
        foreach (var row in cues)
        {
            var cue = row.GetProperty("cue");
            int species = cue.GetProperty("species").GetInt32();
            string name = dex.Entries.Single(e => e.SourceSpeciesId == species).Name;
            if (cue.GetProperty("id").GetString() != "CRY_" + name + "_NORMAL" || cue.GetProperty("sourcePlayer").GetInt32() != 4 ||
                cue.GetProperty("priority").GetInt32() != 10 ||
                cue.GetProperty("sourceSymbol").GetString() != "SPECIES_" + name || cue.GetProperty("looping").GetBoolean() ||
                cue.GetProperty("cryMode").GetInt32() != 0 || cue.GetProperty("songAddress").GetInt32() != 0 ||
                cue.GetProperty("sourceCounter").ValueKind != JsonValueKind.Null ||
                cue.GetProperty("sourcePath").GetString() != "sound/direct_sound_samples/cries/" + name.ToLowerInvariant() + ".wav")
                throw new InvalidDataException("Breeding normal cry source binding differs.");
        }
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
