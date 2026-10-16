using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Exact pinned FireRed Six Island tables. Rod subdivisions retain their source indexes.</summary>
public static class SixIslandEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(new("map.six-island-water-path"),21,[new(0,new("pret:SPECIES_SPEAROW"),44,44,20),new(1,new("pret:SPECIES_SENTRET"),10,10,20),new(2,new("pret:SPECIES_ODDISH"),44,44,10),new(3,new("pret:SPECIES_FEAROW"),48,48,10),new(4,new("pret:SPECIES_SENTRET"),15,15,10),new(5,new("pret:SPECIES_MEOWTH"),41,41,10),new(6,new("pret:SPECIES_GLOOM"),48,48,5),new(7,new("pret:SPECIES_PSYDUCK"),41,41,5),new(8,new("pret:SPECIES_FEAROW"),50,50,4),new(9,new("pret:SPECIES_PERSIAN"),47,47,4),new(10,new("pret:SPECIES_FEAROW"),50,50,1),new(11,new("pret:SPECIES_PERSIAN"),50,50,1)]),
        new(new("map.six-island-ruin-valley"),21,[new(0,new("pret:SPECIES_NATU"),15,15,20),new(1,new("pret:SPECIES_SPEAROW"),44,44,20),new(2,new("pret:SPECIES_YANMA"),18,18,10),new(3,new("pret:SPECIES_WOOPER"),15,15,10),new(4,new("pret:SPECIES_FEAROW"),49,49,10),new(5,new("pret:SPECIES_MEOWTH"),43,43,10),new(6,new("pret:SPECIES_WOBBUFFET"),25,25,5),new(7,new("pret:SPECIES_PSYDUCK"),41,41,5),new(8,new("pret:SPECIES_NATU"),20,20,4),new(9,new("pret:SPECIES_PERSIAN"),49,49,4),new(10,new("pret:SPECIES_NATU"),20,20,1),new(11,new("pret:SPECIES_PERSIAN"),52,52,1)]),
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(new("map.six-island-water-path"),2,[new(0,new("pret:SPECIES_TENTACOOL"),5,20,60),new(1,new("pret:SPECIES_TENTACOOL"),20,35,30),new(2,new("pret:SPECIES_TENTACOOL"),35,40,5),new(3,new("pret:SPECIES_TENTACRUEL"),35,40,4),new(4,new("pret:SPECIES_TENTACRUEL"),35,40,1)]),
        new(new("map.six-island-ruin-valley"),2,[new(0,new("pret:SPECIES_WOOPER"),5,20,60),new(1,new("pret:SPECIES_WOOPER"),10,20,30),new(2,new("pret:SPECIES_WOOPER"),15,25,5),new(3,new("pret:SPECIES_WOOPER"),20,25,4),new(4,new("pret:SPECIES_WOOPER"),20,25,1)]),
        new(new("map.six-island-green-path"),2,[new(0,new("pret:SPECIES_TENTACOOL"),5,20,60),new(1,new("pret:SPECIES_TENTACOOL"),20,35,30),new(2,new("pret:SPECIES_TENTACOOL"),35,40,5),new(3,new("pret:SPECIES_TENTACRUEL"),35,40,4),new(4,new("pret:SPECIES_TENTACRUEL"),35,40,1)]),
        new(new("map.six-island-outcast-island"),2,[new(0,new("pret:SPECIES_TENTACOOL"),5,20,60),new(1,new("pret:SPECIES_TENTACOOL"),20,35,30),new(2,new("pret:SPECIES_TENTACOOL"),35,40,5),new(3,new("pret:SPECIES_TENTACRUEL"),35,40,4),new(4,new("pret:SPECIES_TENTACRUEL"),35,40,1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> FishingSource { get; } = [
        new(new("map.six-island-water-path"),20,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_HORSEA"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_HORSEA"),5,15,20),new(5,new("pret:SPECIES_HORSEA"),15,25,40),new(6,new("pret:SPECIES_QWILFISH"),15,25,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_SEADRA"),25,35,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
        new(new("map.six-island-ruin-valley"),20,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_POLIWAG"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_GOLDEEN"),5,15,20),new(5,new("pret:SPECIES_POLIWAG"),15,25,40),new(6,new("pret:SPECIES_POLIWHIRL"),20,30,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_PSYDUCK"),15,25,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
        new(new("map.six-island-green-path"),20,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_HORSEA"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_HORSEA"),5,15,20),new(5,new("pret:SPECIES_HORSEA"),15,25,40),new(6,new("pret:SPECIES_QWILFISH"),15,25,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_SEADRA"),25,35,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
        new(new("map.six-island-outcast-island"),20,[new(0,new("pret:SPECIES_MAGIKARP"),5,5,70),new(1,new("pret:SPECIES_MAGIKARP"),5,5,30),new(2,new("pret:SPECIES_HORSEA"),5,15,60),new(3,new("pret:SPECIES_MAGIKARP"),5,15,20),new(4,new("pret:SPECIES_HORSEA"),5,15,20),new(5,new("pret:SPECIES_HORSEA"),15,25,40),new(6,new("pret:SPECIES_QWILFISH"),15,25,40),new(7,new("pret:SPECIES_GYARADOS"),15,25,15),new(8,new("pret:SPECIES_SEADRA"),25,35,4),new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
    ];
}
