using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

public sealed record SilphScrollArrow(string Id, int ArrowType, int SourceTileOffset, bool FlipV, int Width, int Height, string TexturePath,
    ImmutableArray<int> BounceOffsets);
public sealed record SilphInterfaceDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<SilphScrollArrow> Arrows,
    ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>The source elevator list's actual scroll sprites, with original integer sine bounce.</summary>
public sealed class PretSilphInterface
{
    public const string GeneratedDirectory = "content/generated/silph-interface/";
    public SilphInterfaceDocument Document { get; }
    public ImmutableDictionary<string, byte[]> Files { get; }
    public SilphScrollArrow Up => Document.Arrows[0];
    public SilphScrollArrow Down => Document.Arrows[1];
    private PretSilphInterface(SilphInterfaceDocument doc, ImmutableDictionary<string, byte[]> files) { Document = doc; Files = files; }
    public static PretSilphInterface Load(string root)
    {
        var doc = SafariSource.Parse<SilphInterfaceDocument>(SafariSource.Read(root,GeneratedDirectory + "manifest.json"));
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Arrows.Length == 2 , "Silph interface manifest differs.");
        var files = PretSilphPresentationSource.Outputs(root, GeneratedDirectory, doc.Outputs);
        for (int i = 0; i < 2; i++)
        {
            var arrow = doc.Arrows[i];
            SafariSource.Require(arrow.Id == (i == 0 ? "scroll-up" : "scroll-down") && arrow.ArrowType == i + 2 && arrow.SourceTileOffset == 4 && arrow.FlipV == (i == 1) &&
                arrow.Width == 16 && arrow.Height == 16 && arrow.TexturePath == arrow.Id + ".png" &&
                PngRgba8.Decode(files[arrow.TexturePath], arrow.Width, arrow.Height).Length == 1024 &&
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
