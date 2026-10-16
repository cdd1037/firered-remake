using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignFieldPickup(string Id,bool InventoryAdded=false);

public sealed partial class CampaignSession
{
    private FieldPickupDefinition? PickupForActor(CampaignState state,CampaignActor actor)=>
        _content.FieldPickups.FirstOrDefault(p=>!p.Hidden&&p.MapSymbol==_content.MapDefinition(state.World.Player.MapId).SourceMapSymbol&&p.SourceLocalId==actor.SourceLocalId);

    private CampaignState BeginItemBall(CampaignState state,CampaignActor actor)
    {
        var definition=PickupForActor(state,actor);if(definition is null)return state;
        if(state.FieldPickup is not null||actor.ScriptId!=definition.Script||actor.HideFlag!=definition.Flag||
            actor.X!=definition.X||actor.Y!=definition.Y||!actor.Active||state.Script.Flags.GetValueOrDefault(definition.Flag))
            throw new InvalidDataException("field_pickup_actor_context_invalid");
        return state with{FieldPickup=new(definition.Id)};
    }

    private Decision<CampaignState> BeginHiddenPickup(CampaignState state,FieldBackgroundTrigger sign)
    {
        var definition=_content.FieldPickups.FirstOrDefault(p=>p.Hidden&&p.MapSymbol==_content.MapDefinition(state.World.Player.MapId).SourceMapSymbol&&p.X==sign.X&&p.Y==sign.Y);
        if(definition is null)return new(CommandStatus.Rejected,state,"hidden_item_not_implemented");
        if(state.Script.Flags.GetValueOrDefault(definition.Flag))return new(CommandStatus.Rejected,state,"nothing_to_interact");
        if(state.FieldPickup is not null||sign.HiddenItem!=definition.ItemSymbol||sign.HiddenFlag!=definition.Flag||sign.Quantity!=definition.Quantity)
            return new(CommandStatus.Fault,state,"hidden_item_context_invalid");
        // Source GetInteractedBackgroundEventScript writes these exact scratch variables
        // before entering the shared script. The source script owns the later flag commit.
        var variables=state.Script.Variables.SetItem("VAR_0x8004",definition.SourceFlagId)
            .SetItem("VAR_0x8005",definition.SourceItemId).SetItem("VAR_0x8006",definition.Quantity);
        var next=state with{FieldPickup=new(definition.Id),Script=state.Script with{Variables=variables}};
        return new(CommandStatus.Accepted,Pump(StartStory(next,definition.Script)));
    }

