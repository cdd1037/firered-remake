using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

public sealed record CenterHealingPoint(int X, int Y);
public sealed record CenterHealingFrame(int Frame, int Ticks);
public sealed record CenterHealingTiming(int BallSpacingTicks, int LastBallPauseTicks, int FlashPhaseTicks,
    int FirstFlashCycles, int LastFlashTicks, int AfterFlashTicks, int FanfareCounterTicks, int ActiveTicksBase);
public sealed record CenterHealingAsset(string Id, string TexturePath, int Width, int Height,
    int FrameWidth, int FrameHeight, int FrameCount, int PaletteCount, string Sha256, string RgbaSha256);
public sealed record CenterHealingArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    string FullSourceHash, ImmutableArray<PretQuestItemInput> Inputs, ImmutableArray<CenterHealingAsset> Assets,
    CenterHealingPoint FirstBall, ImmutableArray<CenterHealingPoint> BallOffsets, CenterHealingPoint Monitor,
    ImmutableArray<CenterHealingFrame> MonitorFrames, int MonitorLoops, ImmutableArray<string> PaletteVariants,
    CenterHealingTiming Timing, ImmutableArray<string> Limitations);

/// <summary>Locked source sprites, palette phases and animation metadata; no healing business logic.</summary>
public static class PretCenterHealingArt
{
    public const string Profile = "firered-center-healing-art-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string GeneratedSha256 = "fb2aa421fa7da0e2e85cf81d5589f2cdfd32da84d3b449b381352ca1c565aac1";
    public const string SourceManifestSha256 = "4252e3e1d6878ccc056425623a94d21327c51b4c362d7ea9beb559f0d23b11cf";

    public static CenterHealingArtDocument Load(string json)
    {
        if (json is null || Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) != GeneratedSha256)
            throw new InvalidDataException("Healing art catalog checksum differs; run the verified importer.");
        var doc = ContentJson.ParseStrict<CenterHealingArtDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != SourceCommit
            || doc.FullSourceHash != SourceManifestSha256 || doc.Inputs.Length != 6 || doc.Assets.Length != 2
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
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != asset.Sha256)
            throw new InvalidDataException("Healing texture checksum differs: " + asset.Id);
        return bytes;
    }
}
