using System.Collections.Immutable;
using System.Text;

namespace Game.Content;

public sealed record CenterHealingPoint(int X, int Y);
public sealed record CenterHealingFrame(int Frame, int Ticks);
public sealed record CenterHealingTiming(int BallSpacingTicks, int LastBallPauseTicks, int FlashPhaseTicks,
    int FirstFlashCycles, int LastFlashTicks, int AfterFlashTicks, int FanfareCounterTicks, int ActiveTicksBase);
public sealed record CenterHealingAsset(string Id, string TexturePath, int Width, int Height, int FrameWidth, int FrameHeight, int FrameCount,
    int PaletteCount);
public sealed record CenterHealingArtDocument(int SchemaVersion, ImmutableArray<CenterHealingAsset> Assets, CenterHealingPoint FirstBall,
    ImmutableArray<CenterHealingPoint> BallOffsets, CenterHealingPoint Monitor,
    ImmutableArray<CenterHealingFrame> MonitorFrames, int MonitorLoops,
    ImmutableArray<string> PaletteVariants, CenterHealingTiming Timing);

/// <summary>Locked source sprites, palette phases and animation metadata; no healing business logic.</summary>
public static class PretCenterHealingArt
{
    public const string Profile = "firered-center-healing-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static CenterHealingArtDocument Load(string json)
    {
        if (json is null )
            throw new InvalidDataException("Healing art catalog checksum differs; run the verified importer.");
        var doc = ContentJson.ParseStrict<CenterHealingArtDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Assets.Length != 2
            || doc.BallOffsets.Length != 6 || doc.MonitorLoops != 4 || doc.PaletteVariants.Length != 9
            || doc.Timing != new CenterHealingTiming(25, 32, 8, 3, 24, 30, 160, 195))
            throw new InvalidDataException("Healing art profile incomplete.");
        return doc;
    }

    public static byte[] ReadTexture(string directory, CenterHealingAsset asset)
    {
        if (asset.TexturePath != asset.Id + ".png" || asset.TexturePath.Contains('/') || asset.TexturePath.Contains('\\')
            || asset.TexturePath.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe healing texture path.");
        var path = Path.Combine(directory, asset.TexturePath);
        if (new DirectoryInfo(directory).LinkTarget is not null || new FileInfo(path).LinkTarget is not null)
            throw new InvalidDataException("Healing texture symlink rejected.");
        var bytes = File.ReadAllBytes(path);
        return bytes;
    }
}
