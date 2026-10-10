using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Ten existing early-Kanto maps, Original Rod slots 0/1. No Surf or later rods.</summary>
public static class OldRodEncounterRules
{
    public const string SourcePath = "src/data/wild_encounters.json";
    public const string SourceSha256 = "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c";
    public static SpeciesId Magikarp { get; } = new("pret:SPECIES_MAGIKARP");
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
    [
        Table("pallet-town", "PalletTown", 10, 10),
        Table("viridian-city", "ViridianCity", 10),
        Table("cerulean-city", "CeruleanCity", 10),
        Table("vermilion-city", "VermilionCity", 10),
        Table("route-4", "Route4", 20),
        Table("route-6", "Route6", 20),
        Table("route-22", "Route22", 20),
        Table("route-24", "Route24", 20),
        Table("route-25", "Route25", 20),
        Table("ssanne-exterior", "SSAnneExterior", 10)
    ];
    // Verified absent fishing tables, retained to model source short-circuit behavior explicitly.
    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } =
        [new("map.route-1"), new("map.route-2"), new("map.route-3"), new("map.route-5")];
    internal static bool IsAdmittedMap(MapId map) => Tables.Any(t => t.Map == map) || MapsWithoutFishingMons.Contains(map);

    private static OldRodEncounterTable Table(string map, string symbol, int rate, byte max = 5) =>
        new(new("map." + map), OldRodFishingRules.SourceCommit, SourcePath, "s" + symbol + "_FireRed", SourceSha256,
            rate, [new(0, Magikarp, 5, max, 70), new(1, Magikarp, 5, max, 30)]);

    /// <summary>Fishing uses no land cooldown/rate RNG, Repel, lead ability or table encounter-rate roll.</summary>
    public static OldRodEncounterResult GenerateCandidate(OldRodEncounterTable table,
        OldRodFishingState fishing, RngState rng)
    {
        ValidateTable(table);
        OldRodFishingRules.ValidateState(fishing);
        if (fishing.Status != OldRodFishingStatus.EncounterReady || fishing.Map != table.Map)
            throw new InvalidOperationException("The matching fishing task must finish successfully first.");
        var slotDraw = Gen3Rng.Next(rng, "fishing.slot", 100);
        var slot = table.Slots[ChooseSlot(slotDraw.Trace.Result)];
        int lo = Math.Min(slot.MinimumLevel, slot.MaximumLevel), hi = Math.Max(slot.MinimumLevel, slot.MaximumLevel);
        var levelDraw = Gen3Rng.Next(slotDraw.State, "fishing.level", hi - lo + 1);
        return new(new(table.Map, slot.Index, slot.Species, (byte)(lo + levelDraw.Trace.Result)),
            levelDraw.State, [slotDraw.Trace, levelDraw.Trace]);
    }

    public static int ChooseSlot(int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll < 70 ? 0 : 1;
    }

    public static void ValidateTable(OldRodEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var expected = Tables.SingleOrDefault(t => t.Map == table.Map);
        if (expected is null || table.Slots.IsDefault || table with { Slots = expected.Slots } != expected ||
            !table.Slots.SequenceEqual(expected.Slots)) throw new ArgumentException("Old Rod table differs from the fixed FireRed source.");
    }

    public static void ValidateCandidate(OldRodEncounterCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var table = Tables.SingleOrDefault(t => t.Map == candidate.Map);
        if (table is null || candidate.SlotIndex is < 0 or > 1) throw new ArgumentException("Unadmitted Old Rod encounter.");
        var slot = table.Slots[candidate.SlotIndex];
        if (candidate.Species != slot.Species || candidate.Level < slot.MinimumLevel || candidate.Level > slot.MaximumLevel)
            throw new ArgumentException("Fishing species/level differs from source slot.");
    }
}
