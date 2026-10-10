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
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "7c74a69296cba5453da5948d66a3db695b8c840458e422c65562f0b792c54150";
    public const string ManifestSha256 = "8447939fa3cd286b9c024e9f0c0fe690421a69564aba08f3c5d6208a6e90ecb0";
    public const string CatalogSha256 = "b9ae60e66619eadc6e45843e3212ea8cc1381be79142f5ccd316dde5f1986fb8";
    public const string RawManifestSha256 = "47485b86d6a47f7675b6c4577db52160866fd6bd2634e75d0efa49366d08dcf3";
    public const string BattleArtSha256 = "3543357d9cc9d2f25e0fe389da3ac8f88f18df1ab9afd782ea91b01f0f625fdb";
    public const string FullSourceHash = "e09ec7e9c0fec1f156acf3a98540f6b5028a97836f074845443ac06ae6f30a29";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string CatalogSourcePath => Path.Combine(GeneratedDirectory, "catalog.json");
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");

    private PretPewterPokemon(string directory, PewterPokemonCatalogContent content,
        ImmutableArray<ForestPokemonSprite> sprites, ImmutableArray<PretWorldSourceFile> files, JsonElement art)
    {
        GeneratedDirectory = directory; Catalog = content.Catalog; SpeciesNames = content.SpeciesNames;
        MoveNames = content.MoveNames; Constants = content.Constants; EncodedNames = content.EncodedNames; Sprites = sprites; SourceFiles = files; BattleArt = art;
        AdditionalSourceHashes = ["pewter-pokemon-manifest:" + ManifestSha256, "pewter-pokemon-lock:" + LockSha256,
            .. files.Select(f => "pewter-pokemon:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Authenticates all raw, rendered and legacy catalog bytes before exposing content.</summary>
    public static PretPewterPokemon Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/pewter-pokemon");
        var rawDirectory = Path.Combine(project, "content/raw/pewter-pokemon");
        byte[] ReadPinned(string root, string path, string hash, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Pinned Pewter Pokemon bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/pewter-pokemon.lock.json", LockSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("fullSourceHash").GetString() == FullSourceHash && m.GetProperty("inputCount").GetInt32() == 57 &&
            m.GetProperty("outputs").GetArrayLength() == 58 && m.GetProperty("composedSpeciesCount").GetInt32() == 29 &&
            !m.GetProperty("runtimeShadersUsed").GetBoolean(), "Pewter Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate Pewter Pokemon output.");
        }
        Require(Hash(outputs["catalog.json"]) == CatalogSha256 && Hash(outputs["battle-art.json"]) == BattleArtSha256,
            "Pewter Pokemon generated documents differ.");
        using var catalog = JsonDocument.Parse(outputs["catalog.json"]);
        Require(JsonElement.DeepEquals(catalog.RootElement.GetProperty("inputs"), sourceLock.RootElement.GetProperty("files")),
            "Pewter Pokemon source inventory differs from fixed lock.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Pewter Pokemon Git blob differs: " + path);
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        Require(inventory.Length == 57 && inventory.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 57,
            "Incomplete Pewter Pokemon source set.");
        var material = string.Concat(inventory.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Utf8.GetBytes(material)) == FullSourceHash, "Pewter Pokemon source-set hash differs.");
        var content = LoadContent(
            Utf8.GetString(ReadPinned(project, "content/generated/route1-pokemon/catalog.json", PretRoute1PokemonCatalog.GeneratedSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/old-man-tutorial/catalog.json", PretOldManTutorial.GeneratedSha256)),
            Utf8.GetString(ReadPinned(project, "content/generated/forest-pokemon/catalog.json", PretForestPokemon.CatalogSha256)),
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
                Hash(outputs[sprite.TexturePath]) == sprite.Sha256 && Hash(outputs[sprite.RgbaPath]) == sprite.RgbaSha256 &&
                outputs[sprite.RgbaPath].Length == 64 * 64 * 4, "Pewter Pokemon sprite identity/pixels differ.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" && source.Repository == "pret/pokefirered" && source.Commit == SourceCommit &&
                    inventory.Any(f => f.Path == source.Path && f.Sha256 == source.Sha256), "Pewter sprite provenance differs.");
        }
        return new(directory, content, sprites, inventory, art.RootElement.Clone());
    }

    /// <summary>Pure authenticated composition; Load additionally validates all on-disk source and art files.</summary>
    public static PewterPokemonCatalogContent LoadContent(string route1GeneratedJson, string tutorialGeneratedJson, string forestGeneratedJson, string pewterGeneratedJson)
    {
        Require(Hash(Utf8.GetBytes(pewterGeneratedJson)) == CatalogSha256, "Pewter Pokemon catalog differs from reviewed conversion.");
        var original = PretForestPokemon.LoadContent(route1GeneratedJson, tutorialGeneratedJson, forestGeneratedJson);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<PewterPokemonDocument>(pewterGeneratedJson, options);
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == 57 && doc.Species.Length == 7 && doc.Moves.Length == 28 && doc.SpeciesNames.Length == 7 &&
            doc.MoveNames.Length == 28 && doc.Constants.Length == 4 && doc.EncodedNames.Length == 35 && !doc.ItemEvolutionExecutorImplemented &&
            !doc.LevelEvolutionExecutorImplemented && !doc.TradeEvolutionExecutorImplemented && !doc.Limitations.IsDefaultOrEmpty, "Pewter Pokemon scope differs.");
        var inputs = doc.Inputs.ToDictionary(x => x.Path, StringComparer.Ordinal);
        void Source(PokemonSourceRef source, string path)
        {
            Require(source.Path == path && inputs.TryGetValue(path, out var input) && input.Sha256 == source.Sha256 &&
                source.StartLine > 0 && source.EndLine >= source.StartLine && !string.IsNullOrWhiteSpace(source.Symbol),
                "Pewter Pokemon source reference differs.");
        }
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species)
        {
            Source(value.Source, "src/data/pokemon/species_info.h"); Source(value.LearnsetSource, "src/data/pokemon/level_up_learnsets.h");
            foreach (var move in value.LevelUpMoves) Source(move.Source, value.LearnsetSource.Path);
            foreach (var evolution in value.Evolutions) Source(evolution.Source, "src/data/pokemon/evolution.h");
            species.Add(value.Id, value);
        }
        Require(doc.Species.SelectMany(s => s.LevelUpMoves).Select(x => x.Move).Append(new MoveId("pret:MOVE_ROCK_TOMB"))
                .ToHashSet().SetEquals(doc.Moves.Select(x => x.Id)),
            "Pewter Pokemon learnset and Brock custom move closure differs.");
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
            Source(value.Source, value.Symbol.StartsWith("ITEM_", StringComparison.Ordinal) ? "include/constants/items.h" : "include/constants/pokemon.h");
            Require(!constants.TryGetValue(value.Symbol, out int previous) || previous == value.Value, "Existing evolution constant changed.");
            constants[value.Symbol] = value.Value;
        }
        Require(constants["EVO_LEVEL"] == 4 && constants["EVO_TRADE"] == 5 && constants["EVO_TRADE_ITEM"] == 6 &&
            constants["ITEM_METAL_COAT"] == 199 && constants.Count == 6, "Evolution source constants differ.");
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
        Require(result.Species.Count == 29 && result.Moves.Count == 84 && names.Count == 29 && moveNames.Count == result.Moves.Count, "Incomplete composed Pewter Pokemon catalog.");
        return new(result, names.ToImmutable(), moveNames.ToImmutable(), constants.ToImmutable(), encoded);
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
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
