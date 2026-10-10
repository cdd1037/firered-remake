using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Exact pinned FireRed land tables. Content must independently authenticate the source bytes.</summary>
public static class MtMoonLandEncounterTables
{
    public const byte CaveBehavior = 0x08;
    public static ImmutableArray<SpeciesId> WildSpecies { get; } =
        [new("pret:SPECIES_ZUBAT"), new("pret:SPECIES_GEODUDE"), new("pret:SPECIES_PARAS"), new("pret:SPECIES_CLEFAIRY")];
    public static EarlyLandEncounterTable MtMoon1F { get; } = Create("map.mt-moon-1f", "sMtMoon1F_FireRed", 7,
        [
            ("ZUBAT", 7),
            ("ZUBAT", 8),
            ("GEODUDE", 7),
            ("ZUBAT", 9),
            ("ZUBAT", 10),
            ("GEODUDE", 8),
            ("GEODUDE", 9),
            ("PARAS", 8),
            ("ZUBAT", 7),
            ("ZUBAT", 7),
            ("ZUBAT", 7),
            ("CLEFAIRY", 8)
        ]);
    public static EarlyLandEncounterTable MtMoonB1F { get; } = Create("map.mt-moon-b1f", "sMtMoonB1F_FireRed", 5,
        [
            ("PARAS", 7),
            ("PARAS", 8),
            ("PARAS", 5),
            ("PARAS", 6),
            ("PARAS", 9),
            ("PARAS", 10),
            ("PARAS", 7),
            ("PARAS", 8),
            ("PARAS", 5),
            ("PARAS", 6),
            ("PARAS", 9),
            ("PARAS", 10)
        ]);
    public static EarlyLandEncounterTable MtMoonB2F { get; } = Create("map.mt-moon-b2f", "sMtMoonB2F_FireRed", 7,
        [
            ("ZUBAT", 8),
            ("GEODUDE", 9),
            ("ZUBAT", 9),
            ("ZUBAT", 10),
            ("GEODUDE", 10),
            ("PARAS", 10),
            ("PARAS", 12),
            ("CLEFAIRY", 10),
            ("ZUBAT", 11),
            ("ZUBAT", 11),
            ("ZUBAT", 11),
            ("CLEFAIRY", 12)
        ]);
    public static EarlyLandEncounterTable Route4 { get; } = Create("map.route-4", "sRoute4_FireRed", 21,
        [
            ("SPEAROW", 10),
            ("RATTATA", 10),
            ("EKANS", 6),
            ("EKANS", 10),
            ("SPEAROW", 8),
            ("RATTATA", 8),
            ("SPEAROW", 12),
            ("RATTATA", 12),
            ("MANKEY", 10),
            ("EKANS", 8),
            ("MANKEY", 12),
            ("EKANS", 12)
        ]);

    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [MtMoon1F, MtMoonB1F, MtMoonB2F, Route4];
    public static bool IsCave(MapId map) => map.Value is "map.mt-moon-1f" or "map.mt-moon-b1f" or "map.mt-moon-b2f";

    private static EarlyLandEncounterTable Create(string map, string symbol, int rate, (string Species, byte Level)[] slots) =>
        new(new(map), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, symbol,
            "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c", rate,
            slots.Select((s, i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Species), s.Level, s.Level,
                EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
