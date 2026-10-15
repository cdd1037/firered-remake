using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Original18 machines x99 species. Three new machine effects remain unavailable for teaching.</summary>
public static class RocketHideoutMachineRules
{
    public const string Profile = "firered-rocket-hideout-machines-v1";
    private const string KangaskhanBits = "110111111111010010";
    private static readonly (ItemId Item, MoveId Move, int MoveId, int Pp, HashSet<SpeciesId> Compatible)[] Added = [
        (new("pret:ITEM_TM12"),new("pret:MOVE_TAUNT"),269,20,"RATTATA RATICATE ONIX STEELIX MANKEY GYARADOS ZUBAT GOLBAT GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING ABRA KADABRA DROWZEE MEOWTH PERSIAN ELECTRODE HYPNO PRIMEAPE WEEZING MUK".Split(' ').Select(s => new SpeciesId("pret:SPECIES_" + s)).ToHashSet()),
        (new("pret:ITEM_TM21"),new("pret:MOVE_FRUSTRATION"),218,20,"BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE BUTTERFREE BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX SPEAROW FEAROW MANKEY EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF GYARADOS ARBOK CLEFAIRY ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT MAGNEMITE GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF ABRA KADABRA MACHOP DROWZEE SHELLDER HORSEA GOLDEEN STARYU STARMIE SLOWPOKE GLOOM WEEPINBELL MEOWTH PERSIAN GROWLITHE PONYTA TENTACOOL DIGLETT DUGTRIO MAGNETON POLIWAG POLIWHIRL VULPIX CUBONE ELECTRODE HYPNO LICKITUNG MACHOKE MAROWAK PRIMEAPE RHYHORN SEADRA SEAKING TENTACRUEL VENOMOTH VENONAT WEEZING MUK VILEPLUME VICTREEBEL EXEGGCUTE TANGELA KANGASKHAN".Split(' ').Select(s => new SpeciesId("pret:SPECIES_" + s)).ToHashSet()),
        (new("pret:ITEM_TM49"),new("pret:MOVE_SNATCH"),289,10,"EKANS JIGGLYPUFF ARBOK CLEFAIRY ZUBAT GOLBAT CLEFABLE WIGGLYTUFF ABRA KADABRA DROWZEE MEOWTH PERSIAN HYPNO".Split(' ').Select(s => new SpeciesId("pret:SPECIES_" + s)).ToHashSet()),
    ];
    public static bool IsTeachingSupported(ItemId item) => !Added.Any(m => m.Item == item);
    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Profile != Profile || catalog.Compatibility.IsDefault || catalog.Compatibility.Length != 99 ||
            catalog.AdditionalMachines is not { IsDefault: false, Length: 17 } extra || extra.Any(m => m is null))
            throw new NotSupportedException("Exact Rocket Hideout machine scope required.");
        CeladonGymMachineRules.ValidateCatalog(catalog with { Profile = CeladonGymMachineRules.Profile,
            Compatibility = catalog.Compatibility.Take(98).ToImmutableArray(),
            AdditionalMachines = extra.Take(14).Select(m => m with { Compatibility = m.Compatibility.Take(98).ToImmutableArray() }).ToImmutableArray() });
        var rows = new[] { new PewterAdditionalMachine(catalog.Tm39,catalog.Move,catalog.SourceMoveId,catalog.MovePp,catalog.Compatibility) }.Concat(extra).ToArray();
        for (int row = 0; row < rows.Length; row++)
        {
            var cells = rows[row].Compatibility;
            if (cells.IsDefault || cells.Length != 99 || cells.Any(c => c is null))
                throw new NotSupportedException("Rocket Hideout machine compatibility shape differs.");
            var cell = cells[98];
            if (cell.Species != new SpeciesId("pret:SPECIES_KANGASKHAN") || cell.SourceSpeciesId != 115 ||
                cell.CanLearn != (KangaskhanBits[row] == '1'))
                throw new NotSupportedException("Kangaskhan machine compatibility differs.");
            if (row < 15) continue;
            var expected = Added[row - 15]; var value = rows[row]; RocketHideoutItemRules.ValidateMachine(value.Machine);
            if (value.Machine.Item.Id != expected.Item || value.Move != expected.Move || value.SourceMoveId != expected.MoveId || value.MovePp != expected.Pp)
                throw new NotSupportedException("Rocket Hideout machine move identity differs.");
            for (int index = 0; index < cells.Length; index++)
            {
                var basis = catalog.Compatibility[index]; cell = cells[index];
                if (cell.Species != basis.Species || cell.SourceSpeciesId != basis.SourceSpeciesId ||
                    cell.CanLearn != expected.Compatible.Contains(cell.Species))
                    throw new NotSupportedException("Rocket Hideout machine source compatibility differs.");
            }
        }
    }
}
