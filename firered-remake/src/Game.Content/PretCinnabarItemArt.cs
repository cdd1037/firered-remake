using System.Collections.Immutable;

namespace Game.Content;

public sealed record CinnabarItemArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineIcon> Icons, string PaletteExpansion, int TransparentIndex);

/// <summary>Only the four new Cinnabar item icons; historical item pixels remain owned by their original packages.</summary>
public sealed class PretCinnabarItemArt
{
    public const string GeneratedDirectory = "content/generated/cinnabar-item-art/";
    public CinnabarItemArtDocument Document { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons => Document.Icons;
    public ImmutableDictionary<string, byte[]> Files { get; }
    private PretCinnabarItemArt(CinnabarItemArtDocument document, SilphSourcePackage source)
    {
        Document = document; Files = source.Outputs;
    }

    public static PretCinnabarItemArt LoadProject(string root)
    {
        var source = new SilphSourcePackage(root,"cinnabar-item-art",            "scripts/cinnabar-item-art.lock.json");
        var document = source.Parse<CinnabarItemArtDocument>("icons.json");
        SilphSourcePackage.Need(document.SchemaVersion == 1 && document.PaletteExpansion == "(rgb5 << 3) | (rgb5 >> 2)" &&
            document.TransparentIndex == 0 && document.Icons.Select(i => i.Item).SequenceEqual(
                [PretCinnabarItems.SecretKeyId, PretCinnabarItems.Tm14Id, PretCinnabarItems.Tm22Id, PretCinnabarItems.Tm38Id]),
            "Cinnabar item art scope differs.");
        foreach (var icon in document.Icons)
        {
            SilphSourcePackage.Need(icon.Width == 24 && icon.Height == 24 &&
                icon.PngPath == icon.Item.Value[5..].ToLowerInvariant() + ".png" &&
                source.Outputs.TryGetValue(icon.PngPath, out var png) &&
                icon.Source.StartLine == 1 && icon.Source.EndLine == 1 && !string.IsNullOrWhiteSpace(icon.Source.Symbol),
                "Cinnabar item icon identity differs.");
            _ = PretPewterMachines.IconRgba(icon, source.Outputs[icon.PngPath]);
        }
        return new(document, source);
    }

    public byte[] ReadTexture(string projectRelativePath)
    {
        if (!projectRelativePath.StartsWith(GeneratedDirectory, StringComparison.Ordinal) ||
            !Icons.Any(i => GeneratedDirectory + i.PngPath == projectRelativePath) ||
            !Files.TryGetValue(projectRelativePath[GeneratedDirectory.Length..], out var bytes))
            throw new InvalidDataException("Unknown Cinnabar item texture.");
        return [.. bytes];
    }
}
