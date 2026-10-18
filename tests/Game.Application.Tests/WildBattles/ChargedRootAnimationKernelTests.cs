using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using F=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class ChargedRootAnimationKernelTests
{
    private static MoveId SpitUp=>Kernel.Id("SPIT_UP");
    private static SourceBattleFrame SpitFrame(bool single,int actor,int count,bool substitute=false)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon(actor%2==0?"GULPIN":"SWALOT","SPIT_UP",actor%2==0?34:40) with{AccuracyStage=12,SpeedStage=12,CalledVolatile=new(){StockpileCount=count}};
        if(substitute)bodies[actor^1]=bodies[actor^1] with{CalledVolatile=new(){SubstituteHp=100}};
        return Kernel.Frame(bodies,single);
    }
    [TestMethod]
    public void ActualSkyAttackSetupAndReleaseReuseChargeOwnershipForEveryActorAndPartner()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2,actor^3})
        {
            var setup=F.Ordered(F.Initial(143,single,actor),actor,target);var marker=setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");
            F.Check(setup.Events,143,single,marker,marker,0);var charged=F.CompleteTurn(setup);var release=F.Ordered(charged,actor,target);var hit=Envelope.Hits(release.Events).Single();
            F.Check(release.Events,143,single,release.Events.OfType<SourceMoveUsedEvent>().Single(),hit,1);
            Assert.IsTrue(setup.Events.OfType<SourcePpCommandEvent>().Single().After<setup.Events.OfType<SourcePpCommandEvent>().Single().Before);var pp=release.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(pp.Before,pp.After);
        }
    }
    [TestMethod]
    public void SkyAttackFinalPpPressureAndInvalidChargeMarkersRemainFailClosed()
    {
        foreach(bool single in new[]{false,true})
        {
            var release=F.Ordered(F.Charged(143,single,pp:1,pressure:true),0,1);var marker=release.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");var pp=release.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(0,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(2,pp.CommandCost);F.Check(release.Events,143,single,release.Events.OfType<SourceMoveUsedEvent>().Single(),Envelope.Hits(release.Events).Single(),1);
            foreach(var bad in new[]{release.Events.Remove(marker),release.Events.SetItem(release.Events.IndexOf(marker),marker with{Side=new(1)}),release.Events.SetItem(release.Events.IndexOf(marker),marker with{Reason="sleep"}),Shared.Number(release.Events.Insert(release.Events.IndexOf(marker),marker))})F.Deny(bad,single);
            var blocked=F.Ordered(F.Body(F.Charged(143,single),1,b=>b with{Protected=true}),0,1);Assert.IsTrue(blocked.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"));F.Deny(blocked.Events,single);
        }
    }
    [TestMethod]
    public void ActualSpitUpCounterConsumptionOwnsAllThreeBranchesAndFixedSourcePower()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int count in new[]{1,2,3})foreach(bool substitute in new[]{false,true})
        {
            var frame=SpitFrame(single,actor,count,substitute);string before=JsonSerializer.Serialize(frame);var result=Kernel.Ordered(frame,actor,actor^1);var events=result.Events;var used=events.OfType<SourceMoveUsedEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();var consumed=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="stockpile");var hit=Envelope.Hits(events).Single();
            Assert.AreEqual(count,consumed.Before);Assert.AreEqual(0,consumed.After);Assert.AreEqual("spit-up",consumed.Reason);Assert.AreEqual(pp.Sequence+1,consumed.Sequence);Assert.IsTrue(consumed.Sequence<hit.Sequence);Assert.AreEqual(0,result.State.Slots[actor].Body!.CalledVolatile?.StockpileCount??0);
            int dispatches=0;for(int i=0;i<events.Length;i++)
            {
                var decision=F.Project(events,i,single,false);Assert.AreEqual(decision,F.Project(events,i,single,false));Assert.IsNull(F.Project(events,i,single,true).Dispatch);Assert.IsNull(F.Project(events,i,single,false,SourceBattleScene.Off).Dispatch);
                if(events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);dispatches++;Assert.AreEqual(255,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual((byte)count,cue.MoveTurn);Assert.AreEqual((ushort)100,cue.MovePower);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(pp.Sequence,cue.SnapshotSequence);Assert.AreEqual(hit.Sequence,cue.ResultSequence);Assert.AreEqual(substitute,cue.HitsSubstitute);
            }
            Assert.AreEqual(1,dispatches);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
        }
    }
    [TestMethod]
    public void ZeroCountProtectionAccuracyMissAndImmunityNeverBorrowASpitUpBranch()
    {
        foreach(bool single in new[]{false,true})
        {
            var zero=Kernel.Ordered(SpitFrame(single,0,0),0,1);Assert.IsTrue(zero.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="stockpile-empty"));F.Deny(zero.Events,single);
            var protectedFrame=F.Body(SpitFrame(single,0,3),1,b=>b with{Protected=true});var protectedResult=Kernel.Ordered(protectedFrame,0,1);Assert.AreEqual(3,protectedResult.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="stockpile").Before);Assert.IsFalse(Envelope.Hits(protectedResult.Events).Any());F.Deny(protectedResult.Events,single);
            var missFrame=F.Body(F.Body(SpitFrame(single,0,2),0,b=>b with{AccuracyStage=0}),1,b=>b with{EvasionStage=12});var miss=Kernel.Find(missFrame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"spit-up-miss");Assert.IsFalse(miss.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="stockpile"));Assert.AreEqual(2,miss.State.Slots[0].Body!.CalledVolatile!.StockpileCount);F.Deny(miss.Events,single);
            var immune=Kernel.Ordered(F.Body(SpitFrame(single,0,1),1,_=>Kernel.Mon("GASTLY","LICK",100) with{SpeedStage=0}),0,1);Assert.IsTrue(immune.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));F.Deny(immune.Events,single);
        }
    }
    [TestMethod]
    public void MissingForeignDuplicateLateAndOutOfRangeCounterReceiptsCannotAuthorizeSpitUp()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Kernel.Ordered(SpitFrame(single,0,3),0,1).Events;var consumed=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="stockpile");int index=events.IndexOf(consumed);var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=Envelope.Hits(events).Single();
            foreach(var bad in new[]{events.Remove(consumed),events.SetItem(index,consumed with{Side=new(1)}),events.SetItem(index,consumed with{Before=0}),events.SetItem(index,consumed with{Before=4}),events.SetItem(index,consumed with{After=1}),events.SetItem(index,consumed with{Reason="swallow"}),Shared.Number(events.Insert(index,consumed)),Shared.Number(events.RemoveAt(index).Insert(events.Length-1,consumed)),events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),events.Remove(hit)})F.Deny(bad,single);
            var forged=hit switch{SourceHpChangedEvent h=>events.SetItem(events.IndexOf(hit),h with{PreparedBasePower=300}),SourceSubstituteDamagedEvent s=>events.SetItem(events.IndexOf(hit),s with{PreparedBasePower=300}),_=>throw new InvalidOperationException()};F.Deny(forged,single);
        }
    }
}
