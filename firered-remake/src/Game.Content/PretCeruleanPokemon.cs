// Authenticated Cerulean additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CeruleanPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record CeruleanPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated nine-species Cerulean additions and source evolution references; effects execute independently.</summary>
public sealed class PretCeruleanPokemon
{
    public const string Profile = "firered-cerulean-pokemon-v1";
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
    public JsonElement Scope { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretCeruleanPokemon(string directory, CeruleanPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        Route3PokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs, JsonElement scope)
    {
        Scope = scope; GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
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
                Portrait = new("content/generated/cerulean-pokemon/" + sprite.TexturePath,64,64),Footprint = new("content/generated/cerulean-pokemon/" + foot.TexturePath,16,16)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(e => e.Key, e =>
        {
            var d = e.Value;
            return new CaptureDexEntry(d.SourceSpeciesId, d.NationalNumber, d.Name, d.Category, d.DisplayCategory,
                d.HeightDecimeters, d.WeightHectograms, d.HeightDisplay, d.WeightDisplay, d.Description,
                d.Footprint!.TexturePath, d.Portrait!.TexturePath, d.HabitatTitle, d.Source, d.DescriptionSource);
        });
    }

    public static PretCeruleanPokemon Load(string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/cerulean-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project,"scripts/cerulean-pokemon.lock.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Cerulean Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Cerulean Pokemon output.");
        }
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var content = LoadContent(
            Utf8.GetString(ReadPinned(project,"content/generated/route1-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/old-man-tutorial/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/forest-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/pewter-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/route22-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/route3-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/mtmoon-pokemon/catalog.json")),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 36 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 36,
            "Cerulean Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                PngRgba8.Decode(outputs[sprite.TexturePath], sprite.Width, sprite.Height).Length == 64 * 64 * 4, "Cerulean Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" , "Cerulean sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<Route3PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 63,64,66,96,90,116,118,120,121 }) && supplement.Icons.Length == 9 &&
            supplement.Footprints.Length == 9 && supplement.Cries.Length == 9 && supplement.Weights.Length == 65 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit == 25 && !supplement.Limitations.IsDefaultOrEmpty,
            "Cerulean supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 &&
                PngRgba8.Decode(outputs[icon.TexturePath], icon.Width, icon.Height).Length == 4096, "Cerulean icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                PngRgba8.Decode(outputs[foot.TexturePath], foot.Width, foot.Height).Length == 1024, "Cerulean footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal),
                "Cerulean cry metadata differs or claims playback.");
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "Cerulean catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "Cerulean weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Cerulean weight differs from the authenticated Dex source.");
        }
        using var scope = JsonDocument.Parse(outputs["scope.json"]);
        Require(scope.RootElement.GetProperty("trainers").GetArrayLength() == 7 &&
            !scope.RootElement.GetProperty("effectsEnabledByThisCatalog").GetBoolean(), "Cerulean evidence scope differs.");
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary(), scope.RootElement.Clone());
    }

    public static CeruleanPokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson,
        string pewterGeneratedJson, string route22GeneratedJson, string route3GeneratedJson, string mtmoonGeneratedJson, string ceruleanGeneratedJson)
    {
        var original = PretMtMoonPokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson, route22GeneratedJson, route3GeneratedJson, mtmoonGeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<CeruleanPokemonDocument>(ceruleanGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.NaturalLevelLimit == 25 &&
            doc.EvolutionOverrides.Length == 0 && !doc.ItemEvolutionExecutorImplemented && !doc.LevelEvolutionExecutorImplemented &&
            !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Cerulean Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        var requiredMoves = doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).ToHashSet();
        requiredMoves.UnionWith([new("pret:MOVE_BUBBLE"), new("pret:MOVE_CONFUSION"), new("pret:MOVE_DIG"), new("pret:MOVE_DISABLE"), new("pret:MOVE_EMBER"), new("pret:MOVE_FOCUS_ENERGY"), new("pret:MOVE_GROWL"), new("pret:MOVE_GUST"), new("pret:MOVE_HARDEN"), new("pret:MOVE_HEADBUTT"), new("pret:MOVE_HORN_ATTACK"), new("pret:MOVE_HYPNOSIS"), new("pret:MOVE_ICICLE_SPEAR"), new("pret:MOVE_KARATE_CHOP"), new("pret:MOVE_LEECH_SEED"), new("pret:MOVE_LEER"), new("pret:MOVE_LOW_KICK"), new("pret:MOVE_METAL_CLAW"), new("pret:MOVE_POISON_POWDER"), new("pret:MOVE_QUICK_ATTACK"), new("pret:MOVE_RAPID_SPIN"), new("pret:MOVE_RECOVER"), new("pret:MOVE_SAND_ATTACK"), new("pret:MOVE_SCRATCH"), new("pret:MOVE_SLEEP_POWDER"), new("pret:MOVE_SMOKESCREEN"), new("pret:MOVE_SUPERSONIC"), new("pret:MOVE_SWIFT"), new("pret:MOVE_TACKLE"), new("pret:MOVE_TAIL_WHIP"), new("pret:MOVE_TELEPORT"), new("pret:MOVE_VINE_WHIP"), new("pret:MOVE_WATER_GUN"), new("pret:MOVE_WATER_PULSE"), new("pret:MOVE_WATER_SPORT"), new("pret:MOVE_WITHDRAW")]);
        Require(requiredMoves.SetEquals(doc.Moves.Select(x => x.Id)),
            "Cerulean Pokemon full learnset plus trainer and TM03/TM28 move closure differs.");
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
        Require(constants["EVO_LEVEL"] == 4 && constants["EVO_ITEM"] == 7 && constants["EVO_TRADE"] == 5 && constants["ITEM_WATER_STONE"] == 97 &&
            constants["ABILITY_SYNCHRONIZE"] == 28 && constants["ABILITY_INNER_FOCUS"] == 39 && constants["ABILITY_NATURAL_CURE"] == 30 &&
            constants["ABILITY_ILLUMINATE"] == 35 && constants["ABILITY_SHELL_ARMOR"] == 75, "Cerulean evolution or ability constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty && value.Bytes.Length == value.Name.Length + 1 &&
                value.Bytes[^1] == 255 && value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateCatalog(result);
        Require(result.Species.Count == 65 && result.Moves.Count == 183 && names.Count == 65 && moveNames.Count == 183, "Incomplete composed Cerulean Pokemon catalog.");
        Require(doc.DeferredEvolutions.Length == 7, "Missing deferred Cerulean evolutions.");
        foreach (var deferred in doc.DeferredEvolutions)
        {
            Require(!deferred.Executable && !string.IsNullOrWhiteSpace(deferred.Reason) && (deferred.Species.Value == "pret:SPECIES_STARYU" || !species.ContainsKey(deferred.Target)) &&
                species[deferred.Species].Evolutions.IsEmpty && deferred.Method is "EVO_LEVEL" or "EVO_ITEM" or "EVO_TRADE",
                "Deferred evolution became executable or changed identity.");
        }
        var allDeferred = original.DeferredEvolutions.Concat(doc.DeferredEvolutions).ToImmutableArray();
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
            throw new InvalidDataException("Unsafe Cerulean Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Cerulean Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Cerulean Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}
