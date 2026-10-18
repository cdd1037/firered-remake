using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class FakeOutAnimationKernelTests
{
    private static readonly MoveId FakeOut=Id("FAKE_OUT");
    private static WildBattleCombatant[] Participants(int actor,int firstTurn=2)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","DEFENSE_CURL",100)).ToArray();
        bodies[actor]=Mon("MEOWTH","FAKE_OUT") with{FirstTurnCounter=firstTurn,AccuracyStage=12,SpeedStage=12};return bodies;
    }
    private static CampaignMoveAnimationDecision Text(ImmutableArray<SourceBattleEvent> events,int index,bool single,SourceBattleScene scene=SourceBattleScene.On)=>single
        ?AnimationDispatchFixture.AtAttackText(Single(events,index),true,scene):AnimationDispatchFixture.AtAttackText(Double(events,index),scene);
    private static void Check(ImmutableArray<SourceBattleEvent> events,bool single,bool expected,bool substitute=false)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==FakeOut);
        var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==FakeOut);var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==FakeOut);
        var hits=events.Where(e=>e is SourceHpChangedEvent h&&h.Move==FakeOut||e is SourceSubstituteDamagedEvent s&&s.Move==FakeOut).ToArray();
        Assert.AreEqual(used.Actor,pp.Actor);Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(FakeOut,attack.ChosenMove);Assert.AreEqual(FakeOut,pp.ChosenMove);Assert.IsTrue(attack.Sequence<used.Sequence&&used.Sequence<pp.Sequence);
        int roots=0;
        for(int cursor=0;cursor<events.Length;cursor++)
        {
            var decision=Text(events,cursor,single);Assert.AreEqual(decision,Text(events,cursor,single));Assert.IsNull(Text(events,cursor,single,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Single(events,cursor),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Double(events,cursor),SourceBattleScene.On).Dispatch);
            if(expected&&events[cursor]==used)
            {
                var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);roots++;
                Assert.AreEqual(252,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);
                Assert.AreEqual(hits.Single().Sequence,cue.ResultSequence);Assert.AreEqual(substitute,cue.HitsSubstitute);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);Assert.IsNull(cue.MoveTurn);
            }
            else Assert.IsNull(decision.Dispatch);
        }
        Assert.AreEqual(expected?1:0,roots);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    [TestMethod]
    public void PublicFirstTurnActionsUseExistingCommittedDamageGateAtEverySlotAndBothSinglesSides()
    {
        var definition=Mechanics.Catalog.Moves[FakeOut];Assert.AreEqual(252,definition.SourceMoveId);Assert.AreEqual("EFFECT_FAKE_OUT",definition.Effect);
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            int target=actor^1;var frame=Frame(Participants(actor),single);string before=JsonSerializer.Serialize(frame);
            var result=Ordered(frame,actor,target);Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());Assert.AreEqual(1,Hits(result.Events).Length);
            Check(result.Events,single,true);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
        }
    }
    [TestMethod]
    public void LaterTurnFailsAfterOwnedPpAndNeverAcquiresTheAnimationRoot()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var result=Ordered(Frame(Participants(actor,0),single),actor,actor^1);
            Assert.AreEqual("not-first-turn",result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);Assert.AreEqual(0,Hits(result.Events).Length);
            Assert.IsTrue(result.Events.OfType<SourcePpCommandEvent>().Single().After<result.Events.OfType<SourcePpCommandEvent>().Single().Before);Check(result.Events,single,false);
        }
    }
    [TestMethod]
    public void ActualProtectionImmunityMissAndSubstituteReceiptsRetainExistingDispatchSemantics()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(string branch in new[]{"protect","immune","miss","substitute"})
        {
            int target=actor^1;var bodies=Participants(actor);
            if(branch=="protect")bodies[target]=bodies[target] with{Protected=true};
            if(branch=="immune")bodies[target]=Mon("GASTLY","DESTINY_BOND");
            if(branch=="miss")bodies[actor]=bodies[actor] with{AccuracyStage=0};
            if(branch=="substitute")bodies[target]=bodies[target] with{CalledVolatile=new(){SubstituteHp=1}};
            var frame=Frame(bodies,single);var result=branch=="miss"?FindOrdered(frame,actor,target,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Fake Out miss"):Ordered(frame,actor,target);
            if(branch=="substitute")Assert.AreEqual(1,result.Events.OfType<SourceSubstituteDamagedEvent>().Count());
            else if(branch=="immune")Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));
            Check(result.Events,single,branch=="substitute",branch=="substitute");
        }
    }
}
