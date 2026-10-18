using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class FeatherDanceAnimationKernelTests
{
    private static WildBattleCombatant[] FeatherBodies(int actor)
    {var b=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","DEFENSE_CURL",100)).ToArray();b[actor]=Mon("PIDGEY","FEATHER_DANCE") with{AccuracyStage=12};return b;}
    private static void Check(SourceKernelResult result,bool single,bool success)
    {
        string before=JsonSerializer.Serialize(result);var events=result.Events;var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(Id("FEATHER_DANCE"),used.Move);Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.Move,pp.CurrentMove);Assert.AreEqual(pp.Before-pp.CommandCost,pp.After);
        var changes=events.OfType<SourceStatChangedEvent>().ToArray();Assert.AreEqual(success?1:0,changes.Length);int roots=0,waves=0;
        for(int i=0;i<events.Length;i++)
        {
            CampaignMoveAnimationDecision Text(SourceBattleScene scene)=>single?AnimationDispatchFixture.AtAttackText(Single(events,i),true,scene):AnimationDispatchFixture.AtAttackText(Double(events,i),scene);
            var decision=Text(SourceBattleScene.On);Assert.AreEqual(decision,Text(SourceBattleScene.On));Assert.IsNull(Text(SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Single(events,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Double(events,i),SourceBattleScene.On).Dispatch);
            if(success&&events[i]==used)
            {
                var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);roots++;Assert.AreEqual(297,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(changes.Single().Sequence,cue.ResultSequence);Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);Assert.IsFalse(cue.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch);
            var wave=single?AnimationDispatchFixture.StatAtResult(Single(events,i),true):AnimationDispatchFixture.StatAtResult(Double(events,i));
            if(success&&events[i]==changes.Single()){Assert.IsNotNull(wave);waves++;Assert.AreEqual(297,wave.SourceMoveId);Assert.AreEqual(46,wave.AnimationArgument);Assert.AreEqual(used.Target,wave.Affected);Assert.AreEqual(pp.Sequence,wave.PpSequence);}
            else Assert.IsNull(wave);
            if(single)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,i),false,SourceBattleScene.On).Dispatch);
        }
        Assert.AreEqual(success?1:0,roots);Assert.AreEqual(success?1:0,waves);Assert.AreEqual(before,JsonSerializer.Serialize(result),"Projection preserves receipts, trace, state and RNG.");
    }
    [TestMethod]
    public void NaturalPidgeyUsesSharedAttackDownTwoReceiptsAcrossAllSlotsBothSinglesSidesAndAllyTargets()
    {
        var definition=Mechanics.Catalog.Moves[Id("FEATHER_DANCE")];Assert.AreEqual(297,definition.SourceMoveId);Assert.AreEqual("EFFECT_ATTACK_DOWN_2",definition.Effect);Assert.AreEqual("MOVE_TARGET_SELECTED",definition.Target);Assert.AreEqual(0,definition.Power);
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})
        {var result=Resolve(Frame(FeatherBodies(actor),single),actor,target);Assert.AreEqual(4,result.State.Slots[target].Body!.AttackStage);Check(result,single,true);}
    }
    [TestMethod,DataRow(0),DataRow(1),DataRow(2)]
    public void AttackCapSuppressesRootAndClampedChangeStillUsesRequestedTwoStageWave(int before)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {int target=actor^1;var b=FeatherBodies(actor);b[target]=b[target] with{AttackStage=before};var r=Resolve(Frame(b,single),actor,target);Assert.AreEqual(0,r.State.Slots[target].Body!.AttackStage);Assert.AreEqual(before==0?1:0,r.Events.OfType<SourceStatAtMinimumEvent>().Count());Check(r,single,before>0);}
    }
    [TestMethod,DataRow("protect"),DataRow("substitute"),DataRow("miss"),DataRow("clear-body"),DataRow("white-smoke"),DataRow("hyper-cutter"),DataRow("mist")]
    public void ActualPreventionNeverBorrowsRootOrStatWave(string branch)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            int target=actor^1;var b=FeatherBodies(actor);
            if(branch=="protect")b[target]=b[target] with{Protected=true};
            if(branch=="substitute")b[target]=b[target] with{CalledVolatile=new(){SubstituteHp=1}};
            if(branch=="miss")b[actor]=b[actor] with{AccuracyStage=0};
            if(branch=="clear-body")b[target]=Mon("TENTACOOL","ACID");
            if(branch=="white-smoke")b[target]=Mon("TORKOAL","EMBER");
            if(branch=="hyper-cutter")b[target]=Mon("KRABBY","VICE_GRIP");
            var frame=Frame(b,single);if(branch=="mist")frame=frame with{Sides=frame.Sides.SetItem(target&1,frame.Sides[target&1] with{MistTurnsRemaining=5})};
            var r=branch=="miss"?Find(frame,actor,target,x=>x.Events.OfType<SourceMoveMissedEvent>().Any()||x.Events.OfType<SourceMoveFailedEvent>().Any(f=>f.Reason=="accuracy"),"Feather Dance accuracy miss"):Resolve(frame,actor,target);
            Assert.AreEqual(6,r.State.Slots[target].Body!.AttackStage);Check(r,single,false);
        }
    }
    [TestMethod]
    public void DedicatedRootRejectsMissingDuplicateForeignAndWrongStatReceiptsWithoutSupportIdTables()
    {
        foreach(bool single in new[]{false,true})
        {
            var r=Resolve(Frame(FeatherBodies(0),single));var events=r.Events;var stat=events.OfType<SourceStatChangedEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();int at=events.IndexOf(stat);
            var variants=new[]{events.Remove(stat),events.Insert(at,stat),events.SetItem(at,stat with{Actor=new(1)}),events.SetItem(at,stat with{Target=new(0)}),events.SetItem(at,stat with{Stat=WildBattleStat.Defense}),events.SetItem(at,stat with{After=5}),events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)})};
            foreach(var invalid in variants)for(int i=0;i<invalid.Length;i++)Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Single(invalid,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtAttackText(Double(invalid,i),SourceBattleScene.On).Dispatch);
            int cursor=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Single(events,cursor),true,SourceBattleScene.On,Mechanics.Catalog.Moves,(_,_)=>false).Dispatch:CampaignMoveAnimationPresentation.AtAttackText(Double(events,cursor),SourceBattleScene.On,Mechanics.Catalog.Moves,(_,_)=>false).Dispatch);
        }
    }
}
