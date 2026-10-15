// Authenticated Route11 additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record Route11PokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int? NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record Route11PokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated six-species Route11 additions and source evolution references; effects execute independently.</summary>
public sealed class PretRoute11Pokemon
{
    public const string Profile = "firered-route11-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions { get; }
    public Route11PokemonSupplement Supplement { get; }
    public ImmutableArray<StorageArtIcon> Icons => Supplement.Icons;
    public ImmutableArray<Route3PokemonFootprint> Footprints => Supplement.Footprints;
    public ImmutableArray<Route3PokemonCry> Cries => Supplement.Cries;
    public ImmutableDictionary<SpeciesId, int> WeightsHectograms { get; }
    public ImmutableDictionary<int, PokedexMenuEntry> DexEntries { get; }
    public PretPokedexMenu PokedexMenu { get; }
    public ImmutableDictionary<int, CaptureDexEntry> CaptureDexEntries { get; }
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretRoute11Pokemon(string directory, Route11PokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        Route11PokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; EncodedNames = content.EncodedNames;
        Sprites = sprites; SourceFiles = files; BattleArt = art; Supplement = supplement;
        PokedexMenu = dex; _outputs = outputs; DeferredEvolutions = content.DeferredEvolutions;
        WeightsHectograms = supplement.Weights.ToImmutableDictionary(w => new SpeciesId(w.SpeciesId), w => w.WeightHectograms);
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(sid => sid, sid =>
        {
            var sprite = sprites.Single(s => s.SourceSpeciesId == sid && s.Side == "front" && s.PaletteVariant == "normal");
            var foot = supplement.Footprints.Single(f => f.SourceSpeciesId == sid);
            return dex.Entries[sid] with
            {
                Portrait = new("content/generated/route11-pokemon/" + sprite.TexturePath,64,64),Footprint = new("content/generated/route11-pokemon/" + foot.TexturePath,16,16)
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

    public static PretRoute11Pokemon Load(PretCanonicalPokemonCatalog canonical, string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/route11-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 79 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Route11 Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Route11 Pokemon output.");
        }
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var inventory = files.ToImmutable();
        var content = LoadContent(canonical,
            Utf8.GetString(ReadPinned(project,"content/generated/route1-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/old-man-tutorial/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/forest-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/pewter-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/route22-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/route3-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/mtmoon-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/cerulean-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/bill-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/vermilion-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/ssanne-pokemon/catalog.json")),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 24 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 24,
            "Route11 Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Route11 Pokemon sprite identity/pixels differ.");
        }
        var supplement = ContentJson.ParseStrict<Route11PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 50,51,82,60,61,37 }) && supplement.Icons.Length == 6 &&
            supplement.Footprints.Length == 6 && supplement.Cries.Length == 6 && supplement.Weights.Length == 79 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit is null && !supplement.Limitations.IsDefaultOrEmpty,
            "Route11 supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && (outputs[icon.TexturePath]).Length > 0, "Route11 icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "Route11 footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal),
                "Route11 cry metadata differs or claims playback.");
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "Route11 catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "Route11 weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Route11 weight differs from the authenticated Dex source.");
        }
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary());
    }

    public static Route11PokemonCatalogContent LoadContent(PretCanonicalPokemonCatalog canonical, string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson,
        string pewterGeneratedJson, string route22GeneratedJson, string route3GeneratedJson, string mtmoonGeneratedJson, string ceruleanGeneratedJson, string billGeneratedJson, string vermilionGeneratedJson, string ssanneGeneratedJson, string route11GeneratedJson)
    {
        var original = PretSsAnnePokemon.LoadContent(canonical, route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson, route22GeneratedJson, route3GeneratedJson, mtmoonGeneratedJson, ceruleanGeneratedJson, billGeneratedJson, vermilionGeneratedJson, ssanneGeneratedJson);
        var options = canonical.ReferenceOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<Route11PokemonDocument>(route11GeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Species.Length == 6 && doc.Moves.Length == 61 && doc.SpeciesNames.Length == 6 &&
            doc.MoveNames.Length == 61 && doc.Constants.Length == 13 && doc.EncodedNames.Length == 67 && doc.NaturalLevelLimit is null &&
            doc.EvolutionOverrides.Length == 1 && !doc.ItemEvolutionExecutorImplemented && !doc.LevelEvolutionExecutorImplemented &&
            !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Route11 Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        var requiredMoves = doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).ToHashSet();
        requiredMoves.UnionWith([new("pret:MOVE_ASTONISH"), new("pret:MOVE_BITE"), new("pret:MOVE_BUBBLE"), new("pret:MOVE_CHARGE"), new("pret:MOVE_DEFENSE_CURL"), new("pret:MOVE_DOUBLE_KICK"), new("pret:MOVE_EMBER"), new("pret:MOVE_FLASH"), new("pret:MOVE_FOCUS_ENERGY"), new("pret:MOVE_GLARE"), new("pret:MOVE_GROWTH"), new("pret:MOVE_HYPER_FANG"), new("pret:MOVE_HYPNOSIS"), new("pret:MOVE_LEECH_LIFE"), new("pret:MOVE_LEER"), new("pret:MOVE_PECK"), new("pret:MOVE_POISON_POWDER"), new("pret:MOVE_POISON_STING"), new("pret:MOVE_QUICK_ATTACK"), new("pret:MOVE_ROAR"), new("pret:MOVE_SAND_ATTACK"), new("pret:MOVE_SCRATCH"), new("pret:MOVE_SCREECH"), new("pret:MOVE_SLEEP_POWDER"), new("pret:MOVE_SMOKESCREEN"), new("pret:MOVE_SONIC_BOOM"), new("pret:MOVE_STUN_SPORE"), new("pret:MOVE_SUPERSONIC"), new("pret:MOVE_SWEET_SCENT"), new("pret:MOVE_TACKLE"), new("pret:MOVE_TAIL_WHIP"), new("pret:MOVE_THUNDER_SHOCK"), new("pret:MOVE_THUNDER_WAVE"), new("pret:MOVE_WATER_GUN"), new("pret:MOVE_WILL_O_WISP"), new("pret:MOVE_WRAP")]);
        Require(requiredMoves.SetEquals(doc.Moves.Select(x => x.Id)),
            "Route11 Pokemon full learnsets plus ten trainer teams and Flash move closure differs.");
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            if (!moves.ContainsKey(value.Id)) moves.Add(value.Id, value);
        }
        var names = original.SpeciesNames.ToBuilder();
        foreach (var value in doc.SpeciesNames) { names.Add(species[value.Id].SourceSpeciesId, value.Name); }
        var moveNames = original.MoveNames.ToBuilder();
        foreach (var value in doc.MoveNames)
        {
            Require(!moveNames.TryGetValue(value.Id, out var existing) || existing == value.Name, "Existing move name changed.");
            moveNames[value.Id] = value.Name;
        }
        var constants = original.Constants.ToBuilder();
        foreach (var value in doc.Constants)
        {
            Require(!constants.TryGetValue(value.Symbol, out int previous) || previous == value.Value, "Existing evolution constant changed.");
            constants[value.Symbol] = value.Value;
        }
        Require(constants["EVO_LEVEL"] == 4 && constants["EVO_ITEM"] == 7 && constants["EVO_TRADE_ITEM"] == 6 &&
            constants["ITEM_FIRE_STONE"] == 95 && constants["ITEM_WATER_STONE"] == 97 && constants["ITEM_KINGS_ROCK"] == 187 &&
            constants["ABILITY_ARENA_TRAP"] == 71 && constants["ABILITY_WATER_ABSORB"] == 11 && constants["ABILITY_DAMP"] == 6 &&
            constants["ABILITY_SAND_VEIL"] == 8 && constants["ABILITY_MAGNET_PULL"] == 42 && constants["ABILITY_STURDY"] == 5 &&
            constants["ABILITY_FLASH_FIRE"] == 18, "Route11 evolution or ability constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty && value.Bytes.Length == value.Name.Length + 1 &&
                value.Bytes[^1] == 255 && value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        var overlay = doc.EvolutionOverrides.Single();
        Require(overlay.Species.Value == "pret:SPECIES_MAGNEMITE" && overlay.Evolution.Target.Value == "pret:SPECIES_MAGNETON" &&
            overlay.Evolution.Level == 30 && overlay.Evolution.Method == PokemonEvolutionMethod.Level && overlay.Evolution.Parameter is null &&
            species[overlay.Species].Evolutions.IsEmpty,
            "Route11 Magnemite level evolution overlay differs.");
        species[overlay.Species] = species[overlay.Species] with { Evolutions = [overlay.Evolution] };
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateDefinitions(result);
        Require(result.Species.Count == 79 && result.Moves.Count == 209 && names.Count == 79 && moveNames.Count == 209, "Incomplete composed Route11 Pokemon catalog.");
        Require(doc.DeferredEvolutions.Length == 3, "Missing Route11 deferred evolution metadata.");
        var expectedDeferred = new HashSet<(string Species, string Method, string Parameter, string Target)>
        {
            ("pret:SPECIES_POLIWHIRL", "EVO_ITEM", "ITEM_WATER_STONE", "pret:SPECIES_POLIWRATH"),("pret:SPECIES_POLIWHIRL", "EVO_TRADE_ITEM", "ITEM_KINGS_ROCK", "pret:SPECIES_POLITOED"),("pret:SPECIES_VULPIX", "EVO_ITEM", "ITEM_FIRE_STONE", "pret:SPECIES_NINETALES")
        };
        foreach (var deferred in doc.DeferredEvolutions)
        {
            Require(expectedDeferred.Remove((deferred.Species.Value, deferred.Method, deferred.Parameter, deferred.Target.Value)) &&
                !deferred.Executable && !string.IsNullOrWhiteSpace(deferred.Reason) && !species.ContainsKey(deferred.Target) &&
                species[deferred.Species].Evolutions.IsEmpty, "Deferred source evolution changed or became executable.");
        }
        Require(expectedDeferred.Count == 0, "Missing deferred Route11 evolution.");
        var allDeferred = original.DeferredEvolutions.Where(d => !doc.EvolutionOverrides.Any(o => o.Species == d.Species)).Concat(doc.DeferredEvolutions).ToImmutableArray();
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants.ToImmutable(), encoded, allDeferred);
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
            throw new InvalidDataException("Unsafe Route11 Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route11 Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route11 Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record Route11PokemonSupplement(int SchemaVersion, string Profile, string SourceCommit,
    PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route3PokemonFootprint> Footprints, ImmutableArray<Route3PokemonCry> Cries,
    ImmutableArray<Route3PokemonWeight> Weights, bool NormalShinyIconsIdentical, int? NaturalLevelLimit, ImmutableArray<string> Limitations);
