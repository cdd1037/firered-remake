using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

public sealed record SevenIslandHeldItemSource(SpeciesId Species, string Common, string Rare);
public sealed record SevenIslandRockSmashSourceTable(MapId Map, string SourceCommit, string SourcePath,
    string SourceSymbol, string SourceSha256, int RawEncounterRate, ImmutableArray<WaterEncounterSlot> Slots);

/// <summary>Exact FireRed declarations; inactive fishing groups retain their original source slot indexes.</summary>
public static class SevenIslandEncounterTables
{
    public static bool IsChamber(MapId map) => Game.Domain.Pokemon.UnownFormRules.IsChamber(map);
    public const string SourceSha256 = "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c";
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(new("map.seven-island-tanoby-ruins-monean-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsMoneanChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-tanoby-ruins-liptoo-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsLiptooChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-tanoby-ruins-weepth-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsWeepthChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-tanoby-ruins-dilford-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsDilfordChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-tanoby-ruins-scufib-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsScufibChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-tanoby-ruins-rixy-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsRixyChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-tanoby-ruins-viapois-chamber"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuinsViapoisChamber_FireRed", SourceSha256, 7,
            [new(0, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(1, new("pret:SPECIES_UNOWN"), 25, 25, 20), new(2, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(3, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(4, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(5, new("pret:SPECIES_UNOWN"), 25, 25, 10), new(6, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(7, new("pret:SPECIES_UNOWN"), 25, 25, 5), new(8, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(9, new("pret:SPECIES_UNOWN"), 25, 25, 4), new(10, new("pret:SPECIES_UNOWN"), 25, 25, 1), new(11, new("pret:SPECIES_UNOWN"), 25, 25, 1)]),
        new(new("map.seven-island-sevault-canyon-entrance"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandSevaultCanyonEntrance_FireRed", SourceSha256, 21,
            [new(0, new("pret:SPECIES_SPEAROW"), 44, 44, 20), new(1, new("pret:SPECIES_SENTRET"), 10, 10, 20), new(2, new("pret:SPECIES_PHANPY"), 10, 10, 10), new(3, new("pret:SPECIES_FEAROW"), 48, 48, 10), new(4, new("pret:SPECIES_SENTRET"), 15, 15, 10), new(5, new("pret:SPECIES_MEOWTH"), 41, 41, 10), new(6, new("pret:SPECIES_FEAROW"), 50, 50, 5), new(7, new("pret:SPECIES_PSYDUCK"), 41, 41, 5), new(8, new("pret:SPECIES_PHANPY"), 15, 15, 4), new(9, new("pret:SPECIES_PERSIAN"), 47, 47, 4), new(10, new("pret:SPECIES_PHANPY"), 15, 15, 1), new(11, new("pret:SPECIES_PERSIAN"), 50, 50, 1)]),
        new(new("map.seven-island-sevault-canyon"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandSevaultCanyon_FireRed", SourceSha256, 21,
            [new(0, new("pret:SPECIES_GEODUDE"), 46, 46, 20), new(1, new("pret:SPECIES_PHANPY"), 15, 15, 20), new(2, new("pret:SPECIES_CUBONE"), 46, 46, 10), new(3, new("pret:SPECIES_FEAROW"), 50, 50, 10), new(4, new("pret:SPECIES_MAROWAK"), 52, 52, 10), new(5, new("pret:SPECIES_MEOWTH"), 43, 43, 10), new(6, new("pret:SPECIES_ONIX"), 54, 54, 5), new(7, new("pret:SPECIES_SKARMORY"), 30, 30, 5), new(8, new("pret:SPECIES_LARVITAR"), 15, 15, 4), new(9, new("pret:SPECIES_PERSIAN"), 49, 49, 4), new(10, new("pret:SPECIES_LARVITAR"), 20, 20, 1), new(11, new("pret:SPECIES_PERSIAN"), 52, 52, 1)]),
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(new("map.seven-island-trainer-tower"), "MAP_SEVEN_ISLAND_TRAINER_TOWER", EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTrainerTower_FireRed", SourceSha256, 2,
            [new(0, new("pret:SPECIES_TENTACOOL"), 5, 20, 60), new(1, new("pret:SPECIES_TENTACOOL"), 20, 35, 30), new(2, new("pret:SPECIES_TENTACOOL"), 35, 40, 5), new(3, new("pret:SPECIES_TENTACRUEL"), 35, 40, 4), new(4, new("pret:SPECIES_TENTACRUEL"), 35, 40, 1)]),
        new(new("map.seven-island-tanoby-ruins"), "MAP_SEVEN_ISLAND_TANOBY_RUINS", EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuins_FireRed", SourceSha256, 2,
            [new(0, new("pret:SPECIES_TENTACOOL"), 5, 20, 60), new(1, new("pret:SPECIES_TENTACOOL"), 20, 35, 30), new(2, new("pret:SPECIES_TENTACOOL"), 35, 40, 5), new(3, new("pret:SPECIES_TENTACRUEL"), 35, 40, 4), new(4, new("pret:SPECIES_TENTACRUEL"), 35, 40, 1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> FishingSource { get; } = [
        new(new("map.seven-island-trainer-tower"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTrainerTower_FireRed", SourceSha256, 20,
            [new(0, new("pret:SPECIES_MAGIKARP"), 5, 5, 70), new(1, new("pret:SPECIES_MAGIKARP"), 5, 5, 30), new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_HORSEA"), 5, 15, 20), new(5, new("pret:SPECIES_HORSEA"), 15, 25, 40), new(6, new("pret:SPECIES_QWILFISH"), 15, 25, 40), new(7, new("pret:SPECIES_GYARADOS"), 15, 25, 15), new(8, new("pret:SPECIES_SEADRA"), 25, 35, 4), new(9, new("pret:SPECIES_PSYDUCK"), 25, 35, 1)]),
        new(new("map.seven-island-tanoby-ruins"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandTanobyRuins_FireRed", SourceSha256, 20,
            [new(0, new("pret:SPECIES_MAGIKARP"), 5, 5, 70), new(1, new("pret:SPECIES_MAGIKARP"), 5, 5, 30), new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_HORSEA"), 5, 15, 20), new(5, new("pret:SPECIES_HORSEA"), 15, 25, 40), new(6, new("pret:SPECIES_QWILFISH"), 15, 25, 40), new(7, new("pret:SPECIES_GYARADOS"), 15, 25, 15), new(8, new("pret:SPECIES_SEADRA"), 25, 35, 4), new(9, new("pret:SPECIES_PSYDUCK"), 25, 35, 1)]),
    ];
    public static ImmutableArray<SevenIslandRockSmashSourceTable> RockSmashSource { get; } = [
        new(new("map.seven-island-sevault-canyon"), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, "sSevenIslandSevaultCanyon_FireRed", SourceSha256, 25,
            [new(0, new("pret:SPECIES_GEODUDE"), 25, 35, 60), new(1, new("pret:SPECIES_GRAVELER"), 30, 45, 30), new(2, new("pret:SPECIES_GRAVELER"), 35, 50, 5), new(3, new("pret:SPECIES_GEODUDE"), 30, 40, 4), new(4, new("pret:SPECIES_GEODUDE"), 30, 40, 1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } =
        FishingSource.Select(t => t with { Slots = t.Slots.Take(2).ToImmutableArray() }).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> GoodRodSource { get; } =
        FishingSource.Select(t => t with { Slots = t.Slots.Skip(2).Take(3).ToImmutableArray() }).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> SuperRodSource { get; } =
        FishingSource.Select(t => t with { Slots = t.Slots.Skip(5).ToImmutableArray() }).ToImmutableArray();
    public static ImmutableArray<SevenIslandHeldItemSource> HeldItems { get; } = [
        new(new("pret:SPECIES_CUBONE"), "ITEM_NONE", "ITEM_THICK_CLUB"),
        new(new("pret:SPECIES_FEAROW"), "ITEM_NONE", "ITEM_SHARP_BEAK"),
        new(new("pret:SPECIES_GEODUDE"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_GRAVELER"), "ITEM_NONE", "ITEM_HARD_STONE"),
        new(new("pret:SPECIES_GYARADOS"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_HORSEA"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_LARVITAR"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_MAGIKARP"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_MAROWAK"), "ITEM_NONE", "ITEM_THICK_CLUB"),
        new(new("pret:SPECIES_MEOWTH"), "ITEM_NONE", "ITEM_NUGGET"),
        new(new("pret:SPECIES_ONIX"), "ITEM_NONE", "ITEM_HARD_STONE"),
        new(new("pret:SPECIES_PERSIAN"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_PHANPY"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_PSYDUCK"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_QWILFISH"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_SEADRA"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_SENTRET"), "ITEM_NONE", "ITEM_ORAN_BERRY"),
        new(new("pret:SPECIES_SKARMORY"), "ITEM_NONE", "ITEM_SHARP_BEAK"),
        new(new("pret:SPECIES_SPEAROW"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_TENTACOOL"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_TENTACRUEL"), "ITEM_NONE", "ITEM_NONE"),
        new(new("pret:SPECIES_UNOWN"), "ITEM_NONE", "ITEM_NONE"),
    ];
}
