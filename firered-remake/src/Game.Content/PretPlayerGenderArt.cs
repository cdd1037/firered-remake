using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PlayerGenderArtPoint(int X, int Y);
public sealed record PlayerGenderArtDirection(int Stand, ImmutableArray<int> Walk, bool FlipH);
public sealed record PlayerGenderArtSourceRef(string Kind, string Repository, string Commit, string Path, string Symbol);
public sealed record PlayerGenderArtAsset(int Gender, string GenderSymbol, string GraphicsId, int SourceGraphicsId, string TexturePath, int Width,
    int Height, int FrameWidth, int FrameHeight, int FrameCount, string FrameLayout,
    ImmutableArray<int> SourceFrameIndices, string SourceImagePath, string SourcePalettePath,
    string PaletteTag, string PaletteSlot, ImmutableArray<int> PaletteRgb555, int TransparentIndex,
    ImmutableDictionary<string, PlayerGenderArtDirection> Directions, PlayerGenderArtPoint TileCenterAnchor,
    PlayerGenderArtPoint DrawOffsetFromTileOrigin, int WalkFrameDurationSourceFrames, int SourceTimingHz,
    ImmutableArray<PlayerGenderArtSourceRef> SourceRefs);
public sealed record PlayerGenderArtDocument(int SchemaVersion, string Profile, string SourceCommit, string PaletteExpansion,
    ImmutableArray<PlayerGenderArtAsset> Assets, ImmutableArray<string> Limitations);

/// <summary>Authenticated source Red/Green standing and walking art. Gender is the canonical MALE=0/FEMALE=1.</summary>
public sealed class PretPlayerGenderArt
{
    public const string Profile = "firered-player-gender-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly ImmutableDictionary<int, byte[]> textures;
    public PlayerGenderArtDocument Document { get; }
    public ImmutableArray<PlayerGenderArtAsset> Assets => Document.Assets;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public string GeneratedDirectory { get; }

    private PretPlayerGenderArt(string directory, PlayerGenderArtDocument document,
        ImmutableArray<PretWorldSourceFile> sources, ImmutableDictionary<int, byte[]> textureBytes)
    {
        GeneratedDirectory = directory; Document = document; SourceFiles = sources;
        textures = textureBytes;
    }

    /// <summary>Validates the source lock, thirteen raw inputs, both manifests, metadata and both sprite sheets.</summary>
    public static PretPlayerGenderArt Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(root, "content/generated/player-gender-art");
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1             && !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(),
            "Player art manifest identity differs.");
        var sources = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var sourceFiles = sources.ToImmutable();
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path,path.EndsWith(".png", StringComparison.Ordinal))),
                "Duplicate player art output.");
        }
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(["player-gender-art.json", "player-male.png", "player-female.png"])
, "Player art output inventory differs.");
        var document = ContentJson.ParseStrict<PlayerGenderArtDocument>(Utf8.GetString(outputs["player-gender-art.json"]), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.PaletteExpansion == "floor(rgb5 * 255 / 31)"
            && document.Assets.Length == 2 && document.Assets.Select(a => a.Gender).Order().SequenceEqual([0, 1])
            && !document.Limitations.IsDefaultOrEmpty, "Player art document differs.");
        var textures = ImmutableDictionary.CreateBuilder<int, byte[]>();
        foreach (var asset in document.Assets)
        {
            ValidateAsset(asset);
            foreach (var source in asset.SourceRefs)
                Require(source.Kind == "pret" , "Player art provenance differs.");
            var bytes = outputs[asset.TexturePath];
            textures.Add(asset.Gender, bytes);
        }
        return new(directory, document, sourceFiles, textures.ToImmutable());
    }

    public PlayerGenderArtAsset Sprite(int gender)
    {
        ValidateGender(gender);
        return Assets.Single(asset => asset.Gender == gender);
    }

    /// <summary>Fresh authenticated PNG bytes; subsequent caller or disk mutations cannot alter this snapshot.</summary>
    public byte[] ReadTexture(int gender) { ValidateGender(gender); return [.. textures[gender]]; }


    /// <summary>Convenience one-shot load for canonical gender 0 or 1; prefer one Load for repeated rendering.</summary>
    public static byte[] ReadTexture(string projectRoot, int gender)
    {
        ValidateGender(gender);
        return Load(projectRoot).ReadTexture(gender);
    }

    private static void ValidateGender(int gender)
    {
        if (gender is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(gender), "Expected canonical MALE=0 or FEMALE=1.");
    }

    private static void ValidateAsset(PlayerGenderArtAsset a)
    {
        ValidateGender(a.Gender);
        bool female = a.Gender == 1;
        string name = female ? "GREEN" : "RED";
        Require(a.GenderSymbol == (female ? "FEMALE" : "MALE") && a.SourceGraphicsId == (female ? 7 : 0)
            && a.GraphicsId == "OBJ_EVENT_GFX_" + name + "_NORMAL" && a.TexturePath == (female ? "player-female.png" : "player-male.png")
&& a.Width == 144 && a.Height == 32 && a.FrameWidth == 16 && a.FrameHeight == 32
            && a.FrameCount == 9 && a.FrameLayout == "horizontal" && a.SourceFrameIndices.SequenceEqual(Enumerable.Range(0, 9))
            && a.SourceImagePath == "graphics/object_events/pics/people/" + name.ToLowerInvariant() + "_normal.png"
            && a.SourcePalettePath == "graphics/object_events/palettes/player.pal" && a.PaletteTag == "OBJ_EVENT_PAL_TAG_PLAYER_" + name
            && a.PaletteSlot == "PALSLOT_PLAYER" && a.PaletteRgb555.Length == 16 && a.PaletteRgb555.All(w => w is >= 0 and <= 32767)
            && a.TransparentIndex == 0 && a.TileCenterAnchor == new PlayerGenderArtPoint(8, 24)
            && a.DrawOffsetFromTileOrigin == new PlayerGenderArtPoint(0, -16) && a.WalkFrameDurationSourceFrames == 8
            && a.SourceTimingHz == 60 && a.SourceRefs.Length == 12 && a.Directions.Count == 4, "Player sprite metadata differs.");
        foreach (var (direction, stand, walk) in new (string, int, int[])[]
        { ("south", 0, [3, 0, 4, 0]),("north", 1, [5, 1, 6, 1]),("west", 2, [7, 2, 8, 2]),("east", 2, [7, 2, 8, 2]) })
            Require(a.Directions.TryGetValue(direction, out var d) && d.Stand == stand && d.FlipH == (direction == "east")
                && d.Walk.SequenceEqual(walk), "Player sprite facing/walk frames differ.");
    }

    private static byte[] ReadPinned(string root,string relative,bool texture = false)
    {
        var path = SafeFile(root, relative);
var bytes = File.ReadAllBytes(path);
return bytes;
    }





    private static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Split('/').Any(s => s is "" or "." or ".."), "Unsafe player art path.");
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            Require(cursor.LinkTarget is null, "Player art root/ancestor symlink rejected.");
        string path = root;
        foreach (var segment in relative.Split('/'))
        {
            path = Path.Combine(path, segment);
            Require(new FileInfo(path).LinkTarget is null && new DirectoryInfo(path).LinkTarget is null,
                "Player art source/output symlink rejected.");
        }
        return path;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
