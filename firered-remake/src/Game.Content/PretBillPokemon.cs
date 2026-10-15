// Authenticated Bill additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record BillPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record BillPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated three-species Bill additions and source evolution references; effects execute independently.</summary>
public sealed class PretBillPokemon
{
    public const string Profile = "firered-bill-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions { get; }
    public Route3PokemonSupplement Supplement { get; }
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

    private PretBillPokemon(string directory, BillPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        Route3PokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs)
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
                Portrait = new("content/generated/bill-pokemon/" + sprite.TexturePath,64,64),Footprint = new("content/generated/bill-pokemon/" + foot.TexturePath,16,16)
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

    public static PretBillPokemon Load(PretCanonicalPokemonCatalog canonical, string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/bill-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Bill Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Bill Pokemon output.");
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
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 12 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 12,
            "Bill Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Bill Pokemon sprite identity/pixels differ.");
        }
        var supplement = ContentJson.ParseStrict<Route3PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 79,44,70 }) && supplement.Icons.Length == 3 &&
            supplement.Footprints.Length == 3 && supplement.Cries.Length == 3 && supplement.Weights.Length == 68 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit == 25 && !supplement.Limitations.IsDefaultOrEmpty,
            "Bill supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 &&
                (outputs[icon.TexturePath]).Length > 0, "Bill icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "Bill footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal),
                "Bill cry metadata differs or claims playback.");
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "Bill catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "Bill weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Bill weight differs from the authenticated Dex source.");
        }
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary());
    }

    public static BillPokemonCatalogContent LoadContent(PretCanonicalPokemonCatalog canonical, string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson,
        string pewterGeneratedJson, string route22GeneratedJson, string route3GeneratedJson, string mtmoonGeneratedJson, string ceruleanGeneratedJson, string billGeneratedJson)
    {
        var original = PretCeruleanPokemon.LoadContent(canonical, route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson, route22GeneratedJson, route3GeneratedJson, mtmoonGeneratedJson, ceruleanGeneratedJson);
        var options = canonical.ReferenceOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<BillPokemonDocument>(billGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.NaturalLevelLimit == 25 &&
            doc.EvolutionOverrides.Length == 2 && !doc.ItemEvolutionExecutorImplemented && !doc.LevelEvolutionExecutorImplemented &&
            !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Bill Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        var requiredMoves = doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).ToHashSet();
        requiredMoves.UnionWith([new("pret:MOVE_ABSORB"), new("pret:MOVE_ASTONISH"), new("pret:MOVE_BIND"), new("pret:MOVE_BITE"), new("pret:MOVE_CONFUSION"), new("pret:MOVE_DEFENSE_CURL"), new("pret:MOVE_DOUBLE_KICK"), new("pret:MOVE_EMBER"), new("pret:MOVE_FOCUS_ENERGY"), new("pret:MOVE_FURY_ATTACK"), new("pret:MOVE_FURY_SWIPES"), new("pret:MOVE_GROWL"), new("pret:MOVE_GROWTH"), new("pret:MOVE_GUST"), new("pret:MOVE_HARDEN"), new("pret:MOVE_HYPER_FANG"), new("pret:MOVE_KARATE_CHOP"), new("pret:MOVE_LEECH_LIFE"), new("pret:MOVE_LEECH_SEED"), new("pret:MOVE_LEER"), new("pret:MOVE_LOW_KICK"), new("pret:MOVE_METAL_CLAW"), new("pret:MOVE_MUD_SPORT"), new("pret:MOVE_PECK"), new("pret:MOVE_POISON_POWDER"), new("pret:MOVE_POISON_STING"), new("pret:MOVE_QUICK_ATTACK"), new("pret:MOVE_ROCK_THROW"), new("pret:MOVE_SAND_ATTACK"), new("pret:MOVE_SCRATCH"), new("pret:MOVE_SCREECH"), new("pret:MOVE_SLEEP_POWDER"), new("pret:MOVE_STRING_SHOT"), new("pret:MOVE_SUPERSONIC"), new("pret:MOVE_SWEET_SCENT"), new("pret:MOVE_TACKLE"), new("pret:MOVE_TAIL_WHIP"), new("pret:MOVE_TELEPORT"), new("pret:MOVE_VINE_WHIP"), new("pret:MOVE_WATER_GUN"), new("pret:MOVE_WITHDRAW"), new("pret:MOVE_WRAP")]);
        requiredMoves.UnionWith([new("pret:MOVE_ROCK_TOMB"), new("pret:MOVE_ROAR"), new("pret:MOVE_BULLET_SEED"), new("pret:MOVE_THIEF"), new("pret:MOVE_WATER_PULSE"), new("pret:MOVE_DIG"), new("pret:MOVE_ATTRACT"), new("pret:MOVE_SECRET_POWER")]);
        Require(requiredMoves.SetEquals(doc.Moves.Select(x => x.Id)),
            "Bill Pokemon full learnset plus nineteen trainer teams move closure differs.");
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            if (moves.TryGetValue(value.Id, out var existing)) Require(JsonSerializer.Serialize(existing) == JsonSerializer.Serialize(value), "Existing move definition changed.");
            else moves.Add(value.Id, value);
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
            constants["ITEM_LEAF_STONE"] == 98 && constants["ITEM_SUN_STONE"] == 93 && constants["ITEM_KINGS_ROCK"] == 187 &&
            constants["ABILITY_OBLIVIOUS"] == 12 && constants["ABILITY_OWN_TEMPO"] == 20 && constants["ABILITY_CHLOROPHYLL"] == 34,
            "Bill evolution or ability constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty && value.Bytes.Length == value.Name.Length + 1 &&
                value.Bytes[^1] == 255 && value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        foreach (var (from, to) in new[] { ("ODDISH", "GLOOM"),("BELLSPROUT", "WEEPINBELL") })
        {
            var overlay = doc.EvolutionOverrides.Single(o => o.Species.Value == "pret:SPECIES_" + from);
            Require(overlay.Evolution.Target.Value == "pret:SPECIES_" + to && overlay.Evolution.Level == 21 &&
                overlay.Evolution.Method == PokemonEvolutionMethod.Level && overlay.Evolution.Parameter is null &&
                species[overlay.Species].Evolutions.IsEmpty,
                "Bill level evolution overlay differs.");
            species[overlay.Species] = species[overlay.Species] with { Evolutions = [overlay.Evolution] };
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateDefinitions(result);
        Require(result.Species.Count == 68 && result.Moves.Count == 188 && names.Count == 68 && moveNames.Count == 188, "Incomplete composed Bill Pokemon catalog.");
        Require(doc.DeferredEvolutions.Length == 5, "Missing deferred Bill evolutions.");
        foreach (var deferred in doc.DeferredEvolutions)
        {
            Require(!deferred.Executable && !string.IsNullOrWhiteSpace(deferred.Reason) && !species.ContainsKey(deferred.Target) &&
                species[deferred.Species].Evolutions.IsEmpty && deferred.Method is "EVO_LEVEL" or "EVO_ITEM" or "EVO_TRADE_ITEM",
                "Deferred evolution became executable or changed identity.");
        }
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
            throw new InvalidDataException("Unsafe Bill Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Bill Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Bill Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}
