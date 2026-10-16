using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

/// <summary>Source counters exposed to a field service; the original script owns award and repeat dialogue.</summary>
public sealed record FieldMilestone(string Stat, string CountVariable, string AwardVariable,
    int BragFlag, bool ClampCount, bool Available, ImmutableArray<int> MinimumCounts)
{
    public int ReadCount(IReadOnlyDictionary<string, int>? stats)
    {
        int count = Available ? stats?.GetValueOrDefault(Stat) ?? 0 : 0;
        return ClampCount ? Math.Min(count, ushort.MaxValue) : count & ushort.MaxValue;
    }
    public int Tier(IReadOnlyDictionary<string, int>? stats) => MinimumCounts.Count(minimum => ReadCount(stats) >= minimum);
}

public sealed class FieldMilestoneCollection
{
    public MapId Map { get; }
    public string Special { get; }
    public ImmutableArray<FieldMilestone> Milestones { get; }

    public FieldMilestoneCollection(MapId map, string special, ImmutableArray<FieldMilestone> milestones)
    {
        if (string.IsNullOrWhiteSpace(map.Value) || string.IsNullOrWhiteSpace(special) || milestones.IsDefaultOrEmpty ||
            milestones.Any(m => m is null || !m.Stat.StartsWith("GAME_STAT_", StringComparison.Ordinal) ||
                !m.CountVariable.StartsWith("VAR_", StringComparison.Ordinal) || !m.AwardVariable.StartsWith("VAR_", StringComparison.Ordinal) ||
                m.BragFlag <= 0 || (m.BragFlag & (m.BragFlag - 1)) != 0 || m.MinimumCounts.IsDefaultOrEmpty ||
                m.MinimumCounts[0] != 1 || m.MinimumCounts.Any(n => n > ushort.MaxValue) ||
                !m.MinimumCounts.SequenceEqual(m.MinimumCounts.Distinct().Order())) ||
            milestones.Select(m => m.BragFlag).Distinct().Count() != milestones.Length ||
            milestones.Select(m => m.CountVariable).Distinct().Count() != milestones.Length ||
            milestones.Select(m => m.AwardVariable).Distinct().Count() != milestones.Length)
            throw new ArgumentException("Invalid field milestone collection.");
        Map = map; Special = special; Milestones = milestones;
    }

    public ImmutableSortedDictionary<string, int> Query(ImmutableSortedDictionary<string, int> variables,
        IReadOnlyDictionary<string, int>? stats, string resultVariable)
    {
        int flags = 0;
        foreach (var milestone in Milestones)
        {
            int count = milestone.ReadCount(stats);
            variables = variables.SetItem(milestone.CountVariable, count);
            if (count != 0) flags |= milestone.BragFlag;
        }
        return variables.SetItem(resultVariable, flags);
    }
}
