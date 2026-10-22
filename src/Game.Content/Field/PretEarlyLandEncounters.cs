using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Load converted land headers and preserve the current early encounter composition.</summary>
public static class PretEarlyLandEncounters
{
    public static ImmutableDictionary<MapId, EarlyLandEncounterTable> Load(PretCurrentWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        try
        {
            var headers = PretEarlyFieldData.Read(world.ProjectRoot, "data/encounters/early-land.json");
            if (headers.EnumerateArray().Any(h => !h.TryGetProperty("land_mons", out _)))
                throw new InvalidDataException("early_land_header_missing");
            var tables = PretWildEncounterTables.Land(headers, world.Resolve).ToImmutableDictionary(t => t.Map);
            if (!tables.ContainsKey(EarlyLandEncounterRules.Route3Table.Map) ||
                tables.ContainsKey(EarlyLandEncounterRules.Route1Table.Map) || tables.Keys.Any(m => !world.Maps.ContainsKey(m)))
                throw new InvalidDataException("early_land_map_membership");
            // Preserve the protected tables in the existing early-content contract.
            // Other headers retain their supplied probabilities and slots.
            foreach (var expected in MtMoonLandEncounterTables.All.Concat(BillLandEncounterTables.All)
                .Concat(VermilionLandEncounterTables.All).Append(EarlyLandEncounterRules.Route22Table))
                if (!tables.TryGetValue(expected.Map, out var actual) || actual.RawEncounterRate != expected.RawEncounterRate ||
                    !actual.Slots.SequenceEqual(expected.Slots))
                    throw new InvalidDataException("early_land_special_table");
            return tables;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or
            ArgumentException or OverflowException or FormatException)
        {
            throw new InvalidDataException("early_land_document_invalid", error);
        }
    }
}
