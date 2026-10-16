using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Converts generated source encounter headers into shared immutable field tables.</summary>
public static class PretWildEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> Land(JsonElement headers, Func<string, MapId> mapId)
    {
        var tables = ImmutableArray.CreateBuilder<EarlyLandEncounterTable>();
        foreach (var header in headers.EnumerateArray())
        {
            if (!header.TryGetProperty("land_mons", out var land)) continue;
            var slots = land.GetProperty("mons").EnumerateArray().Select((slot, index) =>
                new EarlyLandEncounterSlot(index, new("pret:" + slot.GetProperty("species").GetString()!),
                    slot.GetProperty("min_level").GetByte(), slot.GetProperty("max_level").GetByte(),
                    index < EarlyLandEncounterRules.SlotWeights.Length ? EarlyLandEncounterRules.SlotWeights[index] : 0)).ToImmutableArray();
            var table = new EarlyLandEncounterTable(mapId(header.GetProperty("map").GetString()!), land.GetProperty("encounter_rate").GetInt32(), slots);
            EarlyLandEncounterRules.ValidateTableDefinition(table);
            tables.Add(table);
        }
        if (tables.Count == 0 || tables.Select(t => t.Map).Distinct().Count() != tables.Count)
            throw new InvalidDataException("land_encounter_headers_invalid");
        return tables.ToImmutable();
    }
}
