using System.Collections.Immutable;
namespace Game.Content;

public sealed record SilphDoorMetatile(string Symbol, int MetatileId, uint RawAttributes, int X, int Y, int Width, int Height);
public sealed record SilphDoorLayer(string Layer, string TexturePath, int Width, int Height);
public sealed record SilphDoorArtDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<SilphDoorMetatile> Tiles,
    ImmutableArray<SilphDoorLayer> Layers, ImmutableArray<string> Limits,
    ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>Original three-layer Card Key gate tiles; authoritative door state is owned by Application.</summary>
public sealed class PretSilphDoorArt
{
    public const string GeneratedDirectory = "content/generated/silph-door-art/";
    public SilphDoorArtDocument Document { get; }
    public ImmutableArray<SilphDoorMetatile> Tiles => Document.Tiles;
    public ImmutableArray<SilphDoorLayer> Layers => Document.Layers;
    public ImmutableDictionary<string, byte[]> Files { get; }
    private PretSilphDoorArt(SilphDoorArtDocument doc, ImmutableDictionary<string, byte[]> files) { Document = doc; Files = files; }
    public static PretSilphDoorArt Load(string root)
    {
        _ = PretSilphPresentationSource.Load(root);
        var doc = SafariSource.Parse<SilphDoorArtDocument>(SafariSource.Read(root,GeneratedDirectory + "manifest.json"));
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Tiles.Length == 16 && doc.Layers.Length == 3 && !doc.Limits.IsDefaultOrEmpty,
            "Silph door art scope differs.");
        var files = PretSilphPresentationSource.Outputs(root, GeneratedDirectory, doc.Outputs);
        SafariSource.Require(doc.Tiles.Select(t => t.Symbol).Distinct().Count() == 16 && doc.Tiles.Select(t => t.MetatileId).Distinct().Count() == 16 &&
            doc.Tiles.Select(t => t.X).SequenceEqual(Enumerable.Range(0, 16).Select(i => i * 16)) &&
            doc.Tiles.All(t => t.Symbol.StartsWith("METATILE_SilphCo_", StringComparison.Ordinal) && t.Y == 0 && t.Width == 16 && t.Height == 16) &&
            doc.Layers.Select(l => l.Layer).SequenceEqual(new[] { "bottom","middle","top" }), "Silph door tile identities differ.");
        foreach (var layer in doc.Layers)
            SafariSource.Require(layer.Width == 256 && layer.Height == 16 && layer.TexturePath == "card-key-" + layer.Layer + ".png" &&
                layer.TexturePath == "card-key-" + layer.Layer + ".png" && PngRgba8.Decode(files[layer.TexturePath], layer.Width, layer.Height).Length == 256 * 16 * 4 , "Silph door layer pixels differ.");
        return new(doc, files);
    }
    public byte[] ReadTexture(string projectRelativePath) => PretSilphPresentationSource.Texture(projectRelativePath, GeneratedDirectory, Files);
}
