// Authenticated RockTunnel additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record RockTunnelPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int? NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record RockTunnelPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated fourteen-species RockTunnel additions and source evolution references; effects execute independently.</summary>
public sealed class PretRockTunnelPokemon
{
    public const string Profile = "firered-rocktunnel-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions { get; }
    public RockTunnelPokemonSupplement Supplement { get; }
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

    private PretRockTunnelPokemon(string directory, RockTunnelPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        RockTunnelPokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs, JsonElement scope)
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
                Portrait = new("content/generated/rocktunnel-pokemon/" + sprite.TexturePath,64,64),Footprint = new("content/generated/rocktunnel-pokemon/" + foot.TexturePath,16,16)
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

    public static PretRockTunnelPokemon Load(string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/rocktunnel-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 93 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "RockTunnel Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate RockTunnel Pokemon output.");
        }
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var inventory = files.ToImmutable();
        var content = LoadContent(
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
            Utf8.GetString(ReadPinned(project,"content/generated/route11-pokemon/catalog.json")),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 56 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 56,
            "RockTunnel Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "RockTunnel Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" , "RockTunnel sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<RockTunnelPokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 104,101,97,108,67,105,57,111,117,119,73,49,48,110 }) && supplement.Icons.Length == 14 &&
            supplement.Footprints.Length == 14 && supplement.Cries.Length == 14 && supplement.Weights.Length == 93 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit is null && !supplement.Limitations.IsDefaultOrEmpty,
            "RockTunnel supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && icon.SourceRefs.Length == 5 &&
                (outputs[icon.TexturePath]).Length > 0, "RockTunnel icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "RockTunnel footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal),
                "RockTunnel cry metadata differs or claims playback.");
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "RockTunnel catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "RockTunnel weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "RockTunnel weight differs from the authenticated Dex source.");
        }
        using var scope = JsonDocument.Parse(outputs["scope.json"]);
        Require(scope.RootElement.GetProperty("trainers").GetArrayLength() == 30 &&
            !scope.RootElement.GetProperty("effectsEnabledByThisCatalog").GetBoolean(), "RockTunnel evidence scope differs.");
        Require(scope.RootElement.GetProperty("naturalLevelLimit").ValueKind == JsonValueKind.Null &&
            scope.RootElement.GetProperty("requiredTrainerMoves").GetArrayLength() == 59 &&
            scope.RootElement.GetProperty("sourceDefinitions").GetArrayLength() == 117 &&
            scope.RootElement.GetProperty("teachingMoves").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { "MOVE_AERIAL_ACE" }),
            "RockTunnel source dependency or uncapped battle scope differs.");
        var closure = scope.RootElement.GetProperty("naturalRouteClosure");
        Require(closure.GetProperty("coverageLevel").GetInt32() == 35 && closure.GetProperty("ownedSpecies").GetArrayLength() == 57 &&
            !closure.GetProperty("evolutionBackfill").GetBoolean() && !closure.GetProperty("moveReminderIncluded").GetBoolean() &&
            scope.RootElement.GetProperty("requiredBattleMoves").GetArrayLength() == 126, "RockTunnel natural source move closure differs.");
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary(), scope.RootElement.Clone());
    }

    public static RockTunnelPokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson,
        string pewterGeneratedJson, string route22GeneratedJson, string route3GeneratedJson, string mtmoonGeneratedJson, string ceruleanGeneratedJson, string billGeneratedJson, string vermilionGeneratedJson, string ssanneGeneratedJson, string route11GeneratedJson, string rocktunnelGeneratedJson)
    {
        var original = PretRoute11Pokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson, route22GeneratedJson, route3GeneratedJson, mtmoonGeneratedJson, ceruleanGeneratedJson, billGeneratedJson, vermilionGeneratedJson, ssanneGeneratedJson, route11GeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<RockTunnelPokemonDocument>(rocktunnelGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Species.Length == 14 && doc.Moves.Length == 117 && doc.SpeciesNames.Length == 14 &&
            doc.MoveNames.Length == 117 && doc.Constants.Length == 21 && doc.EncodedNames.Length == 131 && doc.NaturalLevelLimit is null &&
            doc.EvolutionOverrides.Length == 8 && !doc.ItemEvolutionExecutorImplemented && !doc.LevelEvolutionExecutorImplemented &&
            !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "RockTunnel Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        var requiredMoves = doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).ToHashSet();
        requiredMoves.UnionWith([new("pret:MOVE_AERIAL_ACE"), new("pret:MOVE_BIND"), new("pret:MOVE_BITE"), new("pret:MOVE_BONE_CLUB"), new("pret:MOVE_CONFUSION"), new("pret:MOVE_DEFENSE_CURL"), new("pret:MOVE_DISABLE"), new("pret:MOVE_DOUBLE_SLAP"), new("pret:MOVE_DOUBLE_TEAM"), new("pret:MOVE_EMBER"), new("pret:MOVE_ENCORE"), new("pret:MOVE_FOCUS_ENERGY"), new("pret:MOVE_FOLLOW_ME"), new("pret:MOVE_FORESIGHT"), new("pret:MOVE_FURY_ATTACK"), new("pret:MOVE_GROWL"), new("pret:MOVE_GROWTH"), new("pret:MOVE_GUST"), new("pret:MOVE_HARDEN"), new("pret:MOVE_HEADBUTT"), new("pret:MOVE_HYPER_FANG"), new("pret:MOVE_KARATE_CHOP"), new("pret:MOVE_KNOCK_OFF"), new("pret:MOVE_LEER"), new("pret:MOVE_MAGNITUDE"), new("pret:MOVE_METAL_CLAW"), new("pret:MOVE_MINIMIZE"), new("pret:MOVE_MUD_SPORT"), new("pret:MOVE_ODOR_SLEUTH"), new("pret:MOVE_PAY_DAY"), new("pret:MOVE_POISON_POWDER"), new("pret:MOVE_POISON_STING"), new("pret:MOVE_POUND"), new("pret:MOVE_QUICK_ATTACK"), new("pret:MOVE_RAZOR_LEAF"), new("pret:MOVE_ROAR"), new("pret:MOVE_ROCK_BLAST"), new("pret:MOVE_ROCK_THROW"), new("pret:MOVE_ROLLOUT"), new("pret:MOVE_SAND_ATTACK"), new("pret:MOVE_SCARY_FACE"), new("pret:MOVE_SCRATCH"), new("pret:MOVE_SCREECH"), new("pret:MOVE_SEISMIC_TOSS"), new("pret:MOVE_SING"), new("pret:MOVE_SLAM"), new("pret:MOVE_SLEEP_POWDER"), new("pret:MOVE_SMOKESCREEN"), new("pret:MOVE_STOMP"), new("pret:MOVE_STRING_SHOT"), new("pret:MOVE_STUN_SPORE"), new("pret:MOVE_SUPERSONIC"), new("pret:MOVE_SWEET_SCENT"), new("pret:MOVE_TACKLE"), new("pret:MOVE_TAIL_WHIP"), new("pret:MOVE_THUNDER_WAVE"), new("pret:MOVE_VINE_WHIP"), new("pret:MOVE_WATER_GUN"), new("pret:MOVE_WHIRLWIND"), new("pret:MOVE_WRAP")]);
        Require(requiredMoves.SetEquals(doc.Moves.Select(x => x.Id)),
            "RockTunnel Pokemon full learnsets plus thirty trainer teams and Aerial Ace move closure differs.");
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
        Require(constants["EVO_LEVEL"] == 4 &&
            constants["EVO_TRADE"] == 5 &&
            constants["ITEM_DRAGON_SCALE"] == 201 &&
            constants["EVO_TRADE_ITEM"] == 6 &&
            constants["ABILITY_CLEAR_BODY"] == 29 &&
            constants["ABILITY_COMPOUND_EYES"] == 14 &&
            constants["ABILITY_GUTS"] == 62 &&
            constants["ABILITY_INSOMNIA"] == 15 &&
            constants["ABILITY_LEVITATE"] == 26 &&
            constants["ABILITY_LIGHTNING_ROD"] == 31 &&
            constants["ABILITY_LIQUID_OOZE"] == 64 &&
            constants["ABILITY_OBLIVIOUS"] == 12 &&
            constants["ABILITY_OWN_TEMPO"] == 20 &&
            constants["ABILITY_POISON_POINT"] == 38 &&
            constants["ABILITY_ROCK_HEAD"] == 69 &&
            constants["ABILITY_SHIELD_DUST"] == 19 &&
            constants["ABILITY_SOUNDPROOF"] == 43 &&
            constants["ABILITY_STATIC"] == 9 &&
            constants["ABILITY_SWIFT_SWIM"] == 33 &&
            constants["ABILITY_VITAL_SPIRIT"] == 72 &&
            constants["ABILITY_WATER_VEIL"] == 41, "RockTunnel evolution or ability constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty && value.Bytes.Length == value.Name.Length + 1 &&
                value.Bytes[^1] == 255 && value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        var expectedOverlays = new HashSet<(string Species, int Level, string Target)>
        {
            ("pret:SPECIES_DROWZEE", 26, "pret:SPECIES_HYPNO"),("pret:SPECIES_GOLDEEN", 33, "pret:SPECIES_SEAKING"),("pret:SPECIES_HORSEA", 32, "pret:SPECIES_SEADRA"),("pret:SPECIES_KOFFING", 35, "pret:SPECIES_WEEZING"),("pret:SPECIES_MACHOP", 28, "pret:SPECIES_MACHOKE"),("pret:SPECIES_MANKEY", 28, "pret:SPECIES_PRIMEAPE"),("pret:SPECIES_TENTACOOL", 30, "pret:SPECIES_TENTACRUEL"),("pret:SPECIES_VOLTORB", 30, "pret:SPECIES_ELECTRODE")
        };
        foreach(var overlay in doc.EvolutionOverrides)
        {
            Require(expectedOverlays.Remove((overlay.Species.Value, overlay.Evolution.Level, overlay.Evolution.Target.Value)) &&
                overlay.Evolution.Method == PokemonEvolutionMethod.Level && overlay.Evolution.Parameter is null &&
                species[overlay.Species].Evolutions.IsEmpty && overlay.Evolution.Source.Symbol == overlay.Species.Value[5..],
                "RockTunnel source level evolution overlay differs.");
            species[overlay.Species] = species[overlay.Species] with { Evolutions = [overlay.Evolution] };
        }
        Require(expectedOverlays.Count == 0, "Missing RockTunnel evolution overlays.");
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateCatalog(result);
        Require(result.Species.Count == 93 && result.Moves.Count == 217 && names.Count == 93 && moveNames.Count == 217, "Incomplete composed RockTunnel Pokemon catalog.");
        Require(doc.DeferredEvolutions.Length == 3, "Missing RockTunnel deferred evolution metadata.");
        var expectedDeferred = new HashSet<(string Species, string Method, string Parameter, string Target)>
        {
            ("pret:SPECIES_MACHOKE", "EVO_TRADE", "0", "pret:SPECIES_MACHAMP"),("pret:SPECIES_RHYHORN", "EVO_LEVEL", "42", "pret:SPECIES_RHYDON"),("pret:SPECIES_SEADRA", "EVO_TRADE_ITEM", "ITEM_DRAGON_SCALE", "pret:SPECIES_KINGDRA")
        };
        foreach (var deferred in doc.DeferredEvolutions)
        {
            Require(expectedDeferred.Remove((deferred.Species.Value, deferred.Method, deferred.Parameter, deferred.Target.Value)) &&
                !deferred.Executable && !string.IsNullOrWhiteSpace(deferred.Reason) && !species.ContainsKey(deferred.Target) &&
                species[deferred.Species].Evolutions.IsEmpty, "Deferred source evolution changed or became executable.");
        }
        Require(expectedDeferred.Count == 0, "Missing deferred RockTunnel evolution.");
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
            throw new InvalidDataException("Unsafe RockTunnel Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("RockTunnel Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("RockTunnel Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record RockTunnelPokemonSupplement(int SchemaVersion, string Profile, string SourceCommit,
    PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route3PokemonFootprint> Footprints, ImmutableArray<Route3PokemonCry> Cries,
    ImmutableArray<Route3PokemonWeight> Weights, bool NormalShinyIconsIdentical, int? NaturalLevelLimit, ImmutableArray<string> Limitations);
