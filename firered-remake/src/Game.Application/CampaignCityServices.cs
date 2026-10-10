using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static readonly ItemId BikeVoucherId=new("pret:ITEM_BIKE_VOUCHER");
    private static readonly ItemId BicycleId=new("pret:ITEM_BICYCLE");
    private static readonly ItemId OldRodId=new("pret:ITEM_OLD_ROD");
    private const string BikeShopSource="data/maps/CeruleanCity_BikeShop/scripts.inc";
    private static bool IsCityServiceItem(ItemId id)=>id==BikeVoucherId||id==BicycleId||id==OldRodId;
    private InventoryState WithoutCityServiceItems(InventoryState inventory)
    {
        inventory=WithoutRegionalTea(inventory);
        return !_content.CityServicesEnabled?inventory:inventory with{KeyItems=inventory.KeyItems is {} keys?keys.Where(s=>!IsCityServiceItem(s.Item)).ToImmutableArray():null};
    }

    private string? ValidateCityServiceInventory(InventoryState inventory)
    {
        foreach(var stack in inventory.KeyItems??[])
            if(stack is not null&&IsCityServiceItem(stack.Item)&&(!_content.CityServicesEnabled||stack.Quantity!=1))
                return "inventory_city_service_item_invalid";
        return null;
    }
    private string? ValidateCityServicesProgress(CampaignState state)
    {
        bool voucher=state.Script.Flags.GetValueOrDefault("FLAG_GOT_BIKE_VOUCHER");
        bool bike=state.Script.Flags.GetValueOrDefault("FLAG_GOT_BICYCLE");
        bool rod=state.Script.Flags.GetValueOrDefault("FLAG_GOT_OLD_ROD");
        int Count(ItemId id)=>OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],id);
        if(!_content.CityServicesEnabled)return voucher||bike||rod||Count(BikeVoucherId)!=0||Count(BicycleId)!=0||Count(OldRodId)!=0?"save_city_services_profile_missing":null;
        return bike&&!voucher||Count(BikeVoucherId)!=(voucher&&!bike?1:0)||Count(BicycleId)!=(bike?1:0)||Count(OldRodId)!=(rod?1:0)
            ?"save_city_services_reward_mismatch":null;
    }
    private bool TryApplyCityServicesEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.CityServicesEnabled)return false;
        var a=effect.Args;
        int Value(string token)=>int.TryParse(token,out int n)?n:state.Script.Variables.TryGetValue(token,out int v)?v:_content.TryConstant(token,out int c)?c:throw new InvalidDataException("city_service_operand_unknown: "+token);
        CampaignState Result(int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",value)}};
        if(effect.Source.Path==BikeShopSource)
        {
            switch(effect.Opcode)
            {
                case "showmoneybox":
                    if(!a.SequenceEqual(new[]{"0","0"}))throw new InvalidDataException("bike_shop_moneybox_position");
                    next=state with{MoneyBox=new(state.Money,0,0)};return true;
                case "hidemoneybox":next=state with{MoneyBox=null};return true;
                case "multichoice":
                    if(!a.SequenceEqual(new[]{"11","0","MULTICHOICE_BIKE_SHOP","FALSE"}))throw new InvalidDataException("bike_shop_menu_profile_invalid");
                    return true;
            }
        }
        if(effect.Opcode is not ("checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        int id=Value(a[buffer?1:0]);
        var item=_content.CityServiceItems.FirstOrDefault(x=>x.Item.SourceItemId==id)?.Item;
        if(item is null)return false;
        if(buffer)
        {
            if(a[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3"))throw new InvalidDataException("city_service_buffer_invalid");
            next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],item.Name)};return true;
        }
        if(effect.Opcode=="checkitemtype"){next=Result(Value(item.Pocket));return true;}
        int quantity=a.Length>1?Value(a[1]):1;
        if(quantity!=1)throw new InvalidDataException("city_service_quantity_invalid");
        var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
        var error=ValidateCampaignInventory(inventory);if(error is not null)throw new InvalidDataException(error);
        var keys=inventory.KeyItems??[];int index=-1;for(int n=0;n<keys.Length;n++)if(keys[n].Item==item.Id){index=n;break;}
        bool adding=effect.Opcode!="removeitem";
        bool success=adding?index<0&&keys.Length<30:index>=0;
        if(effect.Opcode=="checkitemspace"){next=Result(success?1:0);return true;}
        if(!success){next=Result(0);return true;}
        keys=adding?keys.Add(new(item.Id,1)):keys.RemoveAt(index);
        next=Result(1) with{Inventory=inventory with{KeyItems=keys}};return true;
    }
}
