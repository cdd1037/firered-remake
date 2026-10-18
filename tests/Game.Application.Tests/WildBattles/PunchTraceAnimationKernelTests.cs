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
using Called=FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class PunchTraceAnimationKernelTests
{
    // Natural source learners; controlled speed/HP/substitute states are kernel
    // fixtures and do not claim route-play, encounter or acquisition evidence.
    private static SourceBattleFrame Frame(int move,bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon(move==183?"HITMONCHAN":"HAUNTER",move==183?"MACH_PUNCH":"SHADOW_PUNCH") with{AccuracyStage=12,SpeedStage=12};
        return Kernel.Frame(bodies,single);
    }
    private static void Check(ImmutableArray<SourceBattleEvent> events,int move,bool single,bool accepted=true)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();var hits=Envelope.Hits(events);int roots=0;
        Assert.AreEqual(move,Kernel.Mechanics.Catalog.Moves[used.Move].SourceMoveId);Assert.AreEqual(used.Move,attack.CurrentMove);Assert.AreEqual(used.Move,attack.ChosenMove);Assert.AreEqual(used.Move,pp.CurrentMove);Assert.AreEqual(used.Move,pp.ChosenMove);Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.Slot,pp.PpSlot);Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);
        for(int i=0;i<events.Length;i++)
        {
            var decision=Kernel.Project(events,i,single);Assert.AreEqual(decision,Kernel.Project(events,i,single));Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Envelope.Single(events,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Envelope.Double(events,i),SourceBattleScene.On).Dispatch);
            if(!accepted||events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);roots++;Assert.AreEqual(1,hits.Length);Assert.AreEqual(move,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(hits[0].Sequence,cue.ResultSequence);Assert.AreEqual(hits[0] is SourceSubstituteDamagedEvent,cue.HitsSubstitute);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);Assert.IsNull(cue.MoveTurn);Assert.IsNull(cue.MovePower);Assert.IsNull(cue.MoveDamage);
        }
        Assert.AreEqual(accepted?1:0,roots);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    [TestMethod][DataRow(183)][DataRow(325)]
    public void ActualOrderedKernelHitsUseTheExistingCatalogDispatchForEveryPosition(int move)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Frame(move,single,actor);string before=JsonSerializer.Serialize(frame);var result=Kernel.Ordered(frame,actor,actor^1);Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage>0);Check(result.Events,move,single);Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Kernel.Ordered(frame,actor,actor^1)));
        }
    }
    [TestMethod][DataRow(183)][DataRow(325)]
    public void SubstituteAndLaterKnockoutDoNotReconstructOrReplayTheLaunch(int move)
    {
        foreach(bool single in new[]{false,true})foreach(bool substitute in new[]{false,true})
        {
            var frame=Change.Body(Frame(move,single),1,b=>substitute?b with{CalledVolatile=new(){SubstituteHp=1}}:b with{Pokemon=b.Pokemon with{CurrentHp=1}});var result=Kernel.Ordered(frame);Check(result.Events,move,single);
            if(substitute)Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);else Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
        }
    }
    [TestMethod][DataRow(183)][DataRow(325)]
    public void ProtectionImmunityAndMalformedOwnershipCannotBorrowTheRoot(int move)
    {
        foreach(bool single in new[]{false,true})
        {
            foreach(bool protect in new[]{false,true})
            {var frame=Change.Body(Frame(move,single),1,b=>protect?b with{Protected=true}:Kernel.Mon(move==183?"GASTLY":"SNORLAX",move==183?"LICK":"TACKLE",100));var result=Envelope.Resolve(frame);Check(result.Events,move,single,false);}
            var events=Envelope.Resolve(Frame(move,single)).Events;var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=Envelope.Hits(events).Single();
            foreach(var broken in new[]{events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),events.Remove(hit),Shared.Number(events.Insert(events.IndexOf(hit),hit))})
                for(int i=0;i<broken.Length;i++)Assert.IsNull(Kernel.Project(broken,i,single).Dispatch);
        }
    }
    [TestMethod][DataRow(183)][DataRow(325)]
    public void ActualMetronomeLeafKeepsTheCallerPpAndExistingHitContract(int move)
    {
        uint seed=Caller.MetronomeSeed(move);
        foreach(bool single in new[]{false,true})
        {
            var bodies=Caller.Bodies(118,0);bodies[1]=Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0};var frame=Kernel.Frame(bodies,single) with{Rng=new(seed)};
            var result=Envelope.Resolve(frame);Called.Check(result,single,Kernel.Id("METRONOME"),move);
        }
    }
}
