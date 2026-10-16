// Authenticated SsAnne additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>SsAnne Pokémon presentation, bound to shared species definitions and independent of gameplay admission.</summary>
public sealed class PretSsAnnePokemon
{
    public const string Profile = "firered-ssanne-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public SsAnnePokemonSupplement Supplement { get; }
    public ImmutableArray<StorageArtIcon> Icons => Supplement.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints => Supplement.Footprints;
    public ImmutableArray<Route3PokemonCry> Cries => Supplement.Cries;
    public ImmutableDictionary<SpeciesId, int> WeightsHectograms { get; }
    public ImmutableDictionary<int, PokedexMenuEntry> DexEntries { get; }
    public PretPokedexMenu PokedexMenu { get; }
    public ImmutableDictionary<int, CaptureDexEntry> CaptureDexEntries { get; }
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretSsAnnePokemon(string directory, PokemonCatalog catalog,
        ImmutableArray<ForestPokemonSprite> sprites, JsonElement art,
        SsAnnePokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory; Catalog = catalog;
        Sprites = sprites; BattleArt = art; Supplement = supplement;
        PokedexMenu = dex; _outputs = outputs;
        WeightsHectograms = supplement.Weights.ToImmutableDictionary(w => new SpeciesId(w.SpeciesId), w => w.WeightHectograms);
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(sid => sid, sid =>
        {
            var sprite = sprites.Single(s => s.SourceSpeciesId == sid && s.Side == "front" && s.PaletteVariant == "normal");
            var foot = supplement.Footprints.Single(f => f.SourceSpeciesId == sid);
            return dex.Entries[sid] with
            {
                Portrait = new("content/generated/sprites/pokemon/ssanne/" + sprite.TexturePath,64,64),Footprint = new("content/generated/sprites/pokemon/ssanne/" + foot.TexturePath,16,16)
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

    public static PretSsAnnePokemon Load(PretCanonicalPokemonCatalog canonical, string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/sprites/pokemon/ssanne");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 73, "SsAnne Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "catalog.json") continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate SsAnne Pokemon output.");
        }
        var catalog = canonical.Catalog;
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 12 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 12,
            "SsAnne Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "SsAnne Pokemon sprite identity/pixels differ.");
        }
        var supplement = ContentJson.ParseStrict<SsAnnePokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 58,77,72 }) && supplement.Icons.Length == 3 &&
            supplement.Footprints.Length == 3 && supplement.Cries.Length == 3 && supplement.Weights.Length == 73 &&
            supplement.NaturalLevelLimit is null,
            "SsAnne supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && (outputs[icon.TexturePath]).Length > 0, "SsAnne icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "SsAnne footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal),
                "SsAnne cry metadata differs or claims playback.");
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "SsAnne catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => w.SpeciesId).Distinct(StringComparer.Ordinal).Count() == supplement.Weights.Length,
            "SsAnne weights contain duplicate species.");
        foreach (var weight in supplement.Weights)
        {
            Require(catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "SsAnne weight differs from the authenticated Dex source.");
        }
        return new(directory, catalog, sprites, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary());
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) => Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(i => i.SpeciesId == species.Value);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _outputs[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _outputs[Footprints.Single(f => f.SourceSpeciesId == sourceSpeciesId).TexturePath]];
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _outputs[Sprite(species, side, shiny).TexturePath]];
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe SsAnne Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("SsAnne Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("SsAnne Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record SsAnnePokemonSupplement(int SchemaVersion, PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route3PokemonFootprint> Footprints, ImmutableArray<Route3PokemonCry> Cries,
    ImmutableArray<Route3PokemonWeight> Weights, int? NaturalLevelLimit);
