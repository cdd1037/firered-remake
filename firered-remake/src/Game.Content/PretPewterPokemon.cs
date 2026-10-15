using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PewterPokemonEncodedName(string Symbol, string Name, ImmutableArray<int> Bytes,
    PokemonSourceRef Source, PokemonSourceRef EncodingSource);
public sealed record PewterPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, bool ItemEvolutionExecutorImplemented,
    bool LevelEvolutionExecutorImplemented, bool TradeEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record PewterPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants, ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames);

/// <summary>
/// Authenticated Pewter Gym families with complete evolution target data plus offline normal/shiny sprites. This content increment
/// does not enable encounters, battle effects, evolution execution, or campaign presentation.
/// </summary>
public sealed class PretPewterPokemon
{
    public const string Profile = "firered-pewter-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretPewterPokemon(string directory, PewterPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art)
    {
        GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; EncodedNames = content.EncodedNames; Sprites = sprites; SourceFiles = files; BattleArt = art;
    }

    /// <summary>Authenticates all raw, rendered and legacy catalog bytes before exposing content.</summary>
    public static PretPewterPokemon Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/pewter-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 29 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Pewter Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Pewter Pokemon output.");
        }
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var inventory = files.ToImmutable();
        var content = LoadContent(
            Utf8.GetString(ReadPinned(project,"content/generated/route1-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/old-man-tutorial/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/forest-pokemon/catalog.json")),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 28 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 28,
            "Pewter Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Pewter Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" , "Pewter sprite provenance differs.");
        }
        return new(directory, content, sprites, inventory, art.RootElement.Clone());
    }

    /// <summary>Pure authenticated composition; Load additionally validates all on-disk source and art files.</summary>
    public static PewterPokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson, string pewterGeneratedJson)
    {
        var original = PretForestPokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<PewterPokemonDocument>(pewterGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Species.Length == 7 && doc.Moves.Length == 28 && doc.SpeciesNames.Length == 7 &&
            doc.MoveNames.Length == 28 && doc.Constants.Length == 4 && doc.EncodedNames.Length == 35 && !doc.ItemEvolutionExecutorImplemented &&
            !doc.LevelEvolutionExecutorImplemented && !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Pewter Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        Require(doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).Append(new MoveId("pret:MOVE_ROCK_TOMB"))
                .ToHashSet().SetEquals(doc.Moves.Select(x => x.Id)),
            "Pewter Pokemon learnset and Brock custom move closure differs.");
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
            constants["ITEM_METAL_COAT"] == 199 && constants.Count == 6, "Evolution source constants differ.");
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
        Require(result.Species.Count == 29 && result.Moves.Count == 84 && names.Count == 29 && moveNames.Count == result.Moves.Count, "Incomplete composed Pewter Pokemon catalog.");
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants.ToImmutable(), encoded);
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Pewter Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Pewter Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Pewter Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}
