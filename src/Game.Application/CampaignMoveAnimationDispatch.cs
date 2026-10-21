using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignMoveAnimationBoundary { AfterAttackText, AfterCallerText, AfterRetainedAttackText, BeforeHitResult, BeforeRecoveryResult, BeforeStatusResult, BeforeChargeResult }

/// <summary>Read-only projection of a committed source invocation at its authored presentation boundary.
/// It adds no Domain event, command, RNG draw, saved field or authoritative acknowledgement.</summary>
public sealed record CampaignMoveAnimationDispatch(string BattleId, long Sequence, long? AttackSequence,
    long? PpSequence, long ResultSequence, int SourceMoveId, BattlerSlot Actor, BattlerSlot Target,
    bool HitsSubstitute)
{
    // Controller byte 11 is gMultiHitCounter, separate from animTurn.
    // Multi-hit loops and switch interception override the ordinary reset value;
    // Beat Up and single-hit called leaves keep zero.
    public byte SubstituteControllerHitCounter {get;init;}
    public byte? MoveTurn {get;init;}
    public ushort? MovePower {get;init;}
    public byte? FuryCutterCounter {get;init;}
    public byte? RolloutCounter {get;init;}
    public int? MoveDamage {get;init;}
    public ushort? AnimationWeather {get;init;}
    // The source damage-register predicate, including a zero-damage result.
    public bool? IsHealingMove {get;init;}
    public CampaignMoveAnimationBoundary Boundary {get;init;} = CampaignMoveAnimationBoundary.AfterAttackText;
    public bool AcknowledgesCurrentReceipt => Boundary is CampaignMoveAnimationBoundary.AfterAttackText or CampaignMoveAnimationBoundary.AfterCallerText or CampaignMoveAnimationBoundary.AfterRetainedAttackText;
    public long SnapshotSequence => PpSequence ?? AttackSequence ?? Sequence;
}
public sealed record CampaignMoveAnimationDecision(CampaignMoveAnimationDispatch? Dispatch, string Reason);

