// Authenticated Vermilion additions; all historical catalogs remain unchanged.
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record VermilionPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MtMoonEvolutionOverride> EvolutionOverrides, int NaturalLevelLimit, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record VermilionPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions);

/// <summary>Authenticated two-species Vermilion additions and source evolution references; effects execute independently.</summary>
public sealed class PretVermilionPokemon
{
    public const string Profile = "firered-vermilion-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "e5def137967d7df9dd3ec5d78ce5b65b0cfa65edf9dc77ab08aaa69f1b6ab9ca";
    public const string ManifestSha256 = "2a6cb76c7027da306f4456b22966f6627af30ab8a3edc3850bed4425de32d9eb";
    public const string CatalogSha256 = "2d60aa60296d468738cc4fa2f20541d9ad77ed7d78915e8bef8fa2679c0b3008";
    public const string RawManifestSha256 = "4787978fddb978fb74b8a0458d5ab80b3fd853d29a68bab91b2cb8ca579a8ac9";
    public const string BattleArtSha256 = "6ec39966caf734c203b2a93da9b71cc5776bde4170dae3ce747cf6da6d1f3308";
    public const string FullSourceHash = "76e2f5e94f4ae5464587f99bc2b231002c50f7a9af5401fe8993ef947ef71dd9";
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
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement BattleArt { get; }
    public JsonElement Scope { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretVermilionPokemon(string directory, VermilionPokemonCatalogContent content,
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
                Portrait = new("content/generated/vermilion-pokemon/" + sprite.TexturePath, sprite.Sha256, sprite.RgbaSha256, 64, 64),
                Footprint = new("content/generated/vermilion-pokemon/" + foot.TexturePath, foot.Sha256, foot.RgbaSha256, 16, 16)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(e => e.Key, e =>
        {
            var d = e.Value;
            return new CaptureDexEntry(d.SourceSpeciesId, d.NationalNumber, d.Name, d.Category, d.DisplayCategory,
                d.HeightDecimeters, d.WeightHectograms, d.HeightDisplay, d.WeightDisplay, d.Description,
                d.Footprint!.TexturePath, d.Portrait!.TexturePath, d.HabitatTitle, d.Source, d.DescriptionSource);
        });
        AdditionalSourceHashes = ["vermilion-pokemon-manifest:" + ManifestSha256, "vermilion-pokemon-lock:" + LockSha256,
            .. files.Select(f => "vermilion-pokemon:" + f.Path + ":" + f.Sha256), "vermilion-pokemon-dex:" + PretPokedexMenu.GeneratedSha256];
    }

    public static PretVermilionPokemon Load(string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/vermilion-pokemon");
        var rawDirectory = Path.Combine(project, "content/raw/vermilion-pokemon");
        byte[] ReadPinned(string root, string path, string hash, int? length = null)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(SafeFile(root, path), length ?? 2 * 1024 * 1024);
            Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Pinned Vermilion Pokemon bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/vermilion-pokemon.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("fullSourceHash").GetString() == FullSourceHash && m.GetProperty("inputCount").GetInt32() == 64 &&
            m.GetProperty("outputs").GetArrayLength() == 28 && m.GetProperty("composedSpeciesCount").GetInt32() == 70 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Vermilion Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var outputIdentities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputIdentities.TryAdd(path, output.GetProperty("sha256").GetString()!), "Duplicate Vermilion Pokemon output identity.");
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Vermilion Pokemon output.");
        }
        Require(Hash(outputs["catalog.json"]) == CatalogSha256 && Hash(outputs["battle-art.json"]) == BattleArtSha256,
            "Vermilion Pokemon generated documents differ.");
        using var catalog = JsonDocument.Parse(outputs["catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputs"), sourceLock.RootElement.GetProperty("files")),
            "Vermilion Pokemon source inventory differs from fixed lock.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Vermilion Pokemon Git blob differs: " + path);
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        Require(inventory.Length == 64 && inventory.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 64,
            "Incomplete Vermilion Pokemon source set.");
        var material = string.Concat(inventory.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Utf8.GetBytes(material)) == FullSourceHash, "Vermilion Pokemon source-set hash differs.");
        var content = LoadContent(
            Utf8.GetString(ReadPinned(project, "content/generated/route1-pokemon/catalog.json", PretRoute1PokemonCatalog.GeneratedSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/old-man-tutorial/catalog.json", PretOldManTutorial.GeneratedSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/forest-pokemon/catalog.json", PretForestPokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/pewter-pokemon/catalog.json", PretPewterPokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/route22-pokemon/catalog.json", PretRoute22Pokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/route3-pokemon/catalog.json", PretRoute3Pokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/mtmoon-pokemon/catalog.json", PretMtMoonPokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/cerulean-pokemon/catalog.json", PretCeruleanPokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/bill-pokemon/catalog.json", PretBillPokemon.CatalogSha256)),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 8 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 8,
            "Vermilion Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                Hash(outputs[sprite.TexturePath]) == sprite.Sha256 && Hash(outputs[sprite.RgbaPath]) == sprite.RgbaSha256 &&
                outputs[sprite.RgbaPath].Length == 64 * 64 * 4, "Vermilion Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" && source.Repository == "pret/pokefirered" && source.Commit == SourceCommit &&
                    inventory.Any(f => f.Path == source.Path && f.Sha256 == source.Sha256), "Vermilion sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<Route3PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 && supplement.Profile == Profile && supplement.SourceCommit == SourceCommit &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexMetadataDependency.Sha256 == PretPokedexMenu.GeneratedSha256 &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 52, 53 }) && supplement.Icons.Length == 2 &&
            supplement.Footprints.Length == 2 && supplement.Cries.Length == 2 && supplement.Weights.Length == 70 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit == 28 && !supplement.Limitations.IsDefaultOrEmpty,
            "Vermilion supplemental data scope differs.");
        _ = ReadPinned(project, supplement.DexMetadataDependency.Path, supplement.DexMetadataDependency.Sha256, supplement.DexMetadataDependency.ByteLength);
        void Reference(StorageArtSourceRef reference)
            => Require(inventory.Any(f => f.Path == reference.Path && f.Sha256 == reference.Sha256), "Supplement source reference differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && icon.SourceRefs.Length == 5 &&
                Hash(outputs[icon.TexturePath]) == icon.Sha256 && Hash(outputs[icon.RgbaPath]) == icon.RgbaSha256 &&
                outputs[icon.RgbaPath].Length == 4096, "Vermilion icon identity/pixels differ.");
            foreach (var reference in icon.SourceRefs) Reference(reference);
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                Hash(outputs[foot.TexturePath]) == foot.Sha256 && Hash(outputs[foot.RgbaPath]) == foot.RgbaSha256 &&
                outputs[foot.RgbaPath].Length == 1024, "Vermilion footprint pixels differ.");
            Reference(foot.Source);
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified && !cry.AudioBytesImported &&
                cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 && cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal),
                "Vermilion cry metadata differs or claims playback.");
            Reference(cry.Source); Reference(cry.MappingSource); Reference(cry.CallerSource);
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "Vermilion catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "Vermilion weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Vermilion weight differs from the authenticated Dex source.");
            Reference(weight.Source);
        }
        using var scope = JsonDocument.Parse(outputs["scope.json"]);
        Require(scope.RootElement.GetProperty("profile").GetString() == Profile && scope.RootElement.GetProperty("trainers").GetArrayLength() == 6 &&
            !scope.RootElement.GetProperty("effectsEnabledByThisCatalog").GetBoolean(), "Vermilion evidence scope differs.");
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary(), scope.RootElement.Clone());
    }

    public static VermilionPokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson,
        string pewterGeneratedJson, string route22GeneratedJson, string route3GeneratedJson, string mtmoonGeneratedJson, string ceruleanGeneratedJson, string billGeneratedJson, string vermilionGeneratedJson)
    {
        Require(Hash(Utf8.GetBytes(vermilionGeneratedJson)) == CatalogSha256, "Vermilion Pokemon catalog differs from reviewed conversion.");
        var original = PretBillPokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson, route22GeneratedJson, route3GeneratedJson, mtmoonGeneratedJson, ceruleanGeneratedJson, billGeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<VermilionPokemonDocument>(vermilionGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == 64 && doc.Species.Length == 2 && doc.Moves.Length == 30 && doc.SpeciesNames.Length == 2 &&
            doc.MoveNames.Length == 30 && doc.Constants.Length == 8 && doc.EncodedNames.Length == 32 && doc.NaturalLevelLimit == 28 &&
            doc.EvolutionOverrides.Length == 0 && !doc.ItemEvolutionExecutorImplemented && !doc.LevelEvolutionExecutorImplemented &&
            !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Vermilion Pokemon scope differs.");
        var inputs = doc.Inputs.ToDictionary(x => x.Path, StringComparer.Ordinal);
        void Source(PokemonSourceRef source, string path)
        {
            Require(source.Path == path && inputs.TryGetValue(path, out var input) && input.Sha256 == source.Sha256 &&
                source.StartLine > 0 && source.EndLine >= source.StartLine && !string.IsNullOrWhiteSpace(source.Symbol),
                "Vermilion Pokemon source reference differs.");
        }
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            Source(value.Source, "src/data/pokemon/species_info.h"); Source(value.LearnsetSource, "src/data/pokemon/level_up_learnsets.h");
            foreach (var move in value.LevelUpMoves) Source(move.Source, value.LearnsetSource.Path);
            foreach (var evolution in value.Evolutions) Source(evolution.Source, "src/data/pokemon/evolution.h");
            species.Add(value.Id, value);
        }
        var requiredMoves = doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).ToHashSet();
        requiredMoves.UnionWith([new("pret:MOVE_BITE"), new("pret:MOVE_BUBBLE"), new("pret:MOVE_DOUBLE_TEAM"), new("pret:MOVE_FURY_ATTACK"), new("pret:MOVE_GROWL"), new("pret:MOVE_GUST"), new("pret:MOVE_HYPER_FANG"), new("pret:MOVE_LEER"), new("pret:MOVE_PECK"), new("pret:MOVE_POISON_POWDER"), new("pret:MOVE_POISON_STING"), new("pret:MOVE_QUICK_ATTACK"), new("pret:MOVE_SAND_ATTACK"), new("pret:MOVE_SLEEP_POWDER"), new("pret:MOVE_STRING_SHOT"), new("pret:MOVE_STUN_SPORE"), new("pret:MOVE_SUPERSONIC"), new("pret:MOVE_TACKLE"), new("pret:MOVE_TAIL_WHIP"), new("pret:MOVE_THUNDER_WAVE"), new("pret:MOVE_WATER_GUN"), new("pret:MOVE_WITHDRAW")]);
        Require(requiredMoves.SetEquals(doc.Moves.Select(x => x.Id)),
            "Vermilion Pokemon full learnset plus six trainer teams move closure differs.");
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            Source(value.Source, "src/data/battle_moves.h");
            if (moves.TryGetValue(value.Id, out var existing)) Require(JsonSerializer.Serialize(existing) == JsonSerializer.Serialize(value), "Existing move definition changed.");
            else moves.Add(value.Id, value);
        }
        var names = original.SpeciesNames.ToBuilder();
        foreach (var value in doc.SpeciesNames) { Source(value.Source, "src/data/text/species_names.h"); names.Add(species[value.Id].SourceSpeciesId, value.Name); }
        var moveNames = original.MoveNames.ToBuilder();
        foreach (var value in doc.MoveNames)
        {
            Source(value.Source, "src/data/text/move_names.h");
            Require(!moveNames.TryGetValue(value.Id, out var existing) || existing == value.Name, "Existing move name changed.");
            moveNames[value.Id] = value.Name;
        }
        var constants = original.Constants.ToBuilder();
        foreach (var value in doc.Constants)
        {
            Source(value.Source, value.Symbol.StartsWith("ABILITY_", StringComparison.Ordinal) ? "include/constants/abilities.h" :
                value.Symbol.StartsWith("ITEM_", StringComparison.Ordinal) ? "include/constants/items.h" : "include/constants/pokemon.h");
            Require(!constants.TryGetValue(value.Symbol, out int previous) || previous == value.Value, "Existing evolution constant changed.");
            constants[value.Symbol] = value.Value;
        }
        Require(constants["EVO_LEVEL"] == 4 && constants["ABILITY_PICKUP"] == 53 && constants["ABILITY_LIMBER"] == 7,
            "Vermilion evolution or ability constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Source(value.Source, isSpecies ? "src/data/text/species_names.h" : "src/data/text/move_names.h"); Source(value.EncodingSource, "charmap.txt");
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty && value.Bytes.Length == value.Name.Length + 1 &&
                value.Bytes[^1] == 255 && value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateCatalog(result);
        Require(result.Species.Count == 70 && result.Moves.Count == 191 && names.Count == 70 && moveNames.Count == 191, "Incomplete composed Vermilion Pokemon catalog.");
        Require(doc.DeferredEvolutions.IsEmpty, "Unexpected deferred Vermilion evolution.");
        var meowthEvolution = result.Species[new("pret:SPECIES_MEOWTH")].Evolutions.Single();
        Require(meowthEvolution.Method == PokemonEvolutionMethod.Level && meowthEvolution.Level == 28 &&
            meowthEvolution.Target.Value == "pret:SPECIES_PERSIAN" && meowthEvolution.Parameter is null &&
            result.Species[new("pret:SPECIES_PERSIAN")].Evolutions.IsEmpty,
            "Meowth source level evolution differs.");
        var allDeferred = original.DeferredEvolutions.Where(d => !doc.EvolutionOverrides.Any(o => o.Species == d.Species)).Concat(doc.DeferredEvolutions).ToImmutableArray();
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants.ToImmutable(), encoded, allDeferred);
    }
    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) => Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(i => i.SpeciesId == species.Value);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _outputs[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _outputs[Footprints.Single(f => f.SourceSpeciesId == sourceSpeciesId).TexturePath]];
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _outputs[Sprite(species, side, shiny).TexturePath]];
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Vermilion Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Vermilion Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Vermilion Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}
