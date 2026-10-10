using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Exact revision-0 FireRed Route9/10 and both RockTunnel land tables.</summary>
public static class RockTunnelLandEncounterTables
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } =
        [new("pret:SPECIES_EKANS"), new("pret:SPECIES_SPEAROW"), new("pret:SPECIES_RATTATA"), new("pret:SPECIES_VOLTORB"), new("pret:SPECIES_ZUBAT"), new("pret:SPECIES_GEODUDE"), new("pret:SPECIES_MANKEY"), new("pret:SPECIES_MACHOP"), new("pret:SPECIES_ONIX")];
    public static EarlyLandEncounterTable Route9 { get; } = Create("map.route-9", "sRoute9_FireRed", 21,
        [("SPEAROW",16), ("RATTATA",16), ("EKANS",11), ("EKANS",15), ("SPEAROW",13), ("RATTATA",14), ("SPEAROW",17), ("RATTATA",17), ("RATTATA",14), ("EKANS",13), ("RATTATA",14), ("EKANS",17)]);
    public static EarlyLandEncounterTable Route10 { get; } = Create("map.route-10", "sRoute10_FireRed", 21,
        [("SPEAROW",16), ("VOLTORB",16), ("EKANS",11), ("EKANS",15), ("SPEAROW",13), ("VOLTORB",14), ("SPEAROW",17), ("VOLTORB",17), ("VOLTORB",14), ("EKANS",13), ("VOLTORB",14), ("EKANS",17)]);
    public static EarlyLandEncounterTable RockTunnel1F { get; } = Create("map.rock-tunnel-1f", "sRockTunnel1F_FireRed", 7,
        [("ZUBAT",15), ("GEODUDE",16), ("MANKEY",16), ("GEODUDE",17), ("ZUBAT",16), ("MACHOP",16), ("MANKEY",17), ("MACHOP",17), ("GEODUDE",15), ("ONIX",13), ("GEODUDE",15), ("ONIX",15)]);
    public static EarlyLandEncounterTable RockTunnelB1F { get; } = Create("map.rock-tunnel-b1f", "sRockTunnelB1F_FireRed", 7,
        [("ZUBAT",16), ("GEODUDE",17), ("MANKEY",17), ("GEODUDE",16), ("ZUBAT",15), ("MACHOP",17), ("MANKEY",16), ("ONIX",13), ("GEODUDE",15), ("ONIX",15), ("GEODUDE",15), ("ONIX",17)]);
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Route9, Route10, RockTunnel1F, RockTunnelB1F];
    public static bool IsCave(MapId map) => map == RockTunnel1F.Map || map == RockTunnelB1F.Map;
    private static EarlyLandEncounterTable Create(string map, string symbol, int rate, (string Species, byte Level)[] rows) =>
        new(new(map), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, symbol,
            "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c", rate,
            rows.Select((s,i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Species), s.Level, s.Level,
                EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
