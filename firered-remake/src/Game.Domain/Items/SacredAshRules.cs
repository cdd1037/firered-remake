using System.Collections.Immutable;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;

public sealed record SacredAshResult(InventoryState Inventory,ImmutableArray<PartyPokemon> Party,int Revived,string? Error);
/// <summary>Source party-menu Sacred Ash loop: revive each fainted member fully, then consume one iff it had effect.</summary>
public static class SacredAshRules
{
    public static SacredAshResult Use(EarlyShopCatalog catalog,CampaignPokemonContent pokemonContent,InventoryState inventory,ImmutableArray<PartyPokemon> party)
    {
        SacredAshResult Fail(string error)=>new(inventory,party,0,error);
        if(EventIslandShopCatalog.For(catalog) is null && PowerPlantShopCatalog.For(catalog) is null && TrainerTowerShopCatalog.For(catalog) is null)return Fail("sacred_ash_catalog_missing");
        if(EarlyShopInventoryRules.Validate(catalog,inventory) is {} error)return Fail(error);
        var item=EarlyShopInventoryRules.Find(catalog,EventIslandShopCatalog.SacredAshId)!;
        if(item.SourceItemId!=45||item.FieldUseFunction!="FieldUseFunc_SacredAsh"||item.BattleUsage!=0)return Fail("sacred_ash_source_mismatch");
        if(OpeningInventoryRules.Quantity(inventory.BagItems,item.Id)<1)return Fail("medicine_not_in_bag");
        if(party.IsDefault||party.Length>6)return Fail("party_invalid");
        var next=party;int revived=0;
        for(int i=0;i<party.Length;i++)
        {
            if(PokemonStateValidator.ValidateParty(pokemonContent,party[i]) is {} invalid)return Fail(invalid);
            var mon=party[i].Pokemon;
            if(mon.CurrentHp!=0)continue;
            next=next.SetItem(i,party[i] with{Pokemon=mon with{CurrentHp=mon.Stats.MaxHp}});revived++;
        }
        if(revived==0)return Fail("medicine_no_effect");
        var removed=EarlyInventoryAddRules.Remove(catalog,inventory,item.Id,1);
        return removed.Succeeded?new(removed.State,next,revived,null):Fail(removed.Error!);
    }
}
