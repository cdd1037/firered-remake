using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OldManArtPatch(string Path, string Sha256, int Frame);
public sealed record OldManArtAsset(string Id, string TexturePath, int Width, int Height,
    int FrameWidth, int FrameHeight, int FrameCount, string FrameLayout, string Sha256, string RgbaSha256,
    string IndexSha256, string SourceImagePath, string SourceImageSha256, string SourcePalettePath,
    string SourcePaletteSha256, string SourcePaletteKind, int TransparentIndex,
    ImmutableArray<int> PaletteRgb555, ImmutableArray<OldManArtPatch> SourcePatches);
public sealed record OldManBallAnimation(string Id, ImmutableArray<OldManThrowFrame> Frames);
public sealed record OldManArtLayout(int TrainerCenterX, int TrainerCenterY, int TrainerSlideOffsetX,
    int TrainerSlidePixelsPerTick, int WeedleCenterX, int WeedleCenterY, int WeedleSourceWidth,
    int WeedleSourceHeight, int WeedleSourceYOffset, int BallStartX, int BallStartY, int BallTargetX, int BallTargetY);
public sealed record OldManBallTiming(int ReleaseTrainerCommandIndex, int ArcTicks, int ArcAmplitude,
    int HitDelayTicks, int ShrinkDuration, int FallAmplitude, int BounceCount, int FirstShakeDelayTicks,
    int BetweenShakeDelayTicks, int SuccessShakeCount, int ClickTicks, int PaletteRestoreTicks,
    int CaughtIntroTicks, int RemoveMonsterTicks);
public sealed record OldManArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    string FullSourceHash, ImmutableArray<PretQuestItemInput> Inputs, string TutorialSha256,
    ImmutableArray<OldManArtAsset> Assets, QuestItemUiEntry TeachyTv, string PaletteExpansion,
    ImmutableArray<OldManThrowFrame> OldManThrowFrames, ImmutableArray<OldManBallAnimation> BallAnimations,
    OldManArtLayout Layout, OldManBallTiming BallTiming, ImmutableArray<PokemonSourceRef> SourceRefs,
    ImmutableArray<string> Limitations);

/// <summary>Authenticated tutorial textures and source animation metadata, independent of game rules.</summary>
public static class PretOldManArt
{
    public const string Profile = "firered-old-man-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string GeneratedSha256 = "214650cb8fb631bd6a7c186267b041e6dd22db4a2b516c6bfcd276f9838fb814";
    public const string SourceManifestSha256 = "9e1538f4d45192253c724ce09358b63e001138dc3e7eb817be1bbbb5937e0c90";