    private bool TryApplyFieldPickupEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(state.FieldPickup is not {} context)return false;
        var definition=_content.FieldPickups.SingleOrDefault(p=>p.Id==context.Id)??throw new InvalidDataException("field_pickup_missing");
        if(definition.MapSymbol!=_content.MapDefinition(state.World.Player.MapId).SourceMapSymbol)
            throw new InvalidDataException("field_pickup_map_changed");
        int Value(string token)=>int.TryParse(token,out var n)?n:_content.TryConstant(token,out n)?n:
            throw new InvalidDataException("field_pickup_value_missing: "+token);
        var args=effect.Args;
        if(IsHiddenCoinPickup(definition)&&TryApplyHiddenCoinEffect(state,definition,context,effect,out next))return true;
        if(effect.Opcode=="specialvar"&&args[1]=="BufferTMHMMoveName")
        {
            if(definition.Hidden||effect.Source.Path!="data/scripts/obtain_item.inc"||effect.Source.Symbol!="EventScript_PickUpItem"||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004")!=definition.SourceItemId||!context.InventoryAdded||args[0]!="VAR_RESULT")
                throw new InvalidDataException("field_pickup_tm_query_context_invalid");
            // None of the three authenticated early pickup items is a TM/HM. The source
            // helper returns FALSE and leaves the item-name buffer untouched in this case.
            var tm=MachineRules.Definition(_content.Machines!,definition.Item);
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",tm is null?0:1)},TextBuffers=tm is null?state.TextBuffers:state.TextBuffers.SetItem("STR_VAR_1",_content.BattleContent!.MoveNames[tm.Move.Value])};return true;
        }
        if(effect.Opcode=="special"&&args[0]=="SetHiddenItemFlag")
        {
            if(!definition.Hidden||!context.InventoryAdded||effect.Source.Path!="data/scripts/obtain_item.inc"||effect.Source.Symbol!=(IsHiddenCoinPickup(definition)?"EventScript_TryPickUpHiddenCoins":"EventScript_PickedUpHiddenItem")||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004")!=definition.SourceFlagId)
                throw new InvalidDataException("hidden_item_flag_context_invalid");
            if(IsHiddenCoinPickup(definition))RequireCoinEffect(state,effect);
            next=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem(definition.Flag,true)}};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "checkitemtype"))return false;
        if(effect.Source.Path!="data/scripts/obtain_item.inc")throw new InvalidDataException("field_pickup_item_source_invalid");
        bool buffer=effect.Opcode=="bufferitemname";
        if(Value(args[buffer?1:0])!=definition.SourceItemId)throw new InvalidDataException("field_pickup_item_identity_invalid");
        if(buffer)
        {
            if(args[0]!="STR_VAR_2")throw new InvalidDataException("field_pickup_buffer_invalid");
            next=state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_2",definition.ItemName)};return true;
        }
        if(effect.Opcode=="checkitemtype")
        {next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",Value(definition.Pocket))}};return true;}
        int quantity=args.Length>1?Value(args[1]):1;
        if(quantity!=definition.Quantity||effect.Opcode=="additem"&&context.InventoryAdded)
            throw new InvalidDataException("field_pickup_quantity_or_duplicate");
        var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
        var error=ValidateCampaignInventory(inventory);if(error is not null)throw new InvalidDataException(error);
        if(definition.Pocket=="POCKET_TM_CASE")
        {
            var machines=_content.Machines!;
            var transaction=effect.Opcode=="additem"?MachineRules.AddMachine(machines,inventory,definition.Item,quantity):new InventoryTransactionResult(inventory,MachineRules.CanAddMachine(machines,inventory,definition.Item,quantity),null);
            if(!transaction.Succeeded&&effect.Opcode=="additem")throw new InvalidDataException(transaction.Error??"field_pickup_machine_add_failed");
            bool committed=transaction.Succeeded&&effect.Opcode=="additem";
            next=state with{Inventory=transaction.State,FieldPickup=committed?context with{InventoryAdded=true}:context,Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",transaction.Succeeded?1:0)}};return true;
        }
        var view=inventory;
        var result=effect.Opcode=="additem"?EarlyInventoryAddRules.Add(ShopCatalog,view,definition.Item,quantity):EarlyInventoryAddRules.CheckSpace(ShopCatalog,view,definition.Item,quantity);
        if(!result.Succeeded&&result.Error is not("inventory_stack_full" or "inventory_destination_full" or "inventory_key_items_full"))throw new InvalidDataException(result.Error);
        bool added=result.Succeeded&&effect.Opcode=="additem";
        next=CommitInventory(state with{
            FieldPickup=added?context with{InventoryAdded=true}:context,
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",result.Succeeded?1:0)}},added?result.State:inventory);return true;
    }

    private CampaignState FinishFieldPickup(CampaignState state)
    {
        if(state.FieldPickup is not {} context)return state;
        var definition=_content.FieldPickups.Single(p=>p.Id==context.Id);
        if(state.Script.Flags.GetValueOrDefault(definition.Flag)!=context.InventoryAdded)
            throw new InvalidDataException("field_pickup_completion_mismatch");
        return state with{FieldPickup=null};
    }

    private string? ValidateFieldPickupState(CampaignState state)
    {
        if(state.FieldPickup is not null)return "save_field_pickup_active";
        foreach(var definition in _content.FieldPickups.Where(p=>!p.Hidden&&p.MapSymbol==_content.MapDefinition(state.World.Player.MapId).SourceMapSymbol))
        {
            var actor=state.Actors.Values.SingleOrDefault(a=>a.SourceLocalId==definition.SourceLocalId);
            if(actor is null||actor.ScriptId!=definition.Script||actor.HideFlag!=definition.Flag||actor.TemplateX!=definition.X||actor.TemplateY!=definition.Y||
                state.Script.Flags.GetValueOrDefault(definition.Flag)&&actor.Active)return "save_field_pickup_actor_mismatch";
        }
        return null;
    }
}
