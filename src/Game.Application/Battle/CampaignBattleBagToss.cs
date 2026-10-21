using Game.Domain.Battle;
using Game.Domain.Items;
using Game.Domain.WildBattle;

namespace Game.Application;

// Transient selection ownership, never a saved battle snapshot or a queued action.
public sealed record CampaignBattleTossOwner(object Battle, int Actor, long InstanceId);

public sealed partial class CampaignSession
{
    private CampaignBattleTossOwner? BattleTossOwner(CampaignState state)
    {
        if (state.SafariBattle is not null || state.OldManTutorial is not null || state.TeachyTv is not null || state.Battle is not null || state.MailComposer is not null || state.MailInteraction is not null) return null;
        if (state.DoubleBattle is { SelectingSlot: {} actor, ReplacementSlot: null, LearningPending: false } d &&
            d.Frame.Outcome == WildBattleOutcome.Ongoing && d.Frame.PendingSwitches.IsEmpty && d.Frame.Pokedude is null)
        {
            var member = d.Frame.Get(new(actor)).Member;
            return member is {} m ? new(d, actor, state.Party[m.Index].InstanceId) : null;
        }
        if (state.ForestTrainerBattle is { Stage: not CampaignForestTrainerStage.Combat } || state.ForestTrainerBattle?.CurrentTrainerEvent is not null) return null;
        var single = state.ForestTrainerBattle?.Presentation ?? state.WildBattle;
        if (state.DoubleBattle is null && single is { Phase: CampaignBattlePhase.ChooseMove, Replacement: null, Capture: null } b &&
            b.Frame.Outcome == WildBattleOutcome.Ongoing && b.Frame.FourTurn is null && b.Frame.PendingSwitches.IsEmpty &&
            b.Frame.Rewards?.Pending is null && b.Frame.Pokedude is null && b.Frame.Get(new(0)).Registers.Turn?.Lock is null && SourceFourMedicineRules.CanSelect(b.Frame, new(0)))
            return new((object?)state.ForestTrainerBattle ?? b, 0, state.Party[b.ActivePartyIndex].InstanceId);
        return null;
    }

    private bool ValidBattleTossOwner(CampaignState state, CampaignBattleTossOwner owner)
    {
        var current = BattleTossOwner(state);
        return current is not null && ReferenceEquals(current.Battle, owner.Battle) &&
            current.Actor == owner.Actor && current.InstanceId == owner.InstanceId;
    }

    private Decision<CampaignState> OpenBattleBagToss(CampaignState state, CampaignCommand command)
    {
        var owner = BattleTossOwner(state);
        if (state.ItemMenu is not null || owner is null || command.PartyIndex != owner.Actor || command.TargetInstanceId != owner.InstanceId)
            return new(CommandStatus.Rejected, state, "battle_toss_context_invalid");
        var menuState = state with { ItemMenu = new("battle", "berries") };
        var opened = OpenBagToss(menuState, command);
        if (opened.Status != CommandStatus.Accepted) return new(CommandStatus.Rejected, state, opened.Error);
        return new(CommandStatus.Accepted, opened.State with { ItemMenu = opened.State.ItemMenu! with {
            Toss = opened.State.ItemMenu!.Toss! with { BattleOwner = owner } } });
    }
}
