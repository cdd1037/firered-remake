using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Eevee source compatibility appended to the existing eighteen machines.</summary>
public static class EeveeMachineRules
{
    public const string Profile = "firered-eevee-machines-v1";
    public const string SourcePath = "src/data/pokemon/tmhm_learnsets.h";
    public static bool CanLearn(ItemId item) => item.Value is "pret:ITEM_TM06" or "pret:ITEM_TM10" or "pret:ITEM_TM11" or "pret:ITEM_TM17" or "pret:ITEM_TM18" or "pret:ITEM_TM21" or "pret:ITEM_TM23" or "pret:ITEM_TM27" or "pret:ITEM_TM28" or "pret:ITEM_TM30" or "pret:ITEM_TM32" or "pret:ITEM_TM42" or "pret:ITEM_TM43" or "pret:ITEM_TM44" or "pret:ITEM_TM45";
    public static void ValidateCatalog(PewterMachineCatalog c)
    {
        if (c.Profile != Profile || c.Compatibility.IsDefault || c.Compatibility.Length != 103 ||
            c.AdditionalMachines is not { IsDefault:false, Length:17 } extra) throw new NotSupportedException("eevee_machine_catalog_scope");
        SnorlaxMachineRules.ValidateCatalog(c with { Profile=SnorlaxMachineRules.Profile,
            Compatibility=c.Compatibility.Take(102).ToImmutableArray(),
            AdditionalMachines=extra.Select(m => m with { Compatibility=m.Compatibility.Take(102).ToImmutableArray() }).ToImmutableArray() });
        foreach (var machine in new[]{new PewterAdditionalMachine(c.Tm39,c.Move,c.SourceMoveId,c.MovePp,c.Compatibility)}.Concat(extra))
        {
            if (machine.Compatibility.Length != 103) throw new NotSupportedException("eevee_machine_compatibility_count");
            var row=machine.Compatibility[102];
            var source=new OpeningItemSourceRef(SourcePath,"SPECIES_EEVEE",3108,3122);
            if (row.Species != new SpeciesId("pret:SPECIES_EEVEE") || row.SourceSpeciesId != 133 || row.CanLearn != CanLearn(machine.Machine.Item.Id) || row.Source != source)
                throw new NotSupportedException("eevee_machine_compatibility_source");
        }
    }
}
