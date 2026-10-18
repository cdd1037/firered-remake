using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Charge=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Natural learners; controlled sleep, stages, PP and Substitute are integration
// fixtures. Every positive invocation executes the public ordered-turn kernel.
[TestClass,TestCategory("Game")]
public sealed class FocusSoundAnimationKernelTests
{
    private static (string Name,string Species,int Level) Spec(int id)=>id switch{
        170=>("MIND_READER","POLIWRATH",51),199=>("LOCK_ON","MAGNEMITE",50),
        173=>("SNORE","SNORLAX",50),253=>("UPROAR","DODRIO",50),_=>throw new ArgumentOutOfRangeException(nameof(id))};
    private static MoveId Move(int id)=>Kernel.Id(Spec(id).Name);
    private static bool Focus(int id)=>id is 170 or 199;
    private static SourceBattleFrame Initial(int id,bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        var spec=Spec(id);bodies[actor]=Kernel.Mon(spec.Species,spec.Name,spec.Level) with{
            AccuracyStage=12,SpeedStage=12,Status=id==173?3u:0u};
        return Kernel.Frame(bodies,single);
    }
    private static SourceBattleFrame Ability(SourceBattleFrame frame,int slot,string ability)=>Charge.Body(frame,slot,b=>
        b with{CalledOverlay=(b.CalledOverlay??new()) with{Ability=ability}});
    private static SourceBattleFrame Opponents(SourceBattleFrame frame,int actor,Func<WildBattleCombatant,WildBattleCombatant> change)
    {
        foreach(int target in Enumerable.Range(0,frame.BattlerCount).Where(s=>(s&1)!=(actor&1)))frame=Charge.Body(frame,target,change);
        return frame;
    }
    private static SourceBattleFrame Substitute(SourceBattleFrame frame,int actor)=>Opponents(frame,actor,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> rows)=>Shared.Number(rows);
    private static void Deny(ImmutableArray<SourceBattleEvent> rows,bool single,int id)=>Caller.DenyLeaf(rows,single,id);
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> rows,bool single,int id,bool retained=false)
    {
        var attack=rows.OfType<SourceAttackStringEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().SingleOrDefault();
        var used=rows.OfType<SourceMoveUsedEvent>().SingleOrDefault();
        Assert.AreEqual(Move(id),attack.CurrentMove);Assert.AreEqual(attack.CurrentMove,attack.ChosenMove);
        SourceBattleEvent owner=retained?attack:used!;
        SourceBattleEvent result=Focus(id)?rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="sure-hit"):Envelope.Hits(rows).Single();
        if(retained){Assert.IsNull(pp);Assert.IsNull(used);}
        else
        {
            Assert.IsNotNull(pp);Assert.IsNotNull(used);Assert.AreEqual(attack.Sequence+1,used.Sequence);Assert.AreEqual(used.Sequence+1,pp.Sequence);
            Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);Assert.AreEqual(pp.Before,used.PpBefore);Assert.AreEqual(pp.After,used.PpAfter);
            Assert.AreEqual(Move(id),pp.ChosenMove);Assert.AreEqual(pp.ChosenMove,pp.CurrentMove);Assert.AreEqual(used.Actor,pp.PpOwner);
        }
        string before=JsonSerializer.Serialize(rows);int count=0;CampaignMoveAnimationDispatch? found=null;
        for(int i=0;i<rows.Length;i++)foreach(bool atResult in new[]{false,true})
        {
            var decision=Charge.Project(rows,i,single,atResult);Assert.AreEqual(decision,Charge.Project(rows,i,single,atResult));
            Assert.IsNull(Charge.Project(rows,i,single,atResult,SourceBattleScene.Off).Dispatch);
            if(single)Assert.IsNull(atResult?AnimationDispatchFixture.AtResult(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch:
                AnimationDispatchFixture.AtAttackText(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch);
            if(rows[i]!=owner||atResult){Assert.IsNull(decision.Dispatch,decision.Reason);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);found=cue;count++;
            Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(owner.Sequence,cue.Sequence);Assert.AreEqual(result.Sequence,cue.ResultSequence);
            Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp?.Sequence,cue.PpSequence);Assert.AreEqual(pp?.Sequence??attack.Sequence,cue.SnapshotSequence);
            Assert.AreEqual(attack.Actor,cue.Actor);Assert.AreEqual(result switch{SourceMtMoonVolatileEvent m=>m.Side,SourceHpChangedEvent h=>h.Target,
                SourceSubstituteDamagedEvent s=>s.Target,_=>throw new InvalidOperationException()},cue.Target);
            Assert.AreEqual(retained?CampaignMoveAnimationBoundary.AfterRetainedAttackText:CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);
            Assert.IsTrue(cue.AcknowledgesCurrentReceipt);Assert.IsNull(cue.MoveTurn);Assert.IsNull(cue.MovePower);Assert.IsNull(cue.MoveDamage);
            Assert.AreEqual(result is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(rows));return found!;
    }

    [TestMethod,DataRow(170),DataRow(199),DataRow(173),DataRow(253)]
    public void NaturalDirectInvocationsOwnTheirActualResultsAcrossAllSourcePositions(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Initial(id,single,actor);string before=JsonSerializer.Serialize(frame);var result=Charge.Ordered(frame,actor,actor^1);
            Check(result.Events,single,id);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            if(Focus(id))
            {
                var mark=result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="sure-hit");
                Assert.AreEqual(0,mark.Before);Assert.AreEqual(2,mark.After);Assert.AreEqual("move",mark.Reason);
                Assert.AreEqual(new BattlerSlot(actor),result.State.Slots[actor^1].Registers.Turn!.SureHitSource);
            }
            if(id==173)
            {
                var sleep=result.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Reason=="sleep-countdown");
                Assert.AreEqual(3u,sleep.Before);Assert.AreEqual(2u,sleep.After);Assert.IsTrue(sleep.Sequence<result.Events.OfType<SourceAttackStringEvent>().Single().Sequence);
            }
            if(id==253)
            {
                var locked=result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-uproar"&&e.Reason=="move");
                Assert.AreEqual(0,locked.Before);Assert.IsTrue(locked.After is >=2 and <=5);Assert.IsTrue(locked.Sequence>Envelope.Hits(result.Events).Single().Sequence);
            }
        }
    }

    [TestMethod,DataRow(170),DataRow(199),DataRow(173)]
    public void SelectedRootsCanUseTheirRealAlliedTargetWithoutChangingOwnership(int id)
    {
        for(int actor=0;actor<4;actor++)
        {
            var result=Charge.Ordered(Initial(id,false,actor),actor,actor^2);var cue=Check(result.Events,false,id);
            Assert.AreEqual(new BattlerSlot(actor^2),cue.Target);
        }
    }

    [TestMethod,DataRow(170),DataRow(199),DataRow(173),DataRow(253)]
    public void PressureAndFinalPpStillAuthenticateTheDirectSourceCommand(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Initial(id,single);foreach(int target in single?new[]{1}:new[]{1,3})frame=Ability(frame,target,"ABILITY_PRESSURE");
            frame=Charge.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=1}]}});
            var result=Charge.Ordered(frame,0,1);Check(result.Events,single,id);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(2,pp.CommandCost);Assert.AreEqual(1,pp.Before);Assert.AreEqual(0,pp.After);
        }
    }

    [TestMethod,DataRow(170),DataRow(199)]
    public void ReapplyingAnExistingSureHitMarkIsARealNewCommandRatherThanACountdown(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Charge.Ordered(Initial(id,single),0,1);var frame=Charge.CompleteTurn(first);
            Assert.AreEqual(1,frame.Slots[1].Registers.Turn!.SureHitTurnsRemaining);
            var second=Charge.Ordered(frame,0,1);Check(second.Events,single,id);
            var mark=second.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="sure-hit");
            Assert.AreEqual(0,mark.Before);Assert.AreEqual(2,mark.After);
            Deny(second.Events.SetItem(second.Events.IndexOf(mark),mark with{Before=2,After=1,Reason="countdown"}),single,id);
        }
    }

    [TestMethod,DataRow(170),DataRow(199)]
    public void SureHitRejectsSubstituteProtectAccuracyFailureAndActionCancellation(int id)
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"substitute","protect","miss","sleep"})
        {
            var frame=Initial(id,single);
            if(branch=="substitute")frame=Substitute(frame,0);
            if(branch=="protect")frame=Charge.Body(frame,1,b=>b with{Protected=true});
            if(branch=="sleep")frame=Charge.Body(frame,0,b=>b with{Status=3});
            if(branch=="miss"){frame=Charge.Body(frame,0,b=>b with{AccuracyStage=0});frame=Charge.Body(frame,1,b=>b with{EvasionStage=12});}
            var result=branch=="miss"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"focus accuracy miss"):Charge.Ordered(frame,0,1);
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="sure-hit"));Deny(result.Events,single,id);
        }
    }

    [TestMethod]
    public void SnoreUsesSubstituteHitAndActualFlinchButAwakeWakeSoundproofAndImmunityCannotAnimate()
    {
        foreach(bool single in new[]{false,true})
        {
            var substitute=Charge.Ordered(Substitute(Initial(173,single),0),0,1);Assert.IsTrue(Check(substitute.Events,single,173).HitsSubstitute);
            var flinch=Charge.Find(Initial(173,single),0,1,r=>r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"Snore secondary flinch");Check(flinch.Events,single,173);
            foreach(string branch in new[]{"awake","wake","soundproof","ghost","protect","miss"})
            {
                var frame=Initial(173,single);
                if(branch is "awake" or "wake")frame=Charge.Body(frame,0,b=>b with{Status=branch=="awake"?0u:1u});
                if(branch=="soundproof")frame=Ability(frame,1,"ABILITY_SOUNDPROOF");
                if(branch=="ghost")frame=Charge.Body(frame,1,_=>Kernel.Mon("GASTLY","LICK",100) with{SpeedStage=0});
                if(branch=="protect")frame=Charge.Body(frame,1,b=>b with{Protected=true});
                if(branch=="miss"){frame=Charge.Body(frame,0,b=>b with{AccuracyStage=0});frame=Charge.Body(frame,1,b=>b with{EvasionStage=12});}
                var result=branch=="miss"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Snore accuracy miss"):Charge.Ordered(frame,0,1);
                Deny(result.Events,single,173);
                if(branch is "awake" or "wake")Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="user-awake"));
                if(branch=="soundproof")Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_SOUNDPROOF"));
                if(branch=="ghost")Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));
            }
        }
    }

    [TestMethod]
    public void UproarRetainedHitsUseOnlyTheirOwnAttackStringWithZeroPpIncludingSubstitute()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var frame=Initial(253,single,actor);foreach(int target in Enumerable.Range(0,frame.BattlerCount).Where(s=>(s&1)!=(actor&1)))frame=Ability(frame,target,"ABILITY_PRESSURE");
            frame=Charge.Body(frame,actor,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=1}]}});
            var first=Charge.Ordered(frame,actor,actor^1);Check(first.Events,single,253);frame=Charge.CompleteTurn(first);
            Assert.AreEqual(0,frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(WildBattleCalledLockKind.Uproar,frame.Slots[actor].Registers.Turn!.Lock!.Kind);
            if(substitute)frame=Substitute(frame,actor);var repeat=Charge.Ordered(frame,actor,actor^1);var cue=Check(repeat.Events,single,253,true);
            Assert.AreEqual(substitute,cue.HitsSubstitute);Assert.AreNotEqual(cue.Actor.Side,cue.Target.Side);
            Assert.IsFalse(repeat.Events.Any(e=>e is SourceMoveUsedEvent or SourcePpCommandEvent));
            Assert.AreEqual(0,repeat.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.IsFalse(repeat.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="called-uproar"&&e.Reason=="move"));
        }
    }

    [TestMethod]
    public void UproarRetainedMissProtectSoundproofAndConfusionCannotBorrowTheSuccessfulNoPpProtocol()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"miss","protect","soundproof","ghost","confusion","paralysis"})
        {
            var first=Charge.Ordered(Initial(253,single),0,1);var frame=Charge.CompleteTurn(first);
            if(branch=="miss"){frame=Charge.Body(frame,0,b=>b with{AccuracyStage=0});frame=Opponents(frame,0,b=>b with{EvasionStage=12});}
            if(branch=="protect")frame=Opponents(frame,0,b=>b with{Protected=true});
            if(branch=="soundproof")foreach(int target in single?new[]{1}:new[]{1,3})frame=Ability(frame,target,"ABILITY_SOUNDPROOF");
            if(branch=="ghost")frame=Opponents(frame,0,_=>Kernel.Mon("GASTLY","LICK",100) with{SpeedStage=0});
            if(branch=="confusion")frame=Charge.Body(frame,0,b=>b with{ConfusionTurnsRemaining=3});
            if(branch=="paralysis")frame=Charge.Body(frame,0,b=>b with{Status=WildBattleStatus.Paralysis});
            var result=branch=="miss"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"retained Uproar miss"):
                branch=="confusion"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceConfusionSelfHitEvent>().Any(),"retained Uproar confusion"):
                branch=="paralysis"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceActionBlockedEvent>().Any(),"retained Uproar paralysis"):Charge.Ordered(frame,0,1);
            Deny(result.Events,single,253);
            if(branch is "miss" or "protect" or "soundproof")Assert.AreEqual(1,result.Events.OfType<SourcePpCommandEvent>().Count());
            if(branch=="soundproof")
            {var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(pp.Before,pp.After);}
        }
    }

    [TestMethod]
    public void ActualUproarResidualWakeAndExpiryNeverReplayTheMoveRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(253,single),1,b=>b with{Status=4,Nightmare=true});
            var action=Charge.Ordered(frame,0,1);Check(action.Events,single,253);bool woke=false,expired=false;int turns=0;
            while(!expired)
            {
                while(!action.CompletedCleanTurn)
                {
                    Assert.IsNotNull(action.Continuation);action=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,action.State,action.Continuation.Value);
                    Assert.IsTrue(action.Succeeded,action.Fault);Deny(action.Events,single,253);
                    woke|=action.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason=="uproar-wake");
                    expired|=action.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="called-uproar"&&e.Reason=="uproar-ended");
                }
                Assert.IsTrue(++turns<=5);if(expired)break;
                action=Charge.Ordered(action.State,0,1);Check(action.Events,single,253,true);
            }
            Assert.IsTrue(woke);Assert.IsNull(action.State.Slots[0].Registers.Turn?.Lock);
        }
    }

    [TestMethod]
    public void ActualSleepTalkSnoreKeepsCallerPpAndLeafCurrentMoveSeparate()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Caller.Bodies(214,sleepingLeaf:"SNORE");
            for(int i=1;i<4;i++)bodies[i]=bodies[i] with{SpeedStage=0};
            var frame=Kernel.Frame(bodies,single);frame=Ability(frame,1,"ABILITY_PRESSURE");
            var result=Charge.Ordered(frame,0,1);Caller.Check(result.Events,result,214,single);Deny(result.Events,single,173);
            var selected=result.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;
            Assert.AreEqual(Move(173),selected.CurrentMove);Assert.AreEqual(Kernel.Id("SLEEP_TALK"),selected.ChosenMove);
            Assert.IsTrue((selected.Flags&WildBattleInvocationFlags.NoPpDeduct)!=0);
            var leaf=result.Events.OfType<SourcePpCommandEvent>().Single(p=>p.CurrentMove==Move(173));
            Assert.AreEqual(1,leaf.PpSlot);Assert.AreEqual(Kernel.Id("SLEEP_TALK"),leaf.ChosenMove);Assert.AreEqual(leaf.Before,leaf.After);
            Assert.AreEqual(1,Envelope.Hits(result.Events).Length);
        }
    }

    private static SourceFourActionResult CalledUproar(bool single)
    {
        var bodies=Caller.Bodies(118);for(int i=1;i<4;i++)bodies[i]=bodies[i] with{SpeedStage=0};
        var frame=Kernel.Frame(bodies,single);var catalog=Kernel.Mechanics.Catalog;var caller=Kernel.Id("METRONOME");
        var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,
            c.Slot.Value==0?new BattlerSlot(1):c.Slot)).ToImmutableArray();
        for(uint seed=0;seed<65536;seed++)
        {
            var candidate=frame with{Rng=new(seed)};var started=SourceFourActionTurns.BeginActions(catalog,candidate,candidate.Revision,choices);
            Assert.IsTrue(started.Succeeded,started.Fault);Assert.AreEqual(0,started.State.Queue[0].Slot.Value);
            // Inspect the public targeting/caller rules only to choose a bounded
            // seed. The positive receipts still come from Advance, never a rewrite.
            var target=SourceMoveTargeting.ResolveLearned(started.State,new(0),new(1),catalog.Moves[caller]);Assert.IsTrue(target.Succeeded,target.Fault);
            if(WildBattleSourceCallers.Metronome(target.Rng).SourceMoveId!=253)continue;
            Assert.IsNotNull(started.Continuation);var result=SourceFourActionTurns.Advance(catalog,started.State,started.Continuation.Value);
            Assert.IsTrue(result.Succeeded,result.Fault);Assert.AreEqual(Move(253),result.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            Console.WriteLine($"Actual ordered Metronome→Uproar: single={single}, seed={seed}");return result;
        }
        throw new InvalidOperationException("No actual ordered Metronome→Uproar in 65536 turn-start seeds.");
    }

    [TestMethod]
    public void MetronomeCalledUproarIsDeniedButItsActualNextTurnRetainedInvocationIsOwned()
    {
        foreach(bool single in new[]{false,true})
        {
            var called=CalledUproar(single);Caller.Check(called.Events,called,118,single);Deny(called.Events,single,253);
            var transition=called.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;
            Assert.AreEqual(Kernel.Id("METRONOME"),transition.ChosenMove);Assert.AreEqual(Move(253),transition.CurrentMove);
            var leaf=called.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(Kernel.Id("METRONOME"),leaf.ChosenMove);Assert.AreEqual(Move(253),leaf.CurrentMove);
            Assert.AreEqual(0,leaf.PpSlot);Assert.AreEqual(leaf.Before-leaf.CommandCost,leaf.After);
            var frame=Charge.CompleteTurn(called);Assert.AreEqual(WildBattleCalledLockKind.Uproar,frame.Slots[0].Registers.Turn!.Lock!.Kind);
            var repeat=Charge.Ordered(frame,0,1);Check(repeat.Events,single,253,true);
            var attack=repeat.Events.OfType<SourceAttackStringEvent>().Single();Assert.AreEqual(Move(253),attack.ChosenMove);Assert.AreEqual(Move(253),attack.CurrentMove);
            Assert.AreEqual(Kernel.Id("METRONOME"),repeat.State.Slots[0].Body!.Pokemon.Moves[0].Move);
            Assert.AreEqual(frame.Slots[0].Body!.Pokemon.Moves[0].CurrentPp,repeat.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
        }
    }

    [TestMethod,DataRow(170),DataRow(199),DataRow(173),DataRow(253)]
    public void DirectRootsRejectForeignDuplicatedOrMissingHeadersPpAndResults(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(id,single),0,1).Events;var attack=rows.OfType<SourceAttackStringEvent>().Single();
            var used=rows.OfType<SourceMoveUsedEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().Single();
            int ai=rows.IndexOf(attack),ui=rows.IndexOf(used),pi=rows.IndexOf(pp);
            foreach(var bad in new[]{attack with{Actor=new(1)},attack with{ChosenMove=Kernel.Id("METRONOME")},attack with{CurrentMove=Kernel.Id("TACKLE")},attack with{Turn=attack.Turn+1}})
                Deny(rows.SetItem(ai,bad),single,id);
            foreach(var bad in new[]{pp with{Actor=new(1)},pp with{PpOwner=new(1)},pp with{Target=new(0)},pp with{PpSlot=3},
                pp with{CommandCost=0},pp with{ChosenMove=Kernel.Id("SLEEP_TALK")},pp with{CurrentMove=Kernel.Id("TACKLE")},pp with{After=pp.Before}})
                Deny(rows.SetItem(pi,bad),single,id);
            Deny(Number(rows.Insert(pi,pp)),single,id);Deny(Number(rows.RemoveAt(pi)),single,id);Deny(Number(rows.RemoveAt(ui)),single,id);
            SourceBattleEvent outcome=Focus(id)?rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="sure-hit"):Envelope.Hits(rows).Single();int oi=rows.IndexOf(outcome);
            Deny(Number(rows.RemoveAt(oi)),single,id);Deny(Number(rows.Insert(oi,outcome)),single,id);
            if(outcome is SourceMtMoonVolatileEvent mark)
            {
                foreach(var bad in new[]{mark with{Side=new(0)},mark with{Before=1},mark with{After=1},mark with{Reason="countdown"},mark with{Volatile="identified"},mark with{Turn=mark.Turn+1}})
                    Deny(rows.SetItem(oi,bad),single,id);
                Deny(Number(rows.Add(new SourceMoveFailedEvent(0,used.Turn,used.Actor,used.Target,used.Move,"substitute"))),single,id);
                Deny(Number(rows.Add(new SourceMtMoonVolatileEvent(0,used.Turn,used.Actor,"foreign",0,1,"move"))),single,id);
            }
            else if(outcome is SourceHpChangedEvent hit)
            {
                Deny(rows.SetItem(oi,hit with{Move=Kernel.Id("TACKLE")}),single,id);
                Deny(rows.SetItem(oi,hit with{Actor=new(1)}),single,id);
                Deny(rows.SetItem(oi,hit with{Target=new(0)}),single,id);
                Deny(Number(rows.Insert(oi,new SourceMoveMissedEvent(0,used.Turn,used.Actor,used.Target,used.Move))),single,id);
            }
        }
    }

    [TestMethod]
    public void RetainedUproarRejectsBorrowedPpCalledHeadersDuplicateHitsAndAlliedTargets()
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Charge.Ordered(Initial(253,single),0,1);var repeat=Charge.Ordered(Charge.CompleteTurn(first),0,1);var rows=repeat.Events;
            var hit=rows.OfType<SourceHpChangedEvent>().Single();var attack=rows.OfType<SourceAttackStringEvent>().Single();int hi=rows.IndexOf(hit),ai=rows.IndexOf(attack);
            Deny(Number(rows.Insert(hi,first.Events.OfType<SourcePpCommandEvent>().Single())),single,253);
            Deny(Number(rows.Insert(hi,hit)),single,253);Deny(Number(rows.RemoveAt(hi)),single,253);
            foreach(var bad in new[]{hit with{Actor=new(1)},hit with{Target=new(single?0:2)},hit with{Move=Kernel.Id("TACKLE")},hit with{Turn=hit.Turn+1},hit with{Sequence=attack.Sequence}})
                Deny(rows.SetItem(hi,bad),single,253);
            Deny(rows.SetItem(ai,attack with{ChosenMove=Kernel.Id("METRONOME")}),single,253);
            Deny(Number(rows.Insert(hi,first.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-uproar"))),single,253);
        }
    }

    [TestMethod]
    public void RetainedUproarWithNoOpponentAfterTwoRealKnockoutsHasOnlyAFailureInvocation()
    {
        var first=Charge.Ordered(Initial(253,false,3),3,2);var frame=Charge.CompleteTurn(first);
        var a=Kernel.Mon("RATTATA","QUICK_ATTACK",100) with{SpeedStage=7};a=a with{Pokemon=a.Pokemon with{CurrentHp=1}};
        frame=Charge.Body(frame,0,_=>a);frame=Charge.Body(frame,1,_=>Kernel.Mon("RATTATA","QUICK_ATTACK",100));
        frame=Charge.Body(frame,2,_=>Kernel.Mon("PIDGEY","TACKLE",1));frame=Charge.Body(frame,3,b=>b with{SpeedStage=0});
        var reserve=Kernel.Mon("WAILORD","SPLASH",100);frame=frame with{Parties=frame.Parties.SetItem(0,frame.Parties[0] with{Members=frame.Parties[0].Members.Add(new(reserve.Pokemon))})};
        int[] targets=[2,0,1,0];var choices=frame.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,
            c.Registers.Turn?.Lock?.Target??new BattlerSlot(targets[c.Slot.Value]))).ToImmutableArray();
        var result=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(result.Succeeded,result.Fault);
        var fainted=new HashSet<int>();int steps=0;
        while(!result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="deferred-ko-no-target"))
        {
            Assert.IsTrue(++steps<=12);Assert.IsNotNull(result.Continuation);
            result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,result.State,result.Continuation.Value);Assert.IsTrue(result.Succeeded,result.Fault);
            foreach(var faint in result.Events.OfType<SourceFaintedEvent>())fainted.Add(faint.Side.Value);
        }
        CollectionAssert.AreEquivalent(new[]{0,2},fainted.ToArray());Assert.AreEqual(1,result.Events.OfType<SourcePpCommandEvent>().Count());
        Assert.IsFalse(Envelope.Hits(result.Events).Any());Deny(result.Events,false,253);
    }

    [TestMethod,DataRow(170),DataRow(199),DataRow(173),DataRow(253)]
    public void EffectMetadataScriptAndSceneStayRequiredForEverySelectedRoot(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Charge.Ordered(Initial(id,single),0,1);var batches=new List<ImmutableArray<SourceBattleEvent>>{first.Events};
            if(id==253)batches.Add(Charge.Ordered(Charge.CompleteTurn(first),0,1).Events);
            foreach(var rows in batches)
            {
                int owner=rows.IndexOf((SourceBattleEvent?)rows.OfType<SourceMoveUsedEvent>().SingleOrDefault()??rows.OfType<SourceAttackStringEvent>().Single());
                var moves=Kernel.Mechanics.Catalog.Moves;var move=Move(id);
                foreach(var metadata in new[]{null,moves.Remove(move),moves.SetItem(move,moves[move] with{Power=Focus(id)?1:0}),
                    moves.SetItem(move,moves[move] with{Target="MOVE_TARGET_USER"}),moves.SetItem(move,moves[move] with{Target="MOVE_TARGET_UNKNOWN"}),
                    moves.SetItem(move,moves[move] with{SourceMoveId=1})})
                    Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Charge.Single(rows,owner),true,SourceBattleScene.On,metadata).Dispatch:
                        AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,owner),SourceBattleScene.On,metadata).Dispatch);
                Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(rows,owner),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                    CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows,owner),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
            }
        }
    }
}
