using System.Collections.Immutable;
namespace Game.Content;
/// <summary>Source fanfare task counters only. This does not implement or verify audible playback.</summary>
public sealed record PretPresentationCues(ImmutableSortedDictionary<string,int> FanfareCounters)
{
    public static PretPresentationCues Load(string root)
    {
        using var stream = File.OpenRead(Path.Combine(root, "content/generated/presentation-cues/fanfares.json"));
        var counters = System.Text.Json.JsonSerializer.Deserialize<SortedDictionary<string, int>>(stream)
            ?? throw new InvalidDataException("Fanfare counters are missing.");
        return new(counters.ToImmutableSortedDictionary(StringComparer.Ordinal));
    }
    // PlayFanfare's original fallback is the first table entry, MUS_LEVEL_UP, not a silent zero wait.
    public int CounterFor(string cue)=>FanfareCounters.GetValueOrDefault(cue,FanfareCounters["MUS_LEVEL_UP"]);
}
