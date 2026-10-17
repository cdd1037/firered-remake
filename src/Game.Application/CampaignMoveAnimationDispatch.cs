using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Read-only projection of a committed source invocation, consumed after its attack text.
/// It adds no Domain event, command, RNG draw, saved field or authoritative acknowledgement.</summary>
public sealed record CampaignMoveAnimationDispatch(string BattleId, long Sequence, long AttackSequence,
    long PpSequence, long ResultSequence, int SourceMoveId, BattlerSlot Actor, BattlerSlot Target,
    bool HitsSubstitute) { public byte? MoveTurn {get;init;} public bool AcknowledgesCurrentReceipt=>MoveTurn is null; }
public sealed record CampaignMoveAnimationDecision(CampaignMoveAnimationDispatch? Dispatch, string Reason);

public static partial class CampaignMoveAnimationPresentation
{
    // Launch snapshots are taken at the authenticated PP command, before later
    // outcome/charge receipts. Reuse the same charge replay as general effects.
    public static bool[]? SpriteVisibilityAtInvocation(IReadOnlyList<SourceBattleEvent> events,
        CampaignMoveAnimationDispatch dispatch,IReadOnlyList<MoveId?> entryChargeMoves)
    {
        int index=-1;
        for(int i=0;i<events.Count;i++)if(events[i].Sequence==dispatch.PpSequence)
        {if(index>=0)return null;index=i;}
        if(index<0||events[index] is not SourcePpCommandEvent pp||pp.Actor!=dispatch.Actor||
            entryChargeMoves.Count is not (2 or 4))return null;
        var visibility=new bool[entryChargeMoves.Count];
        for(int slot=0;slot<visibility.Length;slot++)
        {
            var visible=CampaignHeldItemAnimationPresentation.SpriteVisibleAtResult(events,index,new(slot),entryChargeMoves);
            if(visible is null)return null;
            visibility[slot]=visible.Value;
        }
        return visibility;
    }

    // Primary stat scripts share source effect semantics; numeric identity comes
    // only from the supplied content definition, not another move-name table.
    internal static (WildBattleStat Stat,int Delta)? ReductionAnimation(PokemonMoveDefinition move) => move.Effect switch
    {
        "EFFECT_ACCURACY_DOWN" => (WildBattleStat.Accuracy,-1),
        "EFFECT_DEFENSE_DOWN" => (WildBattleStat.Defense,-1),
        "EFFECT_DEFENSE_DOWN_2" => (WildBattleStat.Defense,-2),
        "EFFECT_SPEED_DOWN" => (WildBattleStat.Speed,-1),
        "EFFECT_SPEED_DOWN_2" => (WildBattleStat.Speed,-2),
        "EFFECT_ATTACK_DOWN" => (WildBattleStat.Attack,-1),
        "EFFECT_ATTACK_DOWN_2" => (WildBattleStat.Attack,-2),
        "EFFECT_SPECIAL_DEFENSE_DOWN_2" => (WildBattleStat.SpecialDefense,-2),
        _ => null
    };
    internal static (WildBattleStat Stat,int Delta)? SelfStatAnimation(PokemonMoveDefinition move) => move.Effect switch
    {
        "EFFECT_ATTACK_UP" => (WildBattleStat.Attack,1),
        "EFFECT_ATTACK_UP_2" => (WildBattleStat.Attack,2),
        "EFFECT_SPEED_UP_2" => (WildBattleStat.Speed,2),
        "EFFECT_SPECIAL_ATTACK_UP" => (WildBattleStat.SpecialAttack,1),
        "EFFECT_MINIMIZE" or "EFFECT_EVASION_UP" => (WildBattleStat.Evasion,1),
        "EFFECT_DEFENSE_CURL" or "EFFECT_DEFENSE_UP" => (WildBattleStat.Defense,1),
        "EFFECT_DEFENSE_UP_2" => (WildBattleStat.Defense,2),
        "EFFECT_SPECIAL_DEFENSE_UP_2" => (WildBattleStat.SpecialDefense,2),
        _ => null
    };
    private static string? FieldSettingAnimation(int move)=>move switch{54=>"mist",113=>"light-screen",115=>"reflect",258=>"hail",_=>null};
    private static bool IsRecoveryAnimation(int move) => move is 105 or 135 or 208 or 235 or 303;

