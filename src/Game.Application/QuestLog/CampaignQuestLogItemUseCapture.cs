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
        if(command is {Kind:"field_medicine" or "field_potion" or "field_rare_candy",PartyIndex:>=0} && command.PartyIndex<before.Party.Length &&
            command.PartyIndex<after.Party.Length && (command.Kind=="field_potion"||command.Text is not null) && before.Inventory is {} fieldBefore && after.Inventory is {} fieldAfter)
        {
            var id=new ItemId(command.Kind=="field_potion"?"pret:ITEM_POTION":command.Text!);
            if(catalog.Inventory.Items.TryGetValue(id,out var definition)&&
                definition.FieldUseFunction is "FieldUseFunc_Medicine" or "FieldUseFunc_Ether" or "FieldUseFunc_PpUp" or "FieldUseFunc_RareCandy" &&
                before.Party[command.PartyIndex].InstanceId==after.Party[command.PartyIndex].InstanceId)
            {
                var previousPocket=definition.Pocket=="POCKET_BERRY_POUCH"?fieldBefore.Berries??[]:fieldBefore.BagItems;
                var currentPocket=definition.Pocket=="POCKET_BERRY_POUCH"?fieldAfter.Berries??[]:fieldAfter.BagItems;
                if(previousPocket.Where(s=>s.Item==id).Sum(s=>s.Quantity)-currentPocket.Where(s=>s.Item==id).Sum(s=>s.Quantity)==1)
                    return new(id,after.Party[command.PartyIndex].Pokemon.SourceSpeciesId);
            }
        }
        if(before.Evolution is null && after.Evolution?.ItemUse?.Eligibility is {} evolution &&
            command is {Kind:"field_evolution",Text:not null} &&
            catalog.Inventory.Items.TryGetValue(new ItemId(command.Text),out var stone) && stone.FieldUseFunction=="FieldUseFunc_EvoItem" &&
            stone.SourceItemSymbol==evolution.Item && before.Party.FirstOrDefault(p=>p.InstanceId==evolution.InstanceId&&p.Pokemon.Species==evolution.PreviousSpecies) is {} recipient &&
            before.Inventory is {} stoneBefore && after.Inventory is {} stoneAfter &&
            stoneBefore.BagItems.Where(s=>s.Item==stone.Id).Sum(s=>s.Quantity)-stoneAfter.BagItems.Where(s=>s.Item==stone.Id).Sum(s=>s.Quantity)==1)
            return new(stone.Id,recipient.Pokemon.SourceSpeciesId);
        if(before.Repel is {Phase:CampaignRepelPhase.UseSound} repel && after.Repel is null &&
            command is {Kind:"repel_presented"} && command.Text==repel.WaitId &&
            catalog.Inventory.Items.GetValueOrDefault(repel.Item)?.FieldUseFunction=="FieldUseFunc_Repel" &&
            before.Inventory is {} repelBefore && after.Inventory is {} repelAfter &&
            repelBefore.BagItems.Where(s=>s.Item==repel.Item).Sum(s=>s.Quantity)-repelAfter.BagItems.Where(s=>s.Item==repel.Item).Sum(s=>s.Quantity)==1)
            return new(repel.Item);
        if(command is {Kind:"field_sacred_ash" or "field_medicine",Text:not null} &&
            catalog.Inventory.Items.TryGetValue(new ItemId(command.Text),out var partyItem) &&
            partyItem.FieldUseFunction=="FieldUseFunc_SacredAsh" && before.Inventory is {} ashBefore && after.Inventory is {} ashAfter &&
            ashBefore.BagItems.Where(s=>s.Item==partyItem.Id).Sum(s=>s.Quantity)-ashAfter.BagItems.Where(s=>s.Item==partyItem.Id).Sum(s=>s.Quantity)==1)
        {
            // Original sSacredAshQuestLogMonBackup keeps the first successful party-loop target.
            var revived=before.Party.FirstOrDefault(p=>p.Pokemon.CurrentHp==0 &&
                after.Party.Any(a=>a.InstanceId==p.InstanceId&&a.Pokemon.CurrentHp>0));
            if(revived is not null)return new(partyItem.Id,revived.Pokemon.SourceSpeciesId);
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
            return Battle(before,wildBefore,after.WildBattle.Events,catalog);
        if(before.ForestTrainerBattle is {} trainerBefore&&after.ForestTrainerBattle is {} trainerAfter)
            return Battle(before,trainerBefore.Frame,trainerAfter.Presentation.Events,catalog);
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
