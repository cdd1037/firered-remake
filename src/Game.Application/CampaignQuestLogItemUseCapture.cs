using Game.Domain.World;
using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;

namespace Game.Application;

/// <summary>A transaction-local source item-use receipt. Never retained in campaign/save state.</summary>
public sealed record CampaignQuestLogItemUse(ItemId Item,int? TargetSpeciesId=null,bool KeyItem=false);

public static class CampaignQuestLogItemUseCapture
{
    public static CampaignQuestLogItemUse? Observe(CampaignState before,CampaignState after,ICampaignCatalog catalog,CampaignCommand? command=null)
    {
        if(command is {Kind:"field_medicine" or "field_potion",PartyIndex:>=0} && command.PartyIndex<before.Party.Length &&
            command.PartyIndex<after.Party.Length && (command.Kind=="field_potion"||command.Text is not null) && before.Inventory is {} fieldBefore && after.Inventory is {} fieldAfter)
        {
            var id=new ItemId(command.Kind=="field_potion"?"pret:ITEM_POTION":command.Text!);
            if(catalog.Inventory.Items.TryGetValue(id,out var definition)&&
                definition.FieldUseFunction is "FieldUseFunc_Medicine" or "FieldUseFunc_Ether" &&
                before.Party[command.PartyIndex].InstanceId==after.Party[command.PartyIndex].InstanceId)
            {
                var previousPocket=definition.Pocket=="POCKET_BERRY_POUCH"?fieldBefore.Berries??[]:fieldBefore.BagItems;
                var currentPocket=definition.Pocket=="POCKET_BERRY_POUCH"?fieldAfter.Berries??[]:fieldAfter.BagItems;
                if(previousPocket.Where(s=>s.Item==id).Sum(s=>s.Quantity)-currentPocket.Where(s=>s.Item==id).Sum(s=>s.Quantity)==1)
                    return new(id,after.Party[command.PartyIndex].Pokemon.SourceSpeciesId);
            }
        }
        if(before.PokeFlute is null&&after.PokeFlute is not null)
            return new(new("pret:ITEM_POKE_FLUTE"),KeyItem:true);
        if(before.ItemMenu?.MessageSymbol!="gText_PowderQty"&&after.ItemMenu?.MessageSymbol=="gText_PowderQty")
            return new(new("pret:ITEM_POWDER_JAR"),KeyItem:true);
        // Escape items finish the ordinary battle inside the accepted menu command.
        // The existing terminal campaign receipt survives that lifecycle completion;
        // its paid source item distinguishes this from an ordinary successful Run.
        if(before.WildBattle is {} escaping && after.WildBattle is null &&
            after.Exploration?.Battles.LastOrDefault() is {Outcome:Game.Domain.WildBattle.WildBattleOutcome.Escaped} ended &&
            ended.Id==escaping.Id && before.Exploration?.Battles.LastOrDefault()?.Id!=ended.Id &&
            before.Inventory is {} previous && after.Inventory is {} current)
            foreach(var stack in previous.BagItems)
                if(catalog.Inventory.Items.GetValueOrDefault(stack.Item)?.BattleUseFunction=="BattleUseFunc_PokeDoll" &&
                    stack.Quantity-current.BagItems.Where(s=>s.Item==stack.Item).Sum(s=>s.Quantity)==1)
                    return new(stack.Item);
        if(before.WildBattle?.Frame is {} wildBefore&&after.WildBattle?.Frame is {} wildAfter)
            return Battle(before,wildBefore,after.WildBattle.SourceEvents,catalog);
        if(before.ForestTrainerBattle is {} trainerBefore&&after.ForestTrainerBattle is {} trainerAfter)
            return Battle(before,trainerBefore.Frame,trainerAfter.Presentation.SourceEvents,catalog);
        if(before.DoubleBattle is {} doubleBefore&&after.DoubleBattle is {} doubleAfter)
            return Battle(before,doubleBefore.Frame,doubleAfter.Events,catalog);
        return null;
    }

    private static CampaignQuestLogItemUse? Battle(CampaignState campaign,SourceBattleFrame before,
        ImmutableArray<SourceBattleEvent> events,ICampaignCatalog catalog)
    {
        // Menu success publishes this receipt before animation. Sequence bounds exclude
        // retained presentation batches, acknowledgements and the opponent's own items.
        if(!events.IsDefault)
            foreach(var used in events.OfType<SourceMedicineAppliedEvent>())
                if(catalog.Inventory.Items.GetValueOrDefault(used.Item)?.BattleUseFunction is "BattleUseFunc_Medicine" or "BattleUseFunc_Ether"&&
                    used.Sequence>before.EventSequence&&used.Actor?.Side==BattleSide.Player&&
                    used.TargetSide==BattleSide.Player&&campaign.Party.FirstOrDefault(p=>p.InstanceId==used.TargetInstanceId) is {} target)
                    return new(used.Item,target.Pokemon.SourceSpeciesId);
        return null;
    }
}
