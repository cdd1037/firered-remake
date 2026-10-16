using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

public sealed record FishingRodTable(FishingRod Rod, OldRodEncounterTable Table);
public sealed record FishingMapAdmission(FishingRod Rod, MapId Map);
public sealed record FishingHeldItem(SpeciesId Species, string Common, string Rare);

/// <summary>Immutable rod definitions and map admission shared by every fishing task.
/// Battle/capture policy belongs to the caller, never to a geographic task variant.</summary>
public sealed class FishingTaskProfile
{
    public string RulesVersion { get; }
    public ImmutableDictionary<(FishingRod Rod, MapId Map), OldRodEncounterTable> Tables { get; }
    public ImmutableHashSet<FishingMapAdmission> EmptyMaps { get; }
    public ImmutableDictionary<SpeciesId, FishingHeldItem> HeldItems { get; }
    private readonly ImmutableHashSet<FishingRod> _rods;

    public FishingTaskProfile(string rulesVersion, ImmutableArray<FishingRodTable> tables,
        ImmutableArray<FishingMapAdmission> emptyMaps, ImmutableArray<FishingHeldItem> heldItems = default)
    {
        if (string.IsNullOrWhiteSpace(rulesVersion) || tables.IsDefaultOrEmpty || emptyMaps.IsDefault)
            throw new ArgumentException("fishing_profile_definitions");
        var rows = ImmutableDictionary.CreateBuilder<(FishingRod, MapId), OldRodEncounterTable>();
        foreach (var row in tables)
        {
            if (row is null || !Enum.IsDefined(row.Rod) || row.Table is not { } table ||
                !ValidMap(table.Map) || table.RawEncounterRate is < 1 or > 255 || table.Slots.IsDefault ||
                !rows.TryAdd((row.Rod, table.Map), table)) throw new ArgumentException("fishing_profile_table");
            var (start, weights) = SlotShape(row.Rod);
            if (table.Slots.Length != weights.Length) throw new ArgumentException("fishing_profile_slots");
            for (int i = 0; i < weights.Length; i++)
            {
                var slot = table.Slots[i];
                if (slot is null || slot.Index != start + i || slot.Weight != weights[i] ||
                    string.IsNullOrWhiteSpace(slot.Species.Value) || slot.MinimumLevel is < 1 or > 100 ||
                    slot.MaximumLevel < slot.MinimumLevel || slot.MaximumLevel > 100)
                    throw new ArgumentException("fishing_profile_slot");
            }
        }
        var empty = ImmutableHashSet.CreateBuilder<FishingMapAdmission>();
        foreach (var map in emptyMaps)
            if (map is null || !Enum.IsDefined(map.Rod) || !ValidMap(map.Map) ||
                rows.ContainsKey((map.Rod, map.Map)) || !empty.Add(map))
                throw new ArgumentException("fishing_profile_empty_map");
        RulesVersion = rulesVersion; Tables = rows.ToImmutable(); EmptyMaps = empty.ToImmutable();
        _rods = Tables.Keys.Select(k => k.Rod).Concat(EmptyMaps.Select(m => m.Rod)).ToImmutableHashSet();
        var items = ImmutableDictionary.CreateBuilder<SpeciesId, FishingHeldItem>();
        if (!heldItems.IsDefault)
        {
            foreach (var item in heldItems)
                if (item is null || string.IsNullOrWhiteSpace(item.Species.Value) ||
                    string.IsNullOrWhiteSpace(item.Common) || !item.Common.StartsWith("ITEM_", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(item.Rare) || !item.Rare.StartsWith("ITEM_", StringComparison.Ordinal) ||
                    !items.TryAdd(item.Species, item)) throw new ArgumentException("fishing_profile_held_items");
            if (!Tables.Values.SelectMany(t => t.Slots).Select(s => s.Species).ToHashSet().SetEquals(items.Keys))
                throw new ArgumentException("fishing_profile_held_item_coverage");
        }
        HeldItems = items.ToImmutable();
    }

    public static FishingTaskProfile ForRod(string rulesVersion, FishingRod rod,
        ImmutableArray<OldRodEncounterTable> tables, IEnumerable<MapId> emptyMaps,
        ImmutableArray<FishingHeldItem> heldItems = default) => new(rulesVersion,
            tables.Select(t => new FishingRodTable(rod, t)).ToImmutableArray(),
            emptyMaps.Distinct().Where(map => !tables.Any(t => t.Map == map))
                .Select(map => new FishingMapAdmission(rod, map)).ToImmutableArray(), heldItems);

    public bool SupportsRod(FishingRod rod) => _rods.Contains(rod);
    public bool HasTable(FishingRod rod, MapId map) => Tables.ContainsKey((rod, map));
    public bool IsAdmittedMap(FishingRod rod, MapId map) => HasTable(rod, map) || EmptyMaps.Contains(new(rod, map));
    public OldRodEncounterTable Table(FishingRod rod, MapId map) => Tables.TryGetValue((rod, map), out var table)
        ? table : throw new ArgumentException("fishing_map_not_admitted");
    public OldRodEncounterSlot? SourceSlot(FishingEncounterOrigin? origin)
    {
        if (origin is null || origin.RulesVersion != FishingBattleContract.Version ||
            !Tables.TryGetValue((origin.Rod, origin.Map), out var table)) return null;
        var slot = table.Slots.SingleOrDefault(s => s.Index == origin.SlotIndex);
        return slot is not null && slot.Species == origin.Species && origin.Level >= slot.MinimumLevel &&
            origin.Level <= slot.MaximumLevel ? slot : null;
    }

    public OldRodFishingResult Start(OldRodCastContext context, RngState rng, bool cancelBeforeCast = false) =>
        OldRodFishingRules.Start(this, context, rng, cancelBeforeCast);
    public OldRodFishingResult Advance(OldRodFishingState state, OldRodFrameInput input, RngState rng) =>
        OldRodFishingRules.Advance(this, state, input, rng);
    public void ValidateState(OldRodFishingState state) => OldRodFishingRules.ValidateState(this, state);
    public OldRodEncounterResult GenerateCandidate(OldRodFishingState fishing, RngState rng)
    {
        ValidateState(fishing);
        if (fishing.Status != OldRodFishingStatus.EncounterReady)
            throw new InvalidOperationException("fishing_task_not_finished");
        var table = Table(fishing.Rod, fishing.Map);
        var draw = Gen3Rng.Next(rng, "fishing.slot", 100);
        int remaining = draw.Trace.Result;
        var slot = table.Slots.First(s => (remaining -= s.Weight) < 0);
        var level = Gen3Rng.Next(draw.State, "fishing.level", slot.MaximumLevel - slot.MinimumLevel + 1);
        return new(new(table.Map, slot.Index, slot.Species, (byte)(slot.MinimumLevel + level.Trace.Result)),
            level.State, [draw.Trace, level.Trace]);
    }

    public Route1WildPokemonResult GeneratePokemon(GamePokemonCatalog owned, FishingEncounterOrigin origin,
        RngState rng, uint trainerId)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (SourceSlot(origin) is null || !owned.Catalog.Species.ContainsKey(origin.Species))
            throw new ArgumentException("fishing_candidate_source");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, origin.Species, origin.Level, rng, trainerId);
    }

    /// <summary>The battle owner consumes the mandatory draw, even for NONE/NONE or identical item pairs.</summary>
    public string HeldItem(FishingEncounterOrigin origin, int roll)
    {
        if (roll is < 0 or >= 100 || SourceSlot(origin) is null || !HeldItems.TryGetValue(origin.Species, out var row))
            throw new ArgumentException("fishing_held_item_source");
        return row.Common == row.Rare ? row.Common : roll < 45 ? "ITEM_NONE" : roll < 95 ? row.Common : row.Rare;
    }

    private static bool ValidMap(MapId map) => !string.IsNullOrWhiteSpace(map.Value) && map.Value.StartsWith("map.", StringComparison.Ordinal);
    private static (int Start, int[] Weights) SlotShape(FishingRod rod) => rod switch
    {
        FishingRod.Old => (0, [70, 30]), FishingRod.Good => (2, [60, 20, 20]),
        FishingRod.Super => (5, [40, 40, 15, 4, 1]), _ => throw new ArgumentOutOfRangeException(nameof(rod))
    };
}
