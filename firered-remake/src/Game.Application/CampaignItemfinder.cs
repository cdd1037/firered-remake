using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;
namespace Game.Application;

public sealed record CampaignItemfinder(ItemfinderResponse Response,ImmutableArray<string> Pages,int Page,bool DigPending);
public sealed partial class CampaignSession
{
    private const string ItemfinderFlag="FLAG_GOT_ITEMFINDER";
    private const string ItemfinderAidePath="data/maps/Route11_EastEntrance_2F/scripts.inc";
    private const string ItemfinderAide="Route11_EastEntrance_2F_EventScript_Aide";
    private ItemfinderSourceContract? ItemfinderSource=>(_content as IItemfinderSourceCatalog)?.ItemfinderSource;
    public bool ItemfinderEnabled=>ItemfinderSource is not null&&(FossilLabShopCatalog.For(_content.EarlyShop)?.Predecessor.Predecessor??TrainerTowerShopCatalog.For(_content.EarlyShop)?.Predecessor??PowerPlantShopCatalog.For(_content.EarlyShop)) is not null;
    public string? VisibleItemfinderMessage=>State.Itemfinder is {} itemfinder?itemfinder.Pages[itemfinder.Page]:null;
    public bool SacredAshEnabled=>(FossilLabShopCatalog.For(_content.EarlyShop)?.Predecessor.Predecessor??TrainerTowerShopCatalog.For(_content.EarlyShop)?.Predecessor??PowerPlantShopCatalog.For(_content.EarlyShop)) is not null;
    private string? ValidateItemfinderProgress(CampaignState state)
    {
        bool got=state.Script.Flags.GetValueOrDefault(ItemfinderFlag);
        int count=OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],ItemfinderRules.Item);
        if(!ItemfinderEnabled)return got||count!=0||state.RegisteredItem is not null||state.Itemfinder is not null?"save_itemfinder_profile_missing":null;
        if(state.Itemfinder is not null)return "save_itemfinder_active";
        if(count!=(got?1:0))return "save_itemfinder_reward_mismatch";
        if(got&&_content.EvaluateOakRating(state.SeenSpecies,state.CaughtSpecies).Caught<30)return "save_itemfinder_caught_requirement";
        if(state.RegisteredItem is {} registered&&(registered!=ItemfinderRules.Item.Value||!got))return "save_registered_item_invalid";
        if(ItemfinderSource!.Underfoot.Any(i=>state.Script.Flags.GetValueOrDefault(i.Flag))&&!got)return "save_itemfinder_pickup_without_itemfinder";
        return null;
    }
    private Decision<CampaignState> RegisterItemfinder(CampaignState state,CampaignCommand command)
    {
        if(!ItemfinderEnabled||state.ItemMenu is not{Origin:"bag",View:"bag",TargetInstanceId:null}||command.Text!=ItemfinderRules.Item.Value||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],ItemfinderRules.Item)!=1)
            return new(CommandStatus.Rejected,state,"itemfinder_registration_context_invalid");
        return new(CommandStatus.Accepted,state with{RegisteredItem=state.RegisteredItem==command.Text?null:command.Text});
    }
    private Decision<CampaignState> UseItemfinder(CampaignState state,CampaignCommand command,bool registered=false)
    {
        Decision<CampaignState> No(string error)=>new(CommandStatus.Rejected,state,error);
        if(!ItemfinderEnabled)return No("itemfinder_not_enabled");
        if(registered?state.RegisteredItem!=ItemfinderRules.Item.Value:command.Text!=ItemfinderRules.Item.Value)return No("itemfinder_item_invalid");
        if(ValidateCampaignInventory(state.Inventory) is {} error)return No(error);
        if(OpeningInventoryRules.Quantity(state.Inventory!.KeyItems??[],ItemfinderRules.Item)!=1)return No("itemfinder_item_missing");
        if((registered?state.ItemMenu is not null:state.ItemMenu is not{Origin:"bag",View:"bag",TargetInstanceId:null})||
            !IsStableFieldForParty(state with{ItemMenu=null}))return No("itemfinder_field_context_invalid");
        var response=ItemfinderRules.Scan(_content,state.World.Player,state.Script.Flags,ItemfinderSource!);
        if(response.Target is {Underfoot:true} target&&!ShopCatalog.Items.Any(i=>i.Id.Value=="pret:"+target.Item))return No("itemfinder_pickup_item_unsupported");
        string text=response.Target is null?"gText_NopeTheresNoResponse":response.Target.Underfoot?"gText_ItemfinderShakingWildly":"gText_ItemfinderResponding";
        return new(CommandStatus.Accepted,state with{ItemMenu=null,Itemfinder=new(response,ItemfinderSource!.Texts[text].Split("\n\n").ToImmutableArray(),0,response.Target?.Underfoot==true)});
    }
    private Decision<CampaignState> DecideItemfinder(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="itemfinder_ack")return new(CommandStatus.Rejected,state,"itemfinder_message_pending");
        var task=state.Itemfinder!;
        if(task.Page+1<task.Pages.Length)return new(CommandStatus.Accepted,state with{Itemfinder=task with{Page=task.Page+1}});
        if(!task.DigPending)return new(CommandStatus.Accepted,state with{Itemfinder=null});
        var target=task.Response.Target!;
        var source=ItemfinderSource!.Underfoot.Single(i=>i.Map==_content.MapDefinition(target.Map).SourceMapSymbol&&i.Flag==target.Flag);
        if(state.World.Player.MapId!=target.Map||state.World.Player.X!=source.X||state.World.Player.Y!=source.Y||state.Script.Flags.GetValueOrDefault(source.Flag))
            return new(CommandStatus.Fault,state,"itemfinder_underfoot_context_changed");
        var item=ShopCatalog.Items.SingleOrDefault(i=>i.Id.Value=="pret:"+source.Item);
        if(item is null||item.SourceItemId!=source.SourceItemId||item.Pocket!="POCKET_ITEMS")return new(CommandStatus.Rejected,state,"itemfinder_pickup_item_unsupported");
        var inventory=state.Inventory!;
        if(ValidateCampaignInventory(inventory) is {} error)return new(CommandStatus.Rejected,state,error);
        var added=EarlyInventoryAddRules.Add(ShopCatalog,ShopInventoryView(inventory),item.Id,1);
        if(!added.Succeeded&&added.Error is not("inventory_stack_full" or "inventory_destination_full"))return new(CommandStatus.Fault,state,added.Error);
        string Render(string symbol)=>ItemfinderSource.Texts[symbol].Replace("{PLAYER}",state.PlayerName,StringComparison.Ordinal).Replace("{STR_VAR_2}",item.Name,StringComparison.Ordinal).Replace("{STR_VAR_3}","ITEMS POCKET",StringComparison.Ordinal);
        var pages=ImmutableArray.Create(Render("Text_DugUpItemFromGround"),Render(added.Succeeded?"Text_PutItemAway":"Text_TooBadBagFull"));
        // One authoritative transaction owns the add and one-time flag. A full pocket changes neither.
        return new(CommandStatus.Accepted,state with{Inventory=added.Succeeded?MergeShopInventory(inventory,added.State):inventory,
            Script=state.Script with{Flags=added.Succeeded?state.Script.Flags.SetItem(source.Flag,true):state.Script.Flags},
            Itemfinder=task with{Pages=pages,Page=0,DigPending=false}});
    }
    private bool TryApplyItemfinderAideEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!ItemfinderEnabled)return false;
        var args=effect.Args;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("itemfinder_aide_operand:"+token);
        void Context()
        {
            if(state.World.Player.MapId.Value!="map.route-11-east-entrance-2f"||state.LastTalked is null||!state.Actors.TryGetValue(state.LastTalked,out var actor)||actor.ScriptId!=ItemfinderAide||!actor.Active||!state.Script.Locked||state.Script.QuestLogState!=0)throw new InvalidDataException("itemfinder_aide_context");
        }
        CampaignState Result(int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",value)}};
        if(effect.Source.Path==ItemfinderAidePath&&effect.Opcode=="buffernumberstring")
        {
            Context();if(args.Length!=2||!(effect.Source.Symbol=="Route11_EastEntrance_2F_EventScript_GetAideRequestInfo"&&args[0]=="STR_VAR_1"&&Value(args[1])==30||effect.Source.Symbol==ItemfinderAide&&args[0]=="STR_VAR_3"&&Value(args[1])==state.Script.Variables.GetValueOrDefault("VAR_0x8006")))throw new InvalidDataException("itemfinder_aide_buffer");
            next=state with{TextBuffers=state.TextBuffers.SetItem(args[0],Value(args[1]).ToString(CultureInfo.InvariantCulture))};return true;
        }
        if(effect.Source.Path==ItemfinderAidePath&&effect.Opcode=="specialvar")
        {
            Context();if(effect.Source.Symbol!=ItemfinderAide||!args.SequenceEqual(new[]{"VAR_RESULT","GetPokedexCount"})||state.Script.Variables.GetValueOrDefault("VAR_0x8004")!=0)throw new InvalidDataException("itemfinder_aide_count");
            var count=_content.EvaluateOakRating(state.SeenSpecies,state.CaughtSpecies);next=Result(NationalDexEnabledIn(state)?1:0);
            next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_0x8005",count.Seen).SetItem("VAR_0x8006",count.Caught)}};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(args.Length<(buffer?2:1)||Value(args[buffer?1:0])!=261)return false;
        Context();if(effect.Source.Path is not(ItemfinderAidePath or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))throw new InvalidDataException("itemfinder_aide_source");
        if(buffer){if(args[0]!="STR_VAR_2"||args.Length>2&&Value(args[2])!=1)throw new InvalidDataException("itemfinder_aide_item_buffer");next=state with{TextBuffers=state.TextBuffers.SetItem(args[0],"ITEMFINDER")};return true;}
        if(effect.Opcode=="checkitemtype"){next=Result(Value("POCKET_KEY_ITEMS"));return true;}
        if(effect.Source.Path!=ItemfinderAidePath||effect.Source.Symbol!=ItemfinderAide||args.Length>2||args.Length==2&&Value(args[1])!=1||state.Script.Flags.GetValueOrDefault(ItemfinderFlag)||_content.EvaluateOakRating(state.SeenSpecies,state.CaughtSpecies).Caught<30)throw new InvalidDataException("itemfinder_aide_reward_context");
        var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
        if(ValidateCampaignInventory(inventory) is {} invalid)throw new InvalidDataException(invalid);
        var keys=inventory.KeyItems??[];bool fits=keys.Length<30&&OpeningInventoryRules.Quantity(keys,ItemfinderRules.Item)==0;
        next=Result(fits?1:0);if(fits&&effect.Opcode=="additem")next=next with{Inventory=inventory with{KeyItems=keys.Add(new(ItemfinderRules.Item,1))}};
        return true;
    }
    private Decision<CampaignState> UseSacredAsh(CampaignState state,CampaignCommand command)
    {
        if(!SacredAshEnabled||state.ItemMenu is not{Origin:"bag",View:"bag",TargetInstanceId:null} menu||command.Text!=EventIslandShopCatalog.SacredAshId.Value||command.MoveSlot!=-1)return new(CommandStatus.Rejected,state,"sacred_ash_field_context_invalid");
        if(ValidateCampaignInventory(state.Inventory) is {} error)return new(CommandStatus.Rejected,state,error);
        var result=SacredAshRules.Use(ShopCatalog,_content.PokemonContent!,ShopInventoryView(state.Inventory!),state.Party);
        if(result.Error is {} invalid)return invalid=="medicine_no_effect"?new(CommandStatus.Accepted,state with{ItemMenu=menu with{View="message",MessageSymbol="gText_WontHaveEffect",ReturnView="bag"}}):new(CommandStatus.Rejected,state,invalid);
        var template=string.Join("\n",_content.TextPages("gText_PkmnHPRestoredByVar2"));
        var messages=state.Party.Where(p=>p.Pokemon.CurrentHp==0).Select(p=>template.Replace("{STR_VAR_1}",p.Nickname,StringComparison.Ordinal).Replace("{STR_VAR_2}",p.Pokemon.Stats.MaxHp.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal)).ToImmutableArray();
        return new(CommandStatus.Accepted,state with{Inventory=MergeShopInventory(state.Inventory!,result.Inventory),Party=result.Party,
            ItemMenu=menu with{View="message",MessageSymbol="gText_PkmnHPRestoredByVar2",ReturnView="bag",SourceMessages=messages,SourceMessageIndex=0}});
    }
}
