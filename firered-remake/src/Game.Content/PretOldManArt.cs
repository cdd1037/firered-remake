using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OldManArtPatch(string Path, int Frame);
public sealed record OldManArtAsset(string Id, string TexturePath, int Width, int Height, int FrameWidth, int FrameHeight, int FrameCount,
    string FrameLayout, string SourceImagePath, string SourcePalettePath, string SourcePaletteKind,
    int TransparentIndex, ImmutableArray<int> PaletteRgb555, ImmutableArray<OldManArtPatch> SourcePatches);
public sealed record OldManBallAnimation(string Id, ImmutableArray<OldManThrowFrame> Frames);
public sealed record OldManArtLayout(int TrainerCenterX, int TrainerCenterY, int TrainerSlideOffsetX,
    int TrainerSlidePixelsPerTick, int WeedleCenterX, int WeedleCenterY, int WeedleSourceWidth,
    int WeedleSourceHeight, int WeedleSourceYOffset, int BallStartX, int BallStartY, int BallTargetX, int BallTargetY);
public sealed record OldManBallTiming(int ReleaseTrainerCommandIndex, int ArcTicks, int ArcAmplitude,
    int HitDelayTicks, int ShrinkDuration, int FallAmplitude, int BounceCount, int FirstShakeDelayTicks,
    int BetweenShakeDelayTicks, int SuccessShakeCount, int ClickTicks, int PaletteRestoreTicks,
    int CaughtIntroTicks, int RemoveMonsterTicks);
public sealed record OldManArtDocument(int SchemaVersion, ImmutableArray<OldManArtAsset> Assets, QuestItemUiEntry TeachyTv, string PaletteExpansion,
    ImmutableArray<OldManThrowFrame> OldManThrowFrames, ImmutableArray<OldManBallAnimation> BallAnimations,
    OldManArtLayout Layout, OldManBallTiming BallTiming);

/// <summary>Authenticated tutorial textures and source animation metadata, independent of game rules.</summary>
public static class PretOldManArt
{
    public const string Profile = "firered-old-man-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    private static readonly ImmutableDictionary<string, (int Width, int Height, int FrameHeight)> Expected =
        new Dictionary<string, (int, int, int)>(StringComparer.Ordinal)
        {
            ["old-man-back"] = (64, 256, 64),["weedle-front-normal"] = (64, 64, 64),["weedle-front-shiny"] = (64, 64, 64),["teachy-tv-icon"] = (24, 24, 24),["poke-ball"] = (16, 48, 16)
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static OldManArtDocument Load(string json)
    {
        if (json is null )
            throw new InvalidDataException("Old Man art catalog is missing.");
        var doc = ContentJson.ParseStrict<OldManArtDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Assets.Length != 5 || doc.Assets.Select(a => a.Id).Distinct().Count() != 5
            || doc.TeachyTv.Id != "pret:ITEM_TEACHY_TV" || doc.TeachyTv.SourceSymbol != "ITEM_TEACHY_TV"
            || doc.TeachyTv.Icon != "teachy-tv-icon" || doc.TeachyTv.FieldUseFunction != "FieldUseFunc_TeachyTv"
            || doc.PaletteExpansion != "(rgb5 << 3) | (rgb5 >> 2)" || doc.BallAnimations.Length != 3
            || !doc.OldManThrowFrames.SequenceEqual(new[] { new OldManThrowFrame(1, 24),new(2, 9),new(3, 24),new(0, 9) })
            || doc.Layout != new OldManArtLayout(80, 80, 240, -2, 176, 57, 40, 32, 17, 60, 91, 176, 24)
            || doc.BallTiming != new OldManBallTiming(1, 34, -40, 10, 28, 40, 4, 31, 31, 3, 40, 60, 95, 315))
            throw new InvalidDataException("Old Man art profile is incomplete.");
        foreach (var asset in doc.Assets) ValidateAsset(asset);
        return doc;
    }

    private static void ValidateAsset(OldManArtAsset asset)
    {
        if (!Expected.TryGetValue(asset.Id, out var expected) || asset.TexturePath != asset.Id + ".png"
            || asset.Width != expected.Width || asset.Height != expected.Height || asset.FrameWidth != expected.Width
            || asset.FrameHeight != expected.FrameHeight || asset.FrameCount != expected.Height / expected.FrameHeight
            || asset.FrameLayout != "vertical"             || asset.TransparentIndex != 0 || asset.PaletteRgb555.Length != 16
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
        return bytes;
    }

}
