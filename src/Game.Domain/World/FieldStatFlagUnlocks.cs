using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

public sealed record FieldStatFlagUnlock(string Stat, int Minimum, string Flag);

/// <summary>Monotonic stat rewards clear original object hide flags; no duplicate progress is saved.</summary>
public sealed class FieldStatFlagUnlocks
{
    public MapId Map { get; }
    public string Special { get; }
    public ImmutableArray<FieldStatFlagUnlock> Rules { get; }

    public FieldStatFlagUnlocks(MapId map, string special, ImmutableArray<FieldStatFlagUnlock> rules)
    {
        if (string.IsNullOrWhiteSpace(map.Value) || string.IsNullOrWhiteSpace(special) || rules.IsDefaultOrEmpty ||
            rules.Any(r => r is null || string.IsNullOrWhiteSpace(r.Stat) || string.IsNullOrWhiteSpace(r.Flag) || !r.Stat.StartsWith("GAME_STAT_", StringComparison.Ordinal) || r.Minimum <= 0 ||
                !r.Flag.StartsWith("FLAG_", StringComparison.Ordinal)) || rules.Select(r => r.Flag).Distinct().Count() != rules.Length)
            throw new ArgumentException("Invalid stat flag unlock definition.");
        Map = map; Special = special; Rules = rules;
    }

    public ImmutableSortedDictionary<string, bool> Apply(ImmutableSortedDictionary<string, bool> flags,
        IReadOnlyDictionary<string, int>? stats)
    {
        foreach (var rule in Rules)
            if (stats is not null && stats.GetValueOrDefault(rule.Stat) >= rule.Minimum)
                flags = flags.SetItem(rule.Flag, false);
        return flags;
    }
}
