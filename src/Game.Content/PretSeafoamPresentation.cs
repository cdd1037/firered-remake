using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record SeafoamDexDocument(int SchemaVersion, ImmutableArray<PokedexMenuEntry> Entries);
public sealed record SeafoamPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Pinned Articuno presentation and Roger's Bird Keeper portrait; shared source inputs remain authenticated.</summary>
public static class PretSeafoamPresentation
{
    public const string Base = "content/generated/sprites/pokemon/seafoam/";
    public const string Profile = "firered-seafoam-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static SeafoamPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") )
                throw new InvalidDataException("Unsafe Seafoam presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Seafoam resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Seafoam directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }

        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("Seafoam presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "sprites", "pokemonIcons", "footprints", "trainerPortraits" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "sprites","pokemonIcons","footprints","trainerPortraits" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var pngBytes) ||
                (pngBytes).Length == 0)
                throw new InvalidDataException("Seafoam pixels differ: " + path);
            if (!files.TryGetValue(asset.GetProperty("texturePath").GetString()!, out var png) )
                throw new InvalidDataException("Seafoam texture differs: " + path);
        }
        var dex = JsonSerializer.Deserialize<SeafoamDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Seafoam dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 144 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 144 }) ||
            manifest.GetProperty("sprites").GetArrayLength() != 4 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 1 ||
            manifest.GetProperty("footprints").GetArrayLength() != 1 ||
            manifest.GetProperty("trainerPortraits").GetArrayLength() != 1 || manifest.GetProperty("trainerBindings").GetArrayLength() != 1)
            throw new InvalidDataException("Seafoam presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!,e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png) )
                throw new InvalidDataException("Seafoam dex texture differs.");
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        if (!sprites.Select(s => (s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!))
            .SequenceEqual(new[] { ("front", "normal"),("front", "shiny"),("back", "normal"),("back", "shiny") }))
            throw new InvalidDataException("Seafoam battler variant inventory differs.");
        foreach (var asset in new[] { "sprites","pokemonIcons","footprints" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()))
            if (asset.GetProperty("sourceSpeciesId").GetInt32() != 144 || asset.GetProperty("speciesId").GetString() != "pret:SPECIES_ARTICUNO")
                throw new InvalidDataException("Seafoam asset species differs.");
        foreach (var sprite in sprites)
        {
            bool front = sprite.GetProperty("side").GetString() == "front";
            if (sprite.GetProperty("width").GetInt32() != 64 || sprite.GetProperty("height").GetInt32() != 64 ||
                sprite.GetProperty("x").GetInt32() != (front ? 144 : 40) || sprite.GetProperty("y").GetInt32() != (front ? 2 : 60) ||
                sprite.GetProperty("sourceYOffset").GetInt32() != (front ? 0 : 12) ||
                sprite.GetProperty("sourceElevation").GetInt32() != (front ? 6 : 0) ||
                sprite.GetProperty("sourceWidth").GetInt32() != (front ? 64 : 48) ||
                sprite.GetProperty("sourceHeight").GetInt32() != (front ? 64 : 40))
                throw new InvalidDataException("Seafoam battler geometry differs.");
        }
        var icon = manifest.GetProperty("pokemonIcons").EnumerateArray().Single();
        var foot = manifest.GetProperty("footprints").EnumerateArray().Single();
        if (icon.GetProperty("width").GetInt32() != 32 || icon.GetProperty("height").GetInt32() != 32 ||
            icon.GetProperty("paletteIndex").GetInt32() != 0 || icon.GetProperty("sourceFrame").GetInt32() != 0 ||
            icon.GetProperty("sourceFrameCount").GetInt32() != 2 ||
            foot.GetProperty("width").GetInt32() != 16 || foot.GetProperty("height").GetInt32() != 16)
            throw new InvalidDataException("Seafoam icon/footprint geometry differs.");
        var portrait = manifest.GetProperty("trainerPortraits").EnumerateArray().Single();
        var trainer = manifest.GetProperty("trainerBindings").EnumerateArray().Single();
        if (portrait.GetProperty("id").GetString() != "bird-keeper-front" ||
            portrait.GetProperty("sourceTrainerPicId").GetInt32() != 104 ||
            portrait.GetProperty("trainerPic").GetString() != "TRAINER_PIC_BIRD_KEEPER" ||
            portrait.GetProperty("trainerClass").GetString() != "TRAINER_CLASS_BIRD_KEEPER" ||
            portrait.GetProperty("className").GetString() != "BIRD KEEPER" ||
            portrait.GetProperty("width").GetInt32() != 64 || portrait.GetProperty("height").GetInt32() != 64 ||
            portrait.GetProperty("x").GetInt32() != 144 || portrait.GetProperty("y").GetInt32() != 8 ||
            portrait.GetProperty("sourceFrame").GetInt32() != 0 || portrait.GetProperty("sourceSize").GetInt32() != 8 ||
            portrait.GetProperty("sourceYOffset").GetInt32() != 1 || portrait.GetProperty("transparentIndex").GetInt32() != 0 ||
            portrait.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)" ||
            portrait.GetProperty("texturePath").GetString() != Base + "bird-keeper-front.png" ||
            portrait.GetProperty("texturePath").GetString() != Base + "bird-keeper-front.png" ||
            trainer.GetProperty("trainer").GetString() != "pret:TRAINER_BIRD_KEEPER_ROGER" ||
            trainer.GetProperty("name").GetString() != "ROGER" || trainer.GetProperty("portrait").GetString() != "bird-keeper-front" ||
            trainer.GetProperty("trainerPic").GetString() != "TRAINER_PIC_BIRD_KEEPER" ||
            trainer.GetProperty("trainerClass").GetString() != "TRAINER_CLASS_BIRD_KEEPER" || trainer.GetProperty("className").GetString() != "BIRD KEEPER")
            throw new InvalidDataException("Seafoam Bird Keeper portrait/binding differs.");
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
