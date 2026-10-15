using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Three exact water-table additions over Surf21; no historical table proof is relabelled.</summary>
public sealed class CinnabarWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,CinnabarWaterEncounterCatalog> Proofs = new();
    public WaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private CinnabarWaterEncounterCatalog(WaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions { get; } = [
        new(new("map.route-21-north"),"MAP_ROUTE21_NORTH",EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sRoute21North_FireRed",2,[new(0,new("pret:SPECIES_TENTACOOL"),5,10,60),new(1,new("pret:SPECIES_TENTACOOL"),10,20,30),new(2,new("pret:SPECIES_TENTACOOL"),20,30,5),new(3,new("pret:SPECIES_TENTACOOL"),30,35,4),new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
        new(new("map.route-21-south"),"MAP_ROUTE21_SOUTH",EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sRoute21South_FireRed",2,[new(0,new("pret:SPECIES_TENTACOOL"),5,10,60),new(1,new("pret:SPECIES_TENTACOOL"),10,20,30),new(2,new("pret:SPECIES_TENTACOOL"),20,30,5),new(3,new("pret:SPECIES_TENTACOOL"),30,35,4),new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
        new(new("map.cinnabar-island"),"MAP_CINNABAR_ISLAND",EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"sCinnabarIsland_FireRed",1,[new(0,new("pret:SPECIES_TENTACOOL"),5,10,60),new(1,new("pret:SPECIES_TENTACOOL"),10,20,30),new(2,new("pret:SPECIES_TENTACOOL"),20,30,5),new(3,new("pret:SPECIES_TENTACOOL"),30,35,4),new(4,new("pret:SPECIES_TENTACOOL"),35,40,1)]),
    ];
    public static CinnabarWaterEncounterCatalog Authenticate(WaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 21 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 3 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 3)
            throw new ArgumentException("cinnabar_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("cinnabar_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new CinnabarWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}
