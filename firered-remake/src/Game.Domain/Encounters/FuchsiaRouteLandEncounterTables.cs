using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
/// <summary>Exact fixed FireRed land slots for the southern route. Surf/fishing are separate contracts.</summary>
public static class FuchsiaRouteLandEncounterTables
{
    public static EarlyLandEncounterTable Route12 {get;} = Create(12,21,[("ODDISH",24),("VENONAT",24),("ODDISH",22),("PIDGEY",23),("PIDGEY",25),("VENONAT",26),("ODDISH",26),("PIDGEY",27),("PIDGEY",23),("GLOOM",28),("PIDGEY",23),("GLOOM",30)]);
    public static EarlyLandEncounterTable Route13 {get;} = Create(13,21,[("ODDISH",24),("VENONAT",24),("ODDISH",22),("PIDGEY",27),("PIDGEY",25),("VENONAT",26),("ODDISH",26),("DITTO",25),("PIDGEOTTO",29),("GLOOM",28),("PIDGEOTTO",29),("GLOOM",30)]);
    public static EarlyLandEncounterTable Route14 {get;} = Create(14,21,[("ODDISH",24),("VENONAT",24),("ODDISH",22),("DITTO",23),("PIDGEY",27),("VENONAT",26),("ODDISH",26),("GLOOM",30),("DITTO",23),("PIDGEOTTO",29),("DITTO",23),("PIDGEOTTO",29)]);
    public static EarlyLandEncounterTable Route15 {get;} = Create(15,21,[("ODDISH",24),("VENONAT",24),("ODDISH",22),("PIDGEY",27),("PIDGEY",25),("VENONAT",26),("ODDISH",26),("DITTO",25),("PIDGEOTTO",29),("GLOOM",28),("PIDGEOTTO",29),("GLOOM",30)]);
    public static ImmutableArray<EarlyLandEncounterTable> All {get;} = [Route12,Route13,Route14,Route15];
    public static ImmutableArray<SpeciesId> WildSpecies {get;} = All.SelectMany(t=>t.Slots).Select(s=>s.Species).Distinct().ToImmutableArray();
    private static EarlyLandEncounterTable Create(int route,int rate,(string Species,byte Level)[] rows)=>new(new("map.route-"+route),rate,rows.Select((s,i)=>new EarlyLandEncounterSlot(i,new("pret:SPECIES_"+s.Species),s.Level,s.Level,EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
