using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Source143 compatibility for the same eighteen machines; no new TM or HM is granted.</summary>
public static class SnorlaxMachineRules
{
    public const string Profile = "firered-snorlax-machines-v1";
    public const string SourcePath = "src/data/pokemon/tmhm_learnsets.h";
    public static bool CanLearn(ItemId item) => item.Value is "pret:ITEM_TM01" or "pret:ITEM_TM03" or "pret:ITEM_TM06" or
        "pret:ITEM_TM10" or "pret:ITEM_TM11" or "pret:ITEM_TM13" or "pret:ITEM_TM14" or "pret:ITEM_TM15" or "pret:ITEM_TM17" or
        "pret:ITEM_TM18" or "pret:ITEM_TM21" or "pret:ITEM_TM22" or "pret:ITEM_TM24" or "pret:ITEM_TM25" or "pret:ITEM_TM26" or
        "pret:ITEM_TM27" or "pret:ITEM_TM29" or "pret:ITEM_TM30" or "pret:ITEM_TM31" or "pret:ITEM_TM32" or "pret:ITEM_TM34" or
        "pret:ITEM_TM35" or "pret:ITEM_TM37" or "pret:ITEM_TM38" or "pret:ITEM_TM39" or "pret:ITEM_TM42" or "pret:ITEM_TM43" or
        "pret:ITEM_TM44" or "pret:ITEM_TM45" or "pret:ITEM_HM03" or "pret:ITEM_HM04";
    public static void ValidateCatalog(PewterMachineCatalog c)
    {
        if (c.Profile != Profile || c.Compatibility.IsDefault || c.Compatibility.Length != 102 ||
            c.AdditionalMachines is not {IsDefault:false,Length:17} extra) throw new NotSupportedException("snorlax_machine_catalog_scope");
        PokemonTowerMachineRules.ValidateCatalog(c with { Profile=PokemonTowerMachineRules.Profile,
            Compatibility=c.Compatibility.Take(101).ToImmutableArray(),
            AdditionalMachines=extra.Select(m => m with {Compatibility=m.Compatibility.Take(101).ToImmutableArray()}).ToImmutableArray() });
        var all=new[]{new PewterAdditionalMachine(c.Tm39,c.Move,c.SourceMoveId,c.MovePp,c.Compatibility)}.Concat(extra);
        foreach (var machine in all)
        {
            if (machine.Compatibility.Length != 102) throw new NotSupportedException("snorlax_machine_compatibility_count");
            var row=machine.Compatibility[101];
            var source=new OpeningItemSourceRef(SourcePath,"SPECIES_SNORLAX",3348,3378);
            if (row.Species!=new SpeciesId("pret:SPECIES_SNORLAX") || row.SourceSpeciesId!=143 ||
                row.CanLearn!=CanLearn(machine.Machine.Item.Id) || row.Source!=source)
                throw new NotSupportedException("snorlax_machine_compatibility_source");
        }
    }
}
