using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record Route3PokemonFootprint(int SourceSpeciesId, string TexturePath, int Width, int Height);

internal sealed record PokemonPresentationSupplement(int SchemaVersion, ImmutableArray<int> DexSpeciesIds,
    ImmutableArray<StorageArtIcon> Icons, ImmutableArray<Route3PokemonFootprint> Footprints);
internal sealed record PokemonPresentationBattleArt(int SchemaVersion, JsonElement Display,
    ImmutableArray<ForestPokemonSprite> Sprites);

/// <summary>Shared battle, party and Dex presentation bound to canonical species and generated textures.</summary>
public sealed class PokemonPresentationPackage
{
    private readonly ImmutableDictionary<string, byte[]> _textures;
    private readonly string _texturePrefix;
    public PokemonCatalog Catalog { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<StorageArtIcon> Icons { get; }
    public ImmutableArray<Route3PokemonFootprint> Footprints { get; }
    public ImmutableDictionary<int, PokedexMenuEntry> DexEntries { get; }
    public PretPokedexMenu PokedexMenu { get; }
    public ImmutableDictionary<int, CaptureDexEntry> CaptureDexEntries { get; }
    public string GeneratedDirectory { get; }

    private PokemonPresentationPackage(string directory, string relativeDirectory, PokemonCatalog catalog,
        PokemonPresentationBattleArt art, PokemonPresentationSupplement supplement, PretPokedexMenu dex,
        ImmutableDictionary<string, byte[]> textures)
    {
        GeneratedDirectory = directory;
        _texturePrefix = relativeDirectory + "/";
        Catalog = catalog;
        Sprites = art.Sprites;
        Icons = supplement.Icons;
        Footprints = supplement.Footprints;
        PokedexMenu = dex;
        _textures = textures;
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(id => id, id =>
        {
            var sprite = Sprites.Single(row => row.SourceSpeciesId == id && row.Side == "front" && row.PaletteVariant == "normal");
            var footprint = Footprints.Single(row => row.SourceSpeciesId == id);
            return dex.Entries[id] with
            {
                Portrait = new(relativeDirectory + "/" + sprite.TexturePath, sprite.Width, sprite.Height),
                Footprint = new(relativeDirectory + "/" + footprint.TexturePath, footprint.Width, footprint.Height)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(pair => pair.Key, pair =>
        {
            var entry = pair.Value;
            return new CaptureDexEntry(entry.SourceSpeciesId, entry.NationalNumber, entry.Name, entry.Category,
                entry.DisplayCategory, entry.HeightDecimeters, entry.WeightHectograms, entry.HeightDisplay,
                entry.WeightDisplay, entry.Description, entry.Footprint!.TexturePath, entry.Portrait!.TexturePath,
                entry.HabitatTitle);
        });
    }

    public static PokemonPresentationPackage Load(PretCanonicalPokemonCatalog canonical, string projectRoot,
        string package, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var relativeDirectory = "content/generated/sprites/pokemon/" + package;
        var directory = GeneratedResourceFile.SafePath(project, relativeDirectory);
        var options = ContentJson.CreateOptions();
        var art = ContentJson.ParseStrict<PokemonPresentationBattleArt>(GeneratedResourceFile.Read(directory, "battle-art.json"), options);
        var supplement = ContentJson.ParseStrict<PokemonPresentationSupplement>(GeneratedResourceFile.Read(directory, "supplement.json"), options);
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        var catalog = canonical.Catalog;
        Require(art.SchemaVersion == 1 && supplement.SchemaVersion == 1 &&
            !art.Sprites.IsDefaultOrEmpty && !supplement.DexSpeciesIds.IsDefaultOrEmpty &&
            !supplement.Icons.IsDefault && !supplement.Footprints.IsDefault,
            "Pokemon presentation schema or asset bindings are missing.");

        var speciesIds = supplement.DexSpeciesIds.ToHashSet();
        Require(speciesIds.Count == supplement.DexSpeciesIds.Length && speciesIds.All(dex.Entries.ContainsKey) &&
            supplement.Icons.Select(row => row.SourceSpeciesId).ToHashSet().SetEquals(speciesIds) &&
            supplement.Footprints.Select(row => row.SourceSpeciesId).ToHashSet().SetEquals(speciesIds) &&
            supplement.Icons.Length == speciesIds.Count && supplement.Footprints.Length == speciesIds.Count,
            "Pokemon presentation Dex bindings differ.");
        Require(art.Sprites.Select(row => (row.SpeciesId, row.Side, row.PaletteVariant)).Distinct().Count() == art.Sprites.Length &&
            art.Sprites.Select(row => row.SourceSpeciesId).ToHashSet().SetEquals(speciesIds) &&
            art.Sprites.GroupBy(row => row.SourceSpeciesId).All(group => group.Count() == 4),
            "Pokemon presentation battler variants differ.");

        var textures = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        void LoadTexture(string path)
        {
            if (textures.ContainsKey(path)) return;
            var bytes = GeneratedResourceFile.Read(directory, path);
            Require(bytes.Length > 0, "Empty Pokemon presentation texture: " + path);
            textures.Add(path, bytes);
        }
        foreach (var sprite in art.Sprites)
        {
            Require(catalog.Species.TryGetValue(new(sprite.SpeciesId), out var species) &&
                species.SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Side is "front" or "back" && sprite.PaletteVariant is "normal" or "shiny" &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                sprite.PaletteRgb555.All(color => color is >= 0 and <= 32767),
                "Pokemon presentation sprite identity or geometry differs.");
            LoadTexture(sprite.TexturePath);
        }
        foreach (var icon in supplement.Icons)
        {
            Require(catalog.Species.TryGetValue(new(icon.SpeciesId), out var species) &&
                species.SourceSpeciesId == icon.SourceSpeciesId && icon.Width == 32 && icon.Height == 32 &&
                icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 && icon.PaletteIndex is >= 0 and <= 2 &&
                icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && icon.PaletteRgb555.All(color => color is >= 0 and <= 32767),
                "Pokemon presentation icon identity or geometry differs.");
            LoadTexture(icon.TexturePath);
        }
        foreach (var footprint in supplement.Footprints)
        {
            Require(footprint.Width == 16 && footprint.Height == 16, "Pokemon presentation footprint geometry differs.");
            LoadTexture(footprint.TexturePath);
        }
        return new(directory, relativeDirectory, catalog, art, supplement, dex, textures.ToImmutable());
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(row => row.SpeciesId == species.Value && row.Side == side && row.PaletteVariant == (shiny ? "shiny" : "normal"));
    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(row => row.SpeciesId == species.Value);
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _textures[Sprite(species, side, shiny).TexturePath]];
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _textures[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _textures[Footprints.Single(row => row.SourceSpeciesId == sourceSpeciesId).TexturePath]];

    public byte[] ReadTexture(string fullRelativePath)
    {
        if (!fullRelativePath.StartsWith(_texturePrefix, StringComparison.Ordinal) ||
            !_textures.TryGetValue(fullRelativePath[_texturePrefix.Length..], out var bytes))
            throw new InvalidDataException("Unknown Pokemon presentation texture: " + fullRelativePath);
        return [.. bytes];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
