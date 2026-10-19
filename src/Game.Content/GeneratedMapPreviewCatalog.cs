using System.Collections.Immutable;

namespace Game.Content;

/// <summary>Row-major indices into source palette banks 13/14. Any low nibble zero selects the backdrop.</summary>
public sealed record MapPreviewArt(int Width, int Height, ImmutableArray<byte> Indices, ImmutableArray<ushort> Palette);
public sealed record MapPreviewEntry(string RegionSection, string Name, string VisitFlag, MapPreviewArt Art);

/// <summary>Immutable cave-type region previews; aliases share art and retain their source visit flag.</summary>
public sealed class GeneratedMapPreviewCatalog
{
    private sealed record ArtDocument(int Width, int Height, string Indices8Bpp, ImmutableArray<ushort> Palette);
    private sealed record EntryDocument(string RegionSection, string Name, string VisitFlag, int Art);
    private sealed record Document(int SchemaVersion, ImmutableArray<ArtDocument> Art, ImmutableArray<EntryDocument> Entries);
    private readonly ImmutableDictionary<string, MapPreviewEntry> _entries;
    public ImmutableArray<MapPreviewEntry> Entries { get; }

    private GeneratedMapPreviewCatalog(ImmutableArray<MapPreviewEntry> entries)
    {
        Entries = entries;
        _entries = entries.ToImmutableDictionary(entry => entry.RegionSection, StringComparer.Ordinal);
    }

    public MapPreviewEntry? Find(string regionSection) => _entries.GetValueOrDefault(regionSection);

    public static GeneratedMapPreviewCatalog Load(string generatedRoot)
    {
        var document = ContentJson.ParseStrict<Document>(
            GeneratedResourceFile.Read(generatedRoot, "maps/map-previews.json"), ContentJson.CreateOptions());
        static bool Symbol(string? value, string prefix) => value is not null && value.Length > prefix.Length &&
            value.Length <= 128 && value.StartsWith(prefix, StringComparison.Ordinal) &&
            value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
        if (document.SchemaVersion != 1 || document.Art.IsDefaultOrEmpty || document.Art.Length > 256 ||
            document.Entries.IsDefaultOrEmpty || document.Entries.Length > 256)
            throw new InvalidDataException("Invalid generated map preview catalog.");
        var art = ImmutableArray.CreateBuilder<MapPreviewArt>(document.Art.Length);
        foreach (var item in document.Art)
        {
            if (item is null || item.Width != 256 || item.Height != 160 || item.Palette.IsDefault ||
                item.Palette.Length != 32 || item.Palette.Any(color => color > 0x7fff) ||
                item.Indices8Bpp is null || item.Indices8Bpp.Length != ((256 * 160 + 2) / 3) * 4)
                throw new InvalidDataException("Invalid generated map preview art.");
            byte[] indices;
            try { indices = Convert.FromBase64String(item.Indices8Bpp); }
            catch (FormatException error) { throw new InvalidDataException("Invalid map preview index encoding.", error); }
            if (indices.Length != 256 * 160 || Convert.ToBase64String(indices) != item.Indices8Bpp || indices.Any(index => index >= 32))
                throw new InvalidDataException("Incomplete or noncanonical map preview indices.");
            art.Add(new(item.Width, item.Height, [.. indices], item.Palette));
        }
        var entries = ImmutableArray.CreateBuilder<MapPreviewEntry>(document.Entries.Length);
        var sections = new HashSet<string>(StringComparer.Ordinal);
        var usedArt = new HashSet<int>();
        foreach (var entry in document.Entries)
        {
            if (entry is null || !Symbol(entry.RegionSection, "MAPSEC_") || !Symbol(entry.VisitFlag, "FLAG_") ||
                string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 64 || entry.Name.Any(char.IsControl) ||
                entry.Art < 0 || entry.Art >= art.Count || !sections.Add(entry.RegionSection))
                throw new InvalidDataException("Invalid or duplicate generated map preview entry.");
            usedArt.Add(entry.Art);
            entries.Add(new(entry.RegionSection, entry.Name, entry.VisitFlag, art[entry.Art]));
        }
        if (usedArt.Count != art.Count) throw new InvalidDataException("Unused generated map preview art.");
        return new(entries.MoveToImmutable());
    }
}
