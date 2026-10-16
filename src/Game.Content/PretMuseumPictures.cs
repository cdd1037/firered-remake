using System.Collections.Immutable;

namespace Game.Content;

public sealed record MuseumPictureAsset(string Id, int SourceSpeciesId, int Width, int Height, string TexturePath);
public sealed record MuseumPictureDocument(int SchemaVersion, ImmutableArray<MuseumPictureAsset> Assets);

/// <summary>Generated, named script pictures. Original inputs belong only to the importer.</summary>
public sealed class PretMuseumPictures
{
    public const string Base = "content/generated/ui/museum-pictures/";
    public ImmutableDictionary<string, MuseumPictureAsset> Assets { get; }
    private readonly ImmutableDictionary<string, byte[]> textures;

    private PretMuseumPictures(ImmutableDictionary<string, MuseumPictureAsset> assets,
        ImmutableDictionary<string, byte[]> textures) => (Assets, this.textures) = (assets, textures);

    public MuseumPictureAsset Asset(string id) => Assets.TryGetValue(id, out var asset) ? asset :
        throw new InvalidDataException("Unknown named picture: " + id);
    public byte[] ReadTexture(string id) => textures.TryGetValue(id, out var bytes) ? bytes :
        throw new InvalidDataException("Unknown named picture: " + id);

    public static PretMuseumPictures Load(string root)
    {
        var document = ContentJson.ParseStrict<MuseumPictureDocument>(
            GeneratedResourceFile.Read(root, Base + "pictures.json"), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Assets.IsDefaultOrEmpty)
            throw new InvalidDataException("Museum picture schema or assets missing.");
        var assets = ImmutableDictionary.CreateBuilder<string, MuseumPictureAsset>(StringComparer.Ordinal);
        var textures = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        var species = new HashSet<int>();
        foreach (var asset in document.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Id) || asset.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
                asset.SourceSpeciesId <= 0 || !species.Add(asset.SourceSpeciesId) ||
                asset.Width != 64 || asset.Height != 64 || asset.TexturePath != asset.Id + ".png" ||
                !assets.TryAdd(asset.Id, asset))
                throw new InvalidDataException("Museum picture asset identity or dimensions invalid.");
            textures.Add(asset.Id, GeneratedResourceFile.Read(root, Base + asset.TexturePath));
        }
        if (!assets.ContainsKey("aerodactyl-fossil") || !assets.ContainsKey("kabutops-fossil"))
            throw new InvalidDataException("Museum fossil picture is missing.");
        return new(assets.ToImmutable(), textures.ToImmutable());
    }
}
