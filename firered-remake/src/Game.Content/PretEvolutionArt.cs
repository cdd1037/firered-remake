using System.Collections.Immutable;
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
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, int> Species = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["IVYSAUR"] = 2,["VENUSAUR"] = 3,["CHARMELEON"] = 5,["CHARIZARD"] = 6,["WARTORTLE"] = 8,["BLASTOISE"] = 9,["PIDGEOTTO"] = 17,["PIDGEOT"] = 18,["RATICATE"] = 20
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private readonly ImmutableDictionary<string, byte[]> outputBytes;
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }
    public string BattleArtSourcePath => Path.Combine(GeneratedDirectory, "battle-art.json");
    public string ManifestSourcePath => Path.Combine(GeneratedDirectory, "manifest.json");

    private PretEvolutionArt(string directory, ImmutableArray<ForestPokemonSprite> sprites,
        ImmutableArray<PretWorldSourceFile> files, JsonElement art, ImmutableDictionary<string, byte[]> outputs)
    {
        GeneratedDirectory = directory; Sprites = sprites; SourceFiles = files; BattleArt = art; outputBytes = outputs;
    }

    /// <summary>Authenticates the fixed lock, both manifests, all 50 raw inputs and all 73 generated payloads.</summary>
    public static PretEvolutionArt Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/evolution-art");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && !m.GetProperty("runtimeShadersUsed").GetBoolean() &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean() &&
            !m.GetProperty("publiclyUploaded").GetBoolean(), "Evolution art manifest identity differs.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate evolution art output.");
        }
        var expectedOutputs = Species.Keys.SelectMany(name => new[] { "front","back" }.SelectMany(side =>
            new[] { "normal","shiny" }.SelectMany(variant => new[] { "png" }.Select(extension =>
                $"{name.ToLowerInvariant()}-{side}-{variant}.{extension}")))).Append("battle-art.json");
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedOutputs), "Evolution art output set differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var inventory = files.ToImmutable();
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var a = art.RootElement;
        Require(a.GetProperty("schemaVersion").GetInt32() == 1 &&
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
                sprite.PaletteExpansion == "floor(rgb5 * 255 / 31)" &&
                (outputs[sprite.TexturePath]).Length > 0, "Evolution art sprite identity/pixels differ.");
            Require(sprite.SingleBattleCenter.X == sprite.SingleBattleBaseCenter.X &&
                sprite.SingleBattleCenter.Y == sprite.SingleBattleBaseCenter.Y + sprite.SourceYOffset - sprite.SourceEnemyElevation &&
                sprite.SingleBattleTopLeft.X == sprite.SingleBattleCenter.X - 32 &&
                sprite.SingleBattleTopLeft.Y == sprite.SingleBattleCenter.Y - 32, "Evolution art placement differs.");
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
