using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Route22 Pokémon presentation, bound to shared species definitions and independent of gameplay admission.</summary>
public sealed class PretRoute22Pokemon
{
    public const string Profile = "firered-route22-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public Route22PokemonSupplement Supplement { get; }
    public ImmutableArray<StorageArtIcon> Icons => Supplement.Icons;
    public ImmutableArray<Route22PokemonFootprint> Footprints => Supplement.Footprints;
    public ImmutableArray<Route22PokemonCry> Cries => Supplement.Cries;
    public ImmutableDictionary<SpeciesId, int> WeightsHectograms { get; }
    public ImmutableDictionary<int, PokedexMenuEntry> DexEntries { get; }
    public PretPokedexMenu PokedexMenu { get; }
    public ImmutableDictionary<int, CaptureDexEntry> CaptureDexEntries { get; }
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretRoute22Pokemon(string directory, PokemonCatalog catalog,
        ImmutableArray<ForestPokemonSprite> sprites, JsonElement art,
        Route22PokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory; Catalog = catalog;
        Sprites = sprites; BattleArt = art;
        Supplement = supplement; PokedexMenu = dex; _outputs = outputs;
        WeightsHectograms = supplement.Weights.ToImmutableDictionary(w => new SpeciesId(w.SpeciesId), w => w.WeightHectograms);
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(sid => sid, sid =>
        {
            var sprite = sprites.Single(s => s.SourceSpeciesId == sid && s.Side == "front" && s.PaletteVariant == "normal");
            var foot = supplement.Footprints.Single(f => f.SourceSpeciesId == sid);
            return dex.Entries[sid] with
            {
                Portrait = new("content/generated/sprites/pokemon/route22/" + sprite.TexturePath,64,64),Footprint = new("content/generated/sprites/pokemon/route22/" + foot.TexturePath,16,16)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(e => e.Key, e =>
        {
            var d = e.Value;
            return new CaptureDexEntry(d.SourceSpeciesId, d.NationalNumber, d.Name, d.Category, d.DisplayCategory,
                d.HeightDecimeters, d.WeightHectograms, d.HeightDisplay, d.WeightDisplay, d.Description,
                d.Footprint!.TexturePath, d.Portrait!.TexturePath, d.HabitatTitle);
        });
    }

    /// <summary>Authenticates content bytes. Campaign omits optional source cry WAV copies while retaining their pinned provenance identities.</summary>
    public static PretRoute22Pokemon Load(PretCanonicalPokemonCatalog canonical, string projectRoot, bool includeSourceCryWaves = true, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/sprites/pokemon/route22");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 32, "Route22 Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (!includeSourceCryWaves && path is "cries/spearow.wav" or "cries/fearow.wav" or "cries/mankey.wav") continue;
            if (path == "catalog.json") continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Route22 Pokemon output.");
        }
        var catalog = canonical.Catalog;
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 12 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 12,
            "Route22 Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Route22 Pokemon sprite identity/pixels differ.");
        }
        var supplement = ContentJson.ParseStrict<Route22PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 21,22,56 }) && supplement.Icons.Length == 3 &&
            supplement.Footprints.Length == 3 && supplement.Cries.Length == 3 && supplement.Weights.Length == 32 &&
            supplement.NaturalLevelLimit == 20,
            "Route22 supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && (outputs[icon.TexturePath]).Length > 0, "Route22 icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "Route22 footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && (!includeSourceCryWaves ),
                "Route22 source cry differs or claims unverified playback.");
        }
        Require(supplement.Weights.Select(w => w.SpeciesId).Distinct(StringComparer.Ordinal).Count() == supplement.Weights.Length,
            "Route22 weights contain duplicate species.");
        foreach (var weight in supplement.Weights)
        {
            Require(catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Route22 weight differs from the authenticated Dex source.");
        }
        return new(directory, catalog, sprites, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary());
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));

    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(i => i.SpeciesId == species.Value);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _outputs[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _outputs[Footprints.Single(f => f.SourceSpeciesId == sourceSpeciesId).TexturePath]];
    public byte[] ReadCryWave(int sourceSpeciesId)
    {
        string path = Cries.Single(c => c.SourceSpeciesId == sourceSpeciesId).WavePath;
        if (!_outputs.TryGetValue(path, out var bytes))
            throw new NotSupportedException("Source cry WAV copies were not loaded; campaign uses the authenticated runtime synth pack.");
        return [.. bytes];
    }
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _outputs[Sprite(species, side, shiny).TexturePath]];
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Route22 Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route22 Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route22 Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record Route22PokemonWeight(string SpeciesId, int SourceSpeciesId, int WeightHectograms);
public sealed record Route22PokemonFootprint(int SourceSpeciesId, string TexturePath, int Width, int Height);
public sealed record Route22PokemonCry(int SourceSpeciesId, string WavePath);
public sealed record Route22PokemonSupplement(int SchemaVersion, PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route22PokemonFootprint> Footprints, ImmutableArray<Route22PokemonCry> Cries,
    ImmutableArray<Route22PokemonWeight> Weights, int NaturalLevelLimit);