    private sealed record Invocation(string Id, SourceBattleEvent[] Events, int EventIndex, int Slots,
        Func<long?> GroupedResult, Func<int,CampaignStatAnimationDispatch?> StatResult);

    internal static PokemonMoveDefinition? Definition(MoveId move, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves) =>
        moves is not null && moves.TryGetValue(move,out var definition) && definition.Id == move &&
        SourceMoveDefinitionRules.IsValid(definition) ? definition : null;

    private static (int Minimum,int Maximum)? MultiHitBounds(PokemonMoveDefinition definition) => definition.Effect switch
    {
        "EFFECT_MULTI_HIT" => (2,5),
        "EFFECT_DOUBLE_HIT" or "EFFECT_TWINEEDLE" => (2,2),
        "EFFECT_TRIPLE_KICK" => (3,3),
        _ => null
    };

    private static bool ValidSpreadPp(SourcePpCommandEvent[] pp,SourceMoveUsedEvent used,SourceAttackStringEvent attack) =>
        used.Actor.Value is >=0 and <4&&used.Target.Value is >=0 and <4&&pp.Length is >=1 and <=2&&
        pp[0].Target==used.Target&&pp.Select(p=>p.Target).Distinct().Count()==pp.Length&&
        pp.All(p=>p.Actor==used.Actor&&p.PpOwner==used.Actor&&p.CommandCost is >=1 and <=3&&
            p.Before is >=0 and <=255&&p.After is >=0 and <=255&&p.CurrentMove==used.Move&&
            p.ChosenMove==attack.ChosenMove&&p.PpSlot==used.Slot&&p.Sequence>used.Sequence&&
            p.Target.Side!=used.Actor.Side&&(p.Target==used.Target||p.Target==used.Target.Partner))&&
        pp[0].Before==used.PpBefore&&pp[0].After==used.PpAfter&&pp[0].Before>0&&
        pp[0].After==Math.Max(0,pp[0].Before-pp[0].CommandCost)&&
        pp.Skip(1).All(p=>p.Before==pp[0].After&&p.After==p.Before);