public static partial class CampaignMoveAnimationPresentation
{
    // Launch snapshots use the authenticated PP command or the explicit
    // no-PP caller/retained/result boundary, before later outcome/charge receipts. Reuse the same charge replay as general effects.
    public static bool[]? SpriteVisibilityAtInvocation(IReadOnlyList<SourceBattleEvent> events,
        CampaignMoveAnimationDispatch dispatch,IReadOnlyList<MoveId?> entryChargeMoves)
    {
        int index=-1;
        for(int i=0;i<events.Count;i++)if(events[i].Sequence==dispatch.SnapshotSequence)
        {if(index>=0)return null;index=i;}
        if(index<0||entryChargeMoves.Count is not (2 or 4))return null;
        bool anchor=dispatch.PpSequence is not null
            ? events[index] is SourcePpCommandEvent pp&&pp.Actor==dispatch.Actor
            : dispatch.Boundary is CampaignMoveAnimationBoundary.AfterCallerText or CampaignMoveAnimationBoundary.AfterRetainedAttackText
                ? dispatch.Sequence==dispatch.AttackSequence&&events[index] is SourceAttackStringEvent attack&&attack.Actor==dispatch.Actor
                : dispatch.Boundary==CampaignMoveAnimationBoundary.BeforeHitResult&&dispatch.AttackSequence is null&&dispatch.Sequence==dispatch.ResultSequence&&
                    (events[index] is SourceHpChangedEvent hp&&hp.Actor==dispatch.Actor&&hp.Target==dispatch.Target||
                     events[index] is SourceSubstituteDamagedEvent sub&&sub.Actor==dispatch.Actor&&sub.Target==dispatch.Target);
        if(!anchor)return null;
        // clearsemiinvulnerablebit changes status3, not the sprite's invisible
        // flag. A successful release retains its actor's preceding charge pose
        // until the authored callback/visible command restores it. Other slots
        // still use the ordinary authenticated PP boundary.
        int actorIndex=index;
        if(dispatch.MoveTurn==1&&events[index] is SourcePpCommandEvent releasePp&&
            SourceChargeRules.SemiInvulnerable(releasePp.CurrentMove)!=WildBattleSemiInvulnerable.None)
        {
            int attackIndex=-1;
            for(int i=0;i<index;i++)if(events[i].Sequence==dispatch.AttackSequence){if(attackIndex>=0)return null;attackIndex=i;}
            if(attackIndex<1||events[attackIndex] is not SourceAttackStringEvent text||text.Actor!=dispatch.Actor||text.CurrentMove!=releasePp.CurrentMove||
                events[attackIndex-1] is not SourceMtMoonVolatileEvent {Volatile:"called-charge",Before:1,After:0,Reason:"released"} release||
                release.Side!=dispatch.Actor||release.Turn!=text.Turn||release.Sequence>=text.Sequence)return null;
            actorIndex=attackIndex-1;
        }
        var visibility=new bool[entryChargeMoves.Count];
        for(int slot=0;slot<visibility.Length;slot++)
        {
            var visible=CampaignHeldItemAnimationPresentation.SpriteVisibleAtResult(events,slot==dispatch.Actor.Value?actorIndex:index,new(slot),entryChargeMoves);
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
        "EFFECT_EVASION_DOWN" => (WildBattleStat.Evasion,-1),
        "EFFECT_DEFENSE_DOWN" => (WildBattleStat.Defense,-1),
        "EFFECT_DEFENSE_DOWN_2" => (WildBattleStat.Defense,-2),
        "EFFECT_SPEED_DOWN" => (WildBattleStat.Speed,-1),
        "EFFECT_SPEED_DOWN_2" => (WildBattleStat.Speed,-2),
        "EFFECT_ATTACK_DOWN" => (WildBattleStat.Attack,-1),
        "EFFECT_ATTACK_DOWN_2" => (WildBattleStat.Attack,-2),
        "EFFECT_SPECIAL_DEFENSE_DOWN_2" => (WildBattleStat.SpecialDefense,-2),
        _ => null
    };
    internal static (WildBattleStat Stat,int Delta)? SelfStatAnimation(PokemonMoveDefinition move) =>
        move.Power!=0||move.Target!="MOVE_TARGET_USER"?null:move.Effect switch
    {
        "EFFECT_ATTACK_UP" => (WildBattleStat.Attack,1),
        "EFFECT_ATTACK_UP_2" => (WildBattleStat.Attack,2),
        "EFFECT_SPEED_UP_2" => (WildBattleStat.Speed,2),
        "EFFECT_SPECIAL_ATTACK_UP" => (WildBattleStat.SpecialAttack,1),
        "EFFECT_SPECIAL_ATTACK_UP_2" => (WildBattleStat.SpecialAttack,2),
        "EFFECT_MINIMIZE" or "EFFECT_EVASION_UP" => (WildBattleStat.Evasion,1),
        "EFFECT_DEFENSE_CURL" or "EFFECT_DEFENSE_UP" => (WildBattleStat.Defense,1),
        "EFFECT_DEFENSE_UP_2" => (WildBattleStat.Defense,2),
        "EFFECT_SPECIAL_DEFENSE_UP_2" => (WildBattleStat.SpecialDefense,2),
        _ => null
    };
    private static string? FieldSettingAnimation(PokemonMoveDefinition move)=>move.Effect switch
    {
        "EFFECT_MIST"=>"mist", "EFFECT_SAFEGUARD"=>"safeguard", "EFFECT_LIGHT_SCREEN"=>"light-screen", "EFFECT_REFLECT"=>"reflect",
        "EFFECT_RAIN_DANCE"=>"rain", "EFFECT_SANDSTORM"=>"sandstorm", "EFFECT_SUNNY_DAY"=>"sun", "EFFECT_HAIL"=>"hail", _=>null
    };
    private static bool IsRecoveryAnimation(PokemonMoveDefinition move) => move.Effect is
        "EFFECT_RESTORE_HP" or "EFFECT_SOFTBOILED" or "EFFECT_MORNING_SUN" or "EFFECT_SYNTHESIS" or "EFFECT_MOONLIGHT";

    private sealed record Invocation(string Id, SourceBattleEvent[] Events, int EventIndex, int Slots,
        Func<long?> GroupedResult, Func<int,CampaignStatAnimationDispatch?> StatResult)
    {
        public CampaignAnimationInvocation? Context { get; init; }
    }

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

    private static bool ValidSpreadPp(SourcePpCommandEvent[] pp,SourceMoveUsedEvent used,SourceAttackStringEvent attack,bool retained=false,CampaignAnimationInvocation? invocation=null) =>
        used.Actor.Value is >=0 and <4&&used.Target.Value is >=0 and <4&&pp.Length is >=1 and <=2&&
        pp[0].Target==used.Target&&pp.Select(p=>p.Target).Distinct().Count()==pp.Length&&
        pp.All(p=>p.Actor==used.Actor&&p.PpOwner==(invocation?.PpOwner??used.Actor)&&p.CommandCost is >=1 and <=3&&
            p.Before is >=0 and <=255&&p.After is >=0 and <=255&&p.CurrentMove==used.Move&&
            p.ChosenMove==attack.ChosenMove&&p.PpSlot==(invocation?.PpSlot??used.Slot)&&p.Sequence>used.Sequence&&
            p.Target.Side!=used.Actor.Side&&(p.Target==used.Target||p.Target==used.Target.Partner))&&
        pp[0].Before==used.PpBefore&&pp[0].After==used.PpAfter&&
        (retained||invocation?.NoPpDeduct==true?pp[0].After==pp[0].Before:pp[0].Before>0&&pp[0].After==Math.Max(0,pp[0].Before-pp[0].CommandCost))&&
        pp.Skip(1).All(p=>p.Before==pp[0].After&&p.After==p.Before);

    public static CampaignMoveAnimationDecision AtAttackText(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        if(!sourceRules)return new(null,"historical-rules");
        // Source attackanimation still performs the two material-changing moves
        // with NO_ANIMATIONS. Only this current MoveUsed can opt in; an eventual
        // called leaf never enables the caller prelude or an unrelated receipt.
        if(scene!=SourceBattleScene.On&&(scene!=SourceBattleScene.Off||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length||
            battle.Events[battle.EventIndex] is not SourceMoveUsedEvent current||
            Definition(current.Move,moves)?.Effect is not("EFFECT_TRANSFORM" or "EFFECT_SUBSTITUTE")))
            return new(null,"animations-disabled");
        if(battle.Phase!=CampaignBattlePhase.Events || battle.EventIndex<0 || battle.EventIndex>=battle.Events.Length)
            return new(null,"not-attack-text");
        return AtAttackText(new(battle.Id,battle.Events.ToArray(),battle.EventIndex,2,
            ()=>CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle,true,moves:moves),
            index=>CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=index},true,moves:moves)),moves,supports);
    }

    public static CampaignMoveAnimationDecision AtAttackText(CampaignDoubleBattle battle,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports)
    {
        if(scene!=SourceBattleScene.On&&(scene!=SourceBattleScene.Off||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length||
            battle.Events[battle.EventIndex] is not SourceMoveUsedEvent current||
            Definition(current.Move,moves)?.Effect is not("EFFECT_TRANSFORM" or "EFFECT_SUBSTITUTE")))
            return new(null,"animations-disabled");
        if(battle.Stage!=CampaignDoubleBattleStage.Combat || battle.EventIndex<0 || battle.EventIndex>=battle.Events.Length)
            return new(null,"not-attack-text");
        return AtAttackText(new(battle.Id,battle.Events.ToArray(),battle.EventIndex,4,
            ()=>CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle,moves:moves),
            index=>CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=index},moves:moves)),moves,supports);
    }

    private static CampaignMoveAnimationDecision AtAttackText(Invocation battle,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves, Func<int,string,bool> supports,
        CampaignMoveAnimationBoundary boundary = CampaignMoveAnimationBoundary.AfterAttackText)
    {
        // Accepted basic hit/slice moves print through SpendInvocationPp. The paired AttackString is silent;
        // MoveUsed is the actual visible page. Callers such as Metronome have a different current move.
        if(battle.Events[battle.EventIndex] is SourceAttackStringEvent callerAttack)
            return Definition(callerAttack.CurrentMove,moves)?.Effect is "EFFECT_RAMPAGE" or "EFFECT_ROLLOUT" or "EFFECT_UPROAR"
                ? RetainedAttack(battle,callerAttack,moves,supports)
                : CallerPrelude(battle,callerAttack,null,moves,supports);
        if (battle.Events[battle.EventIndex] is not SourceMoveUsedEvent used) return new(null, "not-attack-text");
        var definition = Definition(used.Move, moves);
        int move = definition?.SourceMoveId ?? 0;
        if (move == 0 || !supports(move,used.Move.Value)) return new(null, "script-outside-slice");
        battle=battle with{Context=CampaignAnimationInvocation.TryRead(battle.Events,battle.EventIndex,battle.Slots,moves)};
        if(used.Move!=WildBattleRules.StruggleMove&&battle.Context is null)
            return new(null,"unmatched-source-invocation-provenance");
        if(definition!.Effect is "EFFECT_TRANSFORM" or "EFFECT_SUBSTITUTE")
            return MaterialChangeAnimation(battle,used,definition);
        if(definition!.Effect=="EFFECT_SLEEP_TALK")
            return CallerPrelude(battle,null,used,moves,supports);
        if(definition!.Effect=="EFFECT_REST"&&boundary!=CampaignMoveAnimationBoundary.BeforeRecoveryResult)
            return new(null,"source-before-recovery-animation");
        if(definition!.Effect=="EFFECT_TEETER_DANCE"&&boundary!=CampaignMoveAnimationBoundary.BeforeStatusResult)
            return new(null,"source-before-status-animation");
        if(MultiHitBounds(definition!) is not null||definition!.Effect=="EFFECT_BEAT_UP")return new(null,"source-per-hit-animation");
        if (battle.EventIndex == 0 || battle.Events[battle.EventIndex - 1] is not SourceAttackStringEvent attack ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.ChosenMove != (battle.Context?.ChosenMove??used.Move) ||
            attack.Turn != used.Turn || attack.Sequence >= used.Sequence ||
            (used.Move==WildBattleRules.StruggleMove?used.Slot!=-1:used.Slot is <0 or >3))
            return new(null, "unmatched-source-attack-string");
        int end = battle.EventIndex + 1;
        while (end < battle.Events.Length && battle.Events[end].Turn == used.Turn &&
            battle.Events[end] is not (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent) &&
            !(battle.Events[end] is SourcePpCommandEvent nextPp && nextPp.Actor != used.Actor)) end++;
        for(int i=battle.EventIndex+1;i<end;i++)
            if(battle.Events[i].Sequence<=battle.Events[i-1].Sequence)return new(null,"unmatched-source-invocation-sequence");
        var action = battle.Events.Skip(battle.EventIndex + 1).Take(end - battle.EventIndex - 1).ToArray();
        var pp = action.OfType<SourcePpCommandEvent>().ToArray();
        if(used.Move==WildBattleRules.StruggleMove&&
            (definition.Effect!="EFFECT_RECOIL"||definition.Target!="MOVE_TARGET_SELECTED"||definition.Power<=0||
             used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
             used.Actor.Side==used.Target.Side||action.Length<2||pp.Length!=1||action[0]!=pp[0]))
            return new(null,"unmatched-source-forced-invocation");
        bool chargeRelease=HasChargeAnimation(definition!);
        if(chargeRelease&&(!AuthenticatedChargeRelease(battle,used,definition,action)||action.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side==used.Actor&&e.Volatile=="called-charge")))return new(null,"unmatched-source-charge-release");
        if(definition!.Effect=="EFFECT_ERUPTION")return HpScaledAnimation(battle,used,attack,definition,action,pp);
        if(battle.Slots == 4 && definition!.Target == "MOVE_TARGET_BOTH" && definition.Power > 0)
        {
            // Source MOVEEND_NEXT_TARGET revisits the hit script without another attack string.
            // One visible attack page owns both PP-command visits and one visual invocation.
            if(!ValidSpreadPp(pp,used,attack,chargeRelease,battle.Context))
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
            return new(new(battle.Id,used.Sequence,attack.Sequence,hitPp.Sequence,hit.Sequence,move,used.Actor,hit.Target,hit.Substitute){MoveTurn=chargeRelease?(byte)1:null},"committed-source-animation");
        }
        if(battle.Slots==4&&definition!.Target=="MOVE_TARGET_BOTH"&&ReductionAnimation(definition) is {} spreadStat)
        {
            // Primary reductions revisit the same source script once per opposing target.
            // The dedicated root belongs to the first eligible result; each stat wave
            // remains owned by its individual PP pass.
            if(!ValidSpreadPp(pp,used,attack,invocation:battle.Context))return new(null,"unmatched-source-both-stat-pp");
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
        if(definition!.Effect is "EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE")
            return GroundSpreadAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_HELPING_HAND" or "EFFECT_IMPRISON" or "EFFECT_TEETER_DANCE")
            return SupportStatusAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_YAWN" or "EFFECT_SKILL_SWAP" or "EFFECT_GRUDGE" or "EFFECT_SPIKES" or "EFFECT_ENCORE")
            return UtilityAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_DESTINY_BOND" or "EFFECT_PERISH_SONG" or "EFFECT_CURSE")
            return GhostSongAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect=="EFFECT_MEMENTO")
            return MementoAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect=="EFFECT_SPITE")
            return SpiteAnimation(battle,used,attack,definition,action,pp,moves);
        if(definition.Effect=="EFFECT_BATON_PASS")
            return BatonPassAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect=="EFFECT_LOCK_ON")
            return TargetMarkAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_SNORE" or "EFFECT_UPROAR"&&!SoundHitDefinition(definition))
            return new(null,"unmatched-source-sound-hit-definition");
        if (!ValidSingleInvocationPp(pp,used,2,chargeRelease,battle.Context))
            return new(null, "unmatched-source-pp-command");
        if(definition.Effect=="EFFECT_PAIN_SPLIT")return PainSplitAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_TRICK" or "EFFECT_RECYCLE")
            return ItemTransferAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_REFRESH" or "EFFECT_HEAL_BELL")
            return CureAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect=="EFFECT_BIDE")return BideSetup(battle,used,attack,definition,action,pp[0]);
        if(definition.Effect=="EFFECT_BRICK_BREAK"&&action.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Reason=="brick-break"))
            return BrokenScreenAnimation(battle,used,attack,definition,action,pp[0]);
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
        if(definition.Effect=="EFFECT_FUTURE_SIGHT")
            return FutureAttackSetup(battle,used,attack,definition,action,pp[0]);
        if (move is 120 or 153)
        {
            // BattleScript_EffectExplosion animates once immediately after setatkhptozero,
            // before every target pass, even when every pass later misses/is immune.
            var committed=action.OfType<SourceSelfDestructEvent>().ToArray();
            if(pp[0].ChosenMove!=(battle.Context?.ChosenMove??used.Move)||pp[0].PpSlot!=(battle.Context?.PpSlot??used.Slot)||pp[0].Sequence<=used.Sequence||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||committed.Length!=1||
                committed[0].Actor!=used.Actor||committed[0].Move!=used.Move||committed[0].HpBefore<=0||committed[0].Sequence<=pp[0].Sequence)
                return new(null,"unmatched-source-self-destruct");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,committed[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        if(CampaignStatAnimationPresentation.HasGroupedEffect(definition.Effect))
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
            e is SourceAbilityPreventedEvent a && a.Actor == used.Actor && a.Move == used.Move &&
                !CampaignStatAnimationPresentation.IsPreventedItemSecondary(action,used,a.Sequence,moves) ||
            e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } t && t.Actor == used.Actor && t.Move == used.Move &&
                !CampaignStatAnimationPresentation.IsFailedToxicSecondary(action,used,t.Sequence,moves)))
            return new(null, "source-no-animation-result");
        if(definition.Effect=="EFFECT_PSYCH_UP")
            return PsychUpAnimation(battle,used,attack,definition,action,pp[0]);
        if(definition.Effect=="EFFECT_NIGHTMARE")
            return NightmareAnimation(battle,used,attack,definition,action,pp);
        if(definition.Effect is "EFFECT_ROAR" or "EFFECT_TELEPORT")
        {
            if(used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                definition.Power!=0||definition.Target!=(definition.Effect=="EFFECT_ROAR"?"MOVE_TARGET_SELECTED":"MOVE_TARGET_USER")||
                action.Length<2||action[0]!=pp[0]||action[1].Sequence!=pp[0].Sequence+1)return new(null,"unmatched-source-exit-result");
            if(definition.Effect=="EFFECT_ROAR")
            {
                if(used.Actor==used.Target||action[1] is not SourceForcedOutEvent forced||forced.Actor!=used.Actor||
                    forced.Target!=used.Target||forced.Move!=used.Move||battle.Slots==4&&!forced.TrainerBattle||
                    action.OfType<SourceForcedOutEvent>().Count()!=1)return new(null,"unmatched-source-force-out-result");
            }
            else if(battle.Slots!=2||used.Actor!=used.Target||action[1] is not SourceEndedEvent {Outcome:WildBattleOutcome.Escaped,MoveEscape:{} escaped}||
                escaped.Actor!=used.Actor||escaped.Move!=used.Move||
                action.OfType<SourceEndedEvent>().Count()!=1||action.OfType<SourceForcedOutEvent>().Any())
                return new(null,"unmatched-source-teleport-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,action[1].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect is "EFFECT_MIMIC" or "EFFECT_SKETCH")
        {
            bool permanent=definition.Effect=="EFFECT_SKETCH";
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceMoveCopiedEvent copied||copied.Permanent!=permanent||copied.Replacement is null||
                copied.Actor!=used.Actor||copied.Target!=used.Target||copied.Actor==copied.Target||copied.Move!=used.Move||
                copied.MoveSlot!=pp[0].PpSlot||copied.Sequence!=pp[0].Sequence+1||action.OfType<SourceMoveCopiedEvent>().Count()!=1||
                copied.CopiedMove.Value is "pret:MOVE_SKETCH" or "pret:MOVE_STRUGGLE"||
                !permanent&&copied.CopiedMove.Value is "pret:MOVE_MIMIC" or "pret:MOVE_METRONOME"||
                Definition(copied.CopiedMove,moves) is not {} replacement||copied.Replacement.Move!=replacement.Id||
                copied.Replacement.PpUps is <0 or >3||copied.Replacement.CurrentPp!=(permanent?replacement.Pp:Math.Min(5,replacement.Pp))||
                copied.Replacement.MaximumPp!=replacement.Pp+replacement.Pp*20*copied.Replacement.PpUps/100)
                return new(null,"unmatched-source-copy-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,copied.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_HAZE")
        {
            // The source resets changed stages in slot/stat order, then emits one
            // all-stages-reset receipt even when every stage was already neutral.
            var markers=action.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="haze").ToArray();
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||action.Length<2||action[0]!=pp[0]||markers.Length!=1||
                markers[0].Side!=used.Actor||markers[0].Before!=0||markers[0].After!=0||markers[0].Reason!="all-stat-stages-reset")
                return new(null,"unmatched-source-stat-reset-result");
            int markerIndex=Array.IndexOf(action,markers[0]),previous=-1;
            WildBattleStat[] order=[WildBattleStat.Attack,WildBattleStat.Defense,WildBattleStat.Speed,WildBattleStat.SpecialAttack,WildBattleStat.SpecialDefense,WildBattleStat.Accuracy,WildBattleStat.Evasion];
            if(markerIndex<1)return new(null,"unmatched-source-stat-reset-result");
            foreach(var row in action[1..markerIndex])
            {
                if(row is not SourceStatChangedEvent stat||stat.Actor!=used.Actor||stat.Target.Value<0||stat.Target.Value>=battle.Slots||
                    stat.Before is <0 or >12 or 6||stat.After!=6||!order.Contains(stat.Stat))
                    return new(null,"unmatched-source-stat-reset-result");
                int position=stat.Target.Value*order.Length+Array.IndexOf(order,stat.Stat);
                if(position<=previous)return new(null,"unmatched-source-stat-reset-result");
                previous=position;
            }
            var itemSuffix=new HashSet<int>();
            for(int item=markerIndex+1;item<action.Length;item++)if(action[item] is SourceHeldItemConsumedEvent)
            {
                int start=CampaignHeldItemAnimationPresentation.ConsumedGroupStart(action,item);
                if(start>markerIndex)for(int row=start;row<=item;row++)itemSuffix.Add(row);
            }
            if(Enumerable.Range(markerIndex+1,action.Length-markerIndex-1).Any(i=>action[i] is SourceStatChangedEvent&&!itemSuffix.Contains(i)))
                return new(null,"unmatched-source-stat-reset-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,markers[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition!.Effect=="EFFECT_ROLE_PLAY")
        {
            // trycopyability succeeds before attackanimation, including same-ability
            // copies and Substitute. It changes only the actor's battle overlay.
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor.Value>=battle.Slots||used.Target.Value>=battle.Slots||used.Actor==used.Target||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceCalledOverlayChangedEvent changed||
                changed.Side!=used.Actor||changed.Source is not null||changed.Move!=used.Move||changed.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceCalledOverlayChangedEvent>().Count()!=1||changed.After is not {} after||
                after.RulesVersion!=WildBattleRules.CalledMoveKernelRulesVersion||after.Ability is not {} ability||
                ability is "ABILITY_NONE" or "ABILITY_WONDER_GUARD"||!ability.StartsWith("ABILITY_",StringComparison.Ordinal)||ability.Length<=8||
                ability[8..].Any(c=>!(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))||
                changed.Before is {} before&&before.RulesVersion!=WildBattleRules.CalledMoveKernelRulesVersion||
                after!=((changed.Before??new WildBattleCalledOverlay()) with{Ability=ability}))
                return new(null,"unmatched-source-role-play-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
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
        if(definition.Effect=="EFFECT_LEECH_SEED")
        {
            var seeds=action.OfType<SourceLeechSeedChangedEvent>().ToArray();
            if(definition.Target!="MOVE_TARGET_SELECTED"||used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||
                seeds.Length!=1||seeds[0] is not {Before:null,Reason:"move"} seed||seed.Side!=used.Target||seed.After!=used.Actor||
                action.Length<2||action[0]!=pp[0]||action[1]!=seed)
                return new(null,"unmatched-source-leech-seed-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,seed.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_MEAN_LOOK")
        {
            if(used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||used.Actor==used.Target||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceTowerVolatileEvent {Effect:"mean-look",Applied:true,Reason:"move"} trapped||
                trapped.Side!=used.Target||trapped.Sequence!=pp[0].Sequence+1||action.OfType<SourceTowerVolatileEvent>().Count()!=1)
                return new(null,"unmatched-source-trapping-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,trapped.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition!.Effect=="EFFECT_INGRAIN")
        {
            // trysetroots commits before attackanimation. End-of-turn recovery
            // and a later switch/root removal cannot create another invocation.
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||action.Length<2||action[0]!=pp[0]||
                action[1] is not SourceRootsChangedEvent {Rooted:true,Reason:"ingrain"} roots||
                roots.Side!=used.Actor||roots.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceRootsChangedEvent>().Count()!=1)
                return new(null,"unmatched-source-roots-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,roots.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(FieldSettingAnimation(definition!) is not null||move==277)
        {
            long result=0;
            if(move==277)
            {
                if(battle.EventIndex>=2&&battle.Events[battle.EventIndex-2] is SourceMtMoonVolatileEvent coat&&coat.Side==used.Actor&&coat.Turn==used.Turn&&coat.Volatile=="magic-coat"&&coat.Before is 0 or 1&&coat.After==1&&coat.Reason=="move"&&coat.Sequence<attack.Sequence)result=coat.Sequence;
            }
            else
            {
                string field=FieldSettingAnimation(definition!)!;
                // Source set-weather/screen commands precede attackanimation. A
                // suppressed weather still commits; an already-set one does not.
                // Forecast and other move-end outcomes follow this owned marker.
                if(definition!.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Actor.Value<0||used.Actor.Value>=battle.Slots||
                    action.Length<2||action[0]!=pp[0]||action[1] is not SourceMtMoonVolatileEvent changed||
                    changed.Side!=used.Actor||changed.Volatile!=field||changed.Before!=0||changed.After!=5||changed.Reason!="move"||
                    changed.Sequence!=pp[0].Sequence+1||action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile==field)!=1)
                    return new(null,"unmatched-source-wall-state");
                result=changed.Sequence;
            }
            if(used.Target!=used.Actor||result==0)return new(null,"unmatched-source-wall-state");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect is "EFFECT_MUD_SPORT" or "EFFECT_WATER_SPORT")
        {
            string sport=definition.Effect=="EFFECT_MUD_SPORT"?"mud-sport":"water-sport";
            // settypebasedhalvers commits after ppreduce and before attackanimation.
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||action.Length<2||action[0]!=pp[0]||
                action[1] is not SourceMtMoonVolatileEvent changed||changed.Side!=used.Actor||
                changed.Volatile!=sport||changed.Before!=0||changed.After!=1||changed.Reason!="move"||
                changed.Sequence!=pp[0].Sequence+1||action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile==sport)!=1||
                !CureMoveEndTail(action,2,used,battle.Slots,invocation:battle.Context))
                return new(null,"unmatched-source-self-action-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_SPLASH")
        {
            // Splash intentionally animates before the nothing-happened message.
            var results=action.OfType<SourceMoveFailedEvent>().Where(splash=>splash.Actor==used.Actor&&
                splash.Target==used.Actor&&splash.Move==used.Move&&splash.Reason=="nothing-happened"&&splash.Sequence>pp[0].Sequence).ToArray();
            if(used.Target!=used.Actor||results.Length!=1)return new(null,"unmatched-source-self-action-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }

        if(definition.Effect is "EFFECT_SLEEP" or "EFFECT_POISON" or "EFFECT_TOXIC" or "EFFECT_PARALYZE" or "EFFECT_WILL_O_WISP")
        {
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Actor==used.Target||used.Target.Value<0||used.Target.Value>=battle.Slots)
                return new(null,"unmatched-source-primary-status-target");
            bool Matches(uint status)=>definition.Effect switch{"EFFECT_POISON"=>status==WildBattleStatus.Poison,"EFFECT_TOXIC"=>status==WildBattleSourceFieldEffects.ToxicPoison,"EFFECT_PARALYZE"=>status==WildBattleStatus.Paralysis,"EFFECT_WILL_O_WISP"=>status==WildBattleStatus.Burn,_=>WildBattleStatus.IsSleeping(status)};
            var changed=action.OfType<SourceStatusChangedEvent>().Where(e=>e.Side==used.Target&&e.Before==0&&Matches(e.After)&&e.Reason=="move"&&e.Sequence>pp[0].Sequence).ToArray();
            if(changed.Length!=1)return new(null,"unmatched-source-primary-status-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_CHARGE")
        {
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||action.Length<2||action[0]!=pp[0]||
                action[1] is not SourceMtMoonVolatileEvent {Volatile:"charge",Before:>=0 and <=2,After:2,Reason:"move"} charged||
                charged.Side!=used.Actor||charged.Sequence!=pp[0].Sequence+1||
                !CureMoveEndTail(action,2,used,battle.Slots,invocation:battle.Context))
                return new(null,"unmatched-source-charge-command-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,charged.Sequence,move,used.Actor,used.Actor,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_FOCUS_ENERGY")
        {
            var results=action.OfType<SourceFocusEnergyEvent>().Where(focus=>focus.Side==used.Actor&&focus.Sequence>pp[0].Sequence).ToArray();
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||results.Length!=1)
                return new(null,"unmatched-source-focus-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect is "EFFECT_PROTECT" or "EFFECT_ENDURE")
        {
            string state=definition.Effect=="EFFECT_PROTECT"?"protect":"endure";
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                action.Length<3||action[0]!=pp[0]||action[1] is not SourceProtectAttemptEvent attempt||
                attempt.Side!=used.Actor||attempt.Move!=used.Move||!attempt.Succeeded||!attempt.NotLastAction||
                attempt.Sequence!=pp[0].Sequence+1||attempt.Before is <0 or >255||
                attempt.After!=Math.Min(255,attempt.Before+1)||attempt.Threshold!=WildBattleRules.ProtectSuccessThreshold(attempt.Before)||
                attempt.Roll<0||attempt.Roll>attempt.Threshold||action.OfType<SourceProtectAttemptEvent>().Count()!=1||
                action[2] is not SourceMtMoonVolatileEvent marker||marker.Side!=used.Actor||marker.Volatile!=state||
                marker.Before!=0||marker.After!=1||marker.Reason!="move"||marker.Sequence!=attempt.Sequence+1||
                action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile==state&&e.Reason=="move")!=1)
                return new(null,"unmatched-source-protection-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,attempt.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_DISABLE")
        {
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceDisableChangedEvent disabled||
                disabled.Side!=used.Target||disabled.Before!=0||disabled.After is <2 or >5||disabled.Reason!="move"||
                disabled.Move is not {} disabledMove||Definition(disabledMove,moves) is null||
                disabled.Sequence!=pp[0].Sequence+1||action.OfType<SourceDisableChangedEvent>().Count(e=>e.Reason=="move")!=1)
                return new(null,"unmatched-source-disable-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,disabled.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move==289)
        {
            var ready=action.OfType<SourceSnatchReadyEvent>().Where(e=>e.Actor==used.Actor&&e.Sequence>pp[0].Sequence).ToArray();
            if(ready.Length!=1)return new(null,"unmatched-source-snatch-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,ready[0].Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(SelfStatAnimation(definition!) is not null)
        {
            // A real move-end item restoration has its own authenticated group.
            // It cannot erase the earlier move result or masquerade as another one.
            var itemReceipts=CampaignStatAnimationPresentation.ItemReceiptSequences(battle.Events);
            var resultIndices=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1)
                .Where(i=>battle.Events[i] is SourceStatChangedEvent&&!itemReceipts.Contains(battle.Events[i].Sequence)).ToArray();
            if(resultIndices.Length!=1||battle.StatResult(resultIndices[0]) is not {} result||result.SourceMoveId!=move)
                return new(null,"unmatched-source-self-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_CONFUSE")
        {
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceConfusionChangedEvent confused||
                confused.Side!=used.Target||confused.Before!=0||confused.After is <2 or >5||confused.Reason!="move"||
                confused.Sequence!=pp[0].Sequence+1||action.OfType<SourceConfusionChangedEvent>().Count(e=>e.Reason=="move")!=1)
                return new(null,"unmatched-source-confusion-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,confused.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect is "EFFECT_TAUNT" or "EFFECT_TORMENT")
        {
            string state=definition.Effect=="EFFECT_TAUNT"?"taunt":"torment";
            int after=definition.Effect=="EFFECT_TAUNT"?2:1;
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceMtMoonVolatileEvent changed||
                changed.Side!=used.Target||changed.Volatile!=state||changed.Before!=0||changed.After!=after||
                changed.Reason!="move"||changed.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile==state&&e.Reason=="move")!=1)
                return new(null,"unmatched-source-selected-volatile-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_ATTRACT")
        {
            // A later Mental Herb clear does not undo the earlier successful setup.
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceInfatuationChangedEvent changed||
                changed.Side!=used.Target||changed.Before is not null||changed.After!=used.Actor||
                changed.Reason!="attract"||changed.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceInfatuationChangedEvent>().Count(e=>e.Reason=="attract")!=1)
                return new(null,"unmatched-source-infatuation-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_FOLLOW_ME")
        {
            string reason=battle.Slots==2?"single-battle-no-redirection":"move";
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||action.Length<2||action[0]!=pp[0]||
                action[1] is not SourceMtMoonVolatileEvent changed||changed.Side!=used.Actor||changed.Volatile!="follow-me"||
                changed.Before is <0 or >1||battle.Slots==2&&changed.Before!=0||changed.After!=1||changed.Reason!=reason||changed.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="follow-me"&&e.Reason==reason)!=1)
                return new(null,"unmatched-source-redirection-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,move,used.Actor,used.Actor,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_FORESIGHT")
        {
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
                action.Length<2||action[0]!=pp[0]||action[1] is not SourceMtMoonVolatileEvent identified||
                identified.Side!=used.Target||identified.Volatile!="identified"||identified.Before is <0 or >1||
                identified.After!=1||identified.Reason!="move"||identified.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="identified"&&e.Reason=="move")!=1)
                return new(null,"unmatched-source-identification-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,identified.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect is "EFFECT_STOCKPILE" or "EFFECT_SWALLOW")
            return StockpileAnimation(battle,used,attack,definition,action,pp[0]);
        if(definition.Effect=="EFFECT_REST")
        {
            // trysetrest prints the sleep result and updates the status icon before
            // attackanimation. The recovery page remains unacknowledged until playback ends.
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots||action.Length<3||action[0]!=pp[0]||
                action[1] is not SourceStatusChangedEvent {After:3,Reason:"rest"} status||status.Side!=used.Actor||
                WildBattleStatus.IsSleeping(status.Before)||
                action[2] is not SourceRecoveredEvent recovery||recovery.Side!=used.Actor||recovery.Move!=used.Move||
                recovery.HpBefore<=0||recovery.HpAfter<=recovery.HpBefore||recovery.Recovered!=recovery.HpAfter-recovery.HpBefore||
                action.OfType<SourceStatusChangedEvent>().Count(e=>e.Reason=="rest")!=1||
                action.OfType<SourceRecoveredEvent>().Count(e=>e.Move==used.Move)!=1)
                return new(null,"unmatched-source-rest-result");
            return new(new(battle.Id,recovery.Sequence,attack.Sequence,pp[0].Sequence,recovery.Sequence,move,used.Actor,used.Actor,false)
                {Boundary=CampaignMoveAnimationBoundary.BeforeRecoveryResult},"committed-source-recovery-animation");
        }
        if(IsRecoveryAnimation(definition))
        {
            if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Target!=used.Actor||
                used.Actor.Value<0||used.Actor.Value>=battle.Slots)
                return new(null,"unmatched-source-recovery-target");
            if(action.Length<2||action[0]!=pp[0]||action[1] is not SourceRecoveredEvent recovered||
                recovered.Side!=used.Actor||recovered.Move!=used.Move||recovered.HpBefore<=0||
                recovered.HpAfter<=recovered.HpBefore||recovered.Recovered!=recovered.HpAfter-recovered.HpBefore||
                action.OfType<SourceRecoveredEvent>().Count(e=>e.Move==used.Move)!=1)
                return new(null,"unmatched-source-recovery-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,recovered.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(ReductionAnimation(definition!) is {} reduction)
        {
            var results=action.OfType<SourceStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Target==used.Target&&e.Stat==reduction.Stat&&e.Before is >=0 and <=12&&e.After<e.Before&&e.After==Math.Clamp(e.Before+reduction.Delta,0,12)).ToArray();
            if(results.Length!=1||results[0].Sequence<=pp[0].Sequence)return new(null,"unmatched-source-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        if(definition.Effect=="EFFECT_PRESENT"&&action.OfType<SourceRecoveredEvent>().Any(e=>e.Side==used.Target&&e.Move==used.Move))
            return PresentRecoveryAnimation(battle,used,attack,definition,action,pp);
        var hits = action.Where(e => e is SourceHpChangedEvent h && h.Actor == used.Actor && h.Target == used.Target && h.Move == used.Move ||
            e is SourceSubstituteDamagedEvent s && s.Actor == used.Actor && s.Target == used.Target && s.Move == used.Move).ToArray();
        if (hits.Length != 1 || hits[0].Sequence <= pp[0].Sequence) return new(null, "unmatched-source-hit-result");
        // Damage and HP deltas are deliberately not eligibility inputs. Endure and substitute can
        // leave actual Pokémon HP unchanged while the original attack animation still executes.
        var dispatch=new CampaignMoveAnimationDispatch(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,hits[0].Sequence,move,
            used.Actor,used.Target,hits[0] is SourceSubstituteDamagedEvent){MoveTurn=chargeRelease?(byte)1:null};
        return WithCommittedHitInputs(battle,used,attack,definition!,hits[0],dispatch);
    }
}
