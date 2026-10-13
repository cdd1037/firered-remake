using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool BattleMedicineEnabled=>!_content.FieldPickups.IsEmpty&&_content.EarlyPartyEnabled;

    public bool FullBattleItemsEnabled=>BattleMedicineEnabled&&_content.FuchsiaGymEnabled&&
        _content.PresetDoubleItems is not null&&_content.PresetDoubleMechanics is not null;

    private string? ValidateBattleMedicineCommand(CampaignState state,CampaignCommand command)
    {
        if(!BattleMedicineEnabled||_content.EarlyShop is null||_content.OpeningItems is null||state.Inventory is null)
            return "battle_medicine_not_enabled";
        if(command.PartyIndex<0||command.PartyIndex>=state.Party.Length||command.TargetInstanceId is not >0||
            state.Party[command.PartyIndex].InstanceId!=command.TargetInstanceId)return "battle_medicine_target_identity";
        if(!StableId.IsValid(command.Text))return "battle_medicine_item_invalid";
        if(command.MoveSlot < -1)return "source_item_move_slot";
        return FullBattleItemsEnabled?SourceBattleInventoryRules.Validate(_content.PresetDoubleItems!,state.Inventory):
            command.MoveSlot>=0?"source_item_effect_unsupported":ValidateCampaignInventory(state.Inventory);
    }

    private Decision<CampaignState> DecideWildMedicine(CampaignState state,CampaignCommand command)
    {
        var error=ValidateBattleMedicineCommand(state,command);if(error is not null)return new(CommandStatus.Rejected,state,error);
        var battle=state.WildBattle!;var frame=battle.Frame??throw new InvalidDataException("canonical_wild_frame_required");
        var result=SourceFourMedicineRules.Select(_content.PresetDoubleItems!,_content.PresetDoubleMechanics!,
            frame,frame.Revision,frame.Get(new(0)).Occupant!.Value,state.Inventory!,state.Party,
            new(new ItemId(command.Text!),command.TargetInstanceId!.Value,command.MoveSlot<0?null:command.MoveSlot),
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection);
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        var begun=SourceWildBattlePolicy.BeginAction(CanonicalWildPokemon,result.Battle.State,result.Battle.State.Revision,result.Selection!);
        if(!begun.Succeeded)return new(CommandStatus.Rejected,state,begun.Fault);
        return CommitCanonicalWildResult(state with{Inventory=result.Inventory},begun with{
            Events=result.Battle.Events.AddRange(begun.Events),Trace=result.Battle.Trace.AddRange(begun.Trace)},resetTrace:true);
    }

    private Decision<CampaignState> DecideTrainerMedicine(CampaignState state,CampaignCommand command)
    {
        var error=ValidateBattleMedicineCommand(state,command);if(error is not null)return new(CommandStatus.Rejected,state,error);
        var trainer = state.ForestTrainerBattle!; var frame = trainer.Frame;
        var selected = SourceFourMedicineRules.Select(_content.PresetDoubleItems!, SafariOrdinaryOwnership!.Mechanics,
            frame, frame.Revision, frame.Get(new(0)).Occupant!.Value, state.Inventory!, state.Party,
            new(new ItemId(command.Text!), command.TargetInstanceId!.Value, command.MoveSlot < 0 ? null : command.MoveSlot),
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection);
        if (!selected.Succeeded) return new(CommandStatus.Rejected, state, selected.Fault);
        var result = BeginCanonicalTrainerAction(state with { Inventory = selected.Inventory, Party = selected.Party }, selected.Selection!, selected.Battle);
        return result.Status == CommandStatus.Accepted ? result : new(result.Status, state, result.Error);
    }
}
