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
public sealed class PsychicUtilityAnimationKernelTests
{
    private static (string Species,string Move) Spec(int id)=>id switch{134=>("KADABRA","KINESIS"),244=>("GOLDUCK","PSYCH_UP"),295=>("LATIOS","LUSTER_PURGE"),_=>throw new ArgumentOutOfRangeException(nameof(id))};
    private static SourceBattleFrame Initial(int id,bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();var spec=Spec(id);
        bodies[actor]=Kernel.Mon(spec.Species,spec.Move,100) with{SpeedStage=12,AccuracyStage=12};return Kernel.Frame(bodies,single);
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,bool result=false,SourceBattleScene scene=SourceBattleScene.On)=>Shared.Project(events,index,single,result,scene);
    private static void Check(ImmutableArray<SourceBattleEvent> events,int id,bool single,SourceBattleEvent outcome)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();int count=0;
        for(int i=0;i<events.Length;i++)foreach(bool result in new[]{false,true})
        {
            var decision=Project(events,i,single,result);Assert.AreEqual(decision,Project(events,i,single,result));Assert.IsNull(Project(events,i,single,result,SourceBattleScene.Off).Dispatch);
            if(events[i]!=used||result){Assert.IsNull(decision.Dispatch,decision.Reason);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);count++;Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(outcome.Sequence,cue.ResultSequence);Assert.AreEqual(pp.Sequence,cue.SnapshotSequence);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);Assert.AreEqual(outcome is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    private static SourceMtMoonVolatileEvent Copied(ImmutableArray<SourceBattleEvent> events)=>events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="psych-up");

