using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Source encounter declarations; runtime tables use the shared water catalog.</summary>
public static class CinnabarWaterEncounterCatalog
{
    public static ImmutableArray<WaterEncounterTable> SourceAdditions { get; } = [
        new(new("map.route-21-north"),2,[new(0,new("pret:SPECIES_TENTACOOL"),5,10,60),new(1,new("pret:SPECIES_TENTACOOL"),10,20,30),new(2,new("pret:SPECIES_TENTACOOL"),20,30,5),new(3,new("pret:SPECIES_TENTACOOL"),30,35,4),new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
        new(new("map.route-21-south"),2,[new(0,new("pret:SPECIES_TENTACOOL"),5,10,60),new(1,new("pret:SPECIES_TENTACOOL"),10,20,30),new(2,new("pret:SPECIES_TENTACOOL"),20,30,5),new(3,new("pret:SPECIES_TENTACOOL"),30,35,4),new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
        new(new("map.cinnabar-island"),1,[new(0,new("pret:SPECIES_TENTACOOL"),5,10,60),new(1,new("pret:SPECIES_TENTACOOL"),10,20,30),new(2,new("pret:SPECIES_TENTACOOL"),20,30,5),new(3,new("pret:SPECIES_TENTACOOL"),30,35,4),new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
    ];

    public static bool MatchesDefinition(WaterEncounterTable? table) =>
        table is not null && SourceAdditions.Any(expected => WaterEncounterCatalog.SameDefinition(table, expected));
}
