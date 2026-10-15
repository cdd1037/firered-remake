using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PretFishingArtAsset(int Gender, string GraphicsId, int SourceGraphicsId, string TexturePath, int Width, int Height,
    int FrameWidth, int FrameHeight, int FrameCount, PlayerGenderArtPoint DrawOffsetFromTileOrigin,
    ImmutableArray<PlayerGenderArtSourceRef> SourceRefs);
public sealed record PretFishingAnimCommand(string Opcode, int Value, int Duration, bool FlipH, int OffsetX, int OffsetY);
public sealed record PretFishingAnimation(string Id, int SourceAnimationId, string Direction,
    ImmutableArray<PretFishingAnimCommand> Commands, string SourceText, PlayerGenderArtSourceRef SourceRef);
public sealed record PretFishingArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretFishingArtAsset> Assets, ImmutableArray<PretFishingAnimation> Animations,
    ImmutableDictionary<string, string> Texts, int SourceTimingHz, PlayerGenderArtSourceRef AlignmentSource,
    ImmutableArray<string> Limitations);

/// <summary>Authenticated source Red/Green Bicycle art and bounded land-cycling contracts. Gender is the canonical MALE=0/FEMALE=1.</summary>
public sealed class PretCityCycleArt
{
    public const string Profile = "firered-city-cycle-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly ImmutableDictionary<int, byte[]> textures;
    public PlayerGenderArtDocument Document { get; }
    public ImmutableArray<PlayerGenderArtAsset> Assets => Document.Assets;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public string GeneratedDirectory { get; }
    public JsonElement Rules { get; }
    public PretFishingArtDocument Fishing { get; }
    private readonly ImmutableDictionary<int, byte[]> fishingTextures;

    private PretCityCycleArt(string directory, PlayerGenderArtDocument document,
        ImmutableArray<PretWorldSourceFile> sources, ImmutableDictionary<int, byte[]> textureBytes, JsonElement rules, PretFishingArtDocument fishing,
        ImmutableDictionary<int, byte[]> fishingTextureBytes)
    {
        GeneratedDirectory = directory; Document = document; SourceFiles = sources;
        textures = textureBytes; Rules = rules; Fishing = fishing;
        fishingTextures = fishingTextureBytes;
    }

    /// <summary>Loads cycling and fishing sprite sheets, metadata and animation rules.</summary>
    public static PretCityCycleArt Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(root, "content/generated/city-cycle-art");
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1,
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
        Require(outputs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(["city-cycle-art.json", "cycle-rules.json", "cycle-male.png", "cycle-female.png", "fish-male.png", "fish-female.png", "fishing-art.json"])
, "Player art output inventory differs.");
        var document = ContentJson.ParseStrict<PlayerGenderArtDocument>(Utf8.GetString(outputs["city-cycle-art.json"]), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.PaletteExpansion == "floor(rgb5 * 255 / 31)"
            && document.Assets.Length == 2 && document.Assets.Select(a => a.Gender).Order().SequenceEqual([0, 1])
            && !document.Limitations.IsDefaultOrEmpty, "Player art document differs.");
        var textures = ImmutableDictionary.CreateBuilder<int, byte[]>();
        foreach (var asset in document.Assets)
        {
            ValidateAsset(asset);
            var bytes = outputs[asset.TexturePath];
            textures.Add(asset.Gender, bytes);
        }
        using var rules = JsonDocument.Parse(outputs["cycle-rules.json"]);
        Require(!rules.RootElement.GetProperty("runtimeIntegrated").GetBoolean() &&
            rules.RootElement.GetProperty("ordinaryMovement").GetProperty("pixelsPerSourceFrame").EnumerateArray().Select(p => p.GetInt32()).SequenceEqual([2,3,3,2,3,3]),
            "Cycle source movement contract differs.");
        var fishing = ContentJson.ParseStrict<PretFishingArtDocument>(Utf8.GetString(outputs["fishing-art.json"]), ContentJson.CreateOptions());
        Require(fishing.SchemaVersion == 1 &&
            fishing.SourceTimingHz == 60 && fishing.Assets.Length == 2 && fishing.Animations.Length == 12 && fishing.Texts.Count == 3,
            "Fishing art scope differs.");
        var fishTextures = ImmutableDictionary.CreateBuilder<int, byte[]>();
        foreach (var asset in fishing.Assets)
        {
            ValidateGender(asset.Gender);
            Require(asset.SourceGraphicsId == (asset.Gender == 0 ? 4 : 11) && asset.Width == 384 && asset.Height == 32 &&
                asset.FrameWidth == 32 && asset.FrameHeight == 32 && asset.FrameCount == 12 &&
                asset.DrawOffsetFromTileOrigin == new PlayerGenderArtPoint(-8, -16) ,
                "Fishing sprite metadata differs.");
            fishTextures.Add(asset.Gender, outputs[asset.TexturePath]);
        }
        Require(fishing.Animations.Select(a => a.SourceAnimationId).Order().SequenceEqual(Enumerable.Range(0, 12)) &&
            fishing.Animations.All(a => a.Commands.All(c => c.Opcode is "frame" or "loop" or "jump" or "end" &&
                (c.Opcode != "frame" || c.Value is >= 0 and < 12 && c.Duration > 0))), "Fishing animation commands differ.");
        return new(directory, document, sourceFiles, textures.ToImmutable(), rules.RootElement.Clone(),
            fishing, fishTextures.ToImmutable());
    }

    public PlayerGenderArtAsset Sprite(int gender)
    {
        ValidateGender(gender);
        return Assets.Single(asset => asset.Gender == gender);
    }

    public PretFishingArtAsset FishingSprite(int gender)
    {
        ValidateGender(gender);
        return Fishing.Assets.Single(a => a.Gender == gender);
    }
    public byte[] ReadFishingTexture(int gender) { ValidateGender(gender); return [.. fishingTextures[gender]]; }

    public PretFishingAnimation FishingAnimation(string action, string direction) => Fishing.Animations.Single(a => a.Id == action + "." + direction);

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
        Require(a.GenderSymbol == (female ? "FEMALE" : "MALE") && a.SourceGraphicsId == (female ? 8 : 1)
            && a.GraphicsId == "OBJ_EVENT_GFX_" + name + "_BIKE" && a.TexturePath == (female ? "cycle-female.png" : "cycle-male.png")
&& a.Width == 288 && a.Height == 32 && a.FrameWidth == 32 && a.FrameHeight == 32
            && a.FrameCount == 9 && a.FrameLayout == "horizontal" && a.SourceFrameIndices.SequenceEqual(Enumerable.Range(0, 9))
            && a.SourceImagePath == "graphics/object_events/pics/people/" + name.ToLowerInvariant() + "_bike.png"
            && a.SourcePalettePath == "graphics/object_events/palettes/player.pal" && a.PaletteTag == "OBJ_EVENT_PAL_TAG_PLAYER_" + name
            && a.PaletteSlot == "PALSLOT_PLAYER" && a.PaletteRgb555.Length == 16 && a.PaletteRgb555.All(w => w is >= 0 and <= 32767)
            && a.TransparentIndex == 0 && a.TileCenterAnchor == new PlayerGenderArtPoint(16, 24)
            && a.DrawOffsetFromTileOrigin == new PlayerGenderArtPoint(-8, -16) && a.WalkFrameDurationSourceFrames == 4
            && a.SourceTimingHz == 60 && a.Directions.Count == 4, "Player sprite metadata differs.");
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
