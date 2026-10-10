using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool BattleMedicineEnabled=>!_content.FieldPickups.IsEmpty&&_content.EarlyPartyEnabled;

    private string? ValidateBattleMedicineCommand(CampaignState state,CampaignCommand command)
    {
        if(!BattleMedicineEnabled||_content.EarlyShop is null||_content.OpeningItems is null||state.Inventory is null)
            return "battle_medicine_not_enabled";
        if(command.PartyIndex<0||command.PartyIndex>=state.Party.Length||command.TargetInstanceId is not >0||
            state.Party[command.PartyIndex].InstanceId!=command.TargetInstanceId)return "battle_medicine_target_identity";
        if(!StableId.IsValid(command.Text))return "battle_medicine_item_invalid";
        return ValidateCampaignInventory(state.Inventory);
    }

    private Decision<CampaignState> DecideWildMedicine(CampaignState state,CampaignCommand command)
    {
        var error=ValidateBattleMedicineCommand(state,command);if(error is not null)return new(CommandStatus.Rejected,state,error);
        var battle=state.WildBattle!;
        var result=WildBattleRules.ResolveMedicine(ShopCatalog,Items,_content.PokemonContent!,battle.State,
            ShopInventoryView(state.Inventory!),state.Party,battle.ActivePartyIndex,new(new ItemId(command.Text!),command.TargetInstanceId!.Value));
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        var turn=result.Battle;
        battle=RememberKnockedOff(battle,state.Party[battle.ActivePartyIndex].InstanceId,turn.State!.Player);
        var next=state with{Inventory=MergeShopInventory(state.Inventory!,result.Inventory),Party=result.Party,
            WildBattle=battle with{State=turn.State!,Events=turn.Events,EventIndex=0,Phase=CampaignBattlePhase.Events,RandomTrace=turn.Trace},
            World=state.World with{Rng=turn.State!.Rng}};
        return new(CommandStatus.Accepted,PrepareWildPresentation(PenalizeWildFaint(next)));
    }

    private Decision<CampaignState> DecideTrainerMedicine(CampaignState state,CampaignCommand command)
    {
        var error=ValidateBattleMedicineCommand(state,command);if(error is not null)return new(CommandStatus.Rejected,state,error);
        var trainer=state.ForestTrainerBattle!;
        trainer=WithSsAnnePartyContext(state,trainer);
        var result=ForestTrainerCombatRules.ResolveMedicine(ShopCatalog,Items,_content.PokemonContent!,trainer.Combat,
            ShopInventoryView(state.Inventory!),state.Party,trainer.Presentation.ActivePartyIndex,new(new ItemId(command.Text!),command.TargetInstanceId!.Value));
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        return new(CommandStatus.Accepted,CommitForestTrainerTurn(state with
            {Inventory=MergeShopInventory(state.Inventory!,result.Inventory),Party=result.Party},result.Battle));
    }
}
