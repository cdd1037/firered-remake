using System.Collections.Immutable;
namespace Game.Content;

public sealed record SilphItemArtDocument(int SchemaVersion, string Profile, string SourceCommit, string LockSha256,
    ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>All 23 distinct Silph pickup/reward source icons. Metadata and item effects remain separate.</summary>
public sealed class PretSilphItemArt
{
    public const string ManifestSha256 = "322f8309a2ad7d37334717895c0c0cdf80ade9123758cc3311bcab0beba5acde";
    public const string GeneratedDirectory = "content/generated/silph-item-art/";
    public SilphItemArtDocument Document { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons => Document.Icons;
    public ImmutableDictionary<string, byte[]> Files { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretSilphItemArt(SilphItemArtDocument document, ImmutableDictionary<string, byte[]> files)
    {
        Document = document; Files = files;
        AdditionalSourceHashes = ["silph-item-art-manifest:" + ManifestSha256, "silph-presentation-lock:" + PretSilphPresentationSource.LockSha256,
            .. document.Outputs.Select(o => "silph-item-art:" + o.Path + ":" + o.Sha256)];
    }
    public static PretSilphItemArt Load(string root)
    {
        var sources = PretSilphPresentationSource.Load(root);
        var doc = SafariSource.Parse<SilphItemArtDocument>(SafariSource.Read(root, GeneratedDirectory + "manifest.json", ManifestSha256, 93052));
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Profile == "firered-silph-item-art-v1" && doc.SourceCommit == PretWorldMap.SourceCommit &&
            doc.LockSha256 == PretSilphPresentationSource.LockSha256 && doc.Icons.Length == 23 && doc.Outputs.Length == 23, "Silph item art scope differs.");
        var expected = "CARD_KEY MASTER_BALL ULTRA_BALL TM01 TM08 TM41 HYPER_POTION MAX_REVIVE ESCAPE_ROPE FULL_HEAL PROTEIN HP_UP X_SPECIAL CALCIUM CARBOS RARE_CANDY IRON ZINC ELIXIR PP_UP NUGGET MAX_POTION REVIVE".Split(' ').Select(s => "pret:ITEM_" + s).ToHashSet(StringComparer.Ordinal);
        var files = PretSilphPresentationSource.Outputs(root, GeneratedDirectory, doc.Outputs);
        void Source(Game.Domain.Items.OpeningItemSourceRef r) => SafariSource.Require(sources.TryGetValue(r.Path, out var b) && SafariSource.Hash(b) == r.Sha256 && r.StartLine > 0 && r.EndLine >= r.StartLine && !string.IsNullOrWhiteSpace(r.Symbol), "Silph item art provenance differs.");
        foreach (var icon in doc.Icons)
        {
            SafariSource.Require(expected.Remove(icon.Item.Value) && icon.Width == 24 && icon.Height == 24 &&
                icon.PngPath == icon.Item.Value[5..].ToLowerInvariant() + ".png" && SafariSource.Hash(files[icon.PngPath]) == icon.PngSha256, "Silph item icon differs.");
            _ = PretPewterMachines.IconRgba(icon); Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
        }
        SafariSource.Require(expected.Count == 0, "Missing Silph item icon.");
        return new(doc, files);
    }
    public byte[] ReadTexture(string projectRelativePath) => PretSilphPresentationSource.Texture(projectRelativePath, GeneratedDirectory, Files);
}
