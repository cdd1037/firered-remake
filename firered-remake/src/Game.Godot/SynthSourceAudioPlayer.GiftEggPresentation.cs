using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;
using Game.Domain.Pokemon;

public partial class SynthSourceAudioPlayer
{
    /// <summary>Bind two missing normal cries after current audio, preserving the existing Togepi cue. The shared source sample pack stays unchanged.</summary>
    public void RegisterGiftEggCues(GiftEggOwnedPokemonCatalog owned, string root, string? inheritedRoot = null)
    {
        if (!ReferenceEquals(GiftEggOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(LostCaveOwnedPokemonCatalog.For(owned.Predecessor.Catalog), owned.Predecessor))
            throw new InvalidDataException("GiftEgg audio requires the authenticated owned catalog and predecessor.");
        var art = PretGiftEggPresentation.Load(root, inheritedRoot);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        foreach (var row in art.Manifest.GetProperty("audio").GetProperty("cues").EnumerateArray().Where(row => row.GetProperty("inherited").GetBoolean()))
        {
            var expected = row.GetProperty("cue").Deserialize<SynthCue>(options)
                ?? throw new InvalidDataException("GiftEgg inherited source cue missing.");
            if (!_catalog.TryGetValue(expected.Id, out var existing) || existing != expected)
                throw new InvalidDataException("GiftEgg predecessor cry binding differs.");
        }
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretGiftEggPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("GiftEgg synth extension missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Species).SequenceEqual(new[] { 198, 176 }))
            throw new InvalidDataException("GiftEgg source cue inventory differs.");
        foreach (var normal in extension.Cues)
        {
            string name = art.DexEntries[normal.Species].Name;
            if (normal.Id != "CRY_" + name + "_NORMAL" || normal.CryMode != 0 || normal.SourcePlayer != 4 || normal.Priority != 10 ||
                normal.SourceSymbol != "SPECIES_" + name || normal.SourcePath != "sound/direct_sound_samples/cries/" + name.ToLowerInvariant() + ".wav" || normal.SongAddress != 0 || normal.Looping || normal.SourceCounter is not null ||
                _catalog.ContainsKey(normal.Id))
                throw new InvalidDataException("GiftEgg normal source cue binding differs.");
        }
        foreach (var normal in extension.Cues) _catalog.Add(normal.Id, normal);
    }
}
