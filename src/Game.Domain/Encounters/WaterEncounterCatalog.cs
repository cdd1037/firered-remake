using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Immutable map encounter definitions loaded at the content boundary.</summary>
public sealed class WaterEncounterCatalog
{
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private WaterEncounterCatalog(ImmutableArray<WaterEncounterTable> tables) => Tables = tables.ToImmutableDictionary(t => t.Map);
    public static WaterEncounterCatalog Authenticate(ImmutableArray<WaterEncounterTable> tables)
    {
        if (tables.IsDefaultOrEmpty || tables.Any(t => !IsValidDefinition(t)) ||
            tables.Select(t => t.Map).Distinct().Count() != tables.Length)
            throw new ArgumentException("water_catalog_scope");
        return new(tables);
    }
    /// <summary>Merge independently validated definitions. Equivalent overlap retains the
    /// existing table object; conflicting map definitions must be resolved by the caller.</summary>
    public WaterEncounterCatalog Add(ImmutableArray<WaterEncounterTable> additions)
    {
        if (additions.IsDefault || additions.Any(table => !IsValidDefinition(table)) ||
            additions.Select(table => table.Map).Distinct().Count() != additions.Length)
            throw new ArgumentException("water_catalog_additions");
        var merged = Tables.ToBuilder();
        foreach (var table in additions)
        {
            if (merged.TryGetValue(table.Map, out var previous))
            {
                if (!SameDefinition(table, previous)) throw new ArgumentException("water_catalog_conflict");
            }
            else merged.Add(table.Map, table);
        }
        return new(merged.Values.ToImmutableArray());
    }

    /// <summary>Validate actual map and species references once at composition.</summary>
    public void ValidateReferences(IReadOnlyDictionary<MapId, Game.Domain.World.IFieldMap> maps,
        Game.Domain.Pokemon.PokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(pokemon);
        foreach (var (mapId, table) in Tables)
            if (!maps.TryGetValue(mapId, out var map) || map is null || map.Id != mapId ||
                table.Slots.Any(slot => !pokemon.Species.ContainsKey(slot.Species)))
                throw new ArgumentException("water_catalog_reference");
    }

    public static OrdinaryWaterBattleBinding BindBattle(
        IReadOnlyDictionary<MapId, WaterEncounterTable> tables, Game.Domain.Pokemon.GamePokemonCatalog owned,
        WaterEncounterCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(owned);
        if (candidate is null || !tables.TryGetValue(candidate.Map, out var table) ||
            !IsValidDefinition(table) || candidate.Slot < 0 || candidate.Slot >= table.Slots.Length)
            throw new ArgumentException("ordinary_water_candidate");
        var slot = table.Slots[candidate.Slot];
        if (candidate.Species != slot.Species || candidate.Level < slot.MinimumLevel ||
            candidate.Level > slot.MaximumLevel || !owned.Catalog.Species.ContainsKey(candidate.Species))
            throw new ArgumentException("ordinary_water_candidate_slot");
        return new(owned.Catalog, candidate);
    }

    public static bool IsValidDefinition(WaterEncounterTable? table) => table is not null &&
        !string.IsNullOrWhiteSpace(table.Map.Value) && table.RawEncounterRate is >= 1 and <= 100 &&
        !table.Slots.IsDefault && table.Slots.Length == 5 &&
        table.Slots.All(s => s is not null) &&
        table.Slots.Select(s => s.Index).SequenceEqual(Enumerable.Range(0, 5)) &&
        table.Slots.Select(s => s.Weight).SequenceEqual(WaterEncounterRules.SlotWeights) &&
        table.Slots.All(s => s is not null && s.MinimumLevel is >= 1 and <= 100 &&
            s.MaximumLevel >= s.MinimumLevel && s.MaximumLevel <= 100 && !string.IsNullOrWhiteSpace(s.Species.Value));
    public static bool SameDefinition(WaterEncounterTable? actual, WaterEncounterTable expected) =>
        actual is not null && actual.Map == expected.Map && actual.RawEncounterRate == expected.RawEncounterRate &&
        !actual.Slots.IsDefault && actual.Slots.SequenceEqual(expected.Slots);
    public static void ValidateTable(WaterEncounterTable table)
    { if (!IsValidDefinition(table)) throw new ArgumentException("water_table_definition"); }
    public static void ValidateAdmission(IReadOnlyDictionary<MapId, WaterEncounterTable> tables, WaterEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (table is null || !tables.TryGetValue(table.Map, out var expected) || !SameDefinition(table, expected))
            throw new ArgumentException("water_table_admission");
    }
}
