using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;
/// <summary>Preserves all 14×93 previous cells and adds the exact source Muk compatibility column.</summary>
public static class CeladonApproachMachineRules
{
    public const string Profile = "firered-celadon-approach-machines-v1";
    private const string MukCompatibility = "10010111110100";
    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        if (catalog.Profile != Profile || catalog.Compatibility.IsDefault || catalog.Compatibility.Length != 94 ||
            catalog.AdditionalMachines is not { IsDefault: false, Length: 13 } extra)
            throw new NotSupportedException("Exact Celadon approach machine catalog required.");
        RockTunnelMachineRules.ValidateCatalog(catalog with {
            Profile = RockTunnelMachineRules.Profile,
            Compatibility = catalog.Compatibility.Take(93).ToImmutableArray(),
            AdditionalMachines = extra.Select(m => m with { Compatibility = m.Compatibility.Take(93).ToImmutableArray() }).ToImmutableArray()
        });
        var values = new[] { PewterMachineRules.Definition(catalog, PewterMachineRules.Tm39Id)! }.Concat(extra).ToArray();
        for (int row = 0; row < values.Length; row++)
        {
            var compatibility = values[row].Compatibility;
            if (compatibility.IsDefault || compatibility.Length != 94) throw new NotSupportedException("Celadon approach machine shape differs.");
            var cell = compatibility[93];
            if (cell.Species != new SpeciesId("pret:SPECIES_MUK") || cell.SourceSpeciesId != 89 ||
                cell.CanLearn != (MukCompatibility[row] == '1') || cell.Source != new OpeningItemSourceRef(
                    "src/data/pokemon/tmhm_learnsets.h", "SPECIES_MUK", 2035, 2063,
                    "92bf603f90469ccae0398902db943770e2cb12cb910883b42a552cff07cff31f"))
                throw new NotSupportedException("Pinned Muk machine compatibility differs.");
        }
    }
}
