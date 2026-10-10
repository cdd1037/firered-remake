using System.Collections.Immutable;
using System.Globalization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Application;

public sealed record CampaignItemMenu(string Origin,string View,string? MessageSymbol=null,string ReturnView="top",InventoryState? DisplayInventory=null);
public sealed partial class CampaignSession
{
    private const string BedroomPcScript="PalletTown_PlayersHouse_2F_EventScript_PC";
    private const string BedroomPcShutdown="EventScript_PalletTown_PlayersHouse_2F_ShutDownPC";
    private const string BedroomPcSource="data/maps/PalletTown_PlayersHouse_2F/scripts.inc";
    private OpeningItemsCatalog Items=>_content.OpeningItems??throw new InvalidDataException("opening_items_not_enabled");
    private CampaignState ApplyItemSpecial(CampaignState state,StoryEffect effect)
    {
        _=Items;
        if(effect.Source.Path!=BedroomPcSource||effect.Source.Symbol is not (BedroomPcScript or BedroomPcShutdown))throw new InvalidDataException("item_native_source_not_supported");
        var effectName=effect.Args[0];
        return effectName switch
        {
            "AnimatePcTurnOn"=>state with{PcPoweredOn=true},
            "AnimatePcTurnOff"=>state with{PcPoweredOn=false},
            "Script_SetHelpContext"=>state with{HelpContext="HELPCONTEXT_BEDROOM_PC"},
            "SetHelpContextForMap"=>state with{HelpContext="map"},
            "BedroomPC"=>state with{ItemMenu=new("pc","top"),Dialogue=null},
            _=>throw new InvalidDataException("item_native_special_unsupported: "+effectName)
        };
    }
    private Decision<CampaignState> DecideItemMenu(CampaignState state,CampaignCommand command)
    {
        var menu=state.ItemMenu!;
        Decision<CampaignState> No(string error)=>new(CommandStatus.Rejected,state,error);
        Decision<CampaignState> Menu(string view)=>new(CommandStatus.Accepted,state with{ItemMenu=menu with{View=view,MessageSymbol=null,DisplayInventory=null}});
        Decision<CampaignState> Message(string symbol,string returnView)=>new(CommandStatus.Accepted,state with{ItemMenu=menu with{View="message",MessageSymbol=symbol,ReturnView=returnView}});
        if(command.Kind=="berry_open")
        {
            if(!_content.SsAnneEnabled||menu.Origin!="bag"||menu.View!="bag"||OpeningInventoryRules.Quantity(state.Inventory!.KeyItems??[],SsAnneItemRules.BerryPouchId)!=1)return No("berry_pouch_context_invalid");
            return Menu("berries");
        }
        if(command.Kind=="field_poke_flute")return UseFieldPokeFlute(state,command);
        if(command.Kind=="field_bicycle")return UseFieldBicycle(state,command);
        if(command.Kind=="field_fish")return StartFieldFishing(state,command);
        if(command.Kind=="town_map_open")return OpenTownMap(state,command);
        if(command.Kind is "machine_open" or "machine_teach")return DecideMachine(state,command);
        if(command.Kind=="menu_ack")return menu.View=="message"?Menu(menu.ReturnView):No("item_message_not_pending");
        if(command.Kind=="menu_back")
        {
            if(menu.View=="message")return Menu(menu.ReturnView);
            if(menu.Origin=="bag"&&menu.View is "machines" or "berries")return Menu("bag");
            if(menu.Origin=="bag")return new(CommandStatus.Accepted,state with{ItemMenu=null});
            if(menu.View=="top")return CloseBedroomPc(state);
            return Menu(menu.View is "withdraw" or "deposit"?"storage":"top");
        }
        if(command.Kind=="menu_select")
        {
            if(menu.Origin!="pc")return No("item_menu_selection_unsupported");
            if(menu.View=="top")return command.Text switch
            {"storage"=>Menu("storage"),"mailbox"=>Message("gText_TheresNoMailHere","top"),"off"=>CloseBedroomPc(state),_=>No("pc_top_selection_invalid")};
            if(menu.View=="storage")return command.Text switch
            {
                "withdraw"=>state.Inventory!.PcItems.IsEmpty?Message("gText_ThereAreNoItems","storage"):Menu("withdraw"),
                "deposit"=>state.Inventory!.BagItems.IsEmpty?Message("gText_ThereAreNoItems","storage"):Menu("deposit"),
                "cancel"=>Menu("top"),_=>No("pc_storage_selection_invalid")
            };
            return No("item_menu_selection_unsupported");
        }
        if(command.Kind is "pc_withdraw" or "pc_deposit")
        {
            bool withdraw=command.Kind=="pc_withdraw";
            if(menu.Origin!="pc"||menu.View!=(withdraw?"withdraw":"deposit"))return No("pc_transfer_context_invalid");
            if(command.Text!=OpeningInventoryRules.PotionId.Value)return No("inventory_item_unsupported");
            var transfer=TransferCampaignPotion(state.Inventory!,command.Quantity,withdraw);
            if(!transfer.Succeeded)return No(transfer.Error!);
            return new(CommandStatus.Accepted,state with{Inventory=transfer.State,TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",Items.Potion.Name).SetItem("STR_VAR_2",command.Quantity.ToString(CultureInfo.InvariantCulture)),ItemMenu=menu with{View="message",MessageSymbol=withdraw?"gText_WithdrewQuantItem":"gText_DepositedStrVar2StrVar1s",ReturnView=menu.View,DisplayInventory=state.Inventory}});
        }
        if(command.Kind=="pc_give")
        {
            if(menu.Origin!="pc"||menu.View!="withdraw"||OpeningInventoryRules.Quantity(state.Inventory!.PcItems,OpeningInventoryRules.PotionId)==0)return No("pc_give_context_invalid");
            return state.Party.IsEmpty?Message("gText_ThereIsNoPokemon","withdraw"):No("held_item_transfer_not_implemented");
        }
        if(command.Kind=="field_rare_candy")return UseFieldRareCandy(state,command);
        if(command.Kind=="field_escape_rope")return UseFieldEscapeRope(state,command);
        if(command.Kind=="field_repel")return UseFieldRepel(state,command);
        if(command.Kind=="field_evolution")return UseFieldEvolution(state,command);
        if(command.Kind=="field_medicine")return command.Text is "pret:ITEM_REVIVE" or "pret:ITEM_ETHER"?UseMtMoonFieldMedicine(state,command):UseFieldMedicine(state,command);
        if(command.Kind=="field_potion")
        {
            if(menu.Origin!="bag"||menu.View!="bag")return No("field_potion_context_invalid");
            if(state.Party.IsEmpty)return Message("gText_ThereIsNoPokemon","bag");
            if(command.PartyIndex<0||command.PartyIndex>=state.Party.Length)return No("item_party_index_invalid");
            var result=UseCampaignPartyPotion(state.Inventory!,state.Party[command.PartyIndex],PotionUseContext.Field);
            if(!result.Succeeded)return result.Error=="potion_no_effect"?Message("gText_WontHaveEffect","bag"):No(result.Error!);
            return new(CommandStatus.Accepted,state with{Inventory=result.Inventory,Party=state.Party.SetItem(command.PartyIndex,result.Pokemon),TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",result.Pokemon.Nickname).SetItem("STR_VAR_2",result.HpRestored.ToString(CultureInfo.InvariantCulture)),ItemMenu=menu with{View="message",MessageSymbol="gText_PkmnHPRestoredByVar2",ReturnView="bag"}});
        }
        return No("item_menu_active");
    }
    private Decision<CampaignState> CloseBedroomPc(CampaignState state)
    {
        if(state.ItemMenu?.Origin!="pc"||state.Script.Wait?.Kind!=StoryWaitKind.Inventory||!state.PcPoweredOn)return new(CommandStatus.Fault,state,"pc_callback_context_invalid");
        if(_content.PokemonStorageEnabled&&state.Pc?.View==CampaignPcView.PlayerItems)return ResumePcScript(state);
        // Original Task_PlayerPcTurnOff replaces ScriptContext with the room-specific shutdown
        // script; it does not resume the old waitstate. Retain variables/flags and the lock.
        var next=state with{ItemMenu=null,Dialogue=null,Script=state.Script with{Stack=[],Wait=null,Completed=true}};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Start(next.Script,BedroomPcShutdown,_content))));
    }
    public string? VisibleItemMessage
    {
        get
        {
            if(State.ItemMenu?.MessageSymbol is not {} symbol)return null;
            var text=string.Join("\n",_content.TextPages(symbol));
            foreach(var buffer in State.TextBuffers)text=text.Replace("{"+buffer.Key+"}",buffer.Value,StringComparison.Ordinal);
            return text.Replace("{PLAYER}",State.PlayerName,StringComparison.Ordinal);
        }
    }
}
