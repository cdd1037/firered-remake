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
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class SolarBeamAnimationKernelTests
{
    private static MoveId Move=>Kernel.Id("SOLAR_BEAM");
    private static WildBattleCombatant Solar()=>Kernel.Mon("BULBASAUR","SOLAR_BEAM",60) with{SpeedStage=12,AccuracyStage=12};
    private static SourceBattleFrame Sun(SourceBattleFrame frame)=>frame with{Field=frame.Field with{
        PermanentWeather=null,SunTurnsRemaining=5,RainTurnsRemaining=0,SandstormTurnsRemaining=0,HailTurnsRemaining=0}};
    private static SourceBattleFrame Initial(bool single,int actor=0,bool sun=false,int? pp=null,bool pressure=false)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();bodies[actor]=Solar();
        var frame=Kernel.Frame(bodies,single);
        if(pp is {} remaining)frame=Charge.Body(frame,actor,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=remaining}]}});
        if(pressure)frame=Ability(frame,actor^1,"ABILITY_PRESSURE");return sun?Sun(frame):frame;
    }
    private static SourceBattleFrame Ability(SourceBattleFrame frame,int slot,string ability)=>Charge.Body(frame,slot,b=>
        b with{CalledOverlay=(b.CalledOverlay??new()) with{Ability=ability}});
    private static SourceBattleFrame Charged(bool single,int actor=0,int? pp=null,bool pressure=false)
    {
        var setup=Charge.Ordered(Initial(single,actor,pp:pp,pressure:pressure),actor,actor^1);CheckSetup(setup.Events,single);
        return Charge.CompleteTurn(setup);
    }
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> rows)=>Shared.Number(rows);
    private static void Deny(ImmutableArray<SourceBattleEvent> rows,bool single)=>Charge.Deny(rows,single);
    private static void CheckSetup(ImmutableArray<SourceBattleEvent> rows,bool single)
    {
        var marker=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="move");
        var pp=rows.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(pp.Sequence+1,marker.Sequence);
        Assert.IsFalse(rows.Any(e=>e is SourceAttackStringEvent or SourceMoveUsedEvent));
        Check(rows,single,marker,marker,pp,true);
    }
    private static void CheckRelease(ImmutableArray<SourceBattleEvent> rows,bool single,bool sunny)
    {
        var used=rows.OfType<SourceMoveUsedEvent>().Single();var commands=rows.OfType<SourcePpCommandEvent>().ToArray();
        Assert.AreEqual(sunny?2:1,commands.Length);var pp=commands[^1];Assert.AreEqual(pp.Before,pp.After);
        var release=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="released");
        Assert.AreEqual(1,release.Before);Assert.AreEqual(0,release.After);
        if(sunny)
        {
            var early=commands[0];Assert.AreEqual(Math.Max(0,early.Before-early.CommandCost),early.After);
            Assert.AreEqual(early.After,pp.Before);Assert.AreEqual(early.CommandCost,pp.CommandCost);Assert.IsTrue(early.Sequence<release.Sequence);
        }
        Check(rows,single,used,Envelope.Hits(rows).Single(),pp,false);
    }
    private static void Check(ImmutableArray<SourceBattleEvent> rows,bool single,SourceBattleEvent owner,SourceBattleEvent result,SourcePpCommandEvent pp,bool setup)
    {
        string before=JsonSerializer.Serialize(rows);int count=0;
        for(int i=0;i<rows.Length;i++)foreach(bool atResult in new[]{false,true})
        {
            var first=Charge.Project(rows,i,single,atResult);Assert.AreEqual(first,Charge.Project(rows,i,single,atResult));
            Assert.IsNull(Charge.Project(rows,i,single,atResult,SourceBattleScene.Off).Dispatch);
            if(single)Assert.IsNull(atResult?AnimationDispatchFixture.AtResult(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch:
                AnimationDispatchFixture.AtAttackText(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch);
            if(rows[i]!=owner||atResult!=setup){Assert.IsNull(first.Dispatch,first.Reason);continue;}
            var cue=first.Dispatch;Assert.IsNotNull(cue,first.Reason);count++;
            Assert.AreEqual(76,cue.SourceMoveId);Assert.AreEqual(owner.Sequence,cue.Sequence);Assert.AreEqual(result.Sequence,cue.ResultSequence);
            Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(pp.Sequence,cue.SnapshotSequence);Assert.AreEqual(pp.Actor,cue.Actor);Assert.AreEqual(pp.Target,cue.Target);
            Assert.AreEqual(setup?(byte?)0:(byte?)1,cue.MoveTurn);Assert.AreEqual(!setup,cue.AcknowledgesCurrentReceipt);
            Assert.AreEqual(setup?CampaignMoveAnimationBoundary.BeforeChargeResult:CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);
            Assert.AreEqual(result is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
            long? expectedAttack=setup?null:rows.OfType<SourceAttackStringEvent>().Single().Sequence;
            Assert.AreEqual(expectedAttack,cue.AttackSequence);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(rows));
    }

    [TestMethod]
    public void OrdinarySolarBeamUsesExistingSetupAndRetainedReleaseAcrossAllPositions()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var setup=Charge.Ordered(Initial(single,actor),actor,actor^1);CheckSetup(setup.Events,single);
            var frame=Charge.CompleteTurn(setup);if(substitute)frame=Charge.Body(frame,actor^1,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            string before=JsonSerializer.Serialize(frame);var release=Charge.Ordered(frame,actor,actor^1);CheckRelease(release.Events,single,false);
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.IsNull(release.State.Slots[actor].Registers.Turn?.Lock);
        }
    }

    [TestMethod]
    public void SunImmediateReleaseHasTransientMarkerBeforeEarlyPpAndOnlyOneReleaseAnimation()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool pressure in new[]{false,true})foreach(bool substitute in new[]{false,true})
        {
            var frame=Initial(single,actor,true,1,pressure);
            if(substitute)frame=Charge.Body(frame,actor^1,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            string before=JsonSerializer.Serialize(frame);
            var result=Charge.Ordered(frame,actor,actor^1);CheckRelease(result.Events,single,true);
            var marker=result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="move");
            var pp=result.Events.OfType<SourcePpCommandEvent>().First();Assert.AreEqual(marker.Sequence+1,pp.Sequence);Assert.AreEqual(0,marker.Before);Assert.AreEqual(1,marker.After);
            Assert.AreEqual(pressure?2:1,pp.CommandCost);Assert.AreEqual(0,pp.After);Assert.IsNull(result.State.Slots[actor].Registers.Turn?.Lock);
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod]
    public void ActualSunnyDayBetweenTurnsReentersSunBranchForAnInheritedChargeIncludingZeroPp()
    {
        foreach(bool single in new[]{false,true})foreach(bool pressure in new[]{false,true})foreach(int remaining in new[]{1,5})
        {
            var frame=Initial(single,pp:remaining);frame=Charge.Body(frame,1,_=>Kernel.Mon("MAGMAR","SUNNY_DAY",100) with{SpeedStage=0});
            if(pressure)frame=Ability(frame,1,"ABILITY_PRESSURE");
            var setup=Charge.Ordered(frame,0,1);CheckSetup(setup.Events,single);
            var next=Charge.CompleteTurn(setup);Assert.IsTrue(next.Field.SunTurnsRemaining>0,"The target's actual Sunny Day executes after setup.");
            var release=Charge.Ordered(next,0,1);CheckRelease(release.Events,single,true);
            Assert.IsFalse(release.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="called-charge"&&e.Reason=="move"),
                "An equal retained lock emits no invented setup marker.");
            var pp=release.Events.OfType<SourcePpCommandEvent>().First();Assert.AreEqual(Math.Max(0,remaining-(pressure?2:1)),pp.Before);
            Assert.AreEqual(Math.Max(0,pp.Before-(pressure?2:1)),pp.After);
        }
    }

    [TestMethod]
    public void CloudNineAndAirLockPreserveOrdinaryTwoTurnProtocolEvenWhenSunIsSet()
    {
        foreach(bool single in new[]{false,true})foreach(string ability in new[]{"ABILITY_CLOUD_NINE","ABILITY_AIR_LOCK"})
        {
            var frame=Ability(Initial(single,sun:true),1,ability);var setup=Charge.Ordered(frame,0,1);CheckSetup(setup.Events,single);
            Assert.IsTrue(setup.State.Field.SunTurnsRemaining>0);var release=Charge.Ordered(Charge.CompleteTurn(setup),0,1);CheckRelease(release.Events,single,false);
        }
    }

    [TestMethod]
    public void OrdinaryFinalPpAndPressureReleaseWithZeroConsumptionAtZero()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charged(single,pp:1,pressure:true);Assert.AreEqual(0,frame.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
            var result=Charge.Ordered(frame,0,1);CheckRelease(result.Events,single,false);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(0,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(2,pp.CommandCost);
        }
    }

    [TestMethod]
    public void SuccessfulSunnyWakeAndConfusionCancellersCanInterveneAfterEarlyPp()
    {
        foreach(bool single in new[]{false,true})foreach(bool nightmare in new[]{false,true})
        {
            var frame=Charge.Body(Initial(single,sun:true),0,b=>b with{Status=1,Nightmare=nightmare,ConfusionTurnsRemaining=1});
            var result=Charge.Ordered(frame,0,1);CheckRelease(result.Events,single,true);
            var pp=result.Events.OfType<SourcePpCommandEvent>().First();var awake=result.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Reason=="woke-up");
            var confusion=result.Events.OfType<SourceConfusionChangedEvent>().Single(e=>e.Reason=="snapped-out");
            Assert.IsTrue(pp.Sequence<awake.Sequence&&awake.Sequence<confusion.Sequence);
        }
    }

    [TestMethod]
    public void SunnyEarlyPpNeverAnimatesWhenTheLaterCancellerOrHitFails()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"sleep","paralysis","protected","miss"})
        {
            var frame=Initial(single,sun:true);
            if(branch=="sleep")frame=Charge.Body(frame,0,b=>b with{Status=3});
            if(branch=="paralysis")frame=Charge.Body(frame,0,b=>b with{Status=WildBattleStatus.Paralysis});
            if(branch=="protected")frame=Charge.Body(frame,1,b=>b with{Protected=true});
            if(branch=="miss"){frame=Charge.Body(frame,0,b=>b with{AccuracyStage=0});frame=Charge.Body(frame,1,b=>b with{EvasionStage=12});}
            var result=branch=="paralysis"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceActionBlockedEvent>().Any(),"Solar paralysis after early PP"):
                branch=="miss"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Solar accuracy miss"):
                Charge.Ordered(frame,0,1);
            Assert.IsTrue(result.Events.OfType<SourcePpCommandEvent>().Any());Assert.IsFalse(Envelope.Hits(result.Events).Any());Deny(result.Events,single);
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="called-charge"&&e.Reason=="move"&&
                result.Events.OfType<SourcePpCommandEvent>().First().Sequence<e.Sequence));
        }
    }

    [TestMethod]
    public void SunnyAndOrdinaryPrefixesRejectForeignDuplicateAndReorderedReceipts()
    {
        foreach(bool single in new[]{false,true})foreach(bool sunny in new[]{false,true})
        {
            var frame=sunny?Initial(single,sun:true):Charged(single);var rows=Charge.Ordered(frame,0,1).Events;
            var commands=rows.OfType<SourcePpCommandEvent>().ToArray();var release=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="released");
            foreach(var pp in commands)
            {
                int i=rows.IndexOf(pp);
                foreach(var bad in new[]{pp with{Actor=new(1)},pp with{PpOwner=new(1)},pp with{Target=new(0)},pp with{PpSlot=3},
                    pp with{CommandCost=0},pp with{CurrentMove=Kernel.Id("RAZOR_WIND")},pp with{ChosenMove=Kernel.Id("METRONOME")},pp with{Turn=pp.Turn+1}})
                    Deny(rows.SetItem(i,bad),single);
                Deny(Number(rows.Insert(i,pp)),single);Deny(Number(rows.Remove(pp)),single);
            }
            int ri=rows.IndexOf(release);foreach(var bad in new[]{release with{Before=0},release with{After=1},release with{Reason="protected"},release with{Side=new(1)}})
                Deny(rows.SetItem(ri,bad),single);
            Deny(Number(rows.Insert(ri,release)),single);Deny(Number(rows.Remove(release)),single);
            if(sunny)
            {
                var prepared=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="move");int pi=rows.IndexOf(prepared);
                Deny(Number(rows.Insert(pi,prepared)),single);Deny(rows.SetItem(pi,prepared with{Side=new(1)}),single);
                Deny(rows.SetItem(pi,prepared with{After=0}),single);
                var early=commands[0];Deny(rows.SetItem(rows.IndexOf(early),early with{After=early.Before}),single);
                Deny(Number(rows.SetItem(pi,early).SetItem(rows.IndexOf(early),prepared)),single);
            }
        }
    }

    [TestMethod]
    public void SunnyNoTargetAfterTwoActualKnockoutsKeepsBothPpCommandsWithoutAnimation()
    {
        var a=Kernel.Mon("RATTATA","QUICK_ATTACK",100) with{SpeedStage=7};a=a with{Pokemon=a.Pokemon with{CurrentHp=1}};
        var bodies=new[]{a,Kernel.Mon("RATTATA","QUICK_ATTACK",100),Kernel.Mon("PIDGEY","TACKLE",1),Solar() with{SpeedStage=0}};
        var frame=Sun(Kernel.Frame(bodies));var reserve=Kernel.Mon("WAILORD","SPLASH",100);
        frame=frame with{Parties=frame.Parties.SetItem(0,frame.Parties[0] with{Members=frame.Parties[0].Members.Add(new(reserve.Pokemon))})};
        int[] targets=[2,0,1,0];var choices=frame.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(targets[c.Slot.Value]))).ToImmutableArray();
        var result=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(result.Succeeded,result.Fault);
        var fainted=new HashSet<int>();int steps=0;
        while(!result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="deferred-ko-no-target"))
        {
            Assert.IsTrue(++steps<=12);Assert.IsNotNull(result.Continuation);
            result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,result.State,result.Continuation.Value);Assert.IsTrue(result.Succeeded,result.Fault);
            foreach(var faint in result.Events.OfType<SourceFaintedEvent>())fainted.Add(faint.Side.Value);
        }
        CollectionAssert.AreEquivalent(new[]{0,2},fainted.ToArray());
        Assert.AreEqual(2,result.Events.OfType<SourcePpCommandEvent>().Count());Assert.IsFalse(Envelope.Hits(result.Events).Any());Deny(result.Events,false);
    }

    [TestMethod]
    public void SolarDefinitionScriptAndSceneAreRequiredForBothSourcePhases()
    {
        foreach(bool single in new[]{false,true})
        {
            var setup=Charge.Ordered(Initial(single),0,1).Events;var marker=setup.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");
            var release=Charge.Ordered(Initial(single,sun:true),0,1).Events;var used=release.OfType<SourceMoveUsedEvent>().Single();
            var moves=Kernel.Mechanics.Catalog.Moves;
            foreach(var metadata in new[]{null,moves.Remove(Move),moves.SetItem(Move,moves[Move] with{Power=0}),
                moves.SetItem(Move,moves[Move] with{Target="MOVE_TARGET_BOTH"}),moves.SetItem(Move,moves[Move] with{SourceMoveId=1})})
            {
                Assert.IsNull(single?AnimationDispatchFixture.AtResult(Charge.Single(setup,setup.IndexOf(marker)),true,SourceBattleScene.On,metadata).Dispatch:
                    AnimationDispatchFixture.AtResult(Envelope.Double(setup,setup.IndexOf(marker)),SourceBattleScene.On,metadata).Dispatch);
                Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Charge.Single(release,release.IndexOf(used)),true,SourceBattleScene.On,metadata).Dispatch:
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(release,release.IndexOf(used)),SourceBattleScene.On,metadata).Dispatch);
            }
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtResult(Charge.Single(setup,setup.IndexOf(marker)),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtResult(Envelope.Double(setup,setup.IndexOf(marker)),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(release,release.IndexOf(used)),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(release,release.IndexOf(used)),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
        }
    }
}
