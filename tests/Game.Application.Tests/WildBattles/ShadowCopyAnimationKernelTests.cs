using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Numbering=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class ShadowCopyAnimationKernelTests
{
    private static SourceBattleFrame Initial(bool single,int actor=0,int? selected=null)
    {
        int target=selected??(actor^1);var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("GASTLY","NIGHTMARE",50) with{SpeedStage=12};
        var frame=Kernel.Frame(bodies,single);return Shared.Body(frame,target,b=>b with{Status=3});
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,bool result=false,SourceBattleScene scene=SourceBattleScene.On)
    {
        if(single)
        {
            var b=Envelope.Single(events,index);
            return result?AnimationDispatchFixture.AtResult(b,true,scene):AnimationDispatchFixture.AtAttackText(b,true,scene);
        }
        return result?AnimationDispatchFixture.AtResult(Envelope.Double(events,index),scene):AnimationDispatchFixture.AtAttackText(Envelope.Double(events,index),scene);
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single)
    {for(int i=0;i<events.Length;i++)foreach(bool result in new[]{false,true})Assert.IsNull(Project(events,i,single,result).Dispatch);}
    private static void Check(ImmutableArray<SourceBattleEvent> events,bool single)
    {
        var before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();var applied=events.OfType<SourceTowerVolatileEvent>().Single(e=>e.Effect=="nightmare"&&e.Applied);
        Assert.AreEqual(Kernel.Id("NIGHTMARE"),used.Move);Assert.AreEqual(pp.Sequence+1,applied.Sequence);int count=0;
        for(int i=0;i<events.Length;i++)foreach(bool result in new[]{false,true})
        {
            var decision=Project(events,i,single,result);Assert.AreEqual(decision,Project(events,i,single,result));Assert.IsNull(Project(events,i,single,result,SourceBattleScene.Off).Dispatch);
            if(events[i]!=used||result){Assert.IsNull(decision.Dispatch,decision.Reason);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);count++;Assert.AreEqual(171,cue.SourceMoveId);Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(applied.Sequence,cue.ResultSequence);Assert.AreEqual(pp.Sequence,cue.SnapshotSequence);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);Assert.IsFalse(cue.HitsSubstitute);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    [TestMethod]
    public void NaturalNightmareLearnerUsesOwnedSuccessfulReceiptAcrossBothSidesAndPartnerTargets()
    {
        var definition=Kernel.Mechanics.Catalog.Moves[Kernel.Id("NIGHTMARE")];Assert.AreEqual(171,definition.SourceMoveId);Assert.AreEqual("EFFECT_NIGHTMARE",definition.Effect);Assert.IsTrue(AnimationDispatchFixture.Supports(171,definition.Id.Value));
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2,actor^3})
        {var frame=Initial(single,actor,target);var before=JsonSerializer.Serialize(frame);var result=Shared.Ordered(frame,actor,target);Check(result.Events,single);Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.IsTrue(result.State.Slots[target].Body!.Nightmare);}
    }
    [TestMethod,DataRow("awake"),DataRow("already-nightmare"),DataRow("substitute")]
    public void FailedNightmareNeverBorrowsPpAsSuccessfulAnimationEvidence(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Body(Initial(single),1,b=>branch switch{"awake"=>b with{Status=0},"already-nightmare"=>b with{Nightmare=true},_=>b with{CalledVolatile=new(){SubstituteHp=40}}});
            var result=Shared.Ordered(frame,0,1);Assert.IsFalse(result.Events.OfType<SourceTowerVolatileEvent>().Any(e=>e.Effect=="nightmare"&&e.Applied));Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any());Deny(result.Events,single);
        }
    }
    [TestMethod]
    public void NightmareUsesTheSourceNoAccuracyBranchAndDoesNotRecomputeProtectionEligibility()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Body(Initial(single),1,b=>b with{Protected=true,EvasionStage=12});frame=Shared.Body(frame,0,b=>b with{AccuracyStage=0});
            var result=Shared.Ordered(frame,0,1);Check(result.Events,single);Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());
        }
    }
    [TestMethod,DataRow("target"),DataRow("effect"),DataRow("applied"),DataRow("reason"),DataRow("duplicate"),DataRow("sequence"),DataRow("turn"),DataRow("owner"),DataRow("called")]
    public void NightmareRejectsForeignMalformedDuplicateOrCalledReceiptOwnership(string mutation)
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Shared.Ordered(Initial(single),0,1).Events;var marker=events.OfType<SourceTowerVolatileEvent>().Single(e=>e.Effect=="nightmare");int i=events.IndexOf(marker);var pp=events.OfType<SourcePpCommandEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();
            events=mutation switch{
                "target"=>events.SetItem(i,marker with{Side=new(0)}),"effect"=>events.SetItem(i,marker with{Effect="curse"}),"applied"=>events.SetItem(i,marker with{Applied=false}),"reason"=>events.SetItem(i,marker with{Reason="awake"}),
                "duplicate"=>Numbering.Number(events.Insert(i+1,marker)),"sequence"=>events.SetItem(i,marker with{Sequence=pp.Sequence}),"turn"=>events.SetItem(i,marker with{Turn=marker.Turn+1}),"owner"=>events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),
                _=>events.SetItem(events.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")})};
            Deny(events,single);
        }
    }
}
