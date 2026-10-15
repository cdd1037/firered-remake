using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Four exact land tables, Route23 Surf, and all original fishing declarations.</summary>
public static class VictoryRoadEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(new("map.victory-road-1f"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sVictoryRoad1F_FireRed",7,[new(0,new("pret:SPECIES_MACHOP"),32,32,20),new(1,new("pret:SPECIES_GEODUDE"),32,32,20),new(2,new("pret:SPECIES_ONIX"),40,40,10),new(3,new("pret:SPECIES_ONIX"),43,43,10),new(4,new("pret:SPECIES_ONIX"),46,46,10),new(5,new("pret:SPECIES_ZUBAT"),32,32,10),new(6,new("pret:SPECIES_ARBOK"),44,44,5),new(7,new("pret:SPECIES_GOLBAT"),44,44,5),new(8,new("pret:SPECIES_MAROWAK"),44,44,4),new(9,new("pret:SPECIES_MACHOKE"),44,44,4),new(10,new("pret:SPECIES_MACHOKE"),46,46,1),new(11,new("pret:SPECIES_MAROWAK"),46,46,1)]),
        new(new("map.victory-road-2f"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sVictoryRoad2F_FireRed",7,[new(0,new("pret:SPECIES_MACHOP"),34,34,20),new(1,new("pret:SPECIES_GEODUDE"),34,34,20),new(2,new("pret:SPECIES_PRIMEAPE"),42,42,10),new(3,new("pret:SPECIES_ONIX"),45,45,10),new(4,new("pret:SPECIES_ONIX"),48,48,10),new(5,new("pret:SPECIES_ZUBAT"),34,34,10),new(6,new("pret:SPECIES_ARBOK"),46,46,5),new(7,new("pret:SPECIES_GOLBAT"),46,46,5),new(8,new("pret:SPECIES_MAROWAK"),46,46,4),new(9,new("pret:SPECIES_MACHOKE"),46,46,4),new(10,new("pret:SPECIES_MACHOKE"),48,48,1),new(11,new("pret:SPECIES_MAROWAK"),48,48,1)]),
        new(new("map.victory-road-3f"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sVictoryRoad3F_FireRed",7,[new(0,new("pret:SPECIES_MACHOP"),32,32,20),new(1,new("pret:SPECIES_GEODUDE"),32,32,20),new(2,new("pret:SPECIES_ONIX"),40,40,10),new(3,new("pret:SPECIES_ONIX"),43,43,10),new(4,new("pret:SPECIES_ONIX"),46,46,10),new(5,new("pret:SPECIES_ZUBAT"),32,32,10),new(6,new("pret:SPECIES_ARBOK"),44,44,5),new(7,new("pret:SPECIES_GOLBAT"),44,44,5),new(8,new("pret:SPECIES_MAROWAK"),44,44,4),new(9,new("pret:SPECIES_MACHOKE"),44,44,4),new(10,new("pret:SPECIES_MACHOKE"),46,46,1),new(11,new("pret:SPECIES_MAROWAK"),46,46,1)]),
        new(new("map.route-23"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sRoute23_FireRed",21,[new(0,new("pret:SPECIES_MANKEY"),32,32,20),new(1,new("pret:SPECIES_FEAROW"),40,40,20),new(2,new("pret:SPECIES_MANKEY"),34,34,10),new(3,new("pret:SPECIES_SPEAROW"),34,34,10),new(4,new("pret:SPECIES_EKANS"),32,32,10),new(5,new("pret:SPECIES_EKANS"),34,34,10),new(6,new("pret:SPECIES_PRIMEAPE"),42,42,5),new(7,new("pret:SPECIES_ARBOK"),44,44,5),new(8,new("pret:SPECIES_SPEAROW"),32,32,4),new(9,new("pret:SPECIES_FEAROW"),42,42,4),new(10,new("pret:SPECIES_SPEAROW"),32,32,1),new(11,new("pret:SPECIES_FEAROW"),44,44,1)]),
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(new("map.route-23"),"MAP_ROUTE23",EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sRoute23_FireRed",2,[new(0,new("pret:SPECIES_PSYDUCK"),20,25,60),new(1,new("pret:SPECIES_PSYDUCK"),20,25,30),new(2,new("pret:SPECIES_PSYDUCK"),25,30,5),new(3,new("pret:SPECIES_PSYDUCK"),30,35,4),new(4,new("pret:SPECIES_PSYDUCK"),35,40,1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> FishingSource { get; } = [
        new(new("map.route-23"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sRoute23_FireRed",20,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_POLIWAG"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_GOLDEEN"),5,15,20),new(5,new("pret:SPECIES_POLIWAG"),15,25,40),new(6,new("pret:SPECIES_POLIWHIRL"),20,30,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_PSYDUCK"),15,25,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
    ];
    public static bool IsCave(MapId map) => Land.Any(t=>t.Map==map && map.Value.StartsWith("map.victory-road-",StringComparison.Ordinal));
}
