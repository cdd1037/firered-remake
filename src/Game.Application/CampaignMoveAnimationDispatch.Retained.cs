using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static bool InvocationBoundary(SourceBattleEvent e)=>e is SourceAttackStringEvent or SourceMoveUsedEvent or
        SourceOrderEvent or SourceTurnReadyEvent or SourceStartedEvent or SourcePlayerSwitchedEvent or
        SourcePlayerReplacedEvent or SourceCalledPartySwitchedEvent;
    private static bool FailedHit(IEnumerable<SourceBattleEvent> rows,BattlerSlot actor,BattlerSlot target,MoveId move)=>rows.Any(e=>
        e is SourceMoveFailedEvent f&&f.Actor==actor&&f.Target==target&&f.Move==move||
        e is SourceMoveMissedEvent m&&m.Actor==actor&&m.Target==target&&m.Move==move||
        e is SourceAbilityPreventedEvent a&&a.Actor==actor&&a.Target==target&&a.Move==move||
        e is SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} t&&t.Actor==actor&&t.Target==target&&t.Move==move);
    private static (BattlerSlot Actor,BattlerSlot Target,MoveId Move,bool Substitute)? CommittedHit(SourceBattleEvent e,int slots)=>e switch
    {
        SourceHpChangedEvent h when h.Actor.Value>=0&&h.Actor.Value<slots&&h.Target.Value>=0&&h.Target.Value<slots&&h.Actor!=h.Target&&
            h.HpBefore>0&&h.HpAfter>=0&&h.HpAfter<=h.HpBefore&&h.Damage>=0&&h.RandomPercent is >=85 and <=100
            =>(h.Actor,h.Target,h.Move,false),
        SourceSubstituteDamagedEvent h when h.Actor.Value>=0&&h.Actor.Value<slots&&h.Target.Value>=0&&h.Target.Value<slots&&h.Actor!=h.Target&&
            h.HpBefore>0&&h.HpAfter>=0&&h.HpAfter<=h.HpBefore&&h.Damage>=0&&h.RandomPercent is >=85 and <=100
            =>(h.Actor,h.Target,h.Move,true),
        _=>null
    };
    private static CampaignMoveAnimationDecision RetainedAttack(Invocation battle,SourceAttackStringEvent attack,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        var definition=Definition(attack.CurrentMove,moves);
        if(definition is null||!supports(definition.SourceMoveId,definition.Id.Value))return new(null,"script-outside-slice");
        bool rolling=definition.Effect=="EFFECT_ROLLOUT";
        if((!rolling&&definition.Effect!="EFFECT_RAMPAGE")||definition.Power<=0||definition.Target!=(rolling?"MOVE_TARGET_SELECTED":"MOVE_TARGET_RANDOM")||
            attack.ChosenMove!=attack.CurrentMove||attack.Actor.Value<0||attack.Actor.Value>=battle.Slots)
            return new(null,"unmatched-source-retained-invocation");
        int end=battle.EventIndex+1;
        while(end<battle.Events.Length&&battle.Events[end].Turn==attack.Turn&&!InvocationBoundary(battle.Events[end]))end++;
        var action=battle.Events[(battle.EventIndex+1)..end];
        if(action.Any(e=>e is SourcePpCommandEvent or SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent))
            return new(null,"unmatched-source-retained-invocation");
        for(int i=battle.EventIndex+1;i<end;i++)if(battle.Events[i].Sequence<=battle.Events[i-1].Sequence)
            return new(null,"unmatched-source-retained-sequence");
        var hits=action.Where(e=>e is SourceHpChangedEvent h&&h.Actor==attack.Actor&&h.Move==definition.Id||
            e is SourceSubstituteDamagedEvent d&&d.Actor==attack.Actor&&d.Move==definition.Id).ToArray();
        if(hits.Length!=1||CommittedHit(hits[0],battle.Slots) is not {} hit||(!rolling&&hit.Actor.Side==hit.Target.Side)||FailedHit(action,hit.Actor,hit.Target,hit.Move))
            return new(null,"source-no-animation-result");
        var dispatch=new CampaignMoveAnimationDispatch(battle.Id,attack.Sequence,attack.Sequence,null,hits[0].Sequence,definition.SourceMoveId,hit.Actor,hit.Target,hit.Substitute)
            {Boundary=CampaignMoveAnimationBoundary.AfterRetainedAttackText};
        return rolling?WithRolloutInputs(battle,definition,hits[0],dispatch,true):new(dispatch,"committed-source-retained-animation");
    }
    private static CampaignMoveAnimationDecision BideSetup(Invocation battle,SourceMoveUsedEvent used,SourceAttackStringEvent attack,
        PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent pp)
    {
        if(used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target!=used.Actor||action.Length<2||action[0]!=pp||
            action[1] is not SourceMtMoonVolatileEvent {Volatile:"called-bide",Before:0,After:2,Reason:"move"} started||
            started.Side!=used.Actor||action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="called-bide"&&e.Reason=="move")!=1)
            return new(null,"unmatched-source-bide-setup");
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,started.Sequence,definition.SourceMoveId,used.Actor,used.Target,false)
            {MoveTurn=0},"committed-source-animation");
    }
    private static CampaignMoveAnimationDecision? BideRelease(string id,SourceBattleEvent[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        if(index<0||index>=rows.Length||CommittedHit(rows[index],slots) is not {} hit||Definition(hit.Move,moves) is not {} definition||
            definition.Effect!="EFFECT_BIDE")return null;
        CampaignMoveAnimationDecision Deny(string reason)=>new(null,reason);
        if(!supports(definition.SourceMoveId,definition.Id.Value))return Deny("script-outside-slice");
        int start=index-1;
        while(start>=0&&rows[start].Turn==rows[index].Turn&&!InvocationBoundary(rows[start])&&
            rows[start] is not SourceMtMoonVolatileEvent {Volatile:"called-bide",Reason:"released"})start--;
        if(start<0||rows[start] is not SourceMtMoonVolatileEvent {Volatile:"called-bide",Before:1,After:0,Reason:"released"} released||
            released.Side!=hit.Actor||released.Turn!=rows[index].Turn)return Deny("unmatched-source-bide-release");
        if(rows[index] is SourceHpChangedEvent {Critical:true} or SourceSubstituteDamagedEvent {Critical:true}||
            rows[index] is SourceHpChangedEvent {RandomPercent:not 100} or SourceSubstituteDamagedEvent {RandomPercent:not 100})
            return Deny("unmatched-source-bide-hit");
        for(int prior=start-1;prior>=0&&rows[prior].Turn==released.Turn&&!InvocationBoundary(rows[prior]);prior--)
            if(rows[prior] is SourceMtMoonVolatileEvent {Volatile:"called-bide",Reason:"released"} other&&other.Side==hit.Actor)
                return Deny("unmatched-source-bide-release");
        int end=index+1;
        while(end<rows.Length&&rows[end].Turn==released.Turn&&!InvocationBoundary(rows[end]))end++;
        var action=rows[start..end];
        for(int i=start+1;i<end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return Deny("unmatched-source-bide-sequence");
        if(action.Any(e=>e is SourcePpCommandEvent or SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent)||
            action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="called-bide"&&e.Reason=="released"&&e.Side==hit.Actor)!=1||
            action.Count(e=>e is SourceHpChangedEvent h&&h.Move==hit.Move||e is SourceSubstituteDamagedEvent d&&d.Move==hit.Move)!=1||
            FailedHit(action,hit.Actor,hit.Target,hit.Move))return Deny("source-no-animation-result");
        return new(new(id,rows[index].Sequence,null,null,rows[index].Sequence,definition.SourceMoveId,hit.Actor,hit.Target,hit.Substitute)
            {MoveTurn=1,Boundary=CampaignMoveAnimationBoundary.BeforeHitResult},"committed-source-bide-release-animation");
    }
}
