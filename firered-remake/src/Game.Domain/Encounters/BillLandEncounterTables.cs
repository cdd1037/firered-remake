using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
/// <summary>Exact FireRed Route24/25 land slots; water/fishing and trainer-only Slowpoke remain excluded.</summary>
public static class BillLandEncounterTables
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = [new("pret:SPECIES_ODDISH"),new("pret:SPECIES_ABRA")];
    public static EarlyLandEncounterTable Route24 { get; } = Create("map.route-24",21,[("WEEDLE",7),("CATERPIE",7),("PIDGEY",11),("ODDISH",12),("ODDISH",13),("ABRA",10),("PIDGEY",13),("ODDISH",14),("KAKUNA",8),("ABRA",8),("METAPOD",8),("ABRA",12)]);
    public static EarlyLandEncounterTable Route25 { get; } = Create("map.route-25",21,[("WEEDLE",8),("CATERPIE",8),("PIDGEY",13),("ODDISH",14),("ODDISH",13),("ABRA",11),("PIDGEY",11),("ODDISH",12),("KAKUNA",9),("ABRA",9),("METAPOD",9),("ABRA",13)]);
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Route24,Route25];
    private static EarlyLandEncounterTable Create(string map,int rate,(string Species,byte Level)[] slots) =>
        new(new(map),rate,
            slots.Select((s,i)=>new EarlyLandEncounterSlot(i,new("pret:SPECIES_"+s.Species),s.Level,s.Level,EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
