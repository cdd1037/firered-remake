using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;
using Game.Domain.Pokemon;

public partial class SynthSourceAudioPlayer
{
    /// <summary>Verify four existing original normal cries after all predecessor cues are loaded. No new cue or sample is added.</summary>
    public void VerifyOrdinaryEvolutionPresentationCues(GamePokemonCatalog owned, string root, string? inheritedRoot = null)
    {
        var art = PretOrdinaryEvolutionPresentation.Load(root, inheritedRoot);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        foreach (var row in art.Manifest.GetProperty("audio").GetProperty("cues").EnumerateArray())
        {
            var expected = row.GetProperty("cue").Deserialize<SynthCue>(options)
                ?? throw new InvalidDataException("Ordinary evolution inherited source cry missing.");
            if (!_catalog.TryGetValue(expected.Id, out var existing) || existing != expected)
                throw new InvalidDataException("Ordinary evolution predecessor cry binding differs.");
        }
    }
}
