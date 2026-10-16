using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record QwilfishDexDocument(int SchemaVersion, ImmutableArray<PokedexMenuEntry> Entries);
public sealed record QwilfishPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Qwilfish owned icon/footprint and exact reuse of existing Tower battlers, geometry and normal cry.</summary>
public static class PretQwilfishPresentation
{
    public const string Base = "content/generated/sprites/pokemon/qwilfish/";
    public const string Profile = "firered-qwilfish-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static QwilfishPresentationData Load(string root, string? inheritedRoot = null)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") )
                throw new InvalidDataException("Unsafe Qwilfish presentation resource.");
            var file = new FileInfo(Path.Combine(path.StartsWith(Base, StringComparison.Ordinal) ? root : inheritedRoot ?? root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Qwilfish resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Qwilfish directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }

        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("Qwilfish presentation identity differs.");
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
                throw new InvalidDataException("Qwilfish pixels differ: " + path);
            if (!files.TryGetValue(asset.GetProperty("texturePath").GetString()!, out var png) )
                throw new InvalidDataException("Qwilfish texture differs: " + path);
        }
        var dex = JsonSerializer.Deserialize<QwilfishDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Qwilfish dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 211 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 211 }) ||
            manifest.GetProperty("inheritedSprites").GetArrayLength() != 4 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 1 ||
            manifest.GetProperty("footprints").GetArrayLength() != 1)
            throw new InvalidDataException("Qwilfish presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!,e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png) )
                throw new InvalidDataException("Qwilfish dex texture differs.");
        var sprites = manifest.GetProperty("inheritedSprites").EnumerateArray().ToArray();
        if (!sprites.Select(s => (s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!))
            .SequenceEqual(new[] { ("front", "normal"),("front", "shiny"),("back", "normal"),("back", "shiny") }))
            throw new InvalidDataException("Qwilfish battler variant inventory differs.");
        foreach (var asset in new[] { "inheritedSprites","pokemonIcons","footprints" }
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
        using var inherited = JsonDocument.Parse(Read("content/generated/sprites/pokemon/trainer-tower/catalog.json"));
        foreach (var row in sprites)
            if (!inherited.RootElement.GetProperty("sprites").EnumerateArray().Any(old => JsonElement.DeepEquals(old, row)))
                throw new InvalidDataException("Qwilfish Tower sprite binding differs.");
        var geometry = manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray().ToArray();
        if (geometry.Length != 2 || !geometry.Select(r => r.GetProperty("side").GetString()).SequenceEqual(new[] { "front","back" }))
            throw new InvalidDataException("Qwilfish move geometry inventory differs.");
        foreach (var row in geometry)
        {
            bool front = row.GetProperty("side").GetString() == "front";
            if (row.GetProperty("species").GetInt32() != 211 || row.GetProperty("yOffset").GetInt32() != (front ? 10 : 7) ||
                row.GetProperty("elevation").GetInt32() != 0 || row.GetProperty("sourceWidth").GetInt32() != (front ? 40 : 56) ||
                row.GetProperty("sourceHeight").GetInt32() != (front ? 48 : 56))
                throw new InvalidDataException("Qwilfish move geometry differs.");
        }
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
