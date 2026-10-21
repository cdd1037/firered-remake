using System.Collections.Immutable;

namespace Game.Content;

public sealed record CinnabarItemArtDocument(int SchemaVersion, ImmutableArray<ItemIcon> Icons, string PaletteExpansion, int TransparentIndex);

/// <summary>Four Cinnabar inventory icons referencing the shared item texture page.</summary>
public sealed class PretCinnabarItemArt
{
    public const string GeneratedDirectory = "content/generated/data/services/cinnabar-item-art/";
    public CinnabarItemArtDocument Document { get; }
    public ImmutableArray<ItemIcon> Icons => Document.Icons;
    public ImmutableDictionary<string, byte[]> Files { get; }
    private PretCinnabarItemArt(CinnabarItemArtDocument document, ImmutableDictionary<string, byte[]> files)
    {
        Document = document; Files = files;
    }

    public static PretCinnabarItemArt LoadProject(string root)
    {
        var files = PretSilphPresentationSource.LoadGenerated(root, GeneratedDirectory);
        var document = SafariSource.Parse<CinnabarItemArtDocument>(files["icons.json"]);
        SilphSourcePackage.Need(document.SchemaVersion == 1 && document.PaletteExpansion == "(rgb5 << 3) | (rgb5 >> 2)" &&
            document.TransparentIndex == 0 && document.Icons.Select(i => i.Item).SequenceEqual(
                [PretCinnabarItems.SecretKeyId, PretCinnabarItems.Tm14Id, PretCinnabarItems.Tm22Id, PretCinnabarItems.Tm38Id]),
            "Cinnabar item art scope differs.");
        foreach (var icon in document.Icons)
        {
            SilphSourcePackage.Need(icon.Width == 24 && icon.Height == 24 &&
                icon.Region is not null &&
                files.ContainsKey(icon.PngPath),
                "Cinnabar item icon identity differs.");
            ItemIconAssets.Validate(icon);
        }
        return new(document, files);
    }

}
