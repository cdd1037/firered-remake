using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Exact original 15-machine, 98-species profile. Older matrices remain unchanged.</summary>
public static class CeladonGymMachineRules
{
    public const string Profile = "firered-celadon-gym-machines-v1";
    public static ItemId Tm19Id => CeladonGymItemRules.Tm19Id;
    public static MoveId GigaDrainId { get; } = new("pret:MOVE_GIGA_DRAIN");
    private static readonly (string Symbol, int Id, string Bits, OpeningItemSourceRef Source)[] Added = [
        ("VILEPLUME",45,"001001100110101",new("src/data/pokemon/tmhm_learnsets.h","SPECIES_VILEPLUME",1006,1023)),
        ("VICTREEBEL",71,"001101100110101",new("src/data/pokemon/tmhm_learnsets.h","SPECIES_VICTREEBEL",1641,1659)),
        ("EXEGGCUTE",102,"001101100100101",new("src/data/pokemon/tmhm_learnsets.h","SPECIES_EXEGGCUTE",2343,2364)),
        ("TANGELA",114,"001101100110101",new("src/data/pokemon/tmhm_learnsets.h","SPECIES_TANGELA",2675,2694)),
    ];
    private static readonly HashSet<SpeciesId> GigaDrainSpecies = "BULBASAUR IVYSAUR VENUSAUR BUTTERFREE BEEDRILL EKANS ARBOK ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT GRIMER GLOOM WEEPINBELL TENTACOOL TENTACRUEL VENOMOTH VENONAT MUK VILEPLUME VICTREEBEL EXEGGCUTE TANGELA".Split(' ').Select(s => new SpeciesId("pret:SPECIES_" + s)).ToHashSet();
    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Compatibility.IsDefault || catalog.Compatibility.Length != 98 ||
            catalog.AdditionalMachines is not { IsDefault: false, Length: 14 } extra || extra.Any(m => m is null))
            throw new NotSupportedException("Exact Celadon Gym machine scope required.");
        CeladonApproachMachineRules.ValidateCatalog(catalog with {
            Profile = CeladonApproachMachineRules.Profile,
            Compatibility = catalog.Compatibility.Take(94).ToImmutableArray(),
            AdditionalMachines = extra.Take(13).Select(m => m with { Compatibility = m.Compatibility.Take(94).ToImmutableArray() }).ToImmutableArray()
        });
        var values = new[] { new PewterAdditionalMachine(catalog.Tm39, catalog.Move, catalog.SourceMoveId, catalog.MovePp, catalog.Compatibility) }.Concat(extra).ToArray();
        for (int row = 0; row < 15; row++)
        {
            var cells = values[row].Compatibility;
            if (cells.IsDefault || cells.Length != 98) throw new NotSupportedException("Celadon Gym compatibility shape differs.");
            for (int index = 0; index < 4; index++)
            {
                var e = Added[index]; var cell = cells[94 + index];
                if (cell is null || cell.Species != new SpeciesId("pret:SPECIES_" + e.Symbol) || cell.SourceSpeciesId != e.Id ||
                    cell.CanLearn != (e.Bits[row] == '1') || cell.Source != e.Source)
                    throw new NotSupportedException("Celadon Gym source compatibility differs.");
            }
        }
        var tm = values[14]; CeladonGymItemRules.ValidateMachine(tm.Machine);
        if (tm.Move != GigaDrainId || tm.SourceMoveId != 202 || tm.MovePp != 5)
            throw new NotSupportedException("Original TM19 move identity differs.");
        for (int index = 0; index < 98; index++)
        {
            var expected = catalog.Compatibility[index]; var cell = tm.Compatibility[index];
            if (cell is null || cell.Species != expected.Species || cell.SourceSpeciesId != expected.SourceSpeciesId ||
                cell.Source != expected.Source || cell.CanLearn != GigaDrainSpecies.Contains(cell.Species))
                throw new NotSupportedException("Original TM19 species compatibility differs.");
        }
    }
}
