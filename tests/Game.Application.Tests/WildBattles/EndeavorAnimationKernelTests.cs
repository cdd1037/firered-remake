using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Change=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Called=FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class EndeavorAnimationKernelTests
{
    private static SourceBattleFrame Frame(bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        var user=Kernel.Mon("RATICATE","ENDEAVOR",50);
        bodies[actor]=user with{Pokemon=user.Pokemon with{CurrentHp=1},AccuracyStage=12,SpeedStage=12};
        return Kernel.Frame(bodies,single);
    }
    private static void Check(ImmutableArray<SourceBattleEvent> events,bool single,bool accepted)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var hit=Envelope.Hits(events).SingleOrDefault();int count=0;
        for(int i=0;i<events.Length;i++)
        {
            var result=Kernel.Project(events,i,single);Assert.AreEqual(result,Kernel.Project(events,i,single));
            Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);
            if(!accepted||events[i]!=used){Assert.IsNull(result.Dispatch);continue;}
            Assert.IsNotNull(result.Dispatch,result.Reason);count++;Assert.AreEqual(283,result.Dispatch.SourceMoveId);
            Assert.AreEqual(hit!.Sequence,result.Dispatch.ResultSequence);Assert.IsNull(result.Dispatch.MovePower);
            int damage=hit switch{SourceHpChangedEvent h=>h.Damage,SourceSubstituteDamagedEvent s=>s.Damage,_=>-1};
            Assert.AreEqual(damage,result.Dispatch.MoveDamage);Assert.IsTrue(damage>0);
        }
        Assert.AreEqual(accepted?1:0,count);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void CommittedPreClampDamageOwnsTheShakeAcrossPositionsAndSubstitutes(bool single)
    {
        for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var frame=Frame(single,actor);if(substitute)frame=Change.Body(frame,actor^1,b=>b with{CalledVolatile=new(){SubstituteHp=1}});
            string before=JsonSerializer.Serialize(frame);var result=Kernel.Ordered(frame,actor,actor^1);Check(result.Events,single,true);
            if(substitute){var hit=result.Events.OfType<SourceSubstituteDamagedEvent>().Single();Assert.IsTrue(hit.Damage>hit.HpBefore);Assert.AreEqual(0,hit.HpAfter);}
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Kernel.Ordered(frame,actor,actor^1)));
        }
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void FailedHpDifferenceProtectionAndGhostImmunityDoNotAnimate(bool single)
    {
        foreach(int branch in new[]{0,1,2})
        {
            var frame=Change.Body(Frame(single),1,b=>branch switch{0=>b with{Pokemon=b.Pokemon with{CurrentHp=1}},1=>b with{Protected=true},_=>Kernel.Mon("GASTLY","LICK",100)});
            Check(Envelope.Resolve(frame).Events,single,false);
        }
        var events=Envelope.Resolve(Frame(single)).Events;var pp=events.OfType<SourcePpCommandEvent>().Single();
        Check(events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),single,false);
    }
    [TestMethod]
    public void ActualCalledLeafRetainsCallerPaymentAndCommittedDamage()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Caller.Bodies(118,0);bodies[0]=bodies[0] with{Pokemon=bodies[0].Pokemon with{CurrentHp=1}};
            bodies[1]=Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0};
            var frame=Kernel.Frame(bodies,single) with{Rng=new(Caller.MetronomeSeed(283))};var result=Envelope.Resolve(frame);
            Called.Check(result,single,Kernel.Id("METRONOME"),283);
            var used=result.Events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Kernel.Id("ENDEAVOR"));
            var cue=Kernel.Project(result.Events,result.Events.IndexOf(used),single).Dispatch;Assert.IsNotNull(cue);
            Assert.AreEqual(result.Events.OfType<SourceHpChangedEvent>().Single().Damage,cue.MoveDamage);Assert.IsNull(cue.MovePower);
        }
    }
}
