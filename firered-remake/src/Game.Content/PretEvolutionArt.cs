using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// Pinned, preconverted front/back normal/shiny art for the nine evolved starter/Route 1
/// species. Loading is independent of catalogs and does not enable evolution or presentation.
/// </summary>
public sealed class PretEvolutionArt
{
    public const string Profile = "firered-evolution-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "febf00be6af49d0a864f08e9ae514ca8765cbac1de660fdcc8af9bcd295081fa";
    public const string ManifestSha256 = "3fe8060c08125912460b7c99c950265be92705cfc5eca91c80ed7b11c6aa0f5c";
    public const string RawManifestSha256 = "f3d247fbb61e452ccc74171ef2c79e6e14af4063444d19735a6f51f9d8000c84";
    public const string BattleArtSha256 = "9404e2c2fa127c807165edb47d0b22bafa533ad0cb5a4e599f3b05683f0d9f1c";
    public const string FullSourceHash = "1e6ab78846ffc23e9beaeb59485acc179842a22588dbe2798576dbdc7bccc9c8";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, int> Species = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["IVYSAUR"] = 2, ["VENUSAUR"] = 3, ["CHARMELEON"] = 5, ["CHARIZARD"] = 6,
        ["WARTORTLE"] = 8, ["BLASTOISE"] = 9, ["PIDGEOTTO"] = 17, ["PIDGEOT"] = 18, ["RATICATE"] = 20
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory, "manifest.json");

    private PretEvolutionArt(string directory, ImmutableArray<ForestPokemonSprite> sprites,
        ImmutableArray<PretWorldSourceFile> files, JsonElement art, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory; Sprites = sprites; SourceFiles = files; BattleArt = art; outputBytes = outputs;
        AdditionalSourceHashes = ["evolution-art-manifest:" + ManifestSha256, "evolution-art-lock:" + LockSha256,
            .. files.Select(f => "evolution-art:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Authenticates the fixed lock, both manifests, all 50 raw inputs and all 73 generated payloads.</summary>
    public static PretEvolutionArt Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/evolution-art");
        var rawDirectory = Path.Combine(project, "content/raw/evolution-art");
        byte[] ReadPinned(string root, string path, string hash, int? length = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Pinned evolution art bytes differ: " + path);
            return bytes;
        }
        _ = ReadPinned(rawDirectory, "manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/evolution-art.lock.json", LockSha256));
        var m = manifest.RootElement; var l = sourceLock.RootElement;
        Require(l.GetProperty("schemaVersion").GetInt32() == 1 && l.GetProperty("profile").GetString() == Profile &&
            l.GetProperty("repository").GetString() == "pret/pokefirered" && l.GetProperty("commit").GetString() == SourceCommit &&
            l.GetProperty("tree").GetString() == SourceTree && l.GetProperty("gameEdition").GetString() == "FIRERED" &&
            l.GetProperty("romRevision").GetInt32() == 0, "Evolution art source identity differs.");
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("fullSourceHash").GetString() == FullSourceHash && m.GetProperty("lockSha256").GetString() == LockSha256 &&
            m.GetProperty("inputCount").GetInt32() == 50 && m.GetProperty("outputs").GetArrayLength() == 73 &&
            m.GetProperty("artSpeciesCount").GetInt32() == 9 && m.GetProperty("textureCount").GetInt32() == 36 &&
            m.GetProperty("rgbaCount").GetInt32() == 36 && !m.GetProperty("runtimeShadersUsed").GetBoolean() &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean() &&
            !m.GetProperty("publiclyUploaded").GetBoolean(), "Evolution art manifest identity differs.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate evolution art output.");
        }
        var expectedOutputs = Species.Keys.SelectMany(name => new[] { "front", "back" }.SelectMany(side =>
            new[] { "normal", "shiny" }.SelectMany(variant => new[] { "png", "rgba" }.Select(extension =>
                $"{name.ToLowerInvariant()}-{side}-{variant}.{extension}")))).Append("battle-art.json");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedOutputs), "Evolution art output set differs.");
        Require(Hash(outputs["battle-art.json"]) == BattleArtSha256, "Evolution art document differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var entry in l.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!; var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!; var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob,
                "Evolution art Git blob differs: " + path);
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        Require(inventory.Length == 50 && inventory.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 50,
            "Incomplete evolution art source set.");
        var material = string.Concat(inventory.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(Hash(Utf8.GetBytes(material)) == FullSourceHash, "Evolution art source-set hash differs.");
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var a = art.RootElement;
        Require(a.GetProperty("schemaVersion").GetInt32() == 1 && a.GetProperty("profile").GetString() == Profile &&
            a.GetProperty("sourceCommit").GetString() == SourceCommit && a.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            !a.GetProperty("runtimeShadersUsed").GetBoolean(), "Evolution art document identity differs.");
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(a.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 36 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 36,
            "Evolution art sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            var identity = Species.Single(s => sprite.SpeciesId == "pret:SPECIES_" + s.Key);
            Require(sprite.SourceSpeciesId == identity.Value && sprite.Side is "front" or "back" &&
                sprite.PaletteVariant is "normal" or "shiny" && sprite.Width == 64 && sprite.Height == 64 &&
                sprite.FrameWidth == 64 && sprite.FrameHeight == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                sprite.PaletteExpansion == "floor(rgb5 * 255 / 31)" && sprite.SourceRefs.Length == 9 &&
                Hash(outputs[sprite.TexturePath]) == sprite.Sha256 && Hash(outputs[sprite.RgbaPath]) == sprite.RgbaSha256 &&
                outputs[sprite.RgbaPath].Length == 64 * 64 * 4, "Evolution art sprite identity/pixels differ.");
            Require(sprite.SingleBattleCenter.X == sprite.SingleBattleBaseCenter.X &&
                sprite.SingleBattleCenter.Y == sprite.SingleBattleBaseCenter.Y + sprite.SourceYOffset - sprite.SourceEnemyElevation &&
                sprite.SingleBattleTopLeft.X == sprite.SingleBattleCenter.X - 32 &&
                sprite.SingleBattleTopLeft.Y == sprite.SingleBattleCenter.Y - 32, "Evolution art placement differs.");
            foreach (var source in sprite.SourceRefs)
                Require(source.Kind == "pret" && source.Repository == "pret/pokefirered" && source.Commit == SourceCommit &&
                    inventory.Any(f => f.Path == source.Path && f.Sha256 == source.Sha256), "Evolution art provenance differs.");
        }
        return new(directory, sprites, inventory, a.Clone(), outputs.ToImmutable());
    }

    public bool ContainsSpecies(SpeciesId species) => Sprites.Any(s => s.SpeciesId == species.Value);

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false)
    {
        if (side is not ("front" or "back")) throw new ArgumentOutOfRangeException(nameof(side));
        return Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    }

    /// <summary>Returns a fresh copy of the authenticated PNG bytes captured at Load time.</summary>
    public byte[] ReadTexture(SpeciesId species, string side, bool shiny = false) =>
        [.. outputBytes[Sprite(species, side, shiny).TexturePath]];

    /// <summary>Returns a fresh copy of the authenticated 64×64 RGBA8 bytes captured at Load time.</summary>
    public byte[] ReadRgba(SpeciesId species, string side, bool shiny = false) =>
        [.. outputBytes[Sprite(species, side, shiny).RgbaPath]];

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe evolution art path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Evolution art directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Evolution art file symlink is unsupported: " + path);
        }
        return result;
    }
}
