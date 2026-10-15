using System.Collections.Immutable;
namespace Game.Content;

public sealed record SilphItemArtDocument(int SchemaVersion, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>All 23 distinct Silph pickup/reward source icons. Metadata and item effects remain separate.</summary>
public sealed class PretSilphItemArt
{
    public const string GeneratedDirectory = "content/generated/data/services/silph-item-art.json";
    public SilphItemArtDocument Document { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons => Document.Icons;
    public ImmutableDictionary<string, byte[]> Files { get; }
    private PretSilphItemArt(SilphItemArtDocument document, ImmutableDictionary<string, byte[]> files)
    {
        Document = document; Files = files;
    }
    public static PretSilphItemArt Load(string root)
    {
        var doc = SafariSource.Parse<SilphItemArtDocument>(SafariSource.Read(root,GeneratedDirectory));
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Icons.Length == 23 , "Silph item art scope differs.");
        var expected = "CARD_KEY MASTER_BALL ULTRA_BALL TM01 TM08 TM41 HYPER_POTION MAX_REVIVE ESCAPE_ROPE FULL_HEAL PROTEIN HP_UP X_SPECIAL CALCIUM CARBOS RARE_CANDY IRON ZINC ELIXIR PP_UP NUGGET MAX_POTION REVIVE".Split(' ').Select(s => "pret:ITEM_" + s).ToHashSet(StringComparer.Ordinal);
        var files = doc.Outputs.ToImmutableDictionary(output => output.Path, output => SafariSource.Read(root, output.Path), StringComparer.Ordinal);
        foreach (var icon in doc.Icons)
        {
            SafariSource.Require(expected.Remove(icon.Item.Value) && icon.Width == 24 && icon.Height == 24 &&
                icon.PngPath == "content/generated/sprites/items.png" , "Silph item icon differs.");
            _ = PretPewterMachines.TexturePage(icon, files[icon.PngPath]);         }
        SafariSource.Require(expected.Count == 0, "Missing Silph item icon.");
        return new(doc, files);
    }
    public byte[] ReadTexture(string projectRelativePath) => Files[projectRelativePath];
}
