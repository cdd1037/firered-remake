using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;
using Game.Domain.Pokemon;

public partial class SynthSourceAudioPlayer
{
    /// <summary>Bind one missing source cue after ordinary current audio has loaded. No sample pack is changed.</summary>
    public void RegisterGameCornerCues(GameCornerOwnedPokemonCatalog owned, string root, string? inheritedRoot = null)
    {
        if (!ReferenceEquals(GameCornerOwnedPokemonCatalog.For(owned.Catalog), owned))
            throw new InvalidDataException("Game Corner audio requires the authenticated owned catalog.");
        var art = PretGameCornerPresentation.Load(root, inheritedRoot);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        foreach (var row in art.Manifest.GetProperty("audio").GetProperty("cues").EnumerateArray().Where(row => row.GetProperty("inherited").GetBoolean()))
        {
            var expected = row.GetProperty("cue").Deserialize<SynthCue>(options)
                ?? throw new InvalidDataException("Game Corner inherited source cue missing.");
            if (!_catalog.TryGetValue(expected.Id, out var existing) || existing != expected)
                throw new InvalidDataException("Game Corner predecessor cry binding differs.");
        }
        var extension = JsonSerializer.Deserialize<SynthManifest>(art.Files[PretGameCornerPresentation.Base + "synth-manifest.json"], options)
            ?? throw new InvalidDataException("Game Corner synth extension missing.");
        if (extension.SchemaVersion != 1 || extension.Cues is not [var normal] ||
            normal.Id != "CRY_DRATINI_NORMAL" || normal.Species != 147 || normal.CryMode != 0 || normal.SourcePlayer != 4 || normal.Priority != 10 ||
            normal.SourceSymbol != "SPECIES_DRATINI" || normal.SourcePath != "sound/direct_sound_samples/cries/dratini.wav" || normal.SongAddress != 0 || normal.Looping || normal.SourceCounter is not null ||
            _catalog.ContainsKey(normal.Id))
            throw new InvalidDataException("Game Corner Dratini source cue binding differs.");
        _catalog.Add(normal.Id, normal);
    }
}
