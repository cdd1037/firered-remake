using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

public sealed record SeafoamHeldItemSource(SpeciesId Species, string Common, string Rare);
public static class SeafoamEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(new("map.seafoam-islands-1f"), 7, [new(0,new("pret:SPECIES_PSYDUCK"),27,27,20), new(1,new("pret:SPECIES_PSYDUCK"),29,29,20), new(2,new("pret:SPECIES_PSYDUCK"),31,31,10), new(3,new("pret:SPECIES_ZUBAT"),22,22,10), new(4,new("pret:SPECIES_ZUBAT"),22,22,10), new(5,new("pret:SPECIES_ZUBAT"),24,24,10), new(6,new("pret:SPECIES_GOLBAT"),26,26,5), new(7,new("pret:SPECIES_GOLBAT"),28,28,5), new(8,new("pret:SPECIES_PSYDUCK"),33,33,4), new(9,new("pret:SPECIES_ZUBAT"),26,26,4), new(10,new("pret:SPECIES_PSYDUCK"),26,26,1), new(11,new("pret:SPECIES_GOLBAT"),30,30,1)]),
        new(new("map.seafoam-islands-b1f"), 7, [new(0,new("pret:SPECIES_PSYDUCK"),29,29,20), new(1,new("pret:SPECIES_PSYDUCK"),31,31,20), new(2,new("pret:SPECIES_SEEL"),28,28,10), new(3,new("pret:SPECIES_ZUBAT"),22,22,10), new(4,new("pret:SPECIES_ZUBAT"),22,22,10), new(5,new("pret:SPECIES_ZUBAT"),24,24,10), new(6,new("pret:SPECIES_GOLBAT"),26,26,5), new(7,new("pret:SPECIES_GOLBAT"),28,28,5), new(8,new("pret:SPECIES_GOLDUCK"),33,33,4), new(9,new("pret:SPECIES_ZUBAT"),26,26,4), new(10,new("pret:SPECIES_GOLDUCK"),35,35,1), new(11,new("pret:SPECIES_GOLBAT"),30,30,1)]),
        new(new("map.seafoam-islands-b2f"), 7, [new(0,new("pret:SPECIES_PSYDUCK"),30,30,20), new(1,new("pret:SPECIES_PSYDUCK"),32,32,20), new(2,new("pret:SPECIES_SEEL"),30,30,10), new(3,new("pret:SPECIES_SEEL"),32,32,10), new(4,new("pret:SPECIES_ZUBAT"),22,22,10), new(5,new("pret:SPECIES_ZUBAT"),24,24,10), new(6,new("pret:SPECIES_GOLBAT"),26,26,5), new(7,new("pret:SPECIES_GOLDUCK"),34,34,5), new(8,new("pret:SPECIES_GOLDUCK"),32,32,4), new(9,new("pret:SPECIES_GOLBAT"),28,28,4), new(10,new("pret:SPECIES_GOLDUCK"),32,32,1), new(11,new("pret:SPECIES_GOLBAT"),30,30,1)]),
        new(new("map.seafoam-islands-b3f"), 7, [new(0,new("pret:SPECIES_SEEL"),30,30,20), new(1,new("pret:SPECIES_SEEL"),32,32,20), new(2,new("pret:SPECIES_PSYDUCK"),32,32,10), new(3,new("pret:SPECIES_PSYDUCK"),30,30,10), new(4,new("pret:SPECIES_GOLDUCK"),32,32,10), new(5,new("pret:SPECIES_ZUBAT"),24,24,10), new(6,new("pret:SPECIES_GOLBAT"),26,26,5), new(7,new("pret:SPECIES_GOLDUCK"),34,34,5), new(8,new("pret:SPECIES_DEWGONG"),32,32,4), new(9,new("pret:SPECIES_GOLBAT"),28,28,4), new(10,new("pret:SPECIES_DEWGONG"),34,34,1), new(11,new("pret:SPECIES_GOLBAT"),30,30,1)]),
        new(new("map.seafoam-islands-b4f"), 7, [new(0,new("pret:SPECIES_SEEL"),30,30,20), new(1,new("pret:SPECIES_SEEL"),32,32,20), new(2,new("pret:SPECIES_PSYDUCK"),32,32,10), new(3,new("pret:SPECIES_SEEL"),34,34,10), new(4,new("pret:SPECIES_GOLDUCK"),32,32,10), new(5,new("pret:SPECIES_GOLBAT"),26,26,10), new(6,new("pret:SPECIES_DEWGONG"),34,34,5), new(7,new("pret:SPECIES_GOLDUCK"),34,34,5), new(8,new("pret:SPECIES_DEWGONG"),36,36,4), new(9,new("pret:SPECIES_GOLBAT"),28,28,4), new(10,new("pret:SPECIES_DEWGONG"),36,36,1), new(11,new("pret:SPECIES_GOLBAT"),30,30,1)]),
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(new("map.seafoam-islands-b3f"), 2, [new(0,new("pret:SPECIES_SEEL"),25,35,60), new(1,new("pret:SPECIES_HORSEA"),25,30,30), new(2,new("pret:SPECIES_DEWGONG"),35,40,5), new(3,new("pret:SPECIES_PSYDUCK"),30,40,4), new(4,new("pret:SPECIES_GOLDUCK"),35,40,1)]),
        new(new("map.seafoam-islands-b4f"), 2, [new(0,new("pret:SPECIES_SEEL"),25,35,60), new(1,new("pret:SPECIES_HORSEA"),25,30,30), new(2,new("pret:SPECIES_DEWGONG"),35,40,5), new(3,new("pret:SPECIES_PSYDUCK"),30,40,4), new(4,new("pret:SPECIES_GOLDUCK"),35,40,1)]),
        new(new("map.route-19"), 2, [new(0,new("pret:SPECIES_TENTACOOL"),5,10,60), new(1,new("pret:SPECIES_TENTACOOL"),10,20,30), new(2,new("pret:SPECIES_TENTACOOL"),20,30,5), new(3,new("pret:SPECIES_TENTACOOL"),30,35,4), new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
        new(new("map.route-20"), 2, [new(0,new("pret:SPECIES_TENTACOOL"),5,10,60), new(1,new("pret:SPECIES_TENTACOOL"),10,20,30), new(2,new("pret:SPECIES_TENTACOOL"),20,30,5), new(3,new("pret:SPECIES_TENTACOOL"),30,35,4), new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = [
        new(new("map.seafoam-islands-b3f"), 20, [new(0,new("pret:SPECIES_MAGIKARP"),5,5,70), new(1,new("pret:SPECIES_MAGIKARP"),5,5,30)]),
        new(new("map.seafoam-islands-b4f"), 20, [new(0,new("pret:SPECIES_MAGIKARP"),5,5,70), new(1,new("pret:SPECIES_MAGIKARP"),5,5,30)]),
        new(new("map.route-19"), 20, [new(0,new("pret:SPECIES_MAGIKARP"),5,5,70), new(1,new("pret:SPECIES_MAGIKARP"),5,5,30)]),
        new(new("map.route-20"), 20, [new(0,new("pret:SPECIES_MAGIKARP"),5,5,70), new(1,new("pret:SPECIES_MAGIKARP"),5,5,30)]),
    ];
    public static ImmutableArray<OldRodEncounterTable> SuperRodSource { get; } = [
        new(new("map.seafoam-islands-b3f"), 20, [new(5,new("pret:SPECIES_HORSEA"),15,25,40), new(6,new("pret:SPECIES_HORSEA"),20,30,40), new(7,new("pret:SPECIES_GYARADOS"),15,25,15), new(8,new("pret:SPECIES_PSYDUCK"),15,25,4), new(9,new("pret:SPECIES_GYARADOS"),25,35,1)]),
        new(new("map.seafoam-islands-b4f"), 20, [new(5,new("pret:SPECIES_HORSEA"),15,25,40), new(6,new("pret:SPECIES_HORSEA"),20,30,40), new(7,new("pret:SPECIES_GYARADOS"),15,25,15), new(8,new("pret:SPECIES_PSYDUCK"),15,25,4), new(9,new("pret:SPECIES_GYARADOS"),25,35,1)]),
        new(new("map.route-19"), 20, [new(5,new("pret:SPECIES_HORSEA"),15,25,40), new(6,new("pret:SPECIES_HORSEA"),15,25,40), new(7,new("pret:SPECIES_GYARADOS"),15,25,15), new(8,new("pret:SPECIES_SEADRA"),25,35,4), new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
        new(new("map.route-20"), 20, [new(5,new("pret:SPECIES_HORSEA"),15,25,40), new(6,new("pret:SPECIES_HORSEA"),15,25,40), new(7,new("pret:SPECIES_GYARADOS"),15,25,15), new(8,new("pret:SPECIES_SEADRA"),25,35,4), new(9,new("pret:SPECIES_PSYDUCK"),25,35,1)]),
    ];
    public static ImmutableArray<SeafoamHeldItemSource> HeldItems { get; } = [
        new(new("pret:SPECIES_ARTICUNO"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_DEWGONG"),"ITEM_ASPEAR_BERRY","ITEM_NEVER_MELT_ICE"),
        new(new("pret:SPECIES_GOLBAT"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_GOLDUCK"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_GYARADOS"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_HORSEA"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_KRABBY"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_MAGIKARP"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_PSYDUCK"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_SEADRA"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_SEEL"),"ITEM_ASPEAR_BERRY","ITEM_NONE"),
        new(new("pret:SPECIES_TENTACOOL"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_ZUBAT"),"ITEM_NONE","ITEM_NONE"),
    ];
}
