using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Legal = FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class FriendshipContactAnimationKernelTests
{
    private static WildBattleCombatant[] Bodies(int id,int actor=0,int friendship=128)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100)).ToArray();
        var body=id switch {
            216=>Legal.MachineMon("RATICATE","TM27","RETURN"),
            218=>Legal.MachineMon("RATICATE","TM21","FRUSTRATION"),
            122=>Kernel.Mon("GASTLY","LICK",100),
            165=>Kernel.Mon("RATICATE","TACKLE",100),
            _=>throw new ArgumentOutOfRangeException(nameof(id))};
        body=body with{AccuracyStage=12,SpeedStage=12,Pokemon=body.Pokemon with{Friendship=friendship}};
        if(id==165)body=body with{Pokemon=body.Pokemon with{Moves=body.Pokemon.Moves.Select(m=>m with{CurrentPp=0}).ToImmutableArray()}};
        bodies[actor]=body;return bodies;
    }
    private static void Check(SourceFourActionResult result,int id,bool single,bool success=true)
    {
        string before=JsonSerializer.Serialize(result);var events=result.Events;
        var used=events.OfType<SourceMoveUsedEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
        var hit=Kernel.Hits(events).SingleOrDefault();int count=0;
        for(int i=0;i<events.Length;i++)
        {
            var decision=Kernel.Project(events,i,single);Assert.AreEqual(decision,Kernel.Project(events,i,single));
            Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Envelope.Single(events,i),true,SourceBattleScene.On).Dispatch
                :AnimationDispatchFixture.AtResult(Envelope.Double(events,i),SourceBattleScene.On).Dispatch);
            if(!success||events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);Assert.IsNotNull(hit);count++;
            Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);
            Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(hit.Sequence,cue.ResultSequence);
            Assert.AreEqual(hit is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);
        }
        Assert.AreEqual(success?1:0,count);Assert.AreEqual(before,JsonSerializer.Serialize(result));
    }
    [TestMethod,DataRow(216),DataRow(218),DataRow(122),DataRow(165)]
    public void NaturalOrForcedActionsOwnOneRootAcrossEveryActorAndSingles(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var result=Kernel.Ordered(Kernel.Frame(Bodies(id,actor),single),actor,actor^1,id==165);
            Check(result,id,single);
            if(id==165)
            {
                var used=result.Events.OfType<SourceMoveUsedEvent>().Single();var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
                Assert.AreEqual(-1,used.Slot);Assert.AreEqual(0,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(1,pp.CommandCost);
                Assert.IsTrue(result.Events.OfType<SourceRecoilEvent>().Any(e=>e.Move==used.Move&&e.Actor==used.Actor));
            }
        }
    }
    [TestMethod,DataRow(216),DataRow(218)]
    public void FriendshipBoundariesUseCommittedDamageWithoutReselectingPower(int id)
    {
        foreach(int friendship in new[]{0,30,31,60,61,91,92,100,101,200,201,255})
        {
            var result=Kernel.Ordered(Kernel.Frame(Bodies(id,friendship:friendship)));
            Check(result,id,false);
        }
    }
    [TestMethod,DataRow(216),DataRow(218),DataRow(122),DataRow(165)]
    public void SubstituteAndProtectRetainOrdinaryHitOwnership(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(id);foreach(int target in id==165&&!single?new[]{1,3}:new[]{1})
                bodies[target]=bodies[target] with{CalledVolatile=new(){SubstituteHp=1}};
            var sub=Kernel.Ordered(Kernel.Frame(bodies,single),forcedStruggle:id==165);
            Assert.AreEqual(1,sub.Events.OfType<SourceSubstituteDamagedEvent>().Count());Check(sub,id,single);
            bodies=Bodies(id);foreach(int target in id==165&&!single?new[]{1,3}:new[]{1})
                bodies[target]=bodies[target] with{Protected=true};
            var blocked=Kernel.Ordered(Kernel.Frame(bodies,single),forcedStruggle:id==165);
            Assert.IsTrue(blocked.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"));Check(blocked,id,single,false);
        }
    }
    [TestMethod]
    public void ForcedStruggleNominalPressureCostDoesNotSpendSyntheticPp()
    {
        var bodies=Bodies(165);foreach(int target in new[]{1,3})bodies[target]=Kernel.Mon("ARTICUNO","MIST",100);
        var result=Kernel.Ordered(Kernel.Frame(bodies),forcedStruggle:true);
        var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(2,pp.CommandCost);Assert.AreEqual(0,pp.Before);Assert.AreEqual(0,pp.After);
        Check(result,165,false);
    }
    [TestMethod,DataRow(216),DataRow(218)]
    public void TransformDisplayProjectionRetainsActorFriendshipRatherThanDonorFriendship(int id)
    {
        var bodies=Bodies(id,1,255);var ditto=Kernel.Mon("DITTO","TRANSFORM",100);
        bodies[0]=ditto with{Pokemon=ditto.Pokemon with{Friendship=31}};
        var before=Kernel.Frame(bodies);var transformed=Envelope.Resolve(before);
        Assert.AreEqual(1,transformed.Events.OfType<SourceTransformedEvent>().Count());
        var display=CampaignBattleDisplayBodies.FromSlots(before.Slots);
        foreach(var receipt in transformed.Events)display=display.Apply(receipt);
        var body=display.Bodies[0];Assert.IsNotNull(body);Assert.IsNotNull(body.Transform);
        Assert.AreEqual(31,body.Pokemon.Friendship);Assert.AreEqual(31,body.Transform.OriginalPokemon.Friendship);
        Assert.AreEqual(255,body.Transform.CopiedPokemon.Friendship);
    }
    [TestMethod]
    public void ForcedStruggleBypassesGhostAndRockHeadAndKeepsItsRootWhenRecoilFaintsActor()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(165);var actor=Kernel.Mon("GEODUDE","TACKLE",100);
            Assert.AreEqual("ABILITY_ROCK_HEAD",WildBattleEffective.Ability(actor));
            bodies[0]=actor with{AccuracyStage=12,SpeedStage=12,Pokemon=actor.Pokemon with{CurrentHp=1,
                Moves=actor.Pokemon.Moves.Select(m=>m with{CurrentPp=0}).ToImmutableArray()}};
            foreach(int target in new[]{1,3})bodies[target]=Kernel.Mon("GASTLY","LICK",100) with{SpeedStage=0};
            var result=Kernel.Ordered(Kernel.Frame(bodies,single),forcedStruggle:true);
            var recoil=result.Events.OfType<SourceRecoilEvent>().Single();Assert.AreEqual(0,recoil.HpAfter);
            Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==0));
            Assert.IsFalse(result.Events.OfType<SourceEffectivenessEvent>().Any());Check(result,165,single);
        }
    }
    [TestMethod]
    public void ForcedShapeCannotBeBorrowedByRealSlotsCalledLeavesOrMalformedPp()
    {
        foreach(bool single in new[]{false,true})
        {
            var result=Kernel.Ordered(Kernel.Frame(Bodies(165),single),forcedStruggle:true);var events=result.Events;
            int usedIndex=Array.FindIndex(events.ToArray(),e=>e is SourceMoveUsedEvent);
            int ppIndex=Array.FindIndex(events.ToArray(),e=>e is SourcePpCommandEvent);
            var used=(SourceMoveUsedEvent)events[usedIndex];var pp=(SourcePpCommandEvent)events[ppIndex];
            foreach(var invalid in new[]{pp with{CommandCost=0},pp with{CommandCost=3},pp with{Before=1},pp with{After=1},pp with{PpSlot=0},pp with{PpOwner=new(1)},pp with{ChosenMove=Kernel.Id("METRONOME")},pp with{Target=used.Actor}})
                Assert.IsNull(Kernel.Project(events.SetItem(ppIndex,invalid),usedIndex,single).Dispatch);
            foreach(int slot in new[]{-2,0,3,4})
                Assert.IsNull(Kernel.Project(events.SetItem(usedIndex,used with{Slot=slot}).SetItem(ppIndex,pp with{PpSlot=slot}),usedIndex,single).Dispatch);
            var attack=(SourceAttackStringEvent)events[usedIndex-1];
            Assert.IsNull(Kernel.Project(events.SetItem(usedIndex-1,attack with{ChosenMove=Kernel.Id("METRONOME")}),usedIndex,single).Dispatch);
            Assert.IsNull(Kernel.Project(events.RemoveAt(ppIndex),usedIndex,single).Dispatch);
        }
    }
}
