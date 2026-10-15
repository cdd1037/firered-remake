using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;
using Game.Domain.Pokemon;

public partial class SynthSourceAudioPlayer
{
    /// <summary>Bind two original opponent cries; existing sample pack and owned admission stay unchanged.</summary>
    public void RegisterMemorialPillarCues(MemorialPillarOpponentPokemonCatalog opponents, string root, string? inheritedRoot = null)
    {
        if (!ReferenceEquals(MemorialPillarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !ReferenceEquals(OrdinaryEvolutionOwnedPokemonCatalog.For(opponents.Owned.Catalog), opponents.Owned))
            throw new InvalidDataException("Memorial Pillar audio requires authenticated ordinary opponents.");
        var art = PretMemorialPillarPresentation.Load(root, inheritedRoot);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretMemorialPillarPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Memorial Pillar synth extension missing.");
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.Select(c => c.Species).SequenceEqual(new[] { 163, 164 }))
            throw new InvalidDataException("Memorial Pillar normal cry inventory differs.");
        foreach (var cue in extension.Cues)
        {
            string name = art.DexEntries[cue.Species].Name;
            var id = new Game.Domain.Ids.SpeciesId("pret:SPECIES_" + name);
            if (cue.Id != "CRY_" + name + "_NORMAL" || cue.CryMode != 0 || cue.SourcePlayer != 4 || cue.Priority != 10 ||
                cue.SourceSymbol != "SPECIES_" + name || cue.SourcePath != "sound/direct_sound_samples/cries/" + name.ToLowerInvariant() + ".wav" || cue.SongAddress != 0 || cue.Looping || cue.SourceCounter is not null ||
                _catalog.ContainsKey(cue.Id) || !opponents.Catalog.Species.TryGetValue(id, out var species) || species.SourceSpeciesId != cue.Species ||
                opponents.Owned.Catalog.Species.ContainsKey(id))
                throw new InvalidDataException("Memorial Pillar source cry binding differs.");
        }
        foreach (var cue in extension.Cues) _catalog.Add(cue.Id, cue);
    }
}
