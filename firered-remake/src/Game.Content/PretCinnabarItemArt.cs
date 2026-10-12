using System.Collections.Immutable;

namespace Game.Content;

public sealed record CinnabarItemArtDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineIcon> Icons, string PaletteExpansion, int TransparentIndex);

/// <summary>Only the four new Cinnabar item icons; historical item pixels remain owned by their original packages.</summary>
public sealed class PretCinnabarItemArt
{
    public const string ManifestSha256 = "dd04171631d4758c504da254b11516b55b4c4e0e1befa7c57094d3f5c364b9fe";
    public const string LockSha256 = "721a165d868df5d9094ea64aad31f6c6c7211f0ffe055886b33d02996827e81e";
    public const string GeneratedDirectory = "content/generated/cinnabar-item-art/";
    public CinnabarItemArtDocument Document { get; }
    public ImmutableArray<PretPewterMachineIcon> Icons => Document.Icons;
    public ImmutableDictionary<string, byte[]> Files { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretCinnabarItemArt(CinnabarItemArtDocument document, SilphSourcePackage source)
    {
        Document = document; Files = source.Outputs;
        AdditionalSourceHashes = ["cinnabar-item-art-manifest:" + ManifestSha256, "cinnabar-item-art-lock:" + LockSha256,
            .. source.Files.Select(f => "cinnabar-item-art-source:" + f.Path + ":" + f.Sha256)];
    }

    public static PretCinnabarItemArt LoadProject(string root)
    {
        var source = new SilphSourcePackage(root, "cinnabar-item-art", "firered-cinnabar-item-art-source-v1",
            "scripts/cinnabar-item-art.lock.json", LockSha256, 2644, ManifestSha256, 1339, 8, 5);
        var document = source.Parse<CinnabarItemArtDocument>("icons.json");
        SilphSourcePackage.Need(document.SchemaVersion == 1 && document.Profile == "firered-cinnabar-item-art-v1" &&
            document.SourceCommit == SilphSourcePackage.SourceCommit && document.PaletteExpansion == "(rgb5 << 3) | (rgb5 >> 2)" &&
            document.TransparentIndex == 0 && document.Icons.Select(i => i.Item).SequenceEqual(
                [PretCinnabarItems.SecretKeyId, PretCinnabarItems.Tm14Id, PretCinnabarItems.Tm22Id, PretCinnabarItems.Tm38Id]),
            "Cinnabar item art scope differs.");
        foreach (var icon in document.Icons)
        {
            SilphSourcePackage.Need(icon.Width == 24 && icon.Height == 24 &&
                icon.PngPath == icon.Item.Value[5..].ToLowerInvariant() + ".png" &&
                source.Outputs.TryGetValue(icon.PngPath, out var png) && SafariSource.Hash(png) == icon.PngSha256 &&
                source.Files.Any(f => f.Path == icon.Source.Path && f.Sha256 == icon.Source.Sha256) &&
                icon.Source.StartLine == 1 && icon.Source.EndLine == 1 && !string.IsNullOrWhiteSpace(icon.Source.Symbol),
                "Cinnabar item icon identity differs.");
            source.Reference(icon.PaletteSource); source.Reference(icon.BindingSource);
            _ = PretPewterMachines.IconRgba(icon);
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
