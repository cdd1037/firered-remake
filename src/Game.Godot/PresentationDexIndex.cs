using Game.Content;

/// <summary>Scene-lifetime exact presentation bindings. Readers retain encoded assets, never decoded images.</summary>
internal sealed class PresentationDexIndex
{
    private readonly Dictionary<int, PokedexMenuEntry> _entries = [];
    private readonly Dictionary<int, CaptureDexEntry> _captureEntries = [];
    private readonly Dictionary<PokedexMenuTexture, Func<byte[]>> _textures = [];

    internal void AddPackage(PokemonPresentationPackage source)
    {
        foreach (var entry in source.DexEntries.Values)
            Add(entry, source.CaptureDexEntries[entry.SourceSpeciesId],
                () => source.ReadTexture(entry.Portrait!.TexturePath),
                () => source.ReadTexture(entry.Footprint!.TexturePath));
    }

    internal void AddEntries(IEnumerable<PokedexMenuEntry> entries, IReadOnlyDictionary<string, byte[]> files)
    {
        foreach (var entry in entries)
        {
            var portrait = entry.Portrait ?? throw new InvalidDataException("Dex portrait missing.");
            var footprint = entry.Footprint ?? throw new InvalidDataException("Dex footprint missing.");
            Add(entry, new(entry.SourceSpeciesId, entry.NationalNumber, entry.Name, entry.Category,
                entry.DisplayCategory, entry.HeightDecimeters, entry.WeightHectograms, entry.HeightDisplay,
                entry.WeightDisplay, entry.Description, footprint.TexturePath, portrait.TexturePath, entry.HabitatTitle),
                () => files[portrait.TexturePath], () => files[footprint.TexturePath]);
        }
    }

    internal void Add(PokedexMenuEntry entry, CaptureDexEntry capture, Func<byte[]> portrait, Func<byte[]> footprint)
    {
        _entries[entry.SourceSpeciesId] = entry;
        _captureEntries[entry.SourceSpeciesId] = capture;
        _textures[entry.Portrait ?? throw new InvalidDataException("Dex portrait missing.")] = portrait;
        _textures[entry.Footprint ?? throw new InvalidDataException("Dex footprint missing.")] = footprint;
    }

    internal PokedexMenuPage? Present(int species, PokedexMenuPage page) => Entry(species) is { } entry
        ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    internal PokedexMenuEntry? Entry(int species) => _entries.GetValueOrDefault(species);
    internal CaptureDexEntry? CaptureEntry(int species) => _captureEntries.GetValueOrDefault(species);
    internal byte[]? ReadTexture(PokedexMenuTexture texture) => _textures.TryGetValue(texture, out var read) ? read() : null;
    internal void Clear() { _entries.Clear(); _captureEntries.Clear(); _textures.Clear(); }
}
