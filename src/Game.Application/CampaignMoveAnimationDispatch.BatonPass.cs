using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision BatonPassAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        // BattleScript_EffectBatonPass animates after jumpifcantswitch succeeds,
        // before openpartyscreen. The incoming member and switch-in results are
        // deliberately not inputs to this outgoing battler's invocation.
        var context=battle.Context;
        if(context is null||definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||
            used.Target!=used.Actor||!ValidSingleInvocationPp(commands,used,1,invocation:context)||
            attack.Sequence+1!=used.Sequence||action.Length<2||action[0]!=commands[0]||commands[0].Sequence!=used.Sequence+1||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(p=>p.Second.Sequence!=p.First.Sequence+1)||
            action[1] is not SourceCalledPartySwitchRequiredEvent required)
            return new(null,"unmatched-source-baton-pass-invocation");
        var pending=required.Pending;
        if(pending is null||pending.Slot!=used.Actor||pending.Kind!=WildBattleCalledSwitchKind.BatonPass||
            pending.LegalPartyIndices.IsDefaultOrEmpty||pending.LegalPartyIndices.Length>5||
            pending.LegalPartyIndices.Any(i=>i is <0 or >=6)||
            pending.LegalPartyIndices.Zip(pending.LegalPartyIndices.Skip(1)).Any(p=>p.Second<=p.First)||
            pending.Invocation is not {} suspended)
            return new(null,"unmatched-source-baton-pass-choice");
        MoveId? called=null;
        if(context.IsCalled)
        {
            // TryRead has already authenticated this exact final link and its
            // complete caller chain. Retain the source CalledMove register too.
            int previous=context.PrefixStartIndex-1;
            if(previous<0||battle.Events[previous] is not SourceCalledMoveTransitionEvent transition)
                return new(null,"unmatched-source-baton-pass-caller");
            called=transition.Invocation.CalledMove;
        }
        if(suspended.Actor!=context.Actor||suspended.Target!=context.Target||suspended.CurrentMove!=context.CurrentMove||
            suspended.ChosenMove!=context.ChosenMove||suspended.OriginalChosenMove!=context.OriginalChosenMove||
            suspended.ChosenSlot!=context.ChosenSlot||suspended.PpOwner!=context.PpOwner||suspended.PpSlot!=context.PpSlot||
            suspended.CalledMove!=called||suspended.CancellerPhase!=WildBattleAttackCancellerPhase.ConditionsPassed||
            suspended.Flags!=(WildBattleInvocationFlags.Obeys|WildBattleInvocationFlags.AttackStringPrinted))
            return new(null,"unmatched-source-baton-pass-suspended-invocation");
        // The action is suspended before move-end immunity/items. Only the real
        // outgoing party write may follow. Never discard or rewrite its PP data.
        if(action.Length>3||action.Length==3&&(action[2] is not SourceCalledPartyChangedEvent sync||
            !BatonPassPartySync(battle,used,action,sync,pending,context)))
            return new(null,"unmatched-source-baton-pass-party-sync");
        int following=battle.EventIndex+1+action.Length;
        if(following<battle.Events.Length&&battle.Events[following].Turn!=used.Turn&&battle.Events[following] is not
            (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent))
            return new(null,"unmatched-source-baton-pass-turn-boundary");
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,required.Sequence,
            definition.SourceMoveId,used.Actor,used.Actor,false),"committed-source-animation");
    }

    private static bool BatonPassPartySync(Invocation battle,SourceMoveUsedEvent used,SourceBattleEvent[] action,
        SourceCalledPartyChangedEvent sync,SourcePendingSwitch pending,CampaignAnimationInvocation context)
    {
        var before=sync.Before;var after=sync.After;
        if(sync.Reason!="active-sync"||sync.SourceSlot!=used.Actor||sync.Side!=used.Actor.Side||
            sync.PartyIndex is <0 or >=6||pending.LegalPartyIndices.Contains(sync.PartyIndex)||
            sync.TransientMoveSlots is not {} transient||transient>15||before?.Pokemon is null||after?.Pokemon is null||
            before.Pokemon.Moves.IsDefault||after.Pokemon.Moves.IsDefault||before.Pokemon.Moves.Length!=after.Pokemon.Moves.Length||
            before.Pokemon.Moves.Any(m=>m is null)||after.Pokemon.Moves.Any(m=>m is null)||
            before with{Pokemon=after.Pokemon,Status=after.Status}!=after||
            before.Pokemon with{Moves=[]}!=after.Pokemon with{Moves=[]})return false;
        bool changed=before.Status!=after.Status;
        for(int slot=0;slot<before.Pokemon.Moves.Length;slot++)
        {
            var a=before.Pokemon.Moves[slot];var b=after.Pokemon.Moves[slot];
            if((transient&(1<<slot))!=0){if(a!=b)return false;continue;}
            if(a with{CurrentPp=b.CurrentPp}!=b||
                !InvocationPpDelta(action,used,context,slot,a.Move,a.CurrentPp,b.CurrentPp,allowUnchanged:true))return false;
            changed|=a!=b;
        }
        if(before.Status!=after.Status)
        {
            // Successful wake/thaw, or a sleeping caller, can change persistent
            // status before the first attack string. Keep that real prefix.
            int start=context.ChainStartIndex;
            while(start>0&&battle.Events[start-1].Turn==used.Turn&&battle.Events[start-1] is not
                (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent))start--;
            var statuses=battle.Events[start..context.ChainStartIndex].OfType<SourceStatusChangedEvent>()
                .Where(e=>e.Side==used.Actor).ToArray();
            if(statuses.Length==0||SourcePartyStatusRules.FromBattle(statuses[0].Before)!=before.Status||
                SourcePartyStatusRules.FromBattle(statuses[^1].After)!=after.Status||
                statuses.Zip(statuses.Skip(1)).Any(p=>p.First.After!=p.Second.Before))return false;
        }
        return changed;
    }
}
