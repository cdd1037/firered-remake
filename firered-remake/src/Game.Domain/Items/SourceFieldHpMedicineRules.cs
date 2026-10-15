using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Display capability only. Executing a use resolves the authenticated source again.</summary>
public sealed record SourceFieldHpMedicine(ItemId Item,int HealParameter,uint PermanentStatusMask);

/// <summary>Pure HP and HP-plus-permanent-status field effects from the existing source item table.
/// Preserves historical catalog definitions; no revive, PP, EV, friendship or battle effects.</summary>
public static class SourceFieldHpMedicineRules
{
    public static bool Supports(EarlyShopCatalog catalog,SourceBattleItemCatalog sourceItems,ItemId item)=>
        Resolve(catalog,sourceItems,item) is not null;

    public static SourceFieldHpMedicine? Resolve(EarlyShopCatalog catalog,SourceBattleItemCatalog sourceItems,ItemId item)
    {
        if(catalog is null||sourceItems is null||catalog.Items.IsDefaultOrEmpty||catalog.Shops.IsDefault||
            catalog.Items.Any(i=>i is null||i.EffectBytes.IsDefault))return null;
        try{EarlyShopInventoryRules.ValidateCatalog(catalog);}
        catch(Exception error) when(error is ArgumentException or NotSupportedException){return null;}
        var admitted=EarlyShopInventoryRules.Find(catalog,item);var source=sourceItems.Find(item);
        if(admitted is null||source is null||!SourceBattleItemCatalog.SupportsFixedEffect(source)||source.Reusable||
            admitted.SourceItemSymbol!=source.Symbol||admitted.SourceItemId!=source.SourceItemId||admitted.Name!=source.Name||
            admitted.Pocket!=source.Pocket||admitted.Pocket is not("POCKET_ITEMS" or "POCKET_BERRY_POUCH")||
            admitted.Type!="ITEM_TYPE_PARTY_MENU"||admitted.FieldUseFunction!="FieldUseFunc_Medicine"||
            admitted.BattleUsage!=source.BattleUsage||admitted.BattleUseFunction!="BattleUseFunc_Medicine"||
            admitted.BattleUseFunction!=source.BattleUseFunction||admitted.HoldEffectParam!=source.HoldEffectParam)return null;
        var effect=source.EffectBytes;
        // Byte3 may cure permanent status/confusion; confusion has no field representation.
        // Byte4 must be HP only. Six effect bytes + one HP argument excludes all other effects.
        // 253 is level-up HP and requires state outside this bounded field service.
        if(effect.Length!=7||effect[0]!=0||effect[1]!=0||effect[2]!=0||(effect[3]&~0x3f)!=0||effect[4]!=4||effect[5]!=0||effect[6] is 0 or 253)return null;
        if(!admitted.EffectBytes.IsEmpty&&!admitted.EffectBytes.SequenceEqual(effect.Select(b=>(int)b)))return null;
        return new(item,effect[6],SourceItemStatusCure.Mask(effect[3]));
    }

    public static EarlyMedicineUseResult Use(EarlyShopCatalog catalog,SourceBattleItemCatalog sourceItems,
        CampaignPokemonContent pokemonContent,InventoryState inventory,PartyPokemon pokemon,ItemId item)
    {
        ArgumentNullException.ThrowIfNull(inventory);ArgumentNullException.ThrowIfNull(pokemon);
        EarlyMedicineUseResult Fail(string error)=>new(inventory,pokemon,false,0,0,0,false,error);
        var capability=Resolve(catalog,sourceItems,item);
        if(capability is null)return Fail("medicine_effect_unsupported");
        if(EarlyShopInventoryRules.Validate(catalog,inventory) is {} inventoryError)return Fail(inventoryError);
        if(SourceBattleInventoryRules.Validate(sourceItems,inventory) is {} sourceInventoryError)return Fail(sourceInventoryError);
        if(PokemonStateValidator.ValidateParty(pokemonContent,pokemon) is {} pokemonError)return Fail(pokemonError);
        if(pokemon.IsEgg)return Fail("medicine_egg_unsupported");
        var definition=EarlyShopInventoryRules.Find(catalog,item)!;
        int count=OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory,definition),item);
        if(count==0)return Fail("medicine_not_in_bag");
        // ExecuteTableBasedItemEffect cures byte3 before its HP0/non-revive gate in byte4.
        // A fainted status-bearing target can be cured, but its HP remains zero.
        uint cured=pokemon.Status&capability.PermanentStatusMask;
        var target=pokemon.Pokemon;int restored=0;
        if(target.CurrentHp>0&&target.CurrentHp<target.Stats.MaxHp)
        {
            int amount=capability.HealParameter switch{255=>target.Stats.MaxHp-target.CurrentHp,254=>Math.Max(1,target.Stats.MaxHp/2),_=>capability.HealParameter};
            restored=Math.Min(amount,target.Stats.MaxHp-target.CurrentHp);
        }
        if(restored==0&&cured==0)return Fail("medicine_no_effect");
        var changed=pokemon with{Pokemon=restored==0?target:target with{CurrentHp=target.CurrentHp+restored},Status=pokemon.Status&~capability.PermanentStatusMask};
        return new(EarlyShopInventoryRules.SetQuantity(inventory,definition,count-1),changed,true,restored,cured,1,false,null);
    }
}
