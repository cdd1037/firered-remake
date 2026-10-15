using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record ForestPokemonInput(string Path);
public sealed record ForestPokemonConstant(string Symbol, int Value, PokemonSourceRef Source);
public sealed record ForestPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    bool ItemEvolutionExecutorImplemented, bool LevelEvolutionExecutorImplemented, ImmutableArray<string> Limitations);
public sealed record ForestPokemonCatalogContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, int> Constants);
public sealed record ForestPokemonPoint(int X, int Y);
public sealed record ForestPokemonBounds(int X, int Y, int Width, int Height);
public sealed record ForestPokemonSprite(string SpeciesId, int SourceSpeciesId, string Side, string PaletteVariant, string TexturePath,
    string SourceImagePath, string SourcePalettePath, int Width, int Height, int FrameWidth,
    int FrameHeight, int FrameCount, int SourceFrameIndex, int SourceWidth, int SourceHeight,
    int SourceYOffset, int SourceEnemyElevation, ForestPokemonPoint SingleBattleBaseCenter,
    ForestPokemonPoint SingleBattleCenter, ForestPokemonPoint SingleBattleTopLeft,
    ForestPokemonBounds AlphaBounds, int TransparentIndex, ImmutableArray<int> PaletteRgb555,
    string PaletteExpansion, bool FlipH);

/// <summary>
/// Authenticated forward forest families plus offline normal/shiny sprites. This content increment
/// does not enable encounters, battle effects, evolution execution, or campaign presentation.
/// </summary>
public sealed class PretForestPokemon
{
    public const string Profile = "firered-forest-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretForestPokemon(string directory, ForestPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art)
    {
        GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; Sprites = sprites; SourceFiles = files; BattleArt = art;
    }

    /// <summary>Authenticates all raw, rendered and legacy catalog bytes before exposing content.</summary>
    public static PretForestPokemon Load(PretCanonicalPokemonCatalog canonical, string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/forest-pokemon");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Forest Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate forest Pokemon output.");
        }
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var inventory = files.ToImmutable();
        var content = LoadContent(canonical,
            Utf8.GetString(ReadPinned(project,"content/generated/route1-pokemon/catalog.json")),
            Utf8.GetString(ReadPinned(project,"content/generated/old-man-tutorial/catalog.json")),
            Utf8.GetString(outputs["catalog.json"]));
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 32 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 32,
            "Forest Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(content.Catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Forest Pokemon sprite identity/pixels differ.");
        }
        return new(directory, content, sprites, inventory, art.RootElement.Clone());
    }

    /// <summary>Pure authenticated composition; Load additionally validates all on-disk source and art files.</summary>
    public static ForestPokemonCatalogContent LoadContent(PretCanonicalPokemonCatalog canonical, string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson)
    {
        var original = PretOldManTutorial.LoadContent(canonical, route1GeneratedJson, tutorialGeneratedJson);
        var options = canonical.ReferenceOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<ForestPokemonDocument>(forestGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && !doc.ItemEvolutionExecutorImplemented &&
            !doc.LevelEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Forest Pokemon scope differs.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            species.Add(value.Id, value);
        }
        Require(doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).ToHashSet().SetEquals(doc.Moves.Select(x => x.Id)),
            "Forest Pokemon learnset move closure differs.");
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            if (moves.TryGetValue(value.Id, out var existing))
                Require(JsonSerializer.Serialize(existing) == JsonSerializer.Serialize(value), "Existing move definition changed.");
            else moves.Add(value.Id, value);
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
        var constants = doc.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        Require(constants["EVO_LEVEL"] == 4 && constants["EVO_ITEM"] == 7 && constants["ITEM_THUNDER_STONE"] == 96, "Evolution source constants differ.");
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateDefinitions(result);
        Require(result.Species.Count == 22 && names.Count == 22 && moveNames.Count == result.Moves.Count, "Incomplete composed forest Pokemon catalog.");
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants);
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe forest Pokemon path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Forest Pokemon directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Forest Pokemon file symlink is unsupported: " + path);
        }
        return result;
    }
}
