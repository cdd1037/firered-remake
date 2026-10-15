using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Game.Content;

public sealed record FossilLabDexDocument(int SchemaVersion, ImmutableArray<PokedexMenuEntry> Entries);
public sealed record FossilLabPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files,
    Dictionary<int, PokedexMenuEntry> DexEntries);

/// <summary>Five owned fossil species, source dex pages and menus, with inherited Aerodactyl League resources.</summary>
public static class PretFossilLabPresentation
{
    public const string Base = "content/generated/sprites/pokemon/fossil-lab/";
    public const string Profile = "firered-fossil-lab-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static FossilLabPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Fossil Lab presentation resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fossil Lab resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Fossil Lab directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }

        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Fossil Lab presentation schema differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
        foreach (var asset in new[] { "sprites","pokemonIcons","footprints","inheritedSprites","inheritedPokemonIcons" }
            .SelectMany(kind => manifest.GetProperty(kind).EnumerateArray()).Append(manifest.GetProperty("oldAmber").GetProperty("icon")))
        {
            int width = asset.GetProperty("width").GetInt32(),height = asset.GetProperty("height").GetInt32();
            string path = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(path)) files.Add(path, Read(path));
            string texturePath = asset.GetProperty("texturePath").GetString()!;
            if (!files.ContainsKey(texturePath)) files.Add(texturePath, Read(texturePath));
            if (width is <= 0 or > 64 || height is <= 0 or > 64 || !files.TryGetValue(path, out var pngBytes) ||
                (pngBytes).Length == 0)
                throw new InvalidDataException("Fossil Lab pixels differ: " + path);
        }
        var dex = JsonSerializer.Deserialize<FossilLabDexDocument>(files[Base + "dex-entries.json"], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        }) ?? throw new InvalidDataException("Fossil Lab dex document missing.");
        if (dex.SchemaVersion != 1 ||
            !dex.Entries.Select(e => e.SourceSpeciesId).SequenceEqual(new[] { 138,139,140,141,142 }) ||
            dex.Entries.Any(e => e.Portrait is null || e.Footprint is null) ||
            !manifest.GetProperty("newOwnedSpecies").EnumerateArray().Select(e => e.GetInt32()).SequenceEqual(new[] { 138,139,140,141,142 }) ||
            manifest.GetProperty("sprites").GetArrayLength() != 16 || manifest.GetProperty("pokemonIcons").GetArrayLength() != 4 ||
            manifest.GetProperty("footprints").GetArrayLength() != 5 || manifest.GetProperty("menus").GetArrayLength() != 5 ||
            manifest.GetProperty("inheritedSprites").GetArrayLength() != 4 || manifest.GetProperty("inheritedPokemonIcons").GetArrayLength() != 1)
            throw new InvalidDataException("Fossil Lab presentation inventory differs.");
        foreach (var texture in dex.Entries.SelectMany(e => new[] { e.Portrait!,e.Footprint! }))
            if (!files.TryGetValue(texture.TexturePath, out var png))
                throw new InvalidDataException("Fossil Lab dex texture differs.");
        foreach (var menu in manifest.GetProperty("menus").EnumerateArray())
        {
            string id = menu.GetProperty("menuId").GetString()!;
            string[] rows = id switch { "MULTICHOICE_HELIX" => ["HELIX FOSSIL", "EXIT"], "MULTICHOICE_DOME" => ["DOME FOSSIL", "EXIT"],
                "MULTICHOICE_AMBER" => ["OLD AMBER", "EXIT"], "MULTICHOICE_HELIX_AMBER" => ["HELIX FOSSIL", "OLD AMBER", "EXIT"],
                "MULTICHOICE_DOME_AMBER" => ["DOME FOSSIL", "OLD AMBER", "EXIT"],
                _ => throw new InvalidDataException("Unadmitted fossil menu.") };
            if (!menu.GetProperty("rows").EnumerateArray().Select(r => r.GetString()).SequenceEqual(rows) ||
                menu.GetProperty("left").GetInt32() != 0 || menu.GetProperty("top").GetInt32() != 0 ||
                menu.GetProperty("rowHeight").GetInt32() != 14 || menu.GetProperty("interiorHeightTiles").GetInt32() != rows.Length * 2 ||
                menu.GetProperty("ignoreB").GetBoolean() || menu.GetProperty("wrap").GetBoolean() || menu.GetProperty("cancelResult").GetInt32() != 127)
                throw new InvalidDataException("Fossil source menu differs.");
        }
        return new(manifest.Clone(), files, dex.Entries.ToDictionary(e => e.SourceSpeciesId));
    }
}
