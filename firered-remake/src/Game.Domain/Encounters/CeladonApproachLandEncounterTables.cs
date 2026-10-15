using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Exact revision-0 FireRed Route8/7 land tables.</summary>
public static class CeladonApproachLandEncounterTables
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } =
        [new("pret:SPECIES_PIDGEY"), new("pret:SPECIES_MEOWTH"), new("pret:SPECIES_GROWLITHE"), new("pret:SPECIES_EKANS"), new("pret:SPECIES_ODDISH")];
    public static EarlyLandEncounterTable Route8 { get; } = Create("map.route-8", "sRoute8_FireRed", 21,
        [("PIDGEY",18), ("MEOWTH",18), ("GROWLITHE",16), ("PIDGEY",20), ("MEOWTH",20), ("EKANS",17), ("GROWLITHE",17), ("EKANS",19), ("EKANS",17), ("GROWLITHE",15), ("EKANS",17), ("GROWLITHE",18)]);
    public static EarlyLandEncounterTable Route7 { get; } = Create("map.route-7", "sRoute7_FireRed", 21,
        [("PIDGEY",19), ("MEOWTH",17), ("ODDISH",19), ("MEOWTH",18), ("PIDGEY",22), ("ODDISH",22), ("GROWLITHE",18), ("GROWLITHE",20), ("MEOWTH",17), ("MEOWTH",19), ("MEOWTH",17), ("MEOWTH",20)]);
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Route8, Route7];
    private static EarlyLandEncounterTable Create(string map, string symbol, int rate, (string Species, byte Level)[] rows) =>
        new(new(map), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, symbol, rate,
            rows.Select((s,i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Species), s.Level, s.Level,
                EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