    [TestMethod,DataRow(134),DataRow(244),DataRow(295)]
    public void NaturalLearnersOwnOneDedicatedRootAcrossAllSourcePositionsAndSelectedPartners(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2,actor^3})
        {
            var frame=Initial(id,single,actor);string before=JsonSerializer.Serialize(frame);var result=Shared.Ordered(frame,actor,target);SourceBattleEvent outcome=id switch{134=>result.Events.OfType<SourceStatChangedEvent>().Single(),244=>Copied(result.Events),_=>Envelope.Hits(result.Events).Single()};Check(result.Events,id,single,outcome);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            if(id==134){var stat=(SourceStatChangedEvent)outcome;Assert.AreEqual(WildBattleStat.Accuracy,stat.Stat);Assert.AreEqual(stat.Before-1,stat.After);}
        }
    }
    [TestMethod,DataRow("equal"),DataRow("seven"),DataRow("partial")]
    public void PsychUpAuthenticatesOnlyChangedAssignmentsAndStillAnimatesEqualStages(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Initial(244,single);var actor=frame.Slots[0].Body!;
            frame=Shared.Body(frame,1,b=>branch switch{
                "equal"=>b with{AttackStage=actor.AttackStage,DefenseStage=actor.DefenseStage,SpeedStage=actor.SpeedStage,SpecialAttackStageDelta=actor.SpecialAttackStageDelta,SpecialDefenseStageDelta=actor.SpecialDefenseStageDelta,AccuracyStage=actor.AccuracyStage,EvasionStage=actor.EvasionStage},
                "seven"=>b with{AttackStage=9,DefenseStage=3,SpeedStage=0,SpecialAttackStageDelta=1,SpecialDefenseStageDelta=-2,AccuracyStage=2,EvasionStage=11},
                _=>b with{AttackStage=actor.AttackStage,DefenseStage=actor.DefenseStage,SpecialAttackStageDelta=actor.SpecialAttackStageDelta,SpecialDefenseStageDelta=actor.SpecialDefenseStageDelta,EvasionStage=actor.EvasionStage}});
            var result=Shared.Ordered(frame,0,1);var marker=Copied(result.Events);var stats=result.Events.OfType<SourceStatChangedEvent>().Where(e=>e.Sequence<marker.Sequence).ToArray();Assert.AreEqual(branch=="equal"?0:branch=="seven"?7:2,stats.Length);Check(result.Events,244,single,marker);
            CollectionAssert.AreEqual(stats.Select(x=>x.Stat).ToArray(),new[]{WildBattleStat.Attack,WildBattleStat.Defense,WildBattleStat.Speed,WildBattleStat.SpecialAttack,WildBattleStat.SpecialDefense,WildBattleStat.Accuracy,WildBattleStat.Evasion}.Where(stat=>stats.Any(x=>x.Stat==stat)).ToArray());
            foreach(var stat in stats)Assert.IsNull(Shared.Wave(result.Events,result.Events.IndexOf(stat),single),"Copied assignments do not own separate stat-change waves.");
        }
    }
    [TestMethod]
    public void PsychUpOwnsItsCopyMarkerBeforeWhiteHerbRestoresCopiedNegativeStages()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Body(Initial(244,single),0,b=>b with{HeldItem="ITEM_WHITE_HERB"});
            frame=Shared.Body(frame,1,b=>b with{AttackStage=2,DefenseStage=3,SpeedStage=0,SpecialAttackStageDelta=-2,SpecialDefenseStageDelta=-3,AccuracyStage=4,EvasionStage=1});
            var result=Shared.Ordered(frame,0,1);var marker=Copied(result.Events);Check(result.Events,244,single,marker);
            var restored=result.Events.OfType<SourceStatChangedEvent>().Where(e=>e.Sequence>marker.Sequence&&e.Actor==new BattlerSlot(0)&&e.Target==new BattlerSlot(0)).ToArray();
            Assert.AreEqual(7,restored.Length);Assert.IsTrue(restored.All(e=>e.Before<6&&e.After==6));
            var consumed=result.Events.OfType<SourceHeldItemConsumedEvent>().Single(e=>e.Item=="ITEM_WHITE_HERB");Assert.IsTrue(consumed.Sequence>marker.Sequence);
            var body=result.State.Slots[0].Body!;Assert.AreEqual(6,body.AttackStage);Assert.AreEqual(6,body.DefenseStage);Assert.AreEqual(6,body.SpeedStage);Assert.AreEqual(0,body.SpecialAttackStageDelta);Assert.AreEqual(0,body.SpecialDefenseStageDelta);Assert.AreEqual(6,body.AccuracyStage);Assert.AreEqual(6,body.EvasionStage);
            foreach(var stat in restored)Assert.IsNull(Shared.Wave(result.Events,result.Events.IndexOf(stat),single),"White Herb restoration cannot own the copied-stage animation.");
        }
    }
    [TestMethod,DataRow("protect"),DataRow("substitute"),DataRow("evasion")]
    public void PsychUpKeepsItsSourceUnconditionalCopyBranch(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Body(Initial(244,single),1,b=>branch switch{"protect"=>b with{Protected=true},"substitute"=>b with{CalledVolatile=new(){SubstituteHp=40}},_=>b with{EvasionStage=12}});frame=Shared.Body(frame,0,b=>b with{AccuracyStage=0});
            var result=Shared.Ordered(frame,0,1);Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());Check(result.Events,244,single,Copied(result.Events));
        }
    }
    [TestMethod,DataRow("marker-side"),DataRow("marker-reason"),DataRow("marker-value"),DataRow("missing-marker"),DataRow("duplicate-marker"),DataRow("stat-owner"),DataRow("stat-target"),DataRow("stat-order"),DataRow("stat-equal"),DataRow("stat-range"),DataRow("foreign-prefix"),DataRow("pp-owner"),DataRow("called")]
    public void PsychUpRejectsForeignMalformedOrIncompleteReceiptOwnership(string mutation)
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Shared.Ordered(Initial(244,single),0,1).Events;var marker=Copied(events);int i=events.IndexOf(marker);var stat=events.OfType<SourceStatChangedEvent>().First();int j=events.IndexOf(stat);var pp=events.OfType<SourcePpCommandEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();
            events=mutation switch{
                "marker-side"=>events.SetItem(i,marker with{Side=new(1)}),"marker-reason"=>events.SetItem(i,marker with{Reason="reset"}),"marker-value"=>events.SetItem(i,marker with{After=1}),"missing-marker"=>events.RemoveAt(i),"duplicate-marker"=>Numbering.Number(events.Insert(i+1,marker)),
                "stat-owner"=>events.SetItem(j,stat with{Actor=new(1)}),"stat-target"=>events.SetItem(j,stat with{Target=new(1)}),"stat-order"=>events.SetItem(j,stat with{Stat=WildBattleStat.Evasion}),"stat-equal"=>events.SetItem(j,stat with{After=stat.Before}),"stat-range"=>events.SetItem(j,stat with{After=13}),"foreign-prefix"=>Numbering.Number(events.Insert(i,new SourceMtMoonVolatileEvent(0,marker.Turn,new(1),"foreign",0,1,"test"))),"pp-owner"=>events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),_=>events.SetItem(events.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")})};
            Shared.Deny(events,single);
        }
    }
    [TestMethod,DataRow("kinesis-cap"),DataRow("kinesis-substitute"),DataRow("luster-immunity")]
    public void UnsuccessfulAccuracyDropsAndImmuneDamageDoNotBorrowPpAsAnimationEvidence(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            int id=branch=="luster-immunity"?295:134;var frame=Shared.Body(Initial(id,single),1,b=>branch switch{"kinesis-cap"=>b with{AccuracyStage=0},"kinesis-substitute"=>b with{CalledVolatile=new(){SubstituteHp=40}},_=>Kernel.Mon("UMBREON","SAND_ATTACK",100) with{SpeedStage=0}});var result=Shared.Ordered(frame,0,1);Shared.Deny(result.Events,single);
        }
    }
    [TestMethod]
    public void LusterPurgeUsesCommittedSubstituteHitAndLaterSecondaryCannotReplayRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Body(Initial(295,single),1,b=>b with{CalledVolatile=new(){SubstituteHp=100}});var result=Shared.Ordered(frame,0,1);Check(result.Events,295,single,result.Events.OfType<SourceSubstituteDamagedEvent>().Single());
            var secondary=Shared.Find(Initial(295,single),0,1,r=>r.Events.OfType<SourceStatChangedEvent>().Any(e=>e.Stat==WildBattleStat.SpecialDefense),"Luster Purge secondary");Check(secondary.Events,295,single,Envelope.Hits(secondary.Events).Single());
        }
    }
}
