using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

/// <summary>Good Rod slot semantics. Tables are immutable generated definitions supplied by composition.</summary>
public static class GoodRodEncounterRules
{
    public const string RulesVersion = "firered-kanto-good-rod-v1";

    public static int ChooseSourceSlot(int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll < 60 ? 2 : roll < 80 ? 3 : 4;
    }

    public static void ValidateTable(OldRodEncounterTable table, FishingTaskProfile profile)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Tables.TryGetValue((FishingRod.Good, table.Map), out var expected) || table.Slots.IsDefault ||
            table with { Slots = expected.Slots } != expected || !table.Slots.SequenceEqual(expected.Slots))
            throw new ArgumentException("good_rod_table_source");
    }

    public static void ValidateOrigin(FishingEncounterOrigin origin, FishingTaskProfile profile)
    {
        if (origin is null || origin.Rod != FishingRod.Good || profile.SourceSlot(origin) is null)
            throw new ArgumentException("good_rod_candidate_source");
    }
}
