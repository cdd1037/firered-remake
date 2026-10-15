using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

internal static class GeneratedLandEncounterValidation
{
    public static void Validate(int schemaVersion, ImmutableArray<EarlyLandEncounterTable> tables,
        int heldItemDrawBound, ImmutableArray<EncounterHeldItemRow> heldItems,
        Func<EncounterHeldItemRow, bool> heldItemPolicy)
    {
        Need(schemaVersion == 1 && !tables.IsDefaultOrEmpty && !heldItems.IsDefaultOrEmpty,
            "Missing encounter data.");
        // The current factories own this policy until held-item authority is unified.
        Need(heldItemDrawBound == 100, "Unsupported held-item draw bound.");
        var maps = new HashSet<MapId>();
        var species = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            Need(table is not null, "Null encounter table.");
            Need(!string.IsNullOrWhiteSpace(table!.Map.Value) && maps.Add(table.Map), "Invalid or duplicate encounter map.");
            Need(table.RawEncounterRate is >= 1 and <= 255 && !table.Slots.IsDefault &&
                 table.Slots.Length == EarlyLandEncounterRules.SlotWeights.Length, "Invalid land rate or slot count.");
            for (int i = 0; i < table.Slots.Length; i++)
            {
                var slot = table.Slots[i];
                Need(slot is not null && slot.Index == i && slot.Weight == EarlyLandEncounterRules.SlotWeights[i] &&
                     slot.MinimumLevel is >= 1 and <= 100 && slot.MaximumLevel >= slot.MinimumLevel &&
                     slot.MaximumLevel <= 100 && !string.IsNullOrWhiteSpace(slot.Species.Value), "Invalid land slot.");
                species.Add(slot!.Species.Value);
            }
        }
        var heldSpecies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in heldItems)
        {
            Need(row is not null && !string.IsNullOrWhiteSpace(row.Species) && heldSpecies.Add(row.Species),
                 "Invalid or duplicate held-item species.");
            Need(heldItemPolicy(row!), "Held items differ from the current wild-item implementation.");
        }
        Need(heldSpecies.SetEquals(species), "Held-item species must cover the encounter slots exactly.");
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
