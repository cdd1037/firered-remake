using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

public sealed record SilphScrollArrow(string Id, int ArrowType, int SourceTileOffset, bool FlipV, int Width, int Height,
    string TexturePath, string RgbaPath, string Sha256, string RgbaSha256, ImmutableArray<int> BounceOffsets);
public sealed record SilphInterfaceDocument(int SchemaVersion, string Profile, string SourceCommit, string LockSha256,
    ImmutableArray<SilphScrollArrow> Arrows, ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>The source elevator list's actual scroll sprites, with original integer sine bounce.</summary>
public sealed class PretSilphInterface
{
    public const string LockSha256 = "2411fd190ceff5a693f3bc30607846812aec92b9b77a7fd6cd35bf64dd94a904";
    public const string ManifestSha256 = "8ac4b92100dcac03e1406f88a890b6f19c491ba8249f172883a393476d3f3308";
    public const string GeneratedDirectory = "content/generated/silph-interface/";
    public SilphInterfaceDocument Document { get; }
    public ImmutableDictionary<string, byte[]> Files { get; }
    public SilphScrollArrow Up => Document.Arrows[0];
    public SilphScrollArrow Down => Document.Arrows[1];
    public ImmutableArray<string> AdditionalSourceHashes => ["silph-interface-lock:" + LockSha256, "silph-interface-manifest:" + ManifestSha256];
    private PretSilphInterface(SilphInterfaceDocument doc, ImmutableDictionary<string, byte[]> files) { Document = doc; Files = files; }
    public static PretSilphInterface Load(string root)
    {
        var bytes = SafariSource.Read(root, "scripts/silph-interface.lock.json", LockSha256, 1140);
        using var json = JsonDocument.Parse(bytes); var source = json.RootElement;
        SafariSource.Require(source.GetProperty("schemaVersion").GetInt32() == 1 && source.GetProperty("repository").GetString() == "pret/pokefirered" &&
            source.GetProperty("profile").GetString() == "firered-silph-interface-v1" && source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("tree").GetString() == "7515cd4067ea55f8cdbc73624478afd2736c58b9" && source.GetProperty("files").GetArrayLength() == 5, "Silph interface source inventory differs.");
        foreach (var row in source.GetProperty("files").EnumerateArray())
        {
            var raw = SafariSource.Read(root, "content/raw/silph-interface/" + row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            SafariSource.Require(SafariSource.GitBlob(raw) == row.GetProperty("gitBlobSha1").GetString(), "Silph interface Git blob differs.");
        }
        var doc = SafariSource.Parse<SilphInterfaceDocument>(SafariSource.Read(root, GeneratedDirectory + "manifest.json", ManifestSha256, 1503));
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Profile == "firered-silph-interface-v1" && doc.SourceCommit == PretWorldMap.SourceCommit &&
            doc.LockSha256 == LockSha256 && doc.Arrows.Length == 2 && doc.Outputs.Length == 4, "Silph interface manifest differs.");
        var files = PretSilphPresentationSource.Outputs(root, GeneratedDirectory, doc.Outputs);
        for (int i = 0; i < 2; i++)
        {
            var arrow = doc.Arrows[i];
            SafariSource.Require(arrow.Id == (i == 0 ? "scroll-up" : "scroll-down") && arrow.ArrowType == i + 2 && arrow.SourceTileOffset == 4 && arrow.FlipV == (i == 1) &&
                arrow.Width == 16 && arrow.Height == 16 && arrow.TexturePath == arrow.Id + ".png" && arrow.RgbaPath == arrow.Id + ".rgba" &&
                files[arrow.RgbaPath].Length == 1024 && SafariSource.Hash(files[arrow.RgbaPath]) == arrow.RgbaSha256 && SafariSource.Hash(files[arrow.TexturePath]) == arrow.Sha256 &&
                arrow.BounceOffsets.Length == 32 && arrow.BounceOffsets.All(v => v is >= -2 and <= 2) && arrow.BounceOffsets.Min() == -2 && arrow.BounceOffsets.Max() == 2,
                "Silph scroll arrow pixels or movement differ.");
        }
        return new(doc, files);
    }
    /// <summary>Tick zero initializes the sprite; tick one samples sine phase zero, as in the source callback.</summary>
    public int BounceOffset(bool down, int sourceTick)
    {
        if (sourceTick < 0) throw new ArgumentOutOfRangeException(nameof(sourceTick));
        return (down ? Down : Up).BounceOffsets[Math.Max(0, sourceTick - 1) % 32];
    }
    public byte[] ReadTexture(string projectRelativePath) => PretSilphPresentationSource.Texture(projectRelativePath, GeneratedDirectory, Files);
}
