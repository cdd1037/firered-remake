using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Original FireRed upper Route16 grass. Cycling-road admission is separate.</summary>
public static class Route16LandEncounterTables
{
    public static MapId Map { get; } = new("map.route-16");
    public static EarlyLandEncounterTable Route16 { get; } = new(Map, EarlyLandEncounterRules.SourceCommit,
        EarlyLandEncounterRules.SourcePath, "sRoute16_FireRed", EarlyLandEncounterRules.Route1Table.SourceSha256, 21,
        new[] { ("SPEAROW",20), ("DODUO",18), ("RATTATA",18), ("RATTATA",20), ("SPEAROW",22), ("DODUO",20),
            ("RATTATA",22), ("DODUO",22), ("RATTATA",18), ("RATICATE",23), ("RATTATA",18), ("RATICATE",25) }
            .Select((s,i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Item1), (byte)s.Item2, (byte)s.Item2,
                EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
