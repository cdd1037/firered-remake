using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record StoneEvolutionDexDocument(int SchemaVersion, ImmutableArray<PokedexMenuEntry> Entries);
public sealed record StoneEvolutionPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Seven authenticated owned stone targets reuse original battlers, geometry and cries; only missing menu art is added.</summary>
public static class PretStoneEvolutionPresentation
{
    public const string Base = "content/generated/sprites/pokemon/stone-evolution/";
    public const string Profile = "firered-stone-evolution-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static readonly ImmutableArray<int> Species = [38, 59, 62, 91, 103, 134, 136];

    public static StoneEvolutionPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") )
                throw new InvalidDataException("Unsafe StoneEvolution presentation resource.");
            var file = new FileInfo(Path.Combine(path.StartsWith(Base, StringComparison.Ordinal) ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("StoneEvolution resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("StoneEvolution directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }

        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("StoneEvolution presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "inheritedSprites", "pokemonIcons", "footprints" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "inheritedSprites","pokemonIcons","footprints" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var pngBytes) ||
                (pngBytes).Length == 0)
                throw new InvalidDataException("StoneEvolution pixels differ: " + path);
            if (!files.TryGetValue(asset.GetProperty("texturePath").GetString()!, out var png) )
                throw new InvalidDataException("StoneEvolution texture differs: " + path);
        }
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        };
        var dex = JsonSerializer.Deserialize<StoneEvolutionDexDocument>(files[Base + "dex-entries.json"], options)
            ?? throw new InvalidDataException("Stone evolution dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(Species) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(Species) ||
            manifest.GetProperty("inheritedSprites").GetArrayLength() != 28 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 7 ||
            manifest.GetProperty("footprints").GetArrayLength() != 7 ||
            manifest.GetProperty("audio").GetProperty("newCueCount").GetInt32() != 0 ||
            manifest.GetProperty("audio").GetProperty("inheritedCueCount").GetInt32() != 7)
            throw new InvalidDataException("Stone evolution presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!,e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png) )
                throw new InvalidDataException("Stone evolution dex texture differs.");
        using var inheritedDex = JsonDocument.Parse(Read("content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json"));
        foreach (var entry in dex.Entries)
        {
            var original = inheritedDex.RootElement.GetProperty("entries").EnumerateArray()
                .Single(e => e.GetProperty("sourceSpeciesId").GetInt32() == entry.SourceSpeciesId).Deserialize<PokedexMenuEntry>(options);
            if (entry with { Portrait = null,Footprint = null } != original || entry.Name != entry.Name.ToUpperInvariant())
                throw new InvalidDataException("Stone evolution source dex metadata differs.");
        }
        var sprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(), s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!))
            .SequenceEqual(Species.SelectMany(s => new[] { (s, "front", "normal"),(s, "front", "shiny"),(s, "back", "normal"),(s, "back", "shiny") })))
            throw new InvalidDataException("Stone evolution battler variants differ.");
        foreach (var kind in new[] { "inheritedSprites","pokemonIcons","footprints" })
        {
            var rows = manifest.GetProperty(kind).EnumerateArray().ToArray();
            if (kind != "inheritedSprites" && !rows.Select(r => r.GetProperty("sourceSpeciesId").GetInt32()).SequenceEqual(Species))
                throw new InvalidDataException("Stone evolution menu asset inventory differs.");
            foreach (var asset in rows)
            {
                int species = asset.GetProperty("sourceSpeciesId").GetInt32();
                var entry = dex.Entries.Single(e => e.SourceSpeciesId == species);
                if (asset.GetProperty("speciesId").GetString() != "pret:SPECIES_" + entry.Name)
                    throw new InvalidDataException("Stone evolution asset species differs.");
            }
        }
        var icons = manifest.GetProperty("pokemonIcons").EnumerateArray().ToArray();
        var footprints = manifest.GetProperty("footprints").EnumerateArray().ToArray();
        if (icons.Count(i => i.GetProperty("inherited").GetBoolean()) != 4 || footprints.Count(i => i.GetProperty("inherited").GetBoolean()) != 3 ||
            icons.Any(i => i.GetProperty("width").GetInt32() != 32 || i.GetProperty("height").GetInt32() != 32 ||
                i.GetProperty("sourceFrame").GetInt32() != 0 || i.GetProperty("sourceFrameCount").GetInt32() != 2 || i.GetProperty("paletteIndex").GetInt32() is < 0 or > 2) ||
            footprints.Any(i => i.GetProperty("width").GetInt32() != 16 || i.GetProperty("height").GetInt32() != 16))
            throw new InvalidDataException("Stone evolution menu geometry differs.");
        using var inheritedGeometry = JsonDocument.Parse(Read(manifest.GetProperty("moveAnimationGeometry").GetProperty("inheritedManifest").GetString()!));
        var geometry = manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray().ToArray();
        if (!geometry.Select(r => (r.GetProperty("species").GetInt32(), r.GetProperty("side").GetString()!))
            .SequenceEqual(Species.SelectMany(s => new[] { (s, "front"),(s, "back") })))
            throw new InvalidDataException("Stone evolution move geometry inventory differs.");
        foreach (var row in geometry)
        {
            int species = row.GetProperty("species").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            if (!inheritedGeometry.RootElement.GetProperty("geometry").EnumerateArray().Any(old => JsonElement.DeepEquals(old, row)))
                throw new InvalidDataException("Stone evolution inherited move geometry differs.");
            foreach (var sprite in sprites.Where(s => s.GetProperty("sourceSpeciesId").GetInt32() == species && s.GetProperty("side").GetString() == side))
                if (sprite.GetProperty("width").GetInt32() != 64 || sprite.GetProperty("height").GetInt32() != 64 ||
                    sprite.GetProperty("x").GetInt32() != (side == "front" ? 144 : 40) ||
                    sprite.GetProperty("y").GetInt32() != (side == "front" ? 8 + row.GetProperty("yOffset").GetInt32() - row.GetProperty("elevation").GetInt32() : 48 + row.GetProperty("yOffset").GetInt32()) ||
                    sprite.GetProperty("sourceYOffset").GetInt32() != row.GetProperty("yOffset").GetInt32() ||
                    sprite.GetProperty("sourceEnemyElevation").GetInt32() != row.GetProperty("elevation").GetInt32() ||
                    sprite.GetProperty("sourceWidth").GetInt32() != row.GetProperty("sourceWidth").GetInt32() ||
                    sprite.GetProperty("sourceHeight").GetInt32() != row.GetProperty("sourceHeight").GetInt32())
                    throw new InvalidDataException("Stone evolution source battler geometry differs.");
        }
        var cues = manifest.GetProperty("audio").GetProperty("cues").EnumerateArray().ToArray();
        if (!cues.Select(c => c.GetProperty("cue").GetProperty("species").GetInt32()).SequenceEqual(Species))
            throw new InvalidDataException("Stone evolution inherited cry inventory differs.");
        foreach (var row in cues)
        {
            using var synth = JsonDocument.Parse(Read(row.GetProperty("inheritedManifest").GetString()!));
            var cue = row.GetProperty("cue");
            int species = cue.GetProperty("species").GetInt32();
            string name = dex.Entries.Single(e => e.SourceSpeciesId == species).Name;
            if (cue.GetProperty("id").GetString() != "CRY_" + name + "_NORMAL" || cue.GetProperty("sourcePlayer").GetInt32() != 4 ||
                cue.GetProperty("priority").GetInt32() != 10 ||
                cue.GetProperty("sourceSymbol").GetString() != "SPECIES_" + name || cue.GetProperty("looping").GetBoolean() ||
                cue.TryGetProperty("cryMode", out var mode) && mode.GetInt32() != 0 ||
                !synth.RootElement.GetProperty("cues").EnumerateArray().Any(old => JsonElement.DeepEquals(old, cue)))
                throw new InvalidDataException("Stone evolution inherited normal cry differs.");
        }
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
