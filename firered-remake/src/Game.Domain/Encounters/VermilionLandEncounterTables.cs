using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
/// <summary>Exact FireRed Route5/6 land slots. Surfing/fishing and later maps remain excluded.</summary>
public static class VermilionLandEncounterTables
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = [new("pret:SPECIES_MEOWTH"),new("pret:SPECIES_PIDGEY"),new("pret:SPECIES_ODDISH")];
    public static EarlyLandEncounterTable Route5 { get; } = Create("map.route-5","sRoute5_FireRed");
    public static EarlyLandEncounterTable Route6 { get; } = Create("map.route-6","sRoute6_FireRed");
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Route5,Route6];
    private static EarlyLandEncounterTable Create(string map,string symbol) =>
        new(new(map),EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,symbol,
            "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c",21,
            new (string Species,byte Level)[]{("MEOWTH",10),("PIDGEY",13),("ODDISH",13),("MEOWTH",12),("ODDISH",15),("PIDGEY",15),("ODDISH",16),("PIDGEY",16),("PIDGEY",15),("MEOWTH",14),("PIDGEY",15),("MEOWTH",16)}
            .Select((s,i)=>new EarlyLandEncounterSlot(i,new("pret:SPECIES_"+s.Species),s.Level,s.Level,EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