    private static readonly ImmutableDictionary<string, (int Width, int Height, int FrameHeight, string Png, string Rgba)> Expected =
        new Dictionary<string, (int, int, int, string, string)>(StringComparer.Ordinal)
        {
            ["old-man-back"] = (64, 256, 64, "f62807d02d71d2595dc4914a0a7bfa07c15b73e6fe1211b3d27995116688562f", "2c436ea07749ff295fc84dae08b5ee2e56c1d5e196d3ce98430daa4d0761b95f"),
            ["weedle-front-normal"] = (64, 64, 64, "e48615c61472243b3325ad9afe3da1b627f047a26bd3131daed1cec45e6a2fa4", "d36e556649a2aef541090ecedf1e57a13fc56417f19607763a9c980e837161c8"),
            ["weedle-front-shiny"] = (64, 64, 64, "5e744e64fb4d1dcb50e01c51d33aca0aca7f6c6bb1a7d090d28132f69aecd299", "89cc1ce0272e298fa2bab5e3a5c67d2559e878e66410c2041c7e4ce7371decbe"),
            ["teachy-tv-icon"] = (24, 24, 24, "2d1884d7e133ef37cebf57b3b62420a815c3ff3d3041bce893c35a82ad54c7ed", "7e3c2554cd07a9f9fa86557c9609f3559f6f825e47c9f52e9be2c320c306a2a2"),
            ["poke-ball"] = (16, 48, 16, "ed7de1d9879b28fbcd6c7576df23c4fad909c22db54ce0a2460d2153ca997f8e", "9c9e99de607362e3b4e17cc56ef1742f300b9463bb2acde06a54d02682d9bd3d")
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static OldManArtDocument Load(string json)
    {
        if (json is null || Digest(Encoding.UTF8.GetBytes(json)) != GeneratedSha256)
            throw new InvalidDataException("Old Man art catalog hash differs; run the verified importer.");
        var doc = ContentJson.ParseStrict<OldManArtDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != SourceCommit
            || doc.FullSourceHash != SourceManifestSha256 || doc.TutorialSha256 != PretOldManTutorial.GeneratedSha256
            || doc.Inputs.Length != 22 || doc.Assets.Length != 5 || doc.Assets.Select(a => a.Id).Distinct().Count() != 5
            || doc.TeachyTv.Id != "pret:ITEM_TEACHY_TV" || doc.TeachyTv.SourceSymbol != "ITEM_TEACHY_TV"
            || doc.TeachyTv.Icon != "teachy-tv-icon" || doc.TeachyTv.FieldUseFunction != "FieldUseFunc_TeachyTv"
            || doc.PaletteExpansion != "(rgb5 << 3) | (rgb5 >> 2)" || doc.BallAnimations.Length != 3
            || !doc.OldManThrowFrames.SequenceEqual(new[] { new OldManThrowFrame(1, 24), new(2, 9), new(3, 24), new(0, 9) })
            || doc.Layout != new OldManArtLayout(80, 80, 240, -2, 176, 57, 40, 32, 17, 60, 91, 176, 24)
            || doc.BallTiming != new OldManBallTiming(1, 34, -40, 10, 28, 40, 4, 31, 31, 3, 40, 60, 95, 315)
            || doc.SourceRefs.IsDefaultOrEmpty || doc.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Old Man art profile is incomplete.");
        foreach (var asset in doc.Assets) ValidateAsset(asset);
        return doc;
    }

    private static void ValidateAsset(OldManArtAsset asset)
    {
        if (!Expected.TryGetValue(asset.Id, out var expected) || asset.TexturePath != asset.Id + ".png"
            || asset.Width != expected.Width || asset.Height != expected.Height || asset.FrameWidth != expected.Width
            || asset.FrameHeight != expected.FrameHeight || asset.FrameCount != expected.Height / expected.FrameHeight
            || asset.FrameLayout != "vertical" || asset.Sha256 != expected.Png || asset.RgbaSha256 != expected.Rgba
            || asset.TransparentIndex != 0 || asset.PaletteRgb555.Length != 16
            || asset.PaletteRgb555.Any(value => value < 0 || value > 32767)
            || asset.SourcePaletteKind != (asset.Id == "poke-ball" ? "png-plte" : "jasc-pal"))
            throw new InvalidDataException("Old Man art texture identity differs: " + asset.Id);
    }

    public static byte[] ReadTexture(string directory, OldManArtAsset asset)
    {
        ValidateAsset(asset);
        for (var current = new DirectoryInfo(Path.GetFullPath(directory)); current is not null; current = current.Parent)
            if (current.LinkTarget is not null) throw new InvalidDataException("Old Man texture directory symlink rejected.");
        var path = Path.Combine(directory, asset.TexturePath);
        var info = new FileInfo(path);
        if (info.LinkTarget is not null || info.Length > asset.Width * asset.Height * 4 + 1024)
            throw new InvalidDataException("Old Man texture path or size differs.");
        var bytes = File.ReadAllBytes(path);
        if (Digest(bytes) != asset.Sha256) throw new InvalidDataException("Old Man texture hash differs: " + asset.Id);
        VerifyRgba(bytes, asset);
        return bytes;
    }

    private static void VerifyRgba(byte[] bytes, OldManArtAsset asset)
    {
        // Generated format is deliberately narrow: RGBA8, one IDAT, filter 0 on every row.
        if (bytes.Length < 57 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13
            || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) != asset.Width
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) != asset.Height
            || !bytes.AsSpan(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 })
            || !bytes.AsSpan(37, 4).SequenceEqual("IDAT"u8))
            throw new InvalidDataException("Old Man texture encoding differs.");
        int compressedLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(33, 4)));
        if (compressedLength != bytes.Length - 57) throw new InvalidDataException("Old Man texture chunks differ.");
        using var compressed = new MemoryStream(bytes, 41, compressedLength, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        int stride = checked(asset.Width * 4);
        var raw = new byte[checked((stride + 1) * asset.Height)];
        zlib.ReadExactly(raw);
        if (zlib.ReadByte() != -1) throw new InvalidDataException("Old Man RGBA stream exceeds expected size.");
        var rgba = new byte[checked(stride * asset.Height)];
        for (int y = 0; y < asset.Height; y++)
        {
            if (raw[y * (stride + 1)] != 0) throw new InvalidDataException("Old Man RGBA filter differs.");
            raw.AsSpan(y * (stride + 1) + 1, stride).CopyTo(rgba.AsSpan(y * stride, stride));
        }
        if (Digest(rgba) != asset.RgbaSha256) throw new InvalidDataException("Old Man raw RGBA checksum differs.");
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
