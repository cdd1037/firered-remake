using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
/// <summary>Exact pinned FireRed Tower3F–7F slots; 5F purified tiles have no encounter attribute.</summary>
public static class PokemonTowerLandEncounterTables
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = [new("pret:SPECIES_GASTLY"),new("pret:SPECIES_HAUNTER"),new("pret:SPECIES_CUBONE")];
    public static EarlyLandEncounterTable Floor3 { get; } = Create(3, 2, [("GASTLY",15),("GASTLY",16),("GASTLY",17),("GASTLY",13),("GASTLY",14),("GASTLY",18),("GASTLY",19),("CUBONE",15),("GASTLY",17),("CUBONE",17),("GASTLY",17),("HAUNTER",20)]);
    public static EarlyLandEncounterTable Floor4 { get; } = Create(4, 4, [("GASTLY",15),("GASTLY",16),("GASTLY",17),("GASTLY",13),("GASTLY",14),("GASTLY",18),("HAUNTER",20),("CUBONE",15),("GASTLY",17),("CUBONE",17),("GASTLY",17),("GASTLY",19)]);
    public static EarlyLandEncounterTable Floor5 { get; } = Create(5, 6, [("GASTLY",15),("GASTLY",16),("GASTLY",17),("GASTLY",13),("GASTLY",14),("GASTLY",18),("HAUNTER",20),("CUBONE",15),("GASTLY",17),("CUBONE",17),("GASTLY",17),("GASTLY",19)]);
    public static EarlyLandEncounterTable Floor6 { get; } = Create(6, 8, [("GASTLY",16),("GASTLY",17),("GASTLY",18),("GASTLY",14),("GASTLY",15),("GASTLY",19),("HAUNTER",21),("CUBONE",17),("GASTLY",18),("CUBONE",19),("GASTLY",18),("HAUNTER",23)]);
    public static EarlyLandEncounterTable Floor7 { get; } = Create(7, 10, [("GASTLY",16),("GASTLY",17),("GASTLY",18),("GASTLY",15),("GASTLY",19),("HAUNTER",23),("CUBONE",17),("CUBONE",19),("GASTLY",18),("HAUNTER",23),("GASTLY",18),("HAUNTER",25)]);
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Floor3,Floor4,Floor5,Floor6,Floor7];
    public static bool IsCave(MapId map) => All.Any(t => t.Map == map);
    private static EarlyLandEncounterTable Create(int floor,int rate,(string Species,byte Level)[] rows) => new(new("map.pokemon-tower-"+floor+"f"),rate,rows.Select((s,i)=>new EarlyLandEncounterSlot(i,new("pret:SPECIES_"+s.Species),s.Level,s.Level,EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
