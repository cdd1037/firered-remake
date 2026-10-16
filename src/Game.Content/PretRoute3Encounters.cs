using System.Collections.Immutable;
using Game.Domain.Encounters;

namespace Game.Content;

/// <summary>Admits only Route3 grass from the authenticated region. Route4 remains outside the approach scope.</summary>
public static class PretRoute3Encounters
{
    public static EarlyLandEncounterTable LoadProject(string projectRoot) => Load(PretRoute3Region.Load(projectRoot));

    public static EarlyLandEncounterTable Load(PretRoute3Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        var entries = region.Encounters.EnumerateArray().Where(e =>
            PretRoute3Region.MapIdFromSourceSymbol(e.GetProperty("table").GetProperty("map").GetString()!) == EarlyLandEncounterRules.Route3Table.Map).ToArray();
        if (entries.Length != 1) throw new InvalidDataException("The single authenticated Route3 FireRed encounter table is required.");
        var entry = entries[0];
        var table = entry.GetProperty("table");
        var land = table.GetProperty("land_mons");
        var weights = entry.GetProperty("slotWeights").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        if (weights.Length != 12) throw new InvalidDataException("Route3 requires twelve source encounter weights.");
        var result = new EarlyLandEncounterTable(PretRoute3Region.MapIdFromSourceSymbol(table.GetProperty("map").GetString()!),            land.GetProperty("encounter_rate").GetInt32(),land.GetProperty("mons").EnumerateArray().Select((mon, i) =>
                new EarlyLandEncounterSlot(i, new("pret:" + mon.GetProperty("species").GetString()),
                    mon.GetProperty("min_level").GetByte(), mon.GetProperty("max_level").GetByte(), weights[i])).ToImmutableArray());
        EarlyLandEncounterRules.ValidateTable(result);
        return result;
    }
}
