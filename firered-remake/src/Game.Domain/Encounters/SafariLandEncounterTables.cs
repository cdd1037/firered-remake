using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
/// <summary>Exact pinned FireRed Safari ground tables; surf and fishing are excluded.</summary>
public static class SafariLandEncounterTables
{
    public static EarlyLandEncounterTable Center { get; } = Create("Center", [("RHYHORN",25),("NIDORAN_M",22),("EXEGGCUTE",24),("EXEGGCUTE",25),("VENONAT",22),("NIDORINO",31),("NIDORINA",31),("PARASECT",30),("VENONAT",22),("SCYTHER",23),("VENONAT",22),("CHANSEY",23)]);
    public static EarlyLandEncounterTable East { get; } = Create("East", [("NIDORAN_M",24),("DODUO",26),("EXEGGCUTE",23),("EXEGGCUTE",25),("PARAS",22),("NIDORINO",33),("NIDORAN_F",24),("PARASECT",25),("PARAS",22),("KANGASKHAN",25),("PARAS",22),("SCYTHER",28)]);
    public static EarlyLandEncounterTable North { get; } = Create("North", [("RHYHORN",26),("NIDORAN_M",30),("EXEGGCUTE",25),("EXEGGCUTE",27),("PARAS",23),("NIDORINO",30),("NIDORINA",30),("VENOMOTH",32),("PARAS",23),("CHANSEY",26),("PARAS",23),("TAUROS",28)]);
    public static EarlyLandEncounterTable West { get; } = Create("West", [("DODUO",26),("NIDORAN_M",22),("EXEGGCUTE",25),("EXEGGCUTE",27),("VENONAT",23),("NIDORINO",30),("NIDORAN_F",30),("VENOMOTH",32),("VENONAT",23),("TAUROS",25),("VENONAT",23),("KANGASKHAN",28)]);
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [Center, East, North, West];
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = All.SelectMany(t => t.Slots).Select(s => s.Species).Distinct().ToImmutableArray();
    private static EarlyLandEncounterTable Create(string area, (string Species, byte Level)[] rows) =>
        new(new("map.safari-zone-" + area.ToLowerInvariant()), EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath,
            "sSafariZone" + area + "_FireRed", 21,
            rows.Select((s, i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Species), s.Level, s.Level, EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}
