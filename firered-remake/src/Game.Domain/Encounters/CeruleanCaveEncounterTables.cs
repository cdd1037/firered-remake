using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Inactive source declarations are evidence only, with no field dispatch API.</summary>
public sealed record CeruleanCaveRockSmashSourceTable(MapId Map, int RawEncounterRate, ImmutableArray<WaterEncounterSlot> Slots);
public sealed record CeruleanCaveHeldItemSource(SpeciesId Species, string Common, string Rare);

/// <summary>Exact pinned FireRed rows for the three floors. Only Land, Surf and Old Rod are field-enabled.</summary>
public static class CeruleanCaveEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(new("map.cerulean-cave-1f"), 7,
            [new(0, new("pret:SPECIES_MAGNETON"), 49, 49, 20), new(1, new("pret:SPECIES_PARASECT"), 49, 49, 20), new(2, new("pret:SPECIES_GOLBAT"), 46, 46, 10), new(3, new("pret:SPECIES_MACHOKE"), 46, 46, 10), new(4, new("pret:SPECIES_PRIMEAPE"), 52, 52, 10), new(5, new("pret:SPECIES_DITTO"), 52, 52, 10), new(6, new("pret:SPECIES_ELECTRODE"), 58, 58, 5), new(7, new("pret:SPECIES_PARASECT"), 58, 58, 5), new(8, new("pret:SPECIES_GOLBAT"), 55, 55, 4), new(9, new("pret:SPECIES_WOBBUFFET"), 55, 55, 4), new(10, new("pret:SPECIES_PRIMEAPE"), 61, 61, 1), new(11, new("pret:SPECIES_DITTO"), 61, 61, 1)]),
        new(new("map.cerulean-cave-2f"), 7,
            [new(0, new("pret:SPECIES_GOLBAT"), 49, 49, 20), new(1, new("pret:SPECIES_MACHOKE"), 49, 49, 20), new(2, new("pret:SPECIES_MAGNETON"), 52, 52, 10), new(3, new("pret:SPECIES_PARASECT"), 52, 52, 10), new(4, new("pret:SPECIES_KADABRA"), 55, 55, 10), new(5, new("pret:SPECIES_DITTO"), 55, 55, 10), new(6, new("pret:SPECIES_GOLBAT"), 58, 58, 5), new(7, new("pret:SPECIES_WOBBUFFET"), 58, 58, 5), new(8, new("pret:SPECIES_ELECTRODE"), 61, 61, 4), new(9, new("pret:SPECIES_PARASECT"), 61, 61, 4), new(10, new("pret:SPECIES_KADABRA"), 64, 64, 1), new(11, new("pret:SPECIES_DITTO"), 64, 64, 1)]),
        new(new("map.cerulean-cave-b1f"), 7,
            [new(0, new("pret:SPECIES_KADABRA"), 58, 58, 20), new(1, new("pret:SPECIES_DITTO"), 58, 58, 20), new(2, new("pret:SPECIES_MAGNETON"), 55, 55, 10), new(3, new("pret:SPECIES_PARASECT"), 55, 55, 10), new(4, new("pret:SPECIES_GOLBAT"), 52, 52, 10), new(5, new("pret:SPECIES_MACHOKE"), 52, 52, 10), new(6, new("pret:SPECIES_KADABRA"), 67, 67, 5), new(7, new("pret:SPECIES_DITTO"), 67, 67, 5), new(8, new("pret:SPECIES_ELECTRODE"), 64, 64, 4), new(9, new("pret:SPECIES_PARASECT"), 64, 64, 4), new(10, new("pret:SPECIES_GOLBAT"), 61, 61, 1), new(11, new("pret:SPECIES_WOBBUFFET"), 61, 61, 1)]),
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(new("map.cerulean-cave-1f"), 2,
            [new(0, new("pret:SPECIES_PSYDUCK"), 30, 40, 60), new(1, new("pret:SPECIES_GOLDUCK"), 40, 50, 30), new(2, new("pret:SPECIES_GOLDUCK"), 45, 55, 5), new(3, new("pret:SPECIES_PSYDUCK"), 40, 50, 4), new(4, new("pret:SPECIES_PSYDUCK"), 40, 50, 1)]),
        new(new("map.cerulean-cave-b1f"), 2,
            [new(0, new("pret:SPECIES_PSYDUCK"), 40, 50, 60), new(1, new("pret:SPECIES_GOLDUCK"), 50, 60, 30), new(2, new("pret:SPECIES_GOLDUCK"), 55, 65, 5), new(3, new("pret:SPECIES_PSYDUCK"), 50, 60, 4), new(4, new("pret:SPECIES_PSYDUCK"), 50, 60, 1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> FishingSource { get; } = [
        new(new("map.cerulean-cave-1f"), 20,
            [new(0, new("pret:SPECIES_MAGIKARP"), 5, 5, 70), new(1, new("pret:SPECIES_MAGIKARP"), 5, 5, 30), new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20), new(5, new("pret:SPECIES_POLIWAG"), 15, 25, 40), new(6, new("pret:SPECIES_POLIWHIRL"), 20, 30, 40), new(7, new("pret:SPECIES_GYARADOS"), 15, 25, 15), new(8, new("pret:SPECIES_PSYDUCK"), 15, 25, 4), new(9, new("pret:SPECIES_PSYDUCK"), 25, 35, 1)]),
        new(new("map.cerulean-cave-b1f"), 20,
            [new(0, new("pret:SPECIES_MAGIKARP"), 5, 5, 70), new(1, new("pret:SPECIES_MAGIKARP"), 5, 5, 30), new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20), new(5, new("pret:SPECIES_POLIWAG"), 15, 25, 40), new(6, new("pret:SPECIES_POLIWHIRL"), 20, 30, 40), new(7, new("pret:SPECIES_GYARADOS"), 15, 25, 15), new(8, new("pret:SPECIES_PSYDUCK"), 15, 25, 4), new(9, new("pret:SPECIES_GYARADOS"), 25, 35, 1)]),
    ];
    public static ImmutableArray<CeruleanCaveRockSmashSourceTable> RockSmashSource { get; } = [
        new(new("map.cerulean-cave-1f"), 50,
            [new(0, new("pret:SPECIES_GEODUDE"), 30, 40, 60), new(1, new("pret:SPECIES_GRAVELER"), 40, 50, 30), new(2, new("pret:SPECIES_GRAVELER"), 45, 55, 5), new(3, new("pret:SPECIES_GEODUDE"), 40, 50, 4), new(4, new("pret:SPECIES_GEODUDE"), 40, 50, 1)]),
        new(new("map.cerulean-cave-2f"), 50,
            [new(0, new("pret:SPECIES_GEODUDE"), 35, 45, 60), new(1, new("pret:SPECIES_GRAVELER"), 45, 55, 30), new(2, new("pret:SPECIES_GRAVELER"), 50, 60, 5), new(3, new("pret:SPECIES_GEODUDE"), 45, 55, 4), new(4, new("pret:SPECIES_GEODUDE"), 45, 55, 1)]),
        new(new("map.cerulean-cave-b1f"), 50,
            [new(0, new("pret:SPECIES_GEODUDE"), 40, 50, 60), new(1, new("pret:SPECIES_GRAVELER"), 50, 60, 30), new(2, new("pret:SPECIES_GRAVELER"), 55, 65, 5), new(3, new("pret:SPECIES_GEODUDE"), 50, 60, 4), new(4, new("pret:SPECIES_GEODUDE"), 50, 60, 1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } =
        FishingSource.Select(t => t with { Slots = t.Slots.Take(2).ToImmutableArray() }).ToImmutableArray();
    // Source slot indexes2–4 and5–9 stay intact. No Good/Super Rod field mechanics are enabled.
    public static ImmutableArray<OldRodEncounterTable> GoodRodSource { get; } =
        FishingSource.Select(t => t with { Slots = t.Slots.Skip(2).Take(3).ToImmutableArray() }).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> SuperRodSource { get; } =
        FishingSource.Select(t => t with { Slots = t.Slots.Skip(5).ToImmutableArray() }).ToImmutableArray();
    public static bool IsCave(MapId map) => Land.Any(t => t.Map == map);

    public const string HeldItemSourcePath = "src/data/pokemon/species_info.h";

    // Every random source species, including the inactive rod and Rock Smash rows.
    public static ImmutableArray<CeruleanCaveHeldItemSource> HeldItems { get; } = [
        new(new("pret:SPECIES_DITTO"), "ITEM_NONE", "ITEM_METAL_POWDER"),
        new(new("pret:SPECIES_ELECTRODE"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_GEODUDE"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_GOLBAT"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_GOLDEEN"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_GOLDUCK"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_GRAVELER"), "ITEM_NONE", "ITEM_HARD_STONE"),
        new(new("pret:SPECIES_GYARADOS"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_KADABRA"), "ITEM_NONE", "ITEM_TWISTED_SPOON"),
        new(new("pret:SPECIES_MACHOKE"), "ITEM_NONE", "ITEM_FOCUS_BAND"),
        new(new("pret:SPECIES_MAGIKARP"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_MAGNETON"), "ITEM_NONE", "ITEM_MAGNET"),
        new(new("pret:SPECIES_PARASECT"), "ITEM_TINY_MUSHROOM", "ITEM_BIG_MUSHROOM"),
        new(new("pret:SPECIES_POLIWAG"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_POLIWHIRL"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_PRIMEAPE"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_PSYDUCK"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_WOBBUFFET"), "ITEM_NONE", "ITEM_NONE"),
    ];
}
