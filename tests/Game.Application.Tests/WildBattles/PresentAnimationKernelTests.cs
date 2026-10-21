using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Change=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class PresentAnimationKernelTests
{
    private static SourceBattleFrame Frame(bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("DELIBIRD","PRESENT") with{AccuracyStage=12,SpeedStage=12};
        return Kernel.Frame(bodies,single);
    }
    private static SourceBattleEvent? Result(ImmutableArray<SourceBattleEvent> events)=>events.FirstOrDefault(e=>
        e is SourceHpChangedEvent h&&h.Move==Kernel.Id("PRESENT")||e is SourceSubstituteDamagedEvent s&&s.Move==Kernel.Id("PRESENT")||e is SourceRecoveredEvent r&&r.Move==Kernel.Id("PRESENT"));
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,SourceBattleScene scene=SourceBattleScene.On)=>single?
        AnimationDispatchFixture.AtAttackText(Envelope.Single(events,index),true,scene):AnimationDispatchFixture.AtAttackText(Envelope.Double(events,index),scene);
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events,bool single,bool healing,bool substitute=false)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var result=Result(events);Assert.IsNotNull(result);
        CampaignMoveAnimationDispatch? owned=null;
        for(int i=0;i<events.Length;i++)
        {
            var decision=Project(events,i,single);Assert.AreEqual(decision,Project(events,i,single));Assert.IsNull(Project(events,i,single,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Envelope.Single(events,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Envelope.Double(events,i),SourceBattleScene.On).Dispatch);
            if(events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            owned=decision.Dispatch;Assert.IsNotNull(owned,decision.Reason);Assert.AreEqual(217,owned.SourceMoveId);Assert.AreEqual(used.Actor,owned.Actor);Assert.AreEqual(used.Target,owned.Target);
            Assert.AreEqual(result.Sequence,owned.ResultSequence);Assert.AreEqual(healing,owned.IsHealingMove);Assert.AreEqual(substitute,owned.HitsSubstitute);Assert.IsNull(owned.MoveDamage);
            Assert.IsTrue(owned.AcknowledgesCurrentReceipt);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,owned.Boundary);
        }
        Assert.IsNotNull(owned);Assert.AreEqual(before,JsonSerializer.Serialize(events));return owned;
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single)
    {for(int i=0;i<events.Length;i++)Assert.IsNull(Project(events,i,single).Dispatch,JsonSerializer.Serialize(events));}

    [TestMethod]
    public void RealDamageAndCappedRecoverySelectExactPredicateForEveryActorAndPartner()
    {
        foreach(bool single in new[]{true,false})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})
        {
            var frame=Change.Body(Frame(single,actor),target,b=>b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp-1}});
            var damage=Kernel.Find(frame,actor,target,r=>Result(r.Events) is SourceHpChangedEvent {Damage:>0},"Present damaging branch");Check(damage.Events,single,false);
            var heal=Kernel.Find(frame,actor,target,r=>Result(r.Events) is SourceRecoveredEvent {Recovered:>0},"Present capped healing branch");var recovered=(SourceRecoveredEvent)Result(heal.Events)!;
            Assert.AreEqual(1,recovered.Recovered);Assert.IsTrue(frame.Slots[target].Body!.Pokemon.Stats.MaxHp/4>recovered.Recovered);Check(heal.Events,single,true);
        }
    }

    [TestMethod]
    public void SubstituteDamageAndHealingBypassRetainSeparateMaterialOwnership()
    {
        foreach(bool single in new[]{true,false})
        {
            var frame=Change.Body(Frame(single),1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp/2},CalledVolatile=new(){SubstituteHp=1}});
            var damage=Kernel.Find(frame,0,1,r=>Result(r.Events) is SourceSubstituteDamagedEvent,"Present substitute hit");Check(damage.Events,single,false,true);
            var heal=Kernel.Find(frame,0,1,r=>Result(r.Events) is SourceRecoveredEvent {Recovered:>0},"Present heals body through substitute");Check(heal.Events,single,true,false);
            Assert.AreEqual(1,heal.State.Slots[1].Body!.CalledVolatile!.SubstituteHp);
        }
    }

    [TestMethod]
    public void EndureAndFocusBandZeroRegisterSelectHealingVisualDespiteNoGameplayRecovery()
    {
        foreach(bool single in new[]{true,false})foreach(bool band in new[]{false,true})
        {
            var frame=Change.Body(Frame(single),1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1},Enduring=!band,HeldItem=band?"ITEM_FOCUS_BAND":b.HeldItem});
            var result=Kernel.Find(frame,0,1,r=>Result(r.Events) is SourceHpChangedEvent {Damage:0,HpBefore:1,HpAfter:1}&&r.Events.OfType<SourceDamageAdjustedEvent>().Any(x=>band?x.HungOn:x.Endured),"Present zero damage register");
            Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any());Check(result.Events,single,true);
        }
    }

    [TestMethod]
    public void GhostHealClearsTypeImmunityWhileDamageFullHpAndProtectNeverInventRoots()
    {
        foreach(bool single in new[]{true,false})
        {
            var frame=Change.Body(Frame(single),1,_=>Kernel.Mon("GASTLY","LICK",100));
            frame=Change.Body(frame,1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp-1}});
            Check(Kernel.Find(frame,0,1,r=>Result(r.Events) is SourceRecoveredEvent {Recovered:>0},"Present heals Ghost").Events,single,true);
            Deny(Kernel.Find(frame,0,1,r=>r.Events.OfType<SourceEffectivenessEvent>().Any(x=>x.Effectiveness==WildBattleEffectiveness.NoEffect),"Present immune damage").Events,single);
            Deny(Kernel.Find(Frame(single),0,1,r=>r.Events.OfType<SourceMoveFailedEvent>().Any(x=>x.Reason=="already-full-hp"),"Present full HP heal rejection").Events,single);
            Deny(Kernel.Ordered(Change.Body(Frame(single),1,b=>b with{Protected=true}),0,1).Events,single);
        }
    }

    [TestMethod]
    public void WonderGuardPreservesItsMissedFlagForBothPresentBranches()
    {
        foreach(bool single in new[]{true,false})
        {
            var frame=Change.Body(Frame(single),1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp-1},CalledOverlay=new(){Ability="ABILITY_WONDER_GUARD"}});
            var heal=Kernel.Find(frame,0,1,r=>Result(r.Events) is SourceRecoveredEvent {Recovered:0},"Present Wonder Guard healing branch");
            var recovery=(SourceRecoveredEvent)Result(heal.Events)!;Assert.AreEqual(recovery.HpBefore,recovery.HpAfter);Deny(heal.Events,single);
            Deny(Kernel.Find(frame,0,1,r=>r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WONDER_GUARD"),"Present Wonder Guard damaging branch").Events,single);
        }
    }

    [TestMethod]
    public void ActualCriticalRageCureAndShellBellResultsKeepTheirOwnTailReceipts()
    {
        foreach(bool single in new[]{true,false})
        {
            Check(Kernel.Find(Frame(single),0,1,r=>Result(r.Events) is SourceHpChangedEvent {Damage:>0,Critical:true},"Present critical").Events,single,false);
            // BeginActions clears Rage for any chosen move other than RAGE.
            // Keep the authentic flag by selecting a natural learned Rage.
            var rage=Change.Body(Frame(single),1,_=>Kernel.Mon("DODUO","RAGE",100) with{Rage=true,SpeedStage=0});
            Check(Kernel.Find(rage,0,1,r=>Result(r.Events) is SourceHpChangedEvent {Damage:>0}&&r.Events.OfType<SourceStatChangedEvent>().Any(),"Present Rage tail").Events,single,false);
            var bell=Change.Body(Frame(single),0,b=>b with{HeldItem="ITEM_SHELL_BELL",Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp-1}});
            Check(Kernel.Find(bell,0,1,r=>r.Events.OfType<SourceHeldItemHealEvent>().Any(),"Present capped Shell Bell").Events,single,false);
            var berry=Change.Body(Frame(single),1,b=>b with{Status=WildBattleStatus.Poison,HeldItem="ITEM_PECHA_BERRY"});
            Check(Kernel.Find(berry,0,1,r=>Result(r.Events) is SourceHpChangedEvent {Damage:>0}&&r.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"Present common cure tail").Events,single,false);
        }
    }

    [TestMethod]
    public void MissingDuplicateForeignOrMixedReceiptsFailClosedAndLegacySinglesKeepTheSameInput()
    {
        foreach(bool single in new[]{true,false})
        {
            var frame=Change.Body(Frame(single),1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp-1}});
            var events=Kernel.Find(frame,0,1,r=>Result(r.Events) is SourceRecoveredEvent {Recovered:>0},"Present receipt mutation seed").Events;
            var recovery=(SourceRecoveredEvent)Result(events)!;var pp=events.OfType<SourcePpCommandEvent>().Single();var used=events.OfType<SourceMoveUsedEvent>().Single();
            Deny(events.Remove(recovery),single);Deny(Shared.Number(events.Insert(events.IndexOf(recovery),recovery)),single);
            Deny(events.Replace(pp,pp with{PpOwner=new(1)}),single);Deny(events.Replace(recovery,recovery with{Side=new(0)}),single);
            Deny(events.Replace(recovery,recovery with{HpAfter=recovery.HpBefore,Recovered=0}),single);
            Deny(events.Replace(recovery,recovery with{Turn=recovery.Turn+1}),single);Deny(events.Replace(recovery,recovery with{Sequence=pp.Sequence}),single);
            Deny(events.Replace(recovery,recovery with{Recovered=recovery.Recovered+1}),single);
            Deny(events.Replace(recovery,recovery with{HpAfter=recovery.HpAfter+1,Recovered=recovery.Recovered+1}),single);
            var sync=events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.SourceSlot==used.Target);
            Deny(Shared.Number(events.Remove(sync)),single);Deny(Shared.Number(events.Add(sync)),single);
            Deny(events.Replace(sync,sync with{SourceSlot=used.Actor}),single);Deny(events.Replace(sync,sync with{TransientMoveSlots=null}),single);
            Deny(events.Replace(sync,sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{CurrentHp=sync.After.Pokemon.CurrentHp-1}}}),single);
            Deny(Shared.Number(events.Add(new SourceMoveFailedEvent(0,used.Turn,used.Target,used.Actor,Kernel.Id("TACKLE"),"foreign"))),single);
            Deny(Shared.Number(events.Add(new SourceRecoveredEvent(0,used.Turn,used.Actor,Kernel.Id("RECOVER"),50,51,1))),single);
            Deny(Shared.Number(events.Add(new SourceHpChangedEvent(0,used.Turn,used.Target,used.Actor,Kernel.Id("TACKLE"),100,90,10,false,100))),single);
            Deny(Shared.Number(events.Insert(events.IndexOf(recovery),new SourceHpChangedEvent(recovery.Sequence,recovery.Turn,used.Actor,used.Target,used.Move,100,90,10,false,100))),single);
            if(single)
            {
                int index=events.IndexOf(used);var legacy=Envelope.Single(events,index);var cue=AnimationDispatchFixture.AtAttackText(legacy,true,SourceBattleScene.On).Dispatch;
                Assert.IsNotNull(cue);Assert.AreEqual(true,cue.IsHealingMove);
            }
        }
    }
    [TestMethod]
    public void SharedDamageRouteRejectsMalformedOwnedHitAndPreservesItsZeroBoundary()
    {
        foreach(bool single in new[]{true,false})
        {
            var events=Kernel.Find(Frame(single),0,1,r=>Result(r.Events) is SourceHpChangedEvent {Damage:>0},"Present damage mutations").Events;
            var hit=(SourceHpChangedEvent)Result(events)!;
            Deny(events.Replace(hit,hit with{Damage=hit.Damage+1}),single);Deny(events.Replace(hit,hit with{Turn=hit.Turn+1}),single);
            Deny(events.Replace(hit,hit with{Damage=-1}),single);Deny(events.Replace(hit,hit with{RandomPercent=84}),single);
            Deny(Shared.Number(events.Insert(events.IndexOf(hit),hit)),single);
        }
    }
}
