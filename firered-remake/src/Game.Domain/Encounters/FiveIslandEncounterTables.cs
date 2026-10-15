using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Exact pinned FireRed Five Island tables. Rod subdivisions retain their source indexes.</summary>
public static class FiveIslandEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(new("map.five-island-meadow"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sFiveIslandMeadow_FireRed",21,[new(0,new("pret:SPECIES_PIDGEY"),44,44,20),new(1,new("pret:SPECIES_SENTRET"),10,10,20),new(2,new("pret:SPECIES_PIDGEOTTO"),48,48,10),new(3,new("pret:SPECIES_HOPPIP"),10,10,10),new(4,new("pret:SPECIES_SENTRET"),15,15,10),new(5,new("pret:SPECIES_MEOWTH"),41,41,10),new(6,new("pret:SPECIES_HOPPIP"),15,15,5),new(7,new("pret:SPECIES_PSYDUCK"),41,41,5),new(8,new("pret:SPECIES_PIDGEOTTO"),50,50,4),new(9,new("pret:SPECIES_PERSIAN"),47,47,4),new(10,new("pret:SPECIES_PIDGEOTTO"),50,50,1),new(11,new("pret:SPECIES_PERSIAN"),50,50,1)]),
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(new("map.five-island-meadow"),"MAP_FIVE_ISLAND_MEADOW",EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sFiveIslandMeadow_FireRed",2,[new(0,new("pret:SPECIES_TENTACOOL"),5,35,60),new(1,new("pret:SPECIES_HOPPIP"),5,15,30),new(2,new("pret:SPECIES_TENTACOOL"),35,40,5),new(3,new("pret:SPECIES_TENTACRUEL"),35,40,4),new(4,new("pret:SPECIES_TENTACRUEL"),35,40,1)]),
        new(new("map.five-island"),"MAP_FIVE_ISLAND",EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sFiveIsland_FireRed",1,[new(0,new("pret:SPECIES_TENTACOOL"),5,35,60),new(1,new("pret:SPECIES_HOPPIP"),5,15,30),new(2,new("pret:SPECIES_TENTACOOL"),35,40,5),new(3,new("pret:SPECIES_TENTACRUEL"),35,40,4),new(4,new("pret:SPECIES_TENTACRUEL"),35,40,1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> FishingSource { get; } = [
        new(new("map.five-island-meadow"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sFiveIslandMeadow_FireRed",20,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_HORSEA"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_HORSEA"),5,15,20),new(5,new("pret:SPECIES_HORSEA"),15,25,40),new(6,new("pret:SPECIES_QWILFISH"),15,25,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_SEADRA"),25,35,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
        new(new("map.five-island"),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sFiveIsland_FireRed",10,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_HORSEA"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_HORSEA"),5,15,20),new(5,new("pret:SPECIES_HORSEA"),15,25,40),new(6,new("pret:SPECIES_SHELLDER"),15,25,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_SEADRA"),25,35,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = FishingSource.Select(t => t with { Slots = t.Slots.Take(2).ToImmutableArray() }).ToImmutableArray();
}
