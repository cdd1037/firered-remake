using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision HpScaledAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Power is <1 or >65535||definition.Target!="MOVE_TARGET_BOTH"||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            used.Actor.Side==used.Target.Side||action.Length<2||commands.Length==0||action[0]!=commands[0]||
            !(battle.Slots==4?ValidSpreadPp(commands,used,attack):ValidSingleInvocationPp(commands,used,2)))
            return Reject("unmatched-source-hp-scaled-invocation");
        var hits=action.Where(e=>e is SourceHpChangedEvent hp&&hp.Actor==used.Actor&&hp.Move==used.Move||
            e is SourceSubstituteDamagedEvent sub&&sub.Actor==used.Actor&&sub.Move==used.Move).ToArray();
        if(hits.Length>commands.Length)return Reject("unmatched-source-hp-scaled-hit");
        int? prepared=null;CampaignMoveAnimationDispatch? dispatch=null;
        foreach(var pp in commands)
        {
            var owned=hits.Where(h=>CommittedHit(h,battle.Slots) is {} hit&&hit.Target==pp.Target).ToArray();
            if(owned.Length==0)
            {
                if(!FailedHit(action,used.Actor,pp.Target,used.Move))return Reject("unmatched-source-hp-scaled-hit");
                continue;
            }
            if(owned.Length!=1||CommittedHit(owned[0],battle.Slots) is not {} identity||identity.Actor!=used.Actor||identity.Move!=used.Move||
                owned[0].Sequence<=pp.Sequence||commands.Any(next=>next.Sequence>pp.Sequence&&next.Sequence<owned[0].Sequence))
                return Reject("unmatched-source-hp-scaled-hit");
            int? power=owned[0] switch{SourceHpChangedEvent h=>h.PreparedBasePower,SourceSubstituteDamagedEvent h=>h.PreparedBasePower,_=>null};
            if(power is null||power<1||power>definition.Power||prepared is {} earlier&&earlier!=power)
                return Reject("unmatched-source-prepared-power");
            int? animationPower=owned[0] switch{SourceHpChangedEvent h=>h.PreparedAnimationPower,SourceSubstituteDamagedEvent h=>h.PreparedAnimationPower,_=>null};
            if(animationPower is null||animationPower<0||animationPower>65535)
                return Reject("unmatched-source-animation-power");
            prepared=power;
            if(dispatch is null&&!FailedHit(action,used.Actor,pp.Target,used.Move))
                dispatch=new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,owned[0].Sequence,definition.SourceMoveId,
                    used.Actor,pp.Target,identity.Substitute){MovePower=checked((ushort)animationPower.Value)};
        }
        if(hits.Any(h=>CommittedHit(h,battle.Slots) is not {} hit||hit.Actor!=used.Actor||hit.Move!=used.Move||
            !commands.Any(pp=>pp.Target==hit.Target)))return Reject("unmatched-source-hp-scaled-hit");
        return dispatch is null?Reject("source-no-animation-result"):new(dispatch,"committed-source-animation");
    }

    private static CampaignMoveAnimationDecision PainSplitAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            action.Length<3||action[0]!=commands[0]||commands[0].Sequence!=used.Sequence+1||
            action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1)||
            PainSplitHp(action[1],used.Actor,used) is not {} actor||PainSplitHp(action[2],used.Target,used) is not {} target)
            return Reject("unmatched-source-pain-split-result");
        long average=((long)actor.Before+target.Before)/2;
        if(actor.After>average||target.After>average||actor.Before>=average&&actor.After!=average||
            target.Before>=average&&target.After!=average)return Reject("unmatched-source-pain-split-hp");
        var synchronized=new HashSet<BattlerSlot>();
        bool Sync(SourceBattleEvent[] rows,int index,SourceCalledPartyChangedEvent sync,SourceMoveUsedEvent owner)
        {
            if(sync.SourceSlot is not {} source||source.Value<0||source.Value>=battle.Slots||source.Side!=sync.Side||
                sync.TransientMoveSlots is not {} transient||transient>15||sync.Before?.Pokemon is null||sync.After?.Pokemon is null||
                !synchronized.Add(source))return false;
            var before=sync.Before;var after=sync.After;bool hpChanged=before.Pokemon.CurrentHp!=after.Pokemon.CurrentHp;
            if(source==used.Actor||source==used.Target)
            {
                var receipt=source==used.Actor?actor:target;
                if(before.Pokemon.CurrentHp!=receipt.Before||after.Pokemon.CurrentHp!=receipt.After||
                    before.Pokemon.CurrentHp>before.Pokemon.Stats.MaxHp||after.Pokemon.CurrentHp!=Math.Min(average,before.Pokemon.Stats.MaxHp))return false;
            }
            else if(hpChanged)return false;
            var normalized=sync with{Before=before with{Pokemon=before.Pokemon with{CurrentHp=after.Pokemon.CurrentHp}}};
            if(CureActiveSync(rows,index,normalized,owner))return true;
            return hpChanged&&ItemSameMember(normalized.Before,normalized.After);
        }
        // Every changed HP member necessarily differs from its action-entry
        // party and has one active-sync receipt. That existing party snapshot
        // proves the exact MaxHp cap; missing synchronization is incomplete.
        if(!CureMoveEndTail(action,3,used,battle.Slots,Sync)||actor.Before!=actor.After&&!synchronized.Contains(used.Actor)||
            target.Before!=target.After&&!synchronized.Contains(used.Target))return Reject("unmatched-source-pain-split-tail");
        // Source animates once, then updates attacker HP before target HP. Both
        // zero-delta recoveries remain real ordered receipts, never second roots.
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,action[1].Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }

    private static (int Before,int After)? PainSplitHp(SourceBattleEvent row,BattlerSlot recipient,SourceMoveUsedEvent used)=>row switch
    {
        SourceRecoveredEvent h when h.Turn==used.Turn&&h.Side==recipient&&h.Move==used.Move&&h.HpBefore>0&&
            h.HpAfter>=h.HpBefore&&h.Recovered==h.HpAfter-h.HpBefore=>(h.HpBefore,h.HpAfter),
        SourceHpChangedEvent h when h.Turn==used.Turn&&h.Actor==used.Actor&&h.Target==recipient&&h.Move==used.Move&&
            h.HpBefore>0&&h.HpAfter>0&&h.HpAfter<h.HpBefore&&h.Damage==h.HpBefore-h.HpAfter&&!h.Critical&&h.RandomPercent==100&&
            h.PreparedBasePower is null&&h.PreparedFuryCutterCounter is null&&h.PreparedAnimationPower is null=>(h.HpBefore,h.HpAfter),
        _=>null
    };

    /// <summary>Frozen party-HP tier at the actual eligible PP pass. Uses only the
    /// forward event-time display cursor, never the later committed battle frame.</summary>
    public static byte? ActorHpQuarterTierAtInvocation(CampaignMoveAnimationDispatch dispatch,
        CampaignBattleDisplayBodies displayed,IReadOnlyList<SourceBattleEvent> events,int nextEventIndex,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        int count=displayed.Bodies.Length,actor=dispatch.Actor.Value;
        if(count is not(2 or 4)||displayed.Members.Length!=count||actor<0||actor>=count||
            displayed.Members.Where((m,i)=>m.Side!=new BattlerSlot(i).Side||m.Index is <0 or >=6).Any()||
            displayed.Members.Distinct().Count()!=count||dispatch.Boundary!=CampaignMoveAnimationBoundary.AfterAttackText)return null;
        if(nextEventIndex<0||nextEventIndex>events.Count)return null;
        int at=nextEventIndex<events.Count&&events[nextEventIndex].Sequence==dispatch.Sequence?nextEventIndex:nextEventIndex-1;
        if(at<0||events[at].Sequence!=dispatch.Sequence||events[at] is not SourceMoveUsedEvent used||
            Definition(used.Move,moves) is not {Effect:"EFFECT_ERUPTION"} definition)return null;
        int pp=-1;
        for(int i=0;i<events.Count;i++)
        {
            if(i>0&&events[i].Sequence<=events[i-1].Sequence)return null;
            if(events[i].Sequence==dispatch.PpSequence)pp=i;
        }
        if(pp<=at)return null;
        var owned=AtAttackText(new(dispatch.BattleId,events.ToArray(),at,count,()=>null,_=>null),moves,
            (id,move)=>id==definition.SourceMoveId&&move==definition.Id.Value).Dispatch;
        if(owned!=dispatch)return null;
        var snapshot=displayed;
        for(int i=nextEventIndex;i<=pp;i++)
        {
            // A later spread pass may follow a real target result. Check its HP
            // provenance before applying the shared reducer. Identity changes
            // cannot enter this bounded source invocation.
            if(events[i] is SourceHpChangedEvent hp&&(!ValidHp(hp.Target,hp.HpBefore,hp.HpAfter)||
                hp.Actor!=used.Actor||hp.Move!=used.Move||hp.Turn!=used.Turn||hp.Target.Side==used.Actor.Side||
                hp.Damage<0||hp.HpAfter!=Math.Max(0,hp.HpBefore-hp.Damage)||
                events.Skip(at+1).Take(i-at-1).OfType<SourcePpCommandEvent>().LastOrDefault() is not {} hitPass||
                hitPass.Actor!=used.Actor||hitPass.PpOwner!=used.Actor||hitPass.CurrentMove!=used.Move||
                hitPass.Target!=hp.Target||hitPass.Sequence>=hp.Sequence))return null;
            if(events[i] is SourceRecoveredEvent recovery&&!ValidRecovery(recovery,i))return null;
            if(events[i] is SourceAbilityRecoveredEvent recovered&&(!ValidHp(recovered.Side,recovered.HpBefore,recovered.HpAfter)||
                recovered.HpAfter<recovered.HpBefore||recovered.Recovered!=recovered.HpAfter-recovered.HpBefore))return null;
            if(events[i] is SourceHeldItemHealEvent healed&&(!ValidHp(healed.Actor,healed.HpBefore,healed.HpAfter)||
                healed.HpAfter<healed.HpBefore||healed.HpRestored!=healed.HpAfter-healed.HpBefore))return null;
            if(events[i] is SourceHeldItemConsumedEvent item&&(!ValidHp(item.Side,item.HpBefore,item.HpAfter)||
                item.HpAfter<item.HpBefore||item.HpRestored!=item.HpAfter-item.HpBefore))return null;
            // This family publishes active party synchronization only after
            // all target passes. It cannot authorize a pre-PP body replacement.
            if(events[i] is SourceCalledPartyChangedEvent or SourceTransformedEvent or SourcePlayerSwitchedEvent or
                SourcePlayerReplacedEvent or SourceCalledPartySwitchedEvent or SourceRewardAppliedEvent)return null;
            var next=snapshot.Apply(events[i]);
            // No other receipt may silently write the actor's party HP on this
            // non-contact spread path. Add new real semantics at their owner.
            if(next.Bodies[actor].Pokemon.CurrentHp!=snapshot.Bodies[actor].Pokemon.CurrentHp&&events[i] is not
                (SourceHpChangedEvent or SourceAbilityRecoveredEvent or SourceHeldItemHealEvent or SourceHeldItemConsumedEvent))return null;
            snapshot=next;
        }
        var pokemon=WildBattleTransformationRules.PersistentPokemon(snapshot.Bodies[actor]);
        int hpNow=pokemon.CurrentHp,maximum=pokemon.Stats.MaxHp;
        if(maximum is <1 or >65535||hpNow<1||hpNow>maximum)return null;
        int quarter=maximum/4;
        for(byte tier=0;tier<3;tier++)if(hpNow<quarter*(tier+1))return tier;
        return 3;

        bool ValidRecovery(SourceRecoveredEvent recovery,int index)
        {
            // Route11WaterAbsorb emits the matching prevention immediately
            // before its recovery, under the current target's actual PP pass.
            if(!ValidHp(recovery.Side,recovery.HpBefore,recovery.HpAfter)||recovery.Turn!=used.Turn||
                recovery.Move!=used.Move||recovery.Side.Side==used.Actor.Side||recovery.HpAfter<recovery.HpBefore||
                recovery.Recovered!=recovery.HpAfter-recovery.HpBefore||definition.Type!="TYPE_WATER"||index<=at+1||
                events[index-1] is not SourceAbilityPreventedEvent prevention||prevention.Turn!=used.Turn||
                prevention.Sequence<=used.Sequence||prevention.Sequence+1!=recovery.Sequence||prevention.Actor!=used.Actor||
                prevention.Target!=recovery.Side||prevention.Move!=used.Move||prevention.Ability!="ABILITY_WATER_ABSORB")return false;
            var pass=events.Skip(at+1).Take(index-at-1).OfType<SourcePpCommandEvent>().LastOrDefault();
            if(pass is null||pass.Actor!=used.Actor||pass.PpOwner!=used.Actor||pass.Target!=recovery.Side||
                pass.CurrentMove!=used.Move||pass.Sequence>=prevention.Sequence)return false;
            int recoveryMaximum=snapshot.Bodies[recovery.Side.Value].Pokemon.Stats.MaxHp;
            return recovery.HpAfter==Math.Min(recoveryMaximum,recovery.HpBefore+Math.Max(1,recoveryMaximum/4));
        }

        bool ValidHp(BattlerSlot slot,int before,int after)=>slot.Value>=0&&slot.Value<count&&
            before==snapshot.Bodies[slot.Value].Pokemon.CurrentHp&&after>=0&&
            after<=WildBattleTransformationRules.PersistentPokemon(snapshot.Bodies[slot.Value]).Stats.MaxHp;
    }
}
