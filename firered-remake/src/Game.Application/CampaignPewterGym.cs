using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Ids;
using System.Collections.Immutable;
using Game.Domain.Trainers;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplyPewterGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.PewterEnabled)return false;
        if(effect.Opcode=="settrainerflag")
        {
            if(effect.Source.Path!="data/scripts/set_gym_trainers.inc"||effect.Source.Symbol!="EventScript_SetPewterGymTrainers"||
                effect.Args.Length!=1||effect.Args[0]!="TRAINER_CAMPER_LIAM"||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"))
                throw new InvalidDataException("gym_trainer_flag_context_unsupported");
            next=state with{BattledTrainers=state.BattledTrainers.Add(PewterTrainerPartyFactory.Liam.Value)};return true;
        }
        if(_content.PewterMachines is not {} machines)return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("gym_item_value_missing: "+token);
        if(effect.Opcode=="bufferstdstring"&&effect.Args[1]=="STDSTRING_TM_CASE")
        {next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM CASE")};return true;}
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=machines.Tm39.Item.SourceItemId)return false;
        if(effect.Source.Path is not("data/maps/PewterCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))throw new InvalidDataException("gym_item_source_unsupported");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],machines.Tm39.Item.Name)};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(effect.Opcode=="checkitemspace")result=PewterMachineRules.CanAddTm39(machines,inventory,quantity)?1:0;
            else{var added=PewterMachineRules.AddTm39(machines,inventory,quantity);next=state with{Inventory=added.State};result=added.Succeeded?1:0;}
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;

    }
    private string? ValidatePewterProgress(CampaignState state)
    {
        bool badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET");
        if(!_content.PewterEnabled)return badge||state.Inventory?.Machines is not null?"save_pewter_profile_missing":null;
        if(HasUnimplementedBadge(state))return "save_badges_outside_pewter_scope";
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==PewterTrainerPartyFactory.Brock.Value&&h.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won);
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_BROCK")!=won)return "save_brock_badge_history_mismatch";
        if(won&&(!state.BattledTrainers.Contains(PewterTrainerPartyFactory.Liam.Value)||!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_PEWTER_CITY_GYM_GUIDE")||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PEWTER_CITY") is not (1 or 2)))return "save_brock_reward_flags_mismatch";
        // Brock reveals the aide in scene1. The original running-shoes script then
        // removes him, grants B_DASH and advances to scene2 as one stable outcome.
        // Requiring a visible aide forever rejects a normally earned post-gift save.
        bool runningShoes=state.Script.Flags.GetValueOrDefault("FLAG_SYS_B_DASH");
        bool shoesSceneComplete=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PEWTER_CITY")==2;
        bool aideHidden=state.Script.Flags.GetValueOrDefault("FLAG_HIDE_PEWTER_CITY_RUNNING_SHOES_GUY");
        if(runningShoes&&!won||won&&(shoesSceneComplete!=runningShoes||aideHidden!=runningShoes))
            return "save_running_shoes_progress_mismatch";
        var machineError=PewterMachineRules.Validate(_content.PewterMachines!,state.Inventory);if(machineError is not null)return machineError;
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM39_FROM_BROCK");
        bool hasCase=(state.Inventory?.KeyItems??[]).Any(s=>s.Item==PewterMachineRules.TmCaseId);
        int tmQuantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],PewterMachineRules.Tm39Id);
        if(got&&!won||got!=hasCase||!got&&tmQuantity!=0||tmQuantity>1)return "save_tm39_reward_mismatch";
        return null;
    }
    private InventoryState LegacyQuestInventory(InventoryState inventory)
    {
        var view=WithoutCityServiceItems(_content.PewterEnabled?PewterMachineRules.WithoutMachines(inventory):inventory);
        if(_content.FirstSeviiEnabled)view=view with{KeyItems=view.KeyItems?.Where(s=>s.Item!=FirstSeviiShopCatalog.MeteoriteId&&s.Item!=FirstSeviiShopCatalog.TriPassId).ToImmutableArray()};
        if(_content.CinnabarEnabled)view=view with{KeyItems=view.KeyItems?.Where(s=>s.Item!=CinnabarShopCatalog.SecretKeyId).ToImmutableArray()};
        if(_content.SilphEnabled)view=view with
        {
            KeyItems=view.KeyItems?.Where(s=>s.Item!=SilphShopCatalog.CardKeyId).ToImmutableArray(),
            Balls=view.Balls?.Where(s=>s.Item!=SilphShopCatalog.UltraBallId&&s.Item!=SilphShopCatalog.MasterBallId).ToImmutableArray()
        };
        if(_content.PokemonTowerEnabled)view=view with{KeyItems=view.KeyItems?.Where(s=>s.Item!=PokemonTowerItemRules.PokeFluteId).ToImmutableArray()};
        if(_content.RocketHideoutEnabled)view=view with{KeyItems=view.KeyItems?.Where(s=>s.Item.Value is not("pret:ITEM_LIFT_KEY" or "pret:ITEM_SILPH_SCOPE")).ToImmutableArray(),Balls=view.Balls?.Where(s=>s.Item.Value is not("pret:ITEM_NET_BALL" or "pret:ITEM_NEST_BALL")).ToImmutableArray()};
        if(_content.SsAnneEnabled)view=view with{Berries=null,Balls=view.Balls is {} balls?balls.Where(s=>s.Item!=SsAnneItemRules.GreatBallId).ToImmutableArray():null,KeyItems=view.KeyItems is {} shipKeys?shipKeys.Where(s=>s.Item!=SsAnneItemRules.BerryPouchId).ToImmutableArray():null};
        if(_content.BillEnabled)view=view with{KeyItems=view.KeyItems is {} billKeys?billKeys.Where(s=>s.Item!=BillItemRules.FameCheckerId&&s.Item!=BillItemRules.SsTicketId).ToImmutableArray():null};
        return _content.MtMoonEnabled?view with{KeyItems=view.KeyItems is {} keys?keys.Where(s=>s.Item!=MtMoonItemRules.HelixFossilId&&s.Item!=MtMoonItemRules.DomeFossilId).ToImmutableArray():null}:view;
    }
    private string? ValidateQuestInventory(QuestItemsCatalog catalog,InventoryState? inventory)=>inventory is null?"inventory_missing":QuestItemRules.Validate(catalog,LegacyQuestInventory(inventory));
    private int QuestQuantity(QuestItemsCatalog catalog,InventoryState inventory,string symbol)=>QuestItemRules.Quantity(catalog,LegacyQuestInventory(inventory),symbol);
    private InventoryTransactionResult QuestChange(QuestItemsCatalog catalog,InventoryState inventory,string symbol,int quantity,bool add)
    {
        var inventoryError=ValidateQuestInventory(catalog,inventory);
        if(inventoryError is not null)return new(inventory,false,inventoryError);
        // The legacy view strips TM CASE for membership checks, but the physical
        // Key Items pocket still contains that slot when checking new insertions.
        if(add&&catalog.Items.FirstOrDefault(i=>i.SourceItemSymbol==symbol) is {Pocket:"POCKET_KEY_ITEMS"} item&&
            OpeningInventoryRules.Quantity(inventory.KeyItems??[],item.Id)==0&&(inventory.KeyItems?.Length??0)>=catalog.Limits.KeyItemSlots)
            return new(inventory,false,"inventory_destination_full");
        var view=LegacyQuestInventory(inventory);var result=add?QuestItemRules.Add(catalog,view,symbol,quantity):QuestItemRules.Remove(catalog,view,symbol,quantity);
        if(!result.Succeeded)return result with{State=inventory};
        var keys=result.State.KeyItems;if(_content.PewterEnabled&&inventory.KeyItems is {} original)
        {
            var preserved=original.Where(s=>!(view.KeyItems??[]).Any(v=>v.Item==s.Item)).ToArray();
            foreach(var kept in preserved){var index=original.IndexOf(kept);var items=(keys??[]).ToBuilder();items.Insert(Math.Min(index,items.Count),kept);keys=items.ToImmutable();}
        }
        var balls=result.State.Balls;
        if(_content.RocketHideoutEnabled&&inventory.Balls is {} allBalls)
        {
            foreach(var kept in allBalls.Where(s=>!(view.Balls??[]).Any(v=>v.Item==s.Item)))
            {var items=(balls??[]).ToBuilder();items.Insert(Math.Min(allBalls.IndexOf(kept),items.Count),kept);balls=items.ToImmutable();}
        }
        else if(_content.SsAnneEnabled)balls=(balls??[]).AddRange((inventory.Balls??[]).Where(s=>s.Item==SsAnneItemRules.GreatBallId));
        return result with{State=inventory with{KeyItems=keys,Balls=balls}};
    }
}
