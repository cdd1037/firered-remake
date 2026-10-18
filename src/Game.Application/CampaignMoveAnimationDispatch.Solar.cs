using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // The sun branch sets charging and spends PP before attackcanceler, then
    // enters the existing second-turn script. It never invokes the first-turn
    // animation. Proof comes from that command prefix, never final weather.
    private static bool SolarChargePrefix(Invocation battle,SourceMoveUsedEvent used,PokemonMoveDefinition definition,
        SourceBattleEvent[] action,ref int prefix)
    {
        if(definition.Power<=0||definition.Target!="MOVE_TARGET_SELECTED"||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||
            action.Length==0||action[0] is not SourcePpCommandEvent releasePp||
            !ValidSingleInvocationPp([releasePp],used,2,retained:true)||releasePp.Turn!=used.Turn||releasePp.Sequence!=used.Sequence+1||
            FailedHit(action,used.Actor,used.Target,used.Move)||
            action.Any(e=>e is SourceActionBlockedEvent or SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent))return false;
        int released=prefix,prior=released-1;
        // Successful canceller work can intervene after the sunny ppreduce.
        // At most one primary-status clear and one confusion countdown occur,
        // in that order. A failed canceller cannot reach a released hit.
        if(prior>=0&&battle.Events[prior] is SourceConfusionChangedEvent confusion&&confusion.Side==used.Actor&&
            confusion.Before is >=1 and <=5&&confusion.After==confusion.Before-1&&
            confusion.Reason==(confusion.After==0?"snapped-out":"countdown"))prior--;
        if(prior>=0&&battle.Events[prior] is SourceStatusChangedEvent status&&status.Side==used.Actor&&status.After==0&&
            (WildBattleStatus.IsSleeping(status.Before)&&status.Reason is "woke-up" or "uproar-wake"||
                status.Before==WildBattleStatus.Freeze&&status.Reason=="thawed-out"))
        {
            prior--;
            if(prior>=0&&battle.Events[prior] is SourceTowerVolatileEvent {Effect:"nightmare",Applied:false} nightmare&&
                nightmare.Side==used.Actor&&nightmare.Reason==status.Reason&&WildBattleStatus.IsSleeping(status.Before))prior--;
        }
        // No early PP is the ordinary retained second turn. Let the existing
        // charge scanner reject extra charge/PP/caller rows in that prefix.
        if(prior<0||battle.Events[prior] is not SourcePpCommandEvent early)return true;
        if(early.Actor!=used.Actor||early.PpOwner!=used.Actor||early.Target!=used.Target||early.PpSlot!=used.Slot||
            early.ChosenMove!=used.Move||early.CurrentMove!=used.Move||early.Turn!=used.Turn||
            early.Before is <0 or >255||early.CommandCost!=releasePp.CommandCost||
            early.After!=Math.Max(0,early.Before-early.CommandCost)||early.After!=releasePp.Before||
            early.RngCursor>releasePp.RngCursor)return false;
        int start=prior;
        if(prior>0&&battle.Events[prior-1] is SourceMtMoonVolatileEvent {Volatile:"called-charge"} prepared)
        {
            if(prepared.Side!=used.Actor||prepared.Turn!=used.Turn||prepared.Reason!="move"||
                prepared.Before is <0 or >1||prepared.After!=1)return false;
            start--;
        }
        // Existing charged-sun locks may be unchanged and omit that setup
        // marker, including an exhausted slot's genuine 0→0 early PP command.
        for(int i=start;i<=released;i++)
            if(battle.Events[i].Turn!=used.Turn||i>start&&battle.Events[i].Sequence!=battle.Events[i-1].Sequence+1)return false;
        if(battle.Events[released].Sequence+1!=battle.Events[released+1].Sequence||
            battle.Events[released+1].Sequence+1!=used.Sequence)return false;
        prefix=start;return true;
    }
}
