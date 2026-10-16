using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

public sealed record PowerPlantHeldItemSource(SpeciesId Species, string Common, string Rare);
/// <summary>Exact FireRed Power Plant table, generated only from pinned source.</summary>
public static class PowerPlantEncounterTables
{
    public static MapId Map { get; } = new("map.power-plant");
    public static EarlyLandEncounterTable Land { get; } = new(Map, 7,
        [new(0,new("pret:SPECIES_VOLTORB"),22,22,20),
            new(1,new("pret:SPECIES_MAGNEMITE"),22,22,20),
            new(2,new("pret:SPECIES_VOLTORB"),25,25,10),
            new(3,new("pret:SPECIES_MAGNEMITE"),25,25,10),
            new(4,new("pret:SPECIES_PIKACHU"),22,22,10),
            new(5,new("pret:SPECIES_PIKACHU"),24,24,10),
            new(6,new("pret:SPECIES_MAGNETON"),31,31,5),
            new(7,new("pret:SPECIES_MAGNETON"),34,34,5),
            new(8,new("pret:SPECIES_PIKACHU"),26,26,4),
            new(9,new("pret:SPECIES_ELECTABUZZ"),32,32,4),
            new(10,new("pret:SPECIES_PIKACHU"),26,26,1),
            new(11,new("pret:SPECIES_ELECTABUZZ"),35,35,1)]);
    public static ImmutableArray<PowerPlantHeldItemSource> HeldItems { get; } = [
        new(new("pret:SPECIES_ELECTABUZZ"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_ELECTRODE"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_MAGNEMITE"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_MAGNETON"),"ITEM_NONE","ITEM_MAGNET"),
        new(new("pret:SPECIES_PIKACHU"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_VOLTORB"),"ITEM_NONE","ITEM_NONE"),
        new(new("pret:SPECIES_ZAPDOS"),"ITEM_NONE","ITEM_NONE")];
}
