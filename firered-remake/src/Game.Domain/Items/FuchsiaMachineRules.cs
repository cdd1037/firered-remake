using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Four exact southern-route compatibility rows; the inherited eighteen machines stay unchanged.</summary>
public static class FuchsiaMachineRules
{
    public const string Profile = "firered-fuchsia-route-machines-v1";
    public const string SourcePath = EeveeMachineRules.SourcePath;
    private static readonly (string Symbol, int Id, int Start, int End, string Machines)[] Species = [
        ("FARFETCHD", 83, 1914, 1930, "TM06 TM10 TM11 TM17 TM21 TM23 TM27 TM32 TM40 TM42 TM43 TM44 TM45 TM46 TM47 HM01 HM02"),
        ("DODUO", 84, 1932, 1946, "TM06 TM10 TM11 TM17 TM21 TM27 TM32 TM40 TM42 TM43 TM44 TM45 TM46 TM47 HM02"),
        ("DODRIO", 85, 1948, 1965, "TM06 TM10 TM11 TM12 TM15 TM17 TM21 TM27 TM32 TM40 TM41 TM42 TM43 TM44 TM45 TM46 TM47 HM02"),
        ("DITTO", 132, 3106, 3106, "")
    ];

    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Compatibility.IsDefault || catalog.Compatibility.Length != 107 ||
            catalog.AdditionalMachines is not { IsDefault: false, Length: 17 } extra ||
            extra.Any(m => m is null || m.Compatibility.IsDefault || m.Compatibility.Length != 107))
            throw new NotSupportedException("fuchsia_machine_catalog_scope");
        EeveeMachineRules.ValidateCatalog(catalog with { Profile = EeveeMachineRules.Profile,
            Compatibility = catalog.Compatibility.Take(103).ToImmutableArray(),
            AdditionalMachines = extra.Select(m => m with { Compatibility = m.Compatibility.Take(103).ToImmutableArray() }).ToImmutableArray() });
        foreach (var machine in new[] { new PewterAdditionalMachine(catalog.Tm39, catalog.Move, catalog.SourceMoveId, catalog.MovePp, catalog.Compatibility) }.Concat(extra))
        {
            for (int index = 0; index < Species.Length; index++)
            {
                var expected = Species[index];
                var row = machine.Compatibility[103 + index];
                var source = new OpeningItemSourceRef(SourcePath, "SPECIES_" + expected.Symbol, expected.Start, expected.End);
                bool canLearn = expected.Machines.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(item => machine.Machine.Item.Id == new ItemId("pret:ITEM_" + item));
                if (row is null || row.Species != new SpeciesId("pret:SPECIES_" + expected.Symbol) ||
                    row.SourceSpeciesId != expected.Id || row.CanLearn != canLearn || row.Source != source)
                    throw new NotSupportedException("fuchsia_machine_compatibility_source");
            }
        }
    }
}
