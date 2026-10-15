using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record Route22PokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<Route22DeferredEvolution> DeferredEvolutions, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record Route22PokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames, ImmutableArray<Route22DeferredEvolution> DeferredEvolutions);

/// <summary>
/// Authenticated Route22 additions with a closed level-20 evolution target and a deferred Primeape reference. This content increment
/// does not enable encounters, battle effects, evolution execution, or campaign presentation.
/// </summary>
public sealed class PretRoute22Pokemon
{
    public const string Profile = "firered-route22-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<Route22DeferredEvolution> DeferredEvolutions { get; }
    public Route22PokemonSupplement Supplement { get; }
    public ImmutableArray<StorageArtIcon> Icons => Supplement.Icons;
    public ImmutableArray<Route22PokemonFootprint> Footprints => Supplement.Footprints;
    public ImmutableArray<Route22PokemonCry> Cries => Supplement.Cries;
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

    private PretRoute22Pokemon(string directory, Route22PokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art,
        Route22PokemonSupplement supplement, PretPokedexMenu dex, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; EncodedNames = content.EncodedNames; Sprites = sprites; SourceFiles = files; BattleArt = art;
        Supplement = supplement; PokedexMenu = dex; _outputs = outputs; DeferredEvolutions = content.DeferredEvolutions;
        WeightsHectograms = supplement.Weights.ToImmutableDictionary(w => new SpeciesId(w.SpeciesId), w => w.WeightHectograms);
        DexEntries = supplement.DexSpeciesIds.ToImmutableDictionary(sid => sid, sid =>
        {
            var sprite = sprites.Single(s => s.SourceSpeciesId == sid && s.Side == "front" && s.PaletteVariant == "normal");
            var foot = supplement.Footprints.Single(f => f.SourceSpeciesId == sid);
            return dex.Entries[sid] with
            {
                Portrait = new("content/generated/route22-pokemon/" + sprite.TexturePath,64,64),Footprint = new("content/generated/route22-pokemon/" + foot.TexturePath,16,16)
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
    public static PretRoute22Pokemon Load(string projectRoot, bool includeSourceCryWaves = true, PretPokedexMenu? pokedexMenu = null)
    {
        var project = Path.GetFullPath(projectRoot);
        pokedexMenu?.ForProjectRoot(project);
        var directory = Path.Combine(project, "content/generated/route22-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 32 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Route22 Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (!includeSourceCryWaves && path is "cries/spearow.wav" or "cries/fearow.wav" or "cries/mankey.wav") continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Route22 Pokemon output.");
        }
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var inventory = files.ToImmutable();
        var content = LoadContent(
            Utf8.GetString(ReadPinned(project,"content/generated/route1-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/old-man-tutorial/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/forest-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/pewter-pokemon/catalog.json")),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 12 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 12,
            "Route22 Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Route22 Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" , "Route22 sprite provenance differs.");
        }
        var supplement = ContentJson.ParseStrict<Route22PokemonSupplement>(Utf8.GetString(outputs["supplement.json"]), ContentJson.CreateOptions());
        var dex = pokedexMenu ?? PretPokedexMenu.Load(project);
        Require(supplement.SchemaVersion == 1 &&
            supplement.DexMetadataDependency.Path == "content/generated/pokedex-menu/pokedex-menu.json" &&
            supplement.DexSpeciesIds.SequenceEqual(new[] { 21,22,56 }) && supplement.Icons.Length == 3 &&
            supplement.Footprints.Length == 3 && supplement.Cries.Length == 3 && supplement.Weights.Length == 32 &&
            supplement.NormalShinyIconsIdentical && supplement.NaturalLevelLimit == 20 && !supplement.Limitations.IsDefaultOrEmpty,
            "Route22 supplemental data scope differs.");
        foreach (var icon in supplement.Icons)
        {
            Require(supplement.DexSpeciesIds.Contains(icon.SourceSpeciesId) && content.Catalog.Species[new(icon.SpeciesId)].SourceSpeciesId == icon.SourceSpeciesId &&
                icon.Width == 32 && icon.Height == 32 && icon.SourceFrameIndex == 0 && icon.SourceFrameCount == 2 &&
                icon.PaletteIndex is >= 0 and <= 2 && icon.PaletteVariant == "shared-normal-shiny" && icon.TransparentIndex == 0 &&
                icon.PaletteRgb555.Length == 16 && icon.SourceRefs.Length == 5 &&
                (outputs[icon.TexturePath]).Length > 0, "Route22 icon identity/pixels differ.");
        }
        foreach (var foot in supplement.Footprints)
        {
            Require(supplement.DexSpeciesIds.Contains(foot.SourceSpeciesId) && foot.Width == 16 && foot.Height == 16 &&
                (outputs[foot.TexturePath]).Length > 0, "Route22 footprint pixels differ.");
        }
        foreach (var cry in supplement.Cries)
        {
            Require(supplement.DexSpeciesIds.Contains(cry.SourceSpeciesId) && !cry.PlaybackVerified                 && (!includeSourceCryWaves ),
                "Route22 source cry differs or claims unverified playback.");
        }
        Require(supplement.Weights.Select(w => new SpeciesId(w.SpeciesId)).ToHashSet().SetEquals(content.Catalog.Species.Keys),
            "Route22 weights do not cover the full authenticated catalog.");
        foreach (var weight in supplement.Weights)
        {
            Require(content.Catalog.Species[new(weight.SpeciesId)].SourceSpeciesId == weight.SourceSpeciesId &&
                dex.Entries[weight.SourceSpeciesId].WeightHectograms == weight.WeightHectograms && weight.WeightHectograms > 0,
                "Route22 weight differs from the authenticated Dex source.");
        }
        return new(directory, content, sprites, inventory, art.RootElement.Clone(), supplement, dex, outputs.ToImmutableDictionary());
    }

    /// <summary>Pure authenticated composition; Load additionally validates all on-disk source and art files.</summary>
    public static Route22PokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson, string pewterGeneratedJson, string route22GeneratedJson)
    {
        var original = PretPewterPokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson, pewterGeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<Route22PokemonDocument>(route22GeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Species.Length == 3 && doc.Moves.Length == 19 && doc.SpeciesNames.Length == 3 &&
            doc.MoveNames.Length == 19 && doc.Constants.Length == 3 && doc.EncodedNames.Length == 22 && !doc.ItemEvolutionExecutorImplemented &&
            !doc.LevelEvolutionExecutorImplemented && !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Route22 Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        Require(doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move)
                .ToHashSet().SetEquals(doc.Moves.Select(x => x.Id)),
            "Route22 Pokemon full learnset move closure differs.");
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            if (!moves.ContainsKey(value.Id)) moves.Add(value.Id, value);
        }
        var names = original.SpeciesNames.ToBuilder();
        foreach (var value in doc.SpeciesNames)
        {
names.Add(species[value.Id].SourceSpeciesId, value.Name);
        }
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
        Require(constants["EVO_LEVEL"] == 4 && constants["EVO_TRADE"] == 5 && constants["EVO_TRADE_ITEM"] == 6 &&
            constants["ITEM_METAL_COAT"] == 199 && constants["ABILITY_VITAL_SPIRIT"] == 72 && constants["ABILITY_KEEN_EYE"] == 51 && constants.Count == 8, "Evolution source constants differ.");
        var encoded = doc.EncodedNames.ToImmutableDictionary(e => e.Symbol, StringComparer.Ordinal);
        foreach (var value in encoded.Values)
        {
            bool isSpecies = value.Symbol.StartsWith("SPECIES_", StringComparison.Ordinal);
            Require(value.Source.Symbol == value.Symbol && !value.Bytes.IsDefaultOrEmpty &&
                value.Bytes.Length == value.Name.Length + 1 && value.Bytes[^1] == 255 &&
                value.Bytes.Take(value.Bytes.Length - 1).All(b => b is >= 0 and < 255), "Canonical name encoding differs.");
            Require(isSpecies ? doc.SpeciesNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name) :
                doc.MoveNames.Any(n => n.Id.Value == "pret:" + value.Symbol && n.Name == value.Name), "Encoded name is absent from canonical names.");
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateCatalog(result);
        Require(result.Species.Count == 32 && result.Moves.Count == 94 && names.Count == 32 && moveNames.Count == result.Moves.Count, "Incomplete composed Route22 Pokemon catalog.");
        Require(doc.DeferredEvolutions.Length == 1, "Missing deferred Mankey evolution reference.");
        var deferred = doc.DeferredEvolutions[0];
        Require(deferred.Species.Value == "pret:SPECIES_MANKEY" && deferred.Evolution.Target.Value == "pret:SPECIES_PRIMEAPE" &&
            deferred.Evolution.Level == 28 && deferred.Evolution.Method == PokemonEvolutionMethod.Level && !deferred.Executable &&
            !string.IsNullOrWhiteSpace(deferred.Reason) && species[deferred.Species].Evolutions.IsEmpty && !species.ContainsKey(deferred.Evolution.Target),
            "Deferred Primeape reference became executable or changed identity.");
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants.ToImmutable(), encoded, doc.DeferredEvolutions);
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

public sealed record Route22DeferredEvolution(SpeciesId Species, PokemonEvolution Evolution, bool Executable, string Reason);
public sealed record Route22PokemonWeight(string SpeciesId, int SourceSpeciesId, int WeightHectograms, StorageArtSourceRef Source);
public sealed record Route22PokemonFootprint(int SourceSpeciesId, string TexturePath, int Width, int Height,
    StorageArtSourceRef Source);
public sealed record Route22PokemonCry(int SourceSpeciesId, string WavePath, StorageArtSourceRef Source, bool PlaybackVerified);
public sealed record Route22PokemonSupplement(int SchemaVersion, string Profile, string SourceCommit,
    PokedexMenuDependency DexMetadataDependency, ImmutableArray<int> DexSpeciesIds, ImmutableArray<StorageArtIcon> Icons,
    ImmutableArray<Route22PokemonFootprint> Footprints, ImmutableArray<Route22PokemonCry> Cries,
    ImmutableArray<Route22PokemonWeight> Weights, bool NormalShinyIconsIdentical, int NaturalLevelLimit, ImmutableArray<string> Limitations);
