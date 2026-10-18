using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // Caller scripts own their prelude independently of the subsequently selected
    // leaf's outcome. Metronome has no PP command at this source boundary.
    private static CampaignMoveAnimationDecision CallerPrelude(Invocation battle,
        SourceAttackStringEvent? attack,SourceMoveUsedEvent? used,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        CampaignMoveAnimationDecision Deny(string reason)=>new(null,reason);
        var caller=Definition(used?.Move??attack!.CurrentMove,moves);
        if(caller is null||!supports(caller.SourceMoveId,caller.Id.Value))return Deny("script-outside-slice");
        bool sleep=caller.Effect=="EFFECT_SLEEP_TALK";
        if(!sleep&&caller.Effect!="EFFECT_METRONOME")return Deny("not-caller-prelude");
        if(sleep)
        {
            if(used is null||battle.EventIndex<1||battle.Events[battle.EventIndex-1] is not SourceAttackStringEvent previous)
                return Deny("unmatched-source-caller-attack");
            attack=previous;
        }
        else if(used is not null)return Deny("unmatched-source-caller-attack");
        if(attack is null||attack.Actor.Value<0||attack.Actor.Value>=battle.Slots||
            attack.ChosenMove!=caller.Id||attack.CurrentMove!=caller.Id||
            used is not null&&(used.Actor!=attack.Actor||used.Move!=caller.Id||used.Turn!=attack.Turn||
                used.Sequence<=attack.Sequence||used.Slot is <0 or >3||used.Target.Value<0||used.Target.Value>=battle.Slots))
            return Deny("unmatched-source-caller-attack");
        int start=battle.EventIndex+1,end=start;
        while(end<battle.Events.Length&&battle.Events[end].Turn==attack.Turn&&
            battle.Events[end] is not (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent))end++;
        var action=battle.Events[start..end];
        for(int i=battle.EventIndex+1;i<end;i++)if(battle.Events[i].Sequence<=battle.Events[i-1].Sequence)
            return Deny("unmatched-source-caller-sequence");
        int selectionIndex=sleep?2:0;
        if(action.Length<=selectionIndex+1||action[selectionIndex] is not SourceCallerSelectionEvent selection||
            selection.Actor!=attack.Actor||selection.Caller!=caller.Id||selection.Result is not {} selected||
            Definition(selected,moves) is not {} leaf||action[selectionIndex+1] is not SourceCalledMoveTransitionEvent transition||
            action.OfType<SourceCallerSelectionEvent>().Count()!=1||action.OfType<SourceCalledMoveTransitionEvent>().Count()!=1)
            return Deny("unmatched-source-caller-selection");
        var invocation=transition.Invocation;
        if(invocation.Actor!=attack.Actor||invocation.PpOwner!=attack.Actor||
            invocation.OriginalChosenMove!=caller.Id||invocation.ChosenMove!=caller.Id||invocation.CurrentMove!=selected||
            invocation.ChosenSlot is <0 or >3||invocation.PpSlot is <0 or >3||
            invocation.Target.Value<0||invocation.Target.Value>=battle.Slots||
            invocation.CancellerPhase!=WildBattleAttackCancellerPhase.ConditionsPassed||
            invocation.Flags!=(WildBattleInvocationFlags.Obeys|(sleep?WildBattleInvocationFlags.NoPpDeduct:WildBattleInvocationFlags.None)))
            return Deny("unmatched-source-caller-transition");
        long? ppSequence=null;
        if(sleep)
        {
            if(action[0] is not SourcePpCommandEvent pp||!ValidSingleInvocationPp([pp],used!,2)||
                action[1] is not SourceSleepTalkEvent talk||talk.Side!=attack.Actor||talk.CalledMove!=selected||
                talk.CalledSlot is <0 or >3||talk.CalledSlot==used!.Slot||
                invocation.ChosenSlot!=used.Slot||invocation.PpSlot!=talk.CalledSlot||invocation.CalledMove!=selected||
                (invocation.Flags&WildBattleInvocationFlags.NoPpDeduct)==0||
                selection.CandidateSourceMoveIds.IsDefaultOrEmpty||!selection.CandidateSourceMoveIds.Contains(leaf.SourceMoveId)||
                action.OfType<SourceSleepTalkEvent>().Count()!=1)
                return Deny("unmatched-source-sleep-talk-selection");
            ppSequence=pp.Sequence;
        }
        else if(invocation.PpSlot!=invocation.ChosenSlot||invocation.CalledMove is not null||
            (invocation.Flags&WildBattleInvocationFlags.NoPpDeduct)!=0)
            return Deny("unmatched-source-metronome-selection");
        long sequence=used?.Sequence??attack.Sequence;
        return new(new(battle.Id,sequence,attack.Sequence,ppSequence,sleep?selection.Sequence:sequence,
            caller.SourceMoveId,attack.Actor,sleep?invocation.Target:attack.Actor,false){Boundary=CampaignMoveAnimationBoundary.AfterCallerText},
            "committed-source-caller-prelude");
    }
}
