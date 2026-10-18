using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Change=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class VoltTackleAnimationKernelTests
{
    // Controlled typed-kernel move slots, NOT natural acquisition. The pinned
    // FireRed daycare comments out both GiveVoltTackleIfLightBall and its call;
    // no level-up or active egg route supplies this slot. Actual Metronome below
    // proves public source invocation independently through the shared
    // authenticated caller contract, without claiming natural acquisition.
    private static SourceBattleFrame Frame(bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        var body=Kernel.Mon("PIKACHU","THUNDER_SHOCK");var move=Kernel.Id("VOLT_TACKLE");var definition=Kernel.Mechanics.Catalog.Moves[move];
        Assert.AreEqual("EFFECT_DOUBLE_EDGE",definition.Effect);Assert.AreEqual("TYPE_ELECTRIC",definition.Type);Assert.AreEqual(120,definition.Power);
        bodies[actor]=body with{AccuracyStage=12,SpeedStage=12,Pokemon=body.Pokemon with{Moves=[new(move,definition.Pp,definition.Pp)]}};
        return Kernel.Frame(bodies,single);
    }
    private static void Check(ImmutableArray<SourceBattleEvent> events,bool single,bool expected=true)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var hits=Envelope.Hits(events);int roots=0;
        Assert.AreEqual(Kernel.Id("VOLT_TACKLE"),used.Move);Assert.AreEqual(used.Move,pp.CurrentMove);Assert.AreEqual(used.Move,pp.ChosenMove);Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.Slot,pp.PpSlot);Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);
        for(int i=0;i<events.Length;i++)
        {
            var decision=Kernel.Project(events,i,single);Assert.AreEqual(decision,Kernel.Project(events,i,single));Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Envelope.Single(events,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Envelope.Double(events,i),SourceBattleScene.On).Dispatch);
            if(!expected||events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);roots++;Assert.AreEqual(1,hits.Length);Assert.AreEqual(344,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(hits[0].Sequence,cue.ResultSequence);Assert.AreEqual(hits[0] is SourceSubstituteDamagedEvent,cue.HitsSubstitute);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsNull(cue.MoveTurn);Assert.IsNull(cue.MoveDamage);Assert.IsNull(cue.MovePower);
        }
        Assert.AreEqual(expected?1:0,roots);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    [TestMethod]
    public void ControlledTypedSlotsUseTheSharedHitMatcherAndKeepLaterOneThirdRecoilSeparate()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Frame(single,actor);string before=JsonSerializer.Serialize(frame);var result=Kernel.Ordered(frame,actor,actor^1);var hit=result.Events.OfType<SourceHpChangedEvent>().Single();var recoil=result.Events.OfType<SourceRecoilEvent>().Single();
            Assert.AreEqual(actor,recoil.Actor.Value);Assert.AreEqual(Kernel.Id("VOLT_TACKLE"),recoil.Move);Assert.IsTrue(hit.Sequence<recoil.Sequence);Assert.AreEqual(Math.Max(1,(hit.HpBefore-hit.HpAfter)/3),recoil.Damage);Check(result.Events,single);Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Kernel.Ordered(frame,actor,actor^1)));
        }
    }
    [TestMethod]
    public void ControlledSubstituteTargetKnockoutAndRecoilKnockoutRetainTheOriginalRoot()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"substitute","target-ko","actor-ko"})
        {
            var frame=Frame(single);frame=branch=="actor-ko"?Change.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}}):Change.Body(frame,1,b=>branch=="target-ko"?b with{Pokemon=b.Pokemon with{CurrentHp=1}}:b with{CalledVolatile=new(){SubstituteHp=1}});
            var result=Kernel.Ordered(frame);Check(result.Events,single);if(branch=="substitute")Assert.AreEqual(1,result.Events.OfType<SourceSubstituteDamagedEvent>().Count());else{int fainted=branch=="actor-ko"?0:1;Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==fainted));Assert.AreEqual(0,result.State.Parties[fainted].Members[0].Pokemon.CurrentHp);}
        }
    }
    [TestMethod]
    public void ControlledProtectedAndNaturallyImmuneTargetsDoNotBorrowTheRoot()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"protect","immune"})
        {var frame=Change.Body(Frame(single),1,b=>branch=="protect"?b with{Protected=true}:Kernel.Mon("DIGLETT","SCRATCH",100) with{SpeedStage=0});var result=Envelope.Resolve(frame);Check(result.Events,single,false);Assert.IsFalse(result.Events.OfType<SourceRecoilEvent>().Any());}
    }
    [TestMethod]
    public void ActualMetronome344CommitsItsLeafAtTheOriginalAttackTextBoundary()
    {
        // Natural Togetic learns Metronome. The bounded seed chooses through the
        // real source selector; no Volt Tackle slot or called receipt is inserted.
        uint seed=Caller.MetronomeSeed(344);
        var caller=Kernel.Id("METRONOME");var leaf=Kernel.Id("VOLT_TACKLE");
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Kernel.Frame(Caller.Bodies(118,actor),single) with{Rng=new(seed)};
            Assert.IsTrue(frame.Slots.Where(s=>s.Body is not null).All(s=>s.Body!.Pokemon.Moves.All(m=>m.Move!=leaf)));
            string before=JsonSerializer.Serialize(frame);var result=Envelope.Resolve(frame,actor,actor^1);
            string committed=JsonSerializer.Serialize(result);var events=result.Events;
            var selection=events.OfType<SourceCallerSelectionEvent>().Single();
            var transition=events.OfType<SourceCalledMoveTransitionEvent>().Single();var invocation=transition.Invocation;
            var callerAttack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==caller);
            var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==leaf);
            var used=events.OfType<SourceMoveUsedEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
            var hit=events.OfType<SourceHpChangedEvent>().Single();var recoil=events.OfType<SourceRecoilEvent>().Single();
            Assert.AreEqual(1,Envelope.Hits(events).Length);
            Assert.AreEqual(caller,selection.Caller);Assert.AreEqual(leaf,selection.Result);Assert.AreEqual(actor,selection.Actor.Value);
            Assert.AreEqual(caller,invocation.OriginalChosenMove);Assert.AreEqual(caller,invocation.ChosenMove);Assert.AreEqual(leaf,invocation.CurrentMove);
            Assert.AreEqual(actor,invocation.Actor.Value);Assert.AreEqual(invocation.Actor,invocation.PpOwner);Assert.AreEqual(0,invocation.ChosenSlot);Assert.AreEqual(0,invocation.PpSlot);
            Assert.AreEqual(caller,attack.ChosenMove);Assert.AreEqual(invocation.Actor,attack.Actor);
            Assert.AreEqual(leaf,used.Move);Assert.AreEqual(invocation.Actor,used.Actor);Assert.AreEqual(invocation.Target,used.Target);Assert.AreEqual(invocation.PpSlot,used.Slot);
            Assert.AreEqual(leaf,pp.CurrentMove);Assert.AreEqual(caller,pp.ChosenMove);Assert.AreEqual(invocation.PpOwner,pp.PpOwner);Assert.AreEqual(invocation.PpSlot,pp.PpSlot);
            Assert.AreEqual(used.Actor,pp.Actor);Assert.AreEqual(used.Target,pp.Target);Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);
            Assert.AreEqual(1,pp.CommandCost);Assert.AreEqual(pp.Before-pp.CommandCost,pp.After);
            Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp,pp.Before);
            Assert.AreEqual(pp.After,result.State.Parties[actor&1].Members[actor/2].Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual(leaf,hit.Move);Assert.AreEqual(used.Actor,hit.Actor);Assert.AreEqual(used.Target,hit.Target);Assert.IsTrue(hit.HpAfter<hit.HpBefore);
            Assert.AreEqual(leaf,recoil.Move);Assert.AreEqual(used.Actor,recoil.Actor);Assert.AreEqual(Math.Max(1,(hit.HpBefore-hit.HpAfter)/3),recoil.Damage);
            Assert.IsTrue(callerAttack.Sequence<selection.Sequence&&selection.Sequence<transition.Sequence&&transition.Sequence<attack.Sequence&&
                attack.Sequence<used.Sequence&&used.Sequence<pp.Sequence&&pp.Sequence<hit.Sequence&&hit.Sequence<recoil.Sequence);
            int preludes=0,leaves=0;
            for(int i=0;i<events.Length;i++)
            {
                var decision=Kernel.Project(events,i,single);Assert.AreEqual(decision,Kernel.Project(events,i,single));
                Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);Assert.IsNull(Caller.Result(events,i,single).Dispatch);
                if(events[i]==callerAttack)
                {
                    var prelude=decision.Dispatch;Assert.IsNotNull(prelude,decision.Reason);preludes++;
                    Assert.AreEqual(118,prelude.SourceMoveId);Assert.AreEqual(callerAttack.Sequence,prelude.Sequence);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterCallerText,prelude.Boundary);
                }
                else if(events[i]==used)
                {
                    var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);leaves++;
                    Assert.AreEqual(344,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);
                    Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);
                    Assert.AreEqual(hit.Sequence,cue.ResultSequence);Assert.AreEqual(pp.Sequence,cue.SnapshotSequence);
                    Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
                    Assert.IsFalse(cue.HitsSubstitute);Assert.IsNull(cue.MoveTurn);Assert.IsNull(cue.MovePower);Assert.IsNull(cue.MoveDamage);
                    if(single)Assert.AreEqual(cue,AnimationDispatchFixture.AtAttackText(Envelope.Single(events,i) with{SourceEvents=[]},true,SourceBattleScene.On).Dispatch);
                }
                else Assert.IsNull(decision.Dispatch);
            }
            Assert.AreEqual(1,preludes);Assert.AreEqual(1,leaves);
            Assert.AreEqual(committed,JsonSerializer.Serialize(result));Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            Assert.AreEqual(committed,JsonSerializer.Serialize(Envelope.Resolve(frame,actor,actor^1)));
        }
    }
    [TestMethod]
    public void MissingOrForeignControlledHitAndPpReceiptsCannotAuthenticateTheRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Frame(single)).Events;var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=Envelope.Hits(events).Single();
            foreach(var rows in new[]{events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),events.Remove(hit),Shared.Number(events.Insert(events.IndexOf(hit),hit))})
                for(int i=0;i<rows.Length;i++)Assert.IsNull(Kernel.Project(rows,i,single).Dispatch);
        }
    }
}
