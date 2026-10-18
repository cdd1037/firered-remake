using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision StatusResult(string id,SourceBattleEvent[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        if(rows[index] is not SourceConfusionChangedEvent {Reason:"teeter-dance"} result)
            return new(null,"not-status-animation-result");
        int start=index-1;
        while(start>=0&&rows[start].Turn==result.Turn&&
            rows[start] is not (SourceMoveUsedEvent or SourceAttackStringEvent or SourceOrderEvent or SourceTurnReadyEvent))start--;
        if(start<0||rows[start] is not SourceMoveUsedEvent used||Definition(used.Move,moves)?.Effect!="EFFECT_TEETER_DANCE")
            return new(null,"unmatched-source-status-invocation");
        var decision=AtAttackText(new(id,rows,start,slots,()=>null,_=>null),moves,supports,
            CampaignMoveAnimationBoundary.BeforeStatusResult);
        return decision.Dispatch is {} cue&&cue.ResultSequence==result.Sequence?decision:
            new(null,"unmatched-source-status-result");
    }

    private static CampaignMoveAnimationDecision SupportStatusAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] pp)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Power!=0||used.Actor.Value<0||used.Actor.Value>=battle.Slots||
            used.Target.Value<0||used.Target.Value>=battle.Slots||action.Length<2||pp.Length==0||action[0]!=pp[0])
            return Reject("unmatched-source-support-invocation");
        if(definition.Effect=="EFFECT_TEETER_DANCE")
        {
            if(definition.Target!="MOVE_TARGET_FOES_AND_ALLY"||!ValidSingleInvocationPp(pp,used,battle.Slots))
                return Reject("unmatched-source-teeter-pp");
            // The literal numeric target loop may print failed-target messages before
            // reaching attackanimation. Its first successful target owns the only root.
            var results=action.OfType<SourceConfusionChangedEvent>().Where(e=>e.Reason=="teeter-dance").ToArray();
            if(results.Length==0||results.Length>=battle.Slots||
                results.Any(e=>e.Side==used.Actor||e.Side.Value<0||e.Side.Value>=battle.Slots||e.Before!=0||e.After is <2 or >5||
                    e.Sequence<=pp[0].Sequence||PreventedTarget(action,used,e.Side))||
                results.Zip(results.Skip(1)).Any(pair=>pair.First.Side.Value>=pair.Second.Side.Value))
                return Reject("unmatched-source-teeter-result");
            var first=results[0];
            return new(new(battle.Id,first.Sequence,attack.Sequence,pp[0].Sequence,first.Sequence,
                definition.SourceMoveId,used.Actor,first.Side,false){Boundary=CampaignMoveAnimationBoundary.BeforeStatusResult},
                "committed-source-status-animation");
        }
        if(definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor)
            return Reject("unmatched-source-support-target");
        string state=definition.Effect=="EFFECT_HELPING_HAND"?"helping-hand":"imprison";
        var target=definition.Effect=="EFFECT_HELPING_HAND"?used.Actor.Partner:used.Actor;
        var markers=action.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile==state).ToArray();
        if(markers.Length!=1||markers[0].Side!=target||markers[0].Before!=0||markers[0].After!=1||markers[0].Reason!="move"||
            PreventedTarget(action,used,used.Target)||PreventedTarget(action,used,target))
            return Reject("unmatched-source-support-result");
        if(definition.Effect=="EFFECT_HELPING_HAND")
        {
            if(battle.Slots!=4||!ValidSingleInvocationPp(pp,used,1)||action[1]!=markers[0])
                return Reject("unmatched-source-helping-hand-result");
        }
        else
        {
            // Imprison has its ordinary self PP command, then a separate Pressure
            // scan against both foes using the first learned Imprison slot.
            if(pp.Length is <1 or >2||!ValidSingleInvocationPp([pp[0]],used,1))
                return Reject("unmatched-source-imprison-pp");
            if(pp.Length==2)
            {
                var extra=pp[1];
                if(action.Length<3||action[1]!=extra||extra.Actor!=used.Actor||extra.PpOwner!=used.Actor||extra.Target!=used.Actor||
                    extra.CurrentMove!=used.Move||extra.ChosenMove!=used.Move||extra.RngCursor!=pp[0].RngCursor||extra.PpSlot<0||extra.PpSlot>used.Slot||
                    extra.CommandCost<1||extra.CommandCost>battle.Slots/2||extra.Before is <0 or >255||
                    extra.After!=Math.Max(0,extra.Before-extra.CommandCost)||
                    extra.PpSlot==used.Slot&&extra.Before!=pp[0].After)
                    return Reject("unmatched-source-imprison-pressure");
            }
            if(action[pp.Length]!=markers[0])return Reject("unmatched-source-imprison-result");
        }
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,markers[0].Sequence,
            definition.SourceMoveId,used.Actor,target,false),"committed-source-animation");
    }
}
