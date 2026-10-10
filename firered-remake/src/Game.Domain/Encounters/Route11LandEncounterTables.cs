using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Original FireRed Route11 and Digletts Cave B1F land tables, including the rare level31 Dugtrio.</summary>
public static class Route11LandEncounterTables
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } =
        [new("pret:SPECIES_EKANS"), new("pret:SPECIES_SPEAROW"), new("pret:SPECIES_DROWZEE"), new("pret:SPECIES_DIGLETT"), new("pret:SPECIES_DUGTRIO")];
    public static EarlyLandEncounterTable Route11 { get; } = Create("map.route-11", "sRoute11_FireRed", 21,
        [("EKANS",14),("SPEAROW",15),("EKANS",12),("SPEAROW",13),("DROWZEE",11),("DROWZEE",13),
         ("EKANS",15),("SPEAROW",17),("EKANS",12),("DROWZEE",15),("EKANS",12),("DROWZEE",15)]);
    public static EarlyLandEncounterTable DiglettsCaveB1F { get; } = Create("map.digletts-cave-b1f", "sDiglettsCaveB1F_FireRed", 5,
        [("DIGLETT",18),("DIGLETT",19),("DIGLETT",17),("DIGLETT",15),("DIGLETT",16),("DIGLETT",20),
         ("DIGLETT",21),("DIGLETT",22),("DIGLETT",17),("DUGTRIO",29),("DIGLETT",17),("DUGTRIO",31)]);
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Route11, DiglettsCaveB1F];
    public static bool IsCave(MapId map) => map == DiglettsCaveB1F.Map;
    private static EarlyLandEncounterTable Create(string map, string symbol, int rate, (string Species, byte Level)[] rows) =>
        new(new(map), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, symbol,
            "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c", rate,
            rows.Select((s,i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Species), s.Level, s.Level,
                EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