    public static CampaignMoveAnimationDecision AtAttackText(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        if(!sourceRules)return new(null,"historical-rules");
        if(scene!=SourceBattleScene.On)return new(null,"animations-disabled");
        if(battle.Phase!=CampaignBattlePhase.Events || battle.EventIndex<0 || battle.EventIndex>=battle.Events.Length)
            return new(null,"not-attack-text");
        return AtAttackText(new(battle.Id,battle.Events.Select(AnimationReceipt).ToArray(),battle.EventIndex,2,
            ()=>CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle,true),
            index=>CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=index},true,moves:moves)),moves,supports);
    }

    public static CampaignMoveAnimationDecision AtAttackText(CampaignDoubleBattle battle,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        if(scene!=SourceBattleScene.On)return new(null,"animations-disabled");
        if(battle.Stage!=CampaignDoubleBattleStage.Combat || battle.EventIndex<0 || battle.EventIndex>=battle.Events.Length)
            return new(null,"not-attack-text");
        return AtAttackText(new(battle.Id,battle.Events.ToArray(),battle.EventIndex,4,
            ()=>CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle),
            index=>CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=index},moves:moves)),moves,supports);
    }

    private static CampaignMoveAnimationDecision AtAttackText(Invocation battle,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        // Accepted basic hit/slice moves print through SpendInvocationPp. The paired AttackString is silent;
        // MoveUsed is the actual visible page. Callers such as Metronome have a different current move.
        if (battle.Events[battle.EventIndex] is not SourceMoveUsedEvent used) return new(null, "not-attack-text");
        var definition = Definition(used.Move, moves);
        int move = definition?.SourceMoveId ?? 0;
        if (move == 0 || !supports(move,used.Move.Value)) return new(null, "script-outside-slice");
        if(MultiHitBounds(definition!) is not null)return new(null,"source-per-hit-animation");
        if (battle.EventIndex == 0 || battle.Events[battle.EventIndex - 1] is not SourceAttackStringEvent attack ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.ChosenMove != used.Move ||
            attack.Turn != used.Turn || attack.Sequence >= used.Sequence || used.Slot is <0 or >3)
            return new(null, "unmatched-source-attack-string");
        int end = battle.EventIndex + 1;
        while (end < battle.Events.Length && battle.Events[end].Turn == used.Turn &&
            battle.Events[end] is not (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent)) end++;
        for(int i=battle.EventIndex+1;i<end;i++)
            if(battle.Events[i].Sequence<=battle.Events[i-1].Sequence)return new(null,"unmatched-source-invocation-sequence");
        var action = battle.Events.Skip(battle.EventIndex + 1).Take(end - battle.EventIndex - 1).ToArray();
        var pp = action.OfType<SourcePpCommandEvent>().ToArray();
        if(battle.Slots == 4 && definition!.Target == "MOVE_TARGET_BOTH" && definition.Power > 0)
        {
            // Source MOVEEND_NEXT_TARGET revisits the hit script without another attack string.
            // One visible attack page owns both PP-command visits and one visual invocation.
            if(!ValidSpreadPp(pp,used,attack))
                return new(null,"unmatched-source-spread-pp");
            var spreadHits=action.Where(e=>e is SourceHpChangedEvent h&&h.Actor==used.Actor&&h.Move==used.Move||e is SourceSubstituteDamagedEvent d&&d.Actor==used.Actor&&d.Move==used.Move)
                .Select(e=>e switch{SourceHpChangedEvent h=>(h.Target,h.Sequence,Substitute:false),SourceSubstituteDamagedEvent d=>(d.Target,d.Sequence,Substitute:true),_=>throw new InvalidOperationException()}).ToArray();
            if(spreadHits.Length==0)return new(null,"source-no-animation-result");
            if(spreadHits.Length>2||spreadHits.Select(h=>h.Target).Distinct().Count()!=spreadHits.Length||spreadHits.Any(h=>!pp.Any(p=>p.Target==h.Target&&p.Sequence<h.Sequence)))
                return new(null,"unmatched-source-spread-hit");
            // A prevented target may still export a zero-loss HP receipt. Choose the first
            // eligible source pass by its typed outcomes, never by the damage amount: Endure
            // and Focus Band can legitimately animate with no remaining HP loss.
            var eligibleHits=spreadHits.Where(hit=>!action.Any(e=>
                e is SourceMoveMissedEvent m&&m.Actor==used.Actor&&m.Target==hit.Target&&m.Move==used.Move||
                e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Target==hit.Target&&f.Move==used.Move||
                e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Target==hit.Target&&a.Move==used.Move||
                e is SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect} effect&&effect.Actor==used.Actor&&effect.Target==hit.Target&&effect.Move==used.Move)).ToArray();
            if(eligibleHits.Length==0)return new(null,"source-no-animation-result");
            var hit=eligibleHits[0];var hitPp=pp.Single(p=>p.Target==hit.Target);
            return new(new(battle.Id,used.Sequence,attack.Sequence,hitPp.Sequence,hit.Sequence,move,used.Actor,hit.Target,hit.Substitute),"committed-source-animation");
        }
        if(battle.Slots==4&&definition!.Target=="MOVE_TARGET_BOTH"&&ReductionAnimation(definition) is {} spreadStat)
        {
            // Primary reductions revisit the same source script once per opposing target.
            // The dedicated root belongs to the first eligible result; each stat wave
            // remains owned by its individual PP pass.
            if(!ValidSpreadPp(pp,used,attack))return new(null,"unmatched-source-both-stat-pp");
            var itemRows=new HashSet<int>();
            for(int item=battle.EventIndex+1;item<end;item++)
            {
                if(battle.Events[item] is not SourceHeldItemConsumedEvent)continue;
                int start=CampaignHeldItemAnimationPresentation.ConsumedGroupStart(battle.Events,item);
                if(start>battle.EventIndex)for(int i=start;i<=item;i++)itemRows.Add(i);
            }
            var results=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1)
                .Where(i=>!itemRows.Contains(i)).Select(i=>battle.Events[i]).OfType<SourceStatChangedEvent>().ToArray();
            if(results.Length is <1 or >2||results.Select(r=>r.Target).Distinct().Count()!=results.Length||
                results.Any(r=>r.Actor!=used.Actor||r.Stat!=spreadStat.Stat||r.Before is <0 or >12||
                    r.After>=r.Before||r.After!=Math.Clamp(r.Before+spreadStat.Delta,0,12)||
                    !pp.Any(p=>p.Target==r.Target&&p.Sequence<r.Sequence&&
                        !pp.Any(next=>next.Sequence>p.Sequence&&next.Sequence<r.Sequence))))
                return new(null,"unmatched-source-both-stat-result");
            var eligible=results.Where(r=>!action.Any(e=>
                e is SourceMoveMissedEvent m&&m.Actor==used.Actor&&m.Target==r.Target&&m.Move==used.Move||
                e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Target==r.Target&&f.Move==used.Move||
                e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Target==r.Target&&a.Move==used.Move)).ToArray();
            if(eligible.Length==0)return new(null,"source-no-animation-result");
            var result=eligible[0];var ownPp=pp.Single(p=>p.Target==result.Target);
            return new(new(battle.Id,used.Sequence,attack.Sequence,ownPp.Sequence,result.Sequence,move,used.Actor,result.Target,false),"committed-source-animation");
        }
        if (pp.Length != 1 || pp[0].Actor != used.Actor || pp[0].PpOwner != used.Actor ||
            pp[0].Target != used.Target || pp[0].CurrentMove != used.Move || pp[0].ChosenMove != used.Move ||
            pp[0].PpSlot != used.Slot || pp[0].Before != used.PpBefore || pp[0].After != used.PpAfter ||
            pp[0].Before is <1 or >255 || pp[0].After is <0 or >255 || pp[0].CommandCost is <1 or >2 || pp[0].After != Math.Max(0,pp[0].Before-pp[0].CommandCost))
            return new(null, "unmatched-source-pp-command");
        if(move==273)
        {
            // Successful trywish owns the setup root. Countdown and later healing
            // use a separate general animation and never replay this invocation.
            if((used.Actor.Value < 0 || used.Actor.Value >= battle.Slots)||used.Target!=used.Actor||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceCalledDelayedEvent {Before:0,After:2,Reason:"scheduled",SourcePartyIndex:>=0 and <6} scheduled||
                scheduled.Target!=used.Actor||scheduled.Move!=used.Move||scheduled.Sequence<=pp[0].Sequence||
                action.OfType<SourceCalledDelayedEvent>().Count(e=>e.Reason=="scheduled")!=1)
                return new(null,"unmatched-source-wish-setup");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,scheduled.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move==248)
        {
            // trysetfutureattack precedes attackanimation. The delayed impact is
            // a separate general animation and cannot replay this setup root.
            if((used.Actor.Value < 0 || used.Actor.Value >= battle.Slots)||(used.Target.Value < 0 || used.Target.Value >= battle.Slots)||used.Actor==used.Target||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceCalledDelayedEvent {Before:0,After:3,Reason:"scheduled",SourcePartyIndex:null} scheduled||
                scheduled.Target!=used.Target||scheduled.Move!=used.Move||scheduled.Sequence<=pp[0].Sequence||
                action.OfType<SourceCalledDelayedEvent>().Count(e=>e.Reason=="scheduled")!=1)
                return new(null,"unmatched-source-future-setup");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,scheduled.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if (move is 120 or 153)
        {
            // BattleScript_EffectExplosion animates once immediately after setatkhptozero,
            // before every target pass, even when every pass later misses/is immune.
            var committed=action.OfType<SourceSelfDestructEvent>().ToArray();
            if(pp[0].ChosenMove!=used.Move||pp[0].PpSlot!=used.Slot||pp[0].Sequence<=used.Sequence||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||committed.Length!=1||
                committed[0].Actor!=used.Actor||committed[0].Move!=used.Move||committed[0].HpBefore<=0||committed[0].Sequence<=pp[0].Sequence)
                return new(null,"unmatched-source-self-destruct");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,committed[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 207 or 321 or 322 or 339)
        {
            var result=battle.GroupedResult();
            if(result is null)return new(null,action.Any(e=>e is SourceMoveMissedEvent miss&&miss.Actor==used.Actor&&miss.Move==used.Move)?"source-no-animation-result":"unmatched-source-mixed-effect-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Value,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move==187)
        {
            // maxattackhalvehp authenticates success before attackanimation.
            // Its later HP update does not invoke General_StatsChange.
            if(used.Target!=used.Actor||action.Length<3||action[0]!=pp[0]||
                action[1] is not SourceStatChangedEvent {Stat:WildBattleStat.Attack,After:12} stat||
                stat.Actor!=used.Actor||stat.Target!=used.Actor||stat.Before is <0 or >=12||
                action[2] is not SourceHpChangedEvent {Critical:false,RandomPercent:100} cost||
                cost.Actor!=used.Actor||cost.Target!=used.Actor||cost.Move!=used.Move||
                cost.Damage<1||cost.HpBefore<=cost.Damage||cost.HpAfter!=cost.HpBefore-cost.Damage)
                return new(null,"unmatched-source-belly-drum-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,stat.Sequence,move,new(used.Actor.Value),new(used.Target.Value),false),"committed-source-animation");
        }
        if (action.Any(e => e is SourceMoveMissedEvent m && m.Actor == used.Actor && m.Move == used.Move ||
            e is SourceMoveFailedEvent f && f.Actor == used.Actor && f.Move == used.Move && !(move==150&&f.Target==used.Actor&&f.Reason=="nothing-happened") ||
            e is SourceAbilityPreventedEvent a && a.Actor == used.Actor && a.Move == used.Move ||
            e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } t && t.Actor == used.Actor && t.Move == used.Move &&
                !CampaignStatAnimationPresentation.IsFailedToxicSecondary(action,used,t.Sequence,moves)))
            return new(null, "source-no-animation-result");
        if(definition!.Effect is "EFFECT_CONVERSION" or "EFFECT_CONVERSION_2" or "EFFECT_CAMOUFLAGE")
        {
            // Source type mutation succeeds before attackanimation. The receipt
            // supplies the chosen type; presentation never samples or reselects it.
            if(used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target!=used.Actor||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceCalledOverlayChangedEvent changed||
                changed.Source is not null||changed.Side!=used.Actor||changed.Move!=used.Move||
                changed.Sequence!=pp[0].Sequence+1||action.OfType<SourceCalledOverlayChangedEvent>().Count()!=1||
                changed.After is not {} after||after.RulesVersion!=WildBattleRules.CalledMoveKernelRulesVersion||
                !WildBattleEffective.IsSourceType(after.Type1)||after.Type1!=after.Type2||changed.Before==after||
                changed.Before is {} before&&(before.RulesVersion!=WildBattleRules.CalledMoveKernelRulesVersion||
                    before.Type1 is not null&&!WildBattleEffective.IsSourceType(before.Type1)||
                    before.Type2 is not null&&!WildBattleEffective.IsSourceType(before.Type2))||
                after!=((changed.Before??new WildBattleCalledOverlay()) with{Type1=after.Type1,Type2=after.Type1}))
                return new(null,"unmatched-source-type-change-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_MEAN_LOOK")
        {
            if(used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceTowerVolatileEvent {Effect:"mean-look",Applied:true,Reason:"move"} trapped||
                trapped.Side!=used.Target||trapped.Sequence!=pp[0].Sequence+1||action.OfType<SourceTowerVolatileEvent>().Count()!=1)
                return new(null,"unmatched-source-trapping-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,trapped.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(FieldSettingAnimation(move) is not null||move==277)
        {
            long result=0;
            if(move==277)
            {
                if(battle.EventIndex>=2&&battle.Events[battle.EventIndex-2] is SourceMtMoonVolatileEvent coat&&coat.Side==used.Actor&&coat.Turn==used.Turn&&coat.Volatile=="magic-coat"&&coat.Before is 0 or 1&&coat.After==1&&coat.Reason=="move"&&coat.Sequence<attack.Sequence)result=coat.Sequence;
            }
            else
            {
                var changed=action.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Side==used.Actor&&e.Volatile==FieldSettingAnimation(move)&&e.Before==0&&e.After==5&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
                if(changed.Length==1)result=changed[0].Sequence;
            }
            if(used.Target!=used.Actor||result==0)return new(null,"unmatched-source-wall-state");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 150 or 300)
        {
            // Splash's source animation precedes its intentional nothing-happened
            // message. Field sports animate only after the committed state change.
            var results=action.Where(e=>move==150
                ? e is SourceMoveFailedEvent splash&&splash.Actor==used.Actor&&splash.Target==used.Actor&&splash.Move==used.Move&&splash.Reason=="nothing-happened"&&splash.Sequence>pp[0].Sequence
                : e is SourceMtMoonVolatileEvent sport&&sport.Side==used.Actor&&sport.Volatile=="mud-sport"&&sport.Before==0&&sport.After==1&&sport.Reason=="move"&&sport.Sequence>pp[0].Sequence).ToArray();
            if(used.Target!=used.Actor||results.Length!=1)return new(null,"unmatched-source-self-action-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 47 or 77 or 78 or 79 or 86 or 92 or 95 or 147 or 320)
        {
            bool Matches(uint status)=>move switch{77=>status==WildBattleStatus.Poison,92=>status==WildBattleSourceFieldEffects.ToxicPoison,78 or 86=>status==WildBattleStatus.Paralysis,_=>WildBattleStatus.IsSleeping(status)};
            var changed=action.OfType<SourceStatusChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&Matches(e.After)&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(changed.Length!=1)return new(null,"unmatched-source-primary-status-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 116 or 203)
        {
            var results=action.Where(e=>move==116
                ? e is SourceFocusEnergyEvent focus&&focus.Side==used.Actor&&focus.Sequence>pp[0].Sequence
                : e is SourceProtectAttemptEvent protect&&protect.Side==used.Actor&&protect.Move==used.Move&&protect.Succeeded&&protect.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-focus-or-endure-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,new(used.Actor.Value),new(used.Target.Value),false),"committed-source-animation");
        }
        if(move==289)
        {
            var ready=action.OfType<SourceSnatchReadyEvent>().Where(e=>e.Actor==used.Actor&&e.Sequence>pp[0].Sequence).ToArray();
            if(ready.Length!=1)return new(null,"unmatched-source-snatch-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,ready[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(SelfStatAnimation(definition!) is not null)
        {
            var resultIndices=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1).Where(i=>battle.Events[i] is SourceStatChangedEvent).ToArray();
            if(resultIndices.Length!=1||battle.StatResult(resultIndices[0]) is not {} result||result.SourceMoveId!=move)
                return new(null,"unmatched-source-self-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move==48)
        {
            var results=action.OfType<SourceConfusionChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&e.After is >=2 and <=5&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-confusion-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(IsRecoveryAnimation(move))
        {
            if(used.Target!=used.Actor)return new(null,"unmatched-source-recovery-target");
            var results=action.OfType<SourceRecoveredEvent>().Where(e=>e.Side==used.Actor&&e.Move==used.Move&&e.HpAfter>e.HpBefore&&e.Recovered==e.HpAfter-e.HpBefore&&e.Sequence>pp[0].Sequence).ToArray();
            if(results.Length!=1)return new(null,"unmatched-source-recovery-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(ReductionAnimation(definition!) is {} reduction)
        {
            var results=action.OfType<SourceStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Target==used.Target&&e.Stat==reduction.Stat&&e.Before is >=0 and <=12&&e.After<e.Before&&e.After==Math.Clamp(e.Before+reduction.Delta,0,12)).ToArray();
            if(results.Length!=1||results[0].Sequence<=pp[0].Sequence)return new(null,"unmatched-source-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        var hits = action.Where(e => e is SourceHpChangedEvent h && h.Actor == used.Actor && h.Target == used.Target && h.Move == used.Move ||
            e is SourceSubstituteDamagedEvent s && s.Actor == used.Actor && s.Target == used.Target && s.Move == used.Move).ToArray();
        if (hits.Length != 1 || hits[0].Sequence <= pp[0].Sequence) return new(null, "unmatched-source-hit-result");
        // Damage and HP deltas are deliberately not eligibility inputs. Endure and substitute can
        // leave actual Pokémon HP unchanged while the original attack animation still executes.
        return new(new(battle.Id, used.Sequence, attack.Sequence, pp[0].Sequence, hits[0].Sequence, move,
            used.Actor, used.Target, hits[0] is SourceSubstituteDamagedEvent), "committed-source-animation");
    }
}
