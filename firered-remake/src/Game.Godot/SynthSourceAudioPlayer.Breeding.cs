using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;
using Game.Domain.Pokemon;

public partial class SynthSourceAudioPlayer
{
    /// <summary>Bind six missing normal cries after current and Game Corner audio. The shared source sample pack stays unchanged.</summary>
    public void RegisterBreedingCues(GamePokemonCatalog owned, string root, string? inheritedRoot = null)
    {
        var art = PretBreedingPresentation.Load(root, inheritedRoot);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        foreach (var row in art.Manifest.GetProperty("audio").GetProperty("cues").EnumerateArray().Where(row => row.GetProperty("inherited").GetBoolean()))
        {
            var expected = row.GetProperty("cue").Deserialize<SynthCue>(options)
                ?? throw new InvalidDataException("Breeding inherited source cue missing.");
            if (!_catalog.TryGetValue(expected.Id, out var existing) || existing != expected)
                throw new InvalidDataException("Breeding predecessor cry binding differs.");
        }
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretBreedingPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Breeding synth extension missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Species).SequenceEqual(new[] { 172, 173, 174, 236, 239, 360 }))
            throw new InvalidDataException("Breeding source cue inventory differs.");
        foreach (var normal in extension.Cues)
        {
            string name = art.DexEntries[normal.Species].Name;
            if (normal.Id != "CRY_" + name + "_NORMAL" || normal.CryMode != 0 || normal.SourcePlayer != 4 || normal.Priority != 10 ||
                normal.SourceSymbol != "SPECIES_" + name || normal.SongAddress != 0 || normal.Looping || normal.SourceCounter is not null ||
                _catalog.ContainsKey(normal.Id))
                throw new InvalidDataException("Breeding normal source cue binding differs.");
        }
        foreach (var normal in extension.Cues) _catalog.Add(normal.Id, normal);
    }
}
