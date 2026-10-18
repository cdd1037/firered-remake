using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private sealed record MultiHitRow(long Sequence,int Turn,string Kind,int Actor=-1,int Target=-1,
        string Move="",string ChosenMove="",int Slot=-1,int Before=0,int After=0,int Cost=0,int PpOwner=-1,
        int Planned=0,int Actual=0,int Damage=0);
    private static bool MultiHitBoundary(MultiHitRow row)=>row.Kind is "attack" or "used" or "boundary";
    private static bool IsMultiHitReceipt(MultiHitRow row)=>row.Kind is "hp" or "substitute";

    // Result-owned attackanimation runs before its current receipt is exposed.
    // Per-hit ordinals and recovery ownership come only from committed receipts.
    public static CampaignMoveAnimationDecision AtResult(CampaignDoubleBattle battle,SourceBattleScene scene, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        if(scene!=SourceBattleScene.On)return new(null,"animations-disabled");
        if(battle.Stage!=CampaignDoubleBattleStage.Combat)return new(null,"not-hit-result");
        var rows=battle.Events.ToArray();
        if(BeatUpResult(battle.Id,rows,battle.EventIndex,4,moves,supports) is {} member)return member;
        if(ChargeSetup(battle.Id,rows,battle.EventIndex,4,moves,supports) is {} charge)return charge;
        if(BideRelease(battle.Id,rows,battle.EventIndex,4,moves,supports) is {} bide)return bide;
        if(battle.EventIndex>=0&&battle.EventIndex<rows.Length&&rows[battle.EventIndex] is SourceRecoveredEvent)
            return RecoveryResult(battle.Id,rows,battle.EventIndex,4,moves,supports);
        if(battle.EventIndex>=0&&battle.EventIndex<rows.Length&&rows[battle.EventIndex] is SourceConfusionChangedEvent)
            return StatusResult(battle.Id,rows,battle.EventIndex,4,moves,supports);
        return MultiHitResult(battle.Id,rows.Select(MultiHitRead).ToArray(),battle.EventIndex,4,moves,supports);
    }
    public static CampaignMoveAnimationDecision AtResult(CampaignWildBattle battle,bool sourceRules,SourceBattleScene scene, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        if(!sourceRules)return new(null,"historical-rules");
        if(scene!=SourceBattleScene.On)return new(null,"animations-disabled");
        if(battle.Phase!=CampaignBattlePhase.Events)return new(null,"not-hit-result");
        if(BeatUpResult(battle.Id,battle.Events.Select(AnimationReceipt).ToArray(),battle.EventIndex,2,moves,supports) is {} member)return member;
        if(ChargeSetup(battle.Id,battle.Events.Select(AnimationReceipt).ToArray(),battle.EventIndex,2,moves,supports) is {} charge)return charge;
        if(BideRelease(battle.Id,battle.Events.Select(AnimationReceipt).ToArray(),battle.EventIndex,2,moves,supports) is {} bide)return bide;
        if(battle.EventIndex>=0&&battle.EventIndex<battle.Events.Length&&battle.Events[battle.EventIndex] is WildBattleRecoveredEvent)
            return RecoveryResult(battle.Id,battle.Events.Select(AnimationReceipt).ToArray(),battle.EventIndex,2,moves,supports);
        if(battle.EventIndex>=0&&battle.EventIndex<battle.Events.Length&&battle.Events[battle.EventIndex] is WildBattleConfusionChangedEvent)
            return StatusResult(battle.Id,battle.Events.Select(AnimationReceipt).ToArray(),battle.EventIndex,2,moves,supports);
        return MultiHitResult(battle.Id,battle.Events.Select(MultiHitRead).ToArray(),battle.EventIndex,2,moves,supports);
    }
    private static CampaignMoveAnimationDecision RecoveryResult(string id,SourceBattleEvent[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        if(rows[index] is not SourceRecoveredEvent recovery||Definition(recovery.Move,moves)?.Effect!="EFFECT_REST")
            return new(null,"not-recovery-animation-result");
        int start=index-1;
        while(start>=0&&rows[start].Turn==recovery.Turn&&
            rows[start] is not (SourceMoveUsedEvent or SourceAttackStringEvent or SourceOrderEvent or SourceTurnReadyEvent))start--;
        if(start<0||rows[start] is not SourceMoveUsedEvent)return new(null,"unmatched-source-recovery-invocation");
        var decision=AtAttackText(new(id,rows,start,slots,()=>null,_=>null),moves,supports,
            CampaignMoveAnimationBoundary.BeforeRecoveryResult);
        return decision.Dispatch is {} cue&&cue.ResultSequence==recovery.Sequence ? decision :
            new(null,"unmatched-source-recovery-result");
    }
    private static CampaignMoveAnimationDecision MultiHitResult(string battle,MultiHitRow[] rows,int index,int slots,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(index<0||index>=rows.Length||!IsMultiHitReceipt(rows[index]))return Reject("not-hit-result");
        var current=rows[index];
        var definition=Definition(new(current.Move),moves);
        if(definition is null || !supports(definition.SourceMoveId,definition.Id.Value) || MultiHitBounds(definition) is not {} bounds)
            return Reject("script-outside-multihit-slice");
        int move=definition.SourceMoveId, minPlanned=bounds.Minimum, maxPlanned=bounds.Maximum;
        int start=index-1;
        while(start>=0&&rows[start].Turn==current.Turn&&!MultiHitBoundary(rows[start]))start--;
        if(start<1||rows[start].Kind!="used")return Reject("unmatched-source-multihit-invocation");
        var used=rows[start];var attack=rows[start-1];
        if(attack.Kind!="attack"||attack.Turn!=current.Turn||used.Turn!=current.Turn||
            used.Actor<0||used.Actor>=slots||used.Target<0||used.Target>=slots||used.Actor==used.Target||
            used.Actor!=current.Actor||used.Target!=current.Target||used.Move!=current.Move||
            attack.Actor!=used.Actor||attack.Move!=used.Move||attack.ChosenMove!=used.Move||used.Slot is <0 or >3)
            return Reject("unmatched-source-multihit-invocation");
        int end=start+1;
        while(end<rows.Length&&rows[end].Turn==used.Turn&&!MultiHitBoundary(rows[end]))end++;
        if(index>=end)return Reject("unmatched-source-multihit-invocation");
        for(int i=start;i<end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return Reject("unmatched-source-multihit-sequence");
        var action=rows[(start+1)..end];var commands=action.Where(r=>r.Kind=="pp").ToArray();
        if(commands.Length!=1)return Reject("unmatched-source-pp-command");
        var pp=commands[0];
        if(pp.Actor!=used.Actor||pp.PpOwner!=used.Actor||pp.Target!=used.Target||pp.Move!=used.Move||
            pp.ChosenMove!=used.Move||pp.Slot!=used.Slot||pp.Before!=used.Before||pp.After!=used.After||
            pp.Before<1||pp.Cost<1||pp.After!=Math.Max(0,pp.Before-pp.Cost))return Reject("unmatched-source-pp-command");
        if(action.Any(r=>r.Kind=="blocked"&&r.Actor==used.Actor&&r.Move==used.Move))return Reject("source-no-animation-result");
        var summaries=action.Where(r=>r.Kind=="summary").ToArray();
        if(summaries.Length!=1)return Reject("unmatched-source-multihit-summary");
        var summary=summaries[0];var hits=action.Where(IsMultiHitReceipt).ToArray();
        if(summary.Actor!=used.Actor||summary.Target!=used.Target||summary.Move!=used.Move||
            summary.Planned<minPlanned||summary.Planned>maxPlanned||summary.Actual<1||summary.Actual>summary.Planned||hits.Length!=summary.Actual||
            hits.Any(h=>h.Actor!=used.Actor||h.Target!=used.Target||h.Move!=used.Move||h.Sequence<=pp.Sequence||h.Sequence>=summary.Sequence||
                h.Before<1||h.After<0||h.After>h.Before||h.Damage<0))return Reject("unmatched-source-multihit-summary");
        int ordinal=Array.FindIndex(hits,h=>h.Sequence==current.Sequence);
        if(ordinal<0)return Reject("unmatched-source-multihit-result");
        return new(new(battle,current.Sequence,attack.Sequence,pp.Sequence,current.Sequence,move,new(used.Actor),new(used.Target),
            current.Kind=="substitute"){MoveTurn=checked((byte)ordinal),Boundary=CampaignMoveAnimationBoundary.BeforeHitResult},"committed-source-multihit-animation");
    }
    private static MultiHitRow MultiHitRead(SourceBattleEvent e)=>e switch
    {
        SourceAttackStringEvent x=>new(x.Sequence,x.Turn,"attack",x.Actor.Value,Move:x.CurrentMove.Value,ChosenMove:x.ChosenMove.Value),
        SourceMoveUsedEvent x=>new(x.Sequence,x.Turn,"used",x.Actor.Value,x.Target.Value,x.Move.Value,Slot:x.Slot,Before:x.PpBefore,After:x.PpAfter),
        SourcePpCommandEvent x=>new(x.Sequence,x.Turn,"pp",x.Actor.Value,x.Target.Value,x.CurrentMove.Value,x.ChosenMove.Value,x.PpSlot,x.Before,x.After,x.CommandCost,x.PpOwner.Value),
        SourceHpChangedEvent x=>new(x.Sequence,x.Turn,"hp",x.Actor.Value,x.Target.Value,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage),
        SourceSubstituteDamagedEvent x=>new(x.Sequence,x.Turn,"substitute",x.Actor.Value,x.Target.Value,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage),
        SourceMultiHitEvent x=>new(x.Sequence,x.Turn,"summary",x.Actor.Value,x.Target.Value,x.Move.Value,Planned:x.PlannedHits,Actual:x.ActualHits),
        SourceMoveFailedEvent x=>new(x.Sequence,x.Turn,"blocked",x.Actor.Value,x.Target.Value,x.Move.Value),
        SourceMoveMissedEvent x=>new(x.Sequence,x.Turn,"blocked",x.Actor.Value,x.Target.Value,x.Move.Value),
        SourceAbilityPreventedEvent x=>new(x.Sequence,x.Turn,"blocked",x.Actor.Value,x.Target.Value,x.Move.Value),
        SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} x=>new(x.Sequence,x.Turn,"blocked",x.Actor.Value,x.Target.Value,x.Move.Value),
        SourceOrderEvent or SourceTurnReadyEvent or SourceStartedEvent or SourceCalledPartySwitchedEvent or SourcePlayerSwitchedEvent or SourcePlayerReplacedEvent=>new(e.Sequence,e.Turn,"boundary"),
        _=>new(e.Sequence,e.Turn,"other")
    };
    private static MultiHitRow MultiHitRead(WildBattleEvent e)=>e switch
    {
        WildBattleAttackStringEvent x=>new(x.Sequence,x.Turn,"attack",(int)x.Actor,Move:x.CurrentMove.Value,ChosenMove:x.ChosenMove.Value),
        WildBattleMoveUsedEvent x=>new(x.Sequence,x.Turn,"used",(int)x.Actor,(int)x.Target,x.Move.Value,Slot:x.Slot,Before:x.PpBefore,After:x.PpAfter),
        WildBattlePpCommandEvent x=>new(x.Sequence,x.Turn,"pp",(int)x.Actor,(int)x.Target,x.CurrentMove.Value,x.ChosenMove.Value,x.PpSlot,x.Before,x.After,x.CommandCost,(int)x.PpOwner),
        WildBattleHpChangedEvent x=>new(x.Sequence,x.Turn,"hp",(int)x.Actor,(int)x.Target,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage),
        WildBattleSubstituteDamagedEvent x=>new(x.Sequence,x.Turn,"substitute",(int)x.Actor,(int)x.Target,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage),
        WildBattleMultiHitEvent x=>new(x.Sequence,x.Turn,"summary",(int)x.Actor,(int)x.Target,x.Move.Value,Planned:x.PlannedHits,Actual:x.ActualHits),
        WildBattleMoveFailedEvent x=>new(x.Sequence,x.Turn,"blocked",(int)x.Actor,(int)x.Target,x.Move.Value),
        WildBattleMoveMissedEvent x=>new(x.Sequence,x.Turn,"blocked",(int)x.Actor,(int)x.Target,x.Move.Value),
        WildBattleAbilityPreventedEvent x=>new(x.Sequence,x.Turn,"blocked",(int)x.Actor,(int)x.Target,x.Move.Value),
        WildBattleEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} x=>new(x.Sequence,x.Turn,"blocked",(int)x.Actor,(int)x.Target,x.Move.Value),
        WildBattleOrderEvent or WildBattleTurnReadyEvent or WildBattleStartedEvent or WildBattleCalledPartySwitchedEvent or WildBattlePlayerSwitchedEvent or WildBattlePlayerReplacedEvent=>new(e.Sequence,e.Turn,"boundary"),
        _=>new(e.Sequence,e.Turn,"other")
    };
}
