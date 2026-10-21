using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Ids;
using System.Collections.Immutable;
using Game.Domain.World;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplyPewterGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        if(effect.Opcode=="settrainerflag")
        {
            if(effect.Source.Path!="data/scripts/set_gym_trainers.inc"||effect.Source.Symbol!="EventScript_SetPewterGymTrainers"||
                effect.Args.Length!=1||effect.Args[0]!="TRAINER_CAMPER_LIAM"||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"))
                throw new InvalidDataException("gym_trainer_flag_context_unsupported");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:TRAINER_CAMPER_LIAM")};return true;
        }
        if(_content.Machines is not {} machines)return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("gym_item_value_missing: "+token);
        if(effect.Opcode=="bufferstdstring"&&effect.Args[1]=="STDSTRING_TM_CASE")
        {next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM CASE")};return true;}
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=MachineRules.Definition(machines, MachineRules.Tm39Id)!.Machine.Item.SourceItemId)return false;
        if(effect.Source.Path is not("data/maps/PewterCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))throw new InvalidDataException("gym_item_source_unsupported");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],MachineRules.Definition(machines, MachineRules.Tm39Id)!.Machine.Item.Name)};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(effect.Opcode=="checkitemspace")result=MachineRules.CanAddTm39(machines,inventory,quantity)?1:0;
            else{var added=MachineRules.AddTm39(machines,inventory,quantity);next=state with{Inventory=added.State};result=added.Succeeded?1:0;}
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;

    }
    private string? ValidatePewterProgress(CampaignState state)
    {
        bool badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET");
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId=="pret:TRAINER_LEADER_BROCK"&&h.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won);
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_BROCK")!=won)return "save_brock_badge_history_mismatch";
        if(won&&(!state.BattledTrainers.Contains("pret:TRAINER_CAMPER_LIAM")||!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_PEWTER_CITY_GYM_GUIDE")||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PEWTER_CITY") is not (1 or 2)))return "save_brock_reward_flags_mismatch";
        // Brock reveals the aide in scene1. The original running-shoes script then
        // removes him, grants B_DASH and advances to scene2 as one stable outcome.
        // Requiring a visible aide forever rejects a normally earned post-gift save.
        bool runningShoes=state.Script.Flags.GetValueOrDefault("FLAG_SYS_B_DASH");
        bool shoesSceneComplete=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PEWTER_CITY")==2;
        bool aideHidden=state.Script.Flags.GetValueOrDefault("FLAG_HIDE_PEWTER_CITY_RUNNING_SHOES_GUY");
        if(runningShoes&&!won||won&&(shoesSceneComplete!=runningShoes||aideHidden!=runningShoes))
            return "save_running_shoes_progress_mismatch";
        var machineError=MachineRules.Validate(_content.Machines!,state.Inventory);if(machineError is not null)return machineError;
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM39_FROM_BROCK");
        bool hasCase=(state.Inventory?.KeyItems??[]).Any(s=>s.Item==MachineRules.TmCaseId);
        int tmQuantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],MachineRules.Tm39Id);
        if(got&&!won||got!=hasCase||!got&&tmQuantity!=0||tmQuantity>1)return "save_tm39_reward_mismatch";
        return null;
    }
    private string? ValidateQuestInventory(QuestItemsCatalog catalog, InventoryState? inventory) =>
        _content.Inventory.Validate(inventory);
    private int QuestQuantity(QuestItemsCatalog catalog, InventoryState inventory, string symbol)
    {
        var error = _content.Inventory.Validate(inventory);
        if (error is not null) throw new ArgumentException(error, nameof(inventory));
        var item = catalog.Items.SingleOrDefault(i => i.SourceItemSymbol == symbol)
            ?? throw new NotSupportedException("inventory_item_unsupported");
        return OpeningInventoryRules.Quantity(item.Pocket == "POCKET_KEY_ITEMS" ? inventory.KeyItems ?? [] : inventory.Balls ?? [], item.Id);
    }
    private InventoryTransactionResult QuestChange(QuestItemsCatalog catalog, InventoryState inventory, string symbol, int quantity, bool add)
    {
        InventoryTransactionResult Fail(string error) => new(inventory, false, error);
        var error = _content.Inventory.Validate(inventory);
        if (error is not null) return Fail(error);
        var item = catalog.Items.SingleOrDefault(i => i.SourceItemSymbol == symbol);
        if (item is null || !_content.Inventory.Items.TryGetValue(item.Id, out var admitted) || admitted != item)
            return Fail("inventory_item_unsupported");
        if (quantity < 1 || quantity > catalog.Limits.MaximumStack) return Fail("inventory_transfer_quantity");
        var pocket = item.Pocket == "POCKET_KEY_ITEMS" ? inventory.KeyItems ?? [] : inventory.Balls ?? [];
        var current = OpeningInventoryRules.Quantity(pocket, item.Id);
        if (add && current > catalog.Limits.MaximumStack - quantity) return Fail("inventory_stack_full");
        var capacity = item.Pocket == "POCKET_KEY_ITEMS" ? catalog.Limits.KeyItemSlots : catalog.Limits.BallSlots;
        if (add && current == 0 && pocket.Length >= capacity) return Fail("inventory_destination_full");
        if (!add && current < quantity) return Fail("inventory_insufficient_quantity");
        var changed = OpeningInventoryRules.SetQuantity(pocket, item.Id, current + (add ? quantity : -quantity));
        var state = item.Pocket == "POCKET_KEY_ITEMS" ? inventory with { KeyItems = changed } : inventory with { Balls = changed };
        var invalid = _content.Inventory.Validate(state);
        return invalid is null ? new(state, true, null) : Fail(invalid);
    }
}
