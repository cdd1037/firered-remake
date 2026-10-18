using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Called=FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

// Source-natural move learners and genuine public ordered receipts. Stages, HP
// and preexisting dolls are controlled fixture inputs, not played-route evidence.
[TestClass,TestCategory("Game")]
public sealed class SubstituteControllerCounterKernelTests
{
    [TestMethod,DataRow(2),DataRow(3),DataRow(4),DataRow(5)]
    public void NaturalMultiHitUsesRemainingPlannedCounterEvenWhenTheFirstHitEndsTheMove(int planned)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        foreach(bool earlyKo in new[]{false,true})
        {
            int target=actor^1;
            var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
            bodies[actor]=Kernel.Mon("CLEFAIRY","DOUBLE_SLAP",13) with{SpeedStage=12,AccuracyStage=12,CalledVolatile=new(){SubstituteHp=9}};
            if(earlyKo)bodies[target]=bodies[target] with{Pokemon=bodies[target].Pokemon with{CurrentHp=1}};
            var result=Kernel.Find(Kernel.Frame(bodies,single),actor,target,
                r=>r.Events.OfType<SourceMultiHitEvent>().Any(m=>m.PlannedHits==planned),$"Substitute controller {planned}, KO={earlyKo}");
            string unchanged=JsonSerializer.Serialize(result);var rows=result.Events;
            var summary=rows.OfType<SourceMultiHitEvent>().Single();var hits=Envelope.Hits(rows);
            Assert.AreEqual(earlyKo?1:planned,summary.ActualHits);Assert.AreEqual(summary.ActualHits,hits.Length);
            if(earlyKo)Assert.IsTrue(rows.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==target));
            for(int ordinal=0;ordinal<hits.Length;ordinal++)
            {
                int at=rows.IndexOf(hits[ordinal]);
                var decision=single?AnimationDispatchFixture.AtResult(Envelope.Single(rows,at),true,SourceBattleScene.On):
                    AnimationDispatchFixture.AtResult(Envelope.Double(rows,at),SourceBattleScene.On);
                var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);
                Assert.AreEqual((byte)ordinal,cue.MoveTurn);Assert.AreEqual((byte)(planned-ordinal),cue.SubstituteControllerHitCounter);
                Assert.AreEqual(CampaignMoveAnimationBoundary.BeforeHitResult,cue.Boundary);Assert.IsFalse(cue.AcknowledgesCurrentReceipt);
            }
            Assert.AreEqual(unchanged,JsonSerializer.Serialize(result));
        }
    }

    [TestMethod]
    public void PursuitInterceptionUsesItsPositionCounterForBothSidesAndBothColumns()
    {
        for(int actor=0;actor<4;actor++)
        {
            int target=actor^1;var side=(BattleSide)(target&1);
            var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
            bodies[actor]=Kernel.Mon("BEEDRILL","PURSUIT",30) with{SpeedStage=12,AccuracyStage=12,CalledVolatile=new(){SubstituteHp=9}};
            var frame=Kernel.Frame(bodies);var party=frame.Parties[(int)side];int reserve=party.Members.Length;
            frame=frame with{Parties=frame.Parties.SetItem((int)side,party with{Members=party.Members.Add(new(Kernel.Mon("WAILORD","SPLASH",100).Pokemon))})};
            var selections=frame.Slots.Select(c=>c.Slot.Value==target
                ?new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Switch,Incoming:new(side,reserve))
                :new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Value==actor?new(target):c.Slot)).ToImmutableArray();
            var result=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,selections);
            int steps=0;
            while(!result.Events.OfType<SourcePursuitInterceptedEvent>().Any())
            {
                Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsTrue(++steps<=16,"Expected the actual Pursuit interception within this public turn.");
                Assert.IsNotNull(result.Continuation);result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,result.State,result.Continuation.Value);
            }
            Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            string unchanged=JsonSerializer.Serialize(result);var rows=result.Events;
            var marker=rows.OfType<SourcePursuitInterceptedEvent>().Single();Assert.AreEqual(actor,marker.Actor.Value);Assert.AreEqual(target,marker.SwitchingSide.Value);
            var used=rows.OfType<SourceMoveUsedEvent>().Single();int at=rows.IndexOf(used);
            var decision=AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,at),SourceBattleScene.On);
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);Assert.AreEqual((byte)1,cue.MoveTurn);
            Assert.AreEqual((byte)(actor>=2?2:1),cue.SubstituteControllerHitCounter);
            // Marker removal cannot prove interception. It must not inherit the
            // right/left counter solely from actor position or move identity.
            var ordinary=rows.Remove(marker);int ordinaryAt=ordinary.IndexOf(used);
            var without=AnimationDispatchFixture.AtAttackText(Envelope.Double(ordinary,ordinaryAt),SourceBattleScene.On).Dispatch;
            Assert.IsNotNull(without);Assert.AreEqual((byte)0,without.SubstituteControllerHitCounter);Assert.AreEqual((byte)0,without.MoveTurn);
            Assert.AreEqual(unchanged,JsonSerializer.Serialize(result));
        }
    }

    [TestMethod]
    public void OrdinaryAndCalledSingleHitsKeepTheSourceResetCounterAtEveryPosition()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var called=Shared.Body(Called.Nature(single,WildBattleTerrain.Cave,actor),actor,
                b=>b with{CalledVolatile=new(){SubstituteHp=9}});
            var leaf=Kernel.Ordered(called,actor,actor^1);
            Assert.AreEqual((byte)0,Called.Check(leaf,single,Kernel.Id("NATURE_POWER"),247).SubstituteControllerHitCounter);
            for(int i=0;i<leaf.Events.Length;i++)Assert.IsNull(Kernel.Project(leaf.Events,i,single,SourceBattleScene.Off).Dispatch);
            var normal=Shared.Body(called,actor,_=>Kernel.Mon("BEEDRILL","PURSUIT",30) with{SpeedStage=12,AccuracyStage=12,CalledVolatile=new(){SubstituteHp=9}});
            var result=Kernel.Ordered(normal,actor,actor^1);var rows=result.Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();
            var decision=Kernel.Project(rows,rows.IndexOf(used),single);var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);
            Assert.IsFalse(rows.OfType<SourcePursuitInterceptedEvent>().Any());Assert.AreEqual((byte)0,cue.MoveTurn);Assert.AreEqual((byte)0,cue.SubstituteControllerHitCounter);
            for(int i=0;i<rows.Length;i++)Assert.IsNull(Kernel.Project(rows,i,single,SourceBattleScene.Off).Dispatch);
        }
    }
}
