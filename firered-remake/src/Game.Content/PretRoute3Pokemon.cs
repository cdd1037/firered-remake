using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record Route3PokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<Route3DeferredEvolution> DeferredEvolutions, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record Route3PokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames, ImmutableArray<Route3DeferredEvolution> DeferredEvolutions);

/// <summary>
/// Authenticated eight-species Route3 additions with closed level-16/20 natural targets and deferred item/high-level targets. This content increment
/// does not enable encounters, battle effects, evolution execution, or campaign presentation.
/// </summary>
public sealed class PretRoute3Pokemon
{
    public const string Profile = "firered-route3-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "35a3a6947c8554a2450c057722137bf350fcf75c6e3cf8e84d8868898e0e9a74";
    public const string ManifestSha256 = "17ce7ede18cf33bcd69c9d8477000a030bb973f94a963aa3ac0221e1be52e811";
    public const string CatalogSha256 = "fc4a834b216acbc4490a8b38302031dc100b7b8fdfc94b48d6d968e730880efe";
    public const string RawManifestSha256 = "20765a8f0d02431ba6d61c1ce1759750d07e00bce601a29304e1f264fe05fad7";
    public const string BattleArtSha256 = "b875163d87bc5f08450d70d0256a36e4a77de06aba87961313d8976955574f07";
    public const string FullSourceHash = "956ab3a683935b1faf2111dab402695716172d16e057c28078e961ed6a586eb2";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<Route3DeferredEvolution> DeferredEvolutions { get; }
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

    private PretRoute3Pokemon(string directory, Route3PokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        Route3PokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs, JsonElement scope)
    {
        Scope = scope; GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; EncodedNames = content.EncodedNames; Sprites = sprites; SourceFiles = files; BattleArt = art;
        Supplement = supplement; PokedexMenu = dex; _outputs = outputs; DeferredEvolutions = content.DeferredEvolutions;
        WeightsHectograms = supplement.Weights.ToImmutableDictionary(w => new SpeciesId(w.SpeciesId), w => w.WeightHectograms);
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(sid => sid, sid =>
        {
            var sprite = sprites.Single(s => s.SourceSpeciesId == sid && s.Side == "front" && s.PaletteVariant == "normal");
            var foot = supplement.Footprints.Single(f => f.SourceSpeciesId == sid);
            return dex.Entries[sid] with
            {
                Portrait = new("content/generated/route3-pokemon/" + sprite.TexturePath, sprite.Sha256, sprite.RgbaSha256, 64, 64),
                Footprint = new("content/generated/route3-pokemon/" + foot.TexturePath, foot.Sha256, foot.RgbaSha256, 16, 16)
            };
        });
        CaptureDexEntries = DexEntries.ToImmutableDictionary(e => e.Key, e =>
        {
            var d = e.Value;
            return new CaptureDexEntry(d.SourceSpeciesId, d.NationalNumber, d.Name, d.Category, d.DisplayCategory,
                d.HeightDecimeters, d.WeightHectograms, d.HeightDisplay, d.WeightDisplay, d.Description,
                d.Footprint!.TexturePath, d.Portrait!.TexturePath, d.HabitatTitle, d.Source, d.DescriptionSource);
        });
        AdditionalSourceHashes = ["route3-pokemon-manifest:" + ManifestSha256, "route3-pokemon-lock:" + LockSha256,
            .. files.Select(f => "route3-pokemon:" + f.Path + ":" + f.Sha256),
            "route3-pokemon-dex:" + PretPokedexMenu.GeneratedSha256];
    }

