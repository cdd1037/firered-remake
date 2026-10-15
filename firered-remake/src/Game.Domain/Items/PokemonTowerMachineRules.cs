using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;
/// <summary>The same eighteen source machines; Gastly and Haunter extend compatibility only.</summary>
public static class PokemonTowerMachineRules
{
    public const string Profile="firered-pokemon-tower-machines-v1";
    public static bool CanLearn(ItemId item)=>item.Value is "pret:ITEM_TM06" or "pret:ITEM_TM10" or "pret:ITEM_TM11" or "pret:ITEM_TM12" or "pret:ITEM_TM17" or "pret:ITEM_TM18" or "pret:ITEM_TM19" or "pret:ITEM_TM21" or "pret:ITEM_TM24" or "pret:ITEM_TM27" or "pret:ITEM_TM29" or "pret:ITEM_TM30" or "pret:ITEM_TM32" or "pret:ITEM_TM36" or "pret:ITEM_TM41" or "pret:ITEM_TM42" or "pret:ITEM_TM43" or "pret:ITEM_TM44" or "pret:ITEM_TM45" or "pret:ITEM_TM46" or "pret:ITEM_TM48" or "pret:ITEM_TM49";
    public static void ValidateCatalog(PewterMachineCatalog c)
    {
        if(c.Profile!=Profile||c.Compatibility.IsDefault||c.Compatibility.Length!=101||c.AdditionalMachines is not {IsDefault:false,Length:17} extra)throw new NotSupportedException("tower_machine_catalog_scope");
        RocketHideoutMachineRules.ValidateCatalog(c with{Profile=RocketHideoutMachineRules.Profile,Compatibility=c.Compatibility.Take(99).ToImmutableArray(),AdditionalMachines=extra.Select(m=>m with{Compatibility=m.Compatibility.Take(99).ToImmutableArray()}).ToImmutableArray()});
        var all=new[]{new PewterAdditionalMachine(c.Tm39,c.Move,c.SourceMoveId,c.MovePp,c.Compatibility)}.Concat(extra);
        foreach(var m in all)
        {
            if(m.Compatibility.Length!=101)throw new NotSupportedException("tower_machine_compatibility_count");
            for(int i=99;i<101;i++)
            {
                var row=m.Compatibility[i];string symbol=i==99?"SPECIES_GASTLY":"SPECIES_HAUNTER";

                if(row.Species!=new SpeciesId("pret:"+symbol)||row.SourceSpeciesId!=i-7||row.CanLearn!=CanLearn(m.Machine.Item.Id))throw new NotSupportedException("tower_machine_compatibility_source");
            }
        }
    }
}