    /// <summary>Authenticates all content bytes; cries are source ID/name metadata only and require no audio file.</summary>
    public static PretRoute3Pokemon Load(string projectRoot, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/route3-pokemon");
        var rawDirectory = Path.Combine(project, "content/raw/route3-pokemon");
        byte[] ReadPinned(string root, string path, string hash, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Pinned Route3 Pokemon bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/route3-pokemon.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("fullSourceHash").GetString() == FullSourceHash && m.GetProperty("inputCount").GetInt32() == 96 &&
            m.GetProperty("outputs").GetArrayLength() == 100 && m.GetProperty("composedSpeciesCount").GetInt32() == 40 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Route3 Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var outputIdentities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputIdentities.TryAdd(path, output.GetProperty("sha256").GetString()!), "Duplicate Route3 Pokemon output identity.");
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Route3 Pokemon output.");
        }
        Require(Hash(outputs["catalog.json"]) == CatalogSha256 && Hash(outputs["battle-art.json"]) == BattleArtSha256,
            "Route3 Pokemon generated documents differ.");
        using var catalog = JsonDocument.Parse(outputs["catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputs"), sourceLock.RootElement.GetProperty("files")),
            "Route3 Pokemon source inventory differs from fixed lock.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Route3 Pokemon Git blob differs: " + path);
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        Require(inventory.Length == 96 && inventory.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 96,
            "Incomplete Route3 Pokemon source set.");
        var material = string.Concat(inventory.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Utf8.GetBytes(material)) == FullSourceHash, "Route3 Pokemon source-set hash differs.");
        var content = LoadContent(
            Utf8.GetString(ReadPinned(project, "content/generated/route1-pokemon/catalog.json", PretRoute1PokemonCatalog.GeneratedSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/old-man-tutorial/catalog.json", PretOldManTutorial.GeneratedSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/forest-pokemon/catalog.json", PretForestPokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/pewter-pokemon/catalog.json", PretPewterPokemon.CatalogSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/route22-pokemon/catalog.json", PretRoute22Pokemon.CatalogSha256)),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 32 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 32,
            "Route3 Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                Hash(outputs[sprite.TexturePath]) == sprite.Sha256 && Hash(outputs[sprite.RgbaPath]) == sprite.RgbaSha256 &&
                outputs[sprite.RgbaPath].Length == 64 * 64 * 4, "Route3 Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" && source.Repository == "pret/pokefirered" && source.Commit == SourceCommit &&
                    inventory.Any(f => f.Path == source.Path && f.Sha256 == source.Sha256), "Route3 sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<Route3PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 && supplement.Profile == Profile && supplement.SourceCommit == SourceCommit &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexMetadataDependency.Sha256 == PretPokedexMenu.GeneratedSha256 &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 23, 29, 30, 32, 33, 39, 129, 130 }) && supplement.Icons.Length == 8 &&
            supplement.Footprints.Length == 8 && supplement.Cries.Length == 8 && supplement.Weights.Length == 40 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit == 20 && !supplement.Limitations.IsDefaultOrEmpty,
            "Route3 supplemental data scope differs.");
        _ = ReadPinned(project, supplement.DexMetadataDependency.Path, supplement.DexMetadataDependency.Sha256,
            supplement.DexMetadataDependency.ByteLength);
        void Reference(StorageArtSourceRef reference)
            => Require(inventory.Any(f => f.Path == reference.Path && f.Sha256 == reference.Sha256), "Supplement source reference differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && icon.SourceRefs.Length == 5 &&
                Hash(outputs[icon.TexturePath]) == icon.Sha256 && Hash(outputs[icon.RgbaPath]) == icon.RgbaSha256 &&
                outputs[icon.RgbaPath].Length == 4096, "Route3 icon identity/pixels differ.");
            foreach (var reference in icon.SourceRefs) Reference(reference);
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                Hash(outputs[foot.TexturePath]) == foot.Sha256 && Hash(outputs[foot.RgbaPath]) == foot.RgbaSha256 &&
                outputs[foot.RgbaPath].Length == 1024, "Route3 footprint pixels differ.");
            Reference(foot.Source);
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified &&
                !cry.AudioBytesImported && cry.CryId == cry.SourceSpeciesId && cry.TableIndex == cry.SourceSpeciesId - 1 &&
                cry.CrySymbol.StartsWith("Cry_", StringComparison.Ordinal), "Route3 cry metadata differs or claims playback.");
            Reference(cry.Source); Reference(cry.MappingSource); Reference(cry.CallerSource);
        }
        Require(outputs.Keys.All(p => !p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
            !p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".qoa", StringComparison.OrdinalIgnoreCase)),
            "Route3 catalog must not import recorded or converted audio.");
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "Route3 weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Route3 weight differs from the authenticated Dex source.");
            Reference(weight.Source);
        }
        using var scope = JsonDocument.Parse(outputs["scope.json"]);
        Require(scope.RootElement.GetProperty("profile").GetString() == Profile &&
            scope.RootElement.GetProperty("roadTrainers").GetArrayLength() == 9 &&
            !scope.RootElement.GetProperty("effectsEnabledByThisCatalog").GetBoolean(), "Route3 evidence scope differs.");
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary(), scope.RootElement.Clone());
    }

    /// <summary>Pure authenticated composition; Load additionally validates all on-disk source and art files.</summary>
    public static Route3PokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson, string pewterGeneratedJson, string route22GeneratedJson, string route3GeneratedJson)
    {
        Require(Hash(Utf8.GetBytes(route3GeneratedJson)) == CatalogSha256, "Route3 Pokemon catalog differs from reviewed conversion.");
        var original = PretRoute22Pokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson, route22GeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<Route3PokemonDocument>(route3GeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == 96 && doc.Species.Length == 8 && doc.Moves.Length == 45 && doc.SpeciesNames.Length == 8 &&
            doc.MoveNames.Length == 45 && doc.Constants.Length == 8 && doc.EncodedNames.Length == 53 && !doc.ItemEvolutionExecutorImplemented &&
            !doc.LevelEvolutionExecutorImplemented && !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Route3 Pokemon scope differs.");
        var inputs = doc.Inputs.ToDictionary(x => x.Path, StringComparer.Ordinal);
        void Source(PokemonSourceRef source, string path)
        {
            Require(source.Path == path && inputs.TryGetValue(path, out var input) && input.Sha256 == source.Sha256 &&
                source.StartLine > 0 && source.EndLine >= source.StartLine && !string.IsNullOrWhiteSpace(source.Symbol),
                "Route3 Pokemon source reference differs.");
        }
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            Source(value.Source, "src/data/pokemon/species_info.h"); Source(value.LearnsetSource, "src/data/pokemon/level_up_learnsets.h");
            foreach (var move in value.LevelUpMoves) Source(move.Source, value.LearnsetSource.Path);
            foreach (var evolution in value.Evolutions) Source(evolution.Source, "src/data/pokemon/evolution.h");
            species.Add(value.Id, value);
        }
        Require(doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move)
                .ToHashSet().SetEquals(doc.Moves.Select(x => x.Id)),
            "Route3 Pokemon full learnset move closure differs.");
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            Source(value.Source, "src/data/battle_moves.h");
            if (moves.TryGetValue(value.Id, out var existing))
                Require(JsonSerializer.Serialize(existing) == JsonSerializer.Serialize(value), "Existing move definition changed.");
            else moves.Add(value.Id, value);
        }
        var names = original.SpeciesNames.ToBuilder();
        foreach (var value in doc.SpeciesNames)
        {
            Source(value.Source, "src/data/text/species_names.h"); names.Add(species[value.Id].SourceSpeciesId, value.Name);
        }
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
        Require(constants["EVO_LEVEL"] == 4 && constants["EVO_TRADE"] == 5 && constants["EVO_TRADE_ITEM"] == 6 &&
            constants["ITEM_METAL_COAT"] == 199 && constants["ABILITY_VITAL_SPIRIT"] == 72 && constants["ABILITY_KEEN_EYE"] == 51 && constants["ITEM_MOON_STONE"] == 94 && constants["ABILITY_INTIMIDATE"] == 22 &&
            constants["ABILITY_SWIFT_SWIM"] == 33 && constants["ABILITY_POISON_POINT"] == 38 &&
            constants["ABILITY_CUTE_CHARM"] == 56 && constants["ABILITY_SHED_SKIN"] == 61 && constants.Count == 14, "Evolution source constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Source(value.Source, isSpecies ? "src/data/text/species_names.h" : "src/data/text/move_names.h");
            Source(value.EncodingSource, "charmap.txt");
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty &&
                value.Bytes.Length == value.Name.Length + 1 && value.Bytes[^1] == 255 &&
                value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateCatalog(result);
        Require(result.Species.Count == 40 && result.Moves.Count == 119 && names.Count == 40 && moveNames.Count == result.Moves.Count, "Incomplete composed Route3 Pokemon catalog.");
        Require(doc.DeferredEvolutions.Length == 4, "Missing deferred Route3 evolutions.");
        var expectedDeferred = new[]
        {
            ("EKANS", "ARBOK", 22, PokemonEvolutionMethod.Level, (string?)null),
            ("NIDORINA", "NIDOQUEEN", 0, PokemonEvolutionMethod.Item, "ITEM_MOON_STONE"),
            ("NIDORINO", "NIDOKING", 0, PokemonEvolutionMethod.Item, "ITEM_MOON_STONE"),
            ("JIGGLYPUFF", "WIGGLYTUFF", 0, PokemonEvolutionMethod.Item, "ITEM_MOON_STONE")
        };
        foreach (var (name, target, level, method, parameter) in expectedDeferred)
        {
            var deferred = doc.DeferredEvolutions.Single(d => d.Species.Value == "pret:SPECIES_" + name);
            Source(deferred.Evolution.Source, "src/data/pokemon/evolution.h");
            Require(deferred.Evolution.Target.Value == "pret:SPECIES_" + target && deferred.Evolution.Level == level &&
                deferred.Evolution.Method == method && deferred.Evolution.Parameter == parameter && !deferred.Executable &&
                !string.IsNullOrWhiteSpace(deferred.Reason) && species[deferred.Species].Evolutions.IsEmpty &&
                !species.ContainsKey(deferred.Evolution.Target), "Deferred evolution became executable or changed identity.");
        }
        var allDeferred = original.DeferredEvolutions.Select(d => new Route3DeferredEvolution(d.Species, d.Evolution, d.Executable, d.Reason))
            .Concat(doc.DeferredEvolutions).ToImmutableArray();
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants.ToImmutable(), encoded, allDeferred);
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));

    public StorageArtIcon Icon(SpeciesId species, bool shiny = false) => Icons.Single(i => i.SpeciesId == species.Value);
    public byte[] ReadIconTexture(SpeciesId species, bool shiny = false) => [.. _outputs[Icon(species, shiny).TexturePath]];
    public byte[] ReadFootprintTexture(int sourceSpeciesId) => [.. _outputs[Footprints.Single(f => f.SourceSpeciesId == sourceSpeciesId).TexturePath]];
    public byte[] ReadSpriteTexture(SpeciesId species, string side, bool shiny = false) => [.. _outputs[Sprite(species, side, shiny).TexturePath]];

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Route3 Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Route3 Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route3 Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}

public sealed record Route3DeferredEvolution(SpeciesId Species, PokemonEvolution Evolution, bool Executable, string Reason);
public sealed record Route3PokemonWeight(string SpeciesId, int SourceSpeciesId, int WeightHectograms, StorageArtSourceRef Source);
public sealed record Route3PokemonFootprint(int SourceSpeciesId, string TexturePath, string RgbaPath, string Sha256,
    string RgbaSha256, int Width, int Height, StorageArtSourceRef Source);
public sealed record Route3PokemonCry(int SourceSpeciesId, int CryId, int TableIndex, string CrySymbol,
    StorageArtSourceRef Source, StorageArtSourceRef MappingSource, StorageArtSourceRef CallerSource,
    bool PlaybackVerified, bool AudioBytesImported);
public sealed record Route3PokemonSupplement(int SchemaVersion, string Profile, string SourceCommit,
    PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route3PokemonFootprint> Footprints, ImmutableArray<Route3PokemonCry> Cries,
    ImmutableArray<Route3PokemonWeight> Weights, bool NormalShinyIconsIdentical, int NaturalLevelLimit, ImmutableArray<string> Limitations);
