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
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class WeatherBallAnimationKernelTests
{
    private static SourceBattleFrame Frame(bool single,int actor,int weather)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("CASTFORM","WEATHER_BALL",50) with{SpeedStage=12,AccuracyStage=12};
        var frame=Kernel.Frame(bodies,single);
        return frame with{Field=frame.Field with{SunTurnsRemaining=weather==1?5:0,RainTurnsRemaining=weather==2?5:0,SandstormTurnsRemaining=weather==3?5:0,HailTurnsRemaining=weather==4?5:0}};
    }
    private static ushort Bits(int weather)=>weather switch{1=>32,2=>1,3=>8,4=>128,_=>0};
    private static void Check(ImmutableArray<SourceBattleEvent> events,bool single,ushort flags)
    {
        var used=events.OfType<SourceMoveUsedEvent>().Single();var hit=Envelope.Hits(events).Single();string before=JsonSerializer.Serialize(events);int cues=0;
        Assert.AreEqual(flags,hit switch{SourceHpChangedEvent h=>h.PreparedAnimationWeather,SourceSubstituteDamagedEvent h=>h.PreparedAnimationWeather,_=>null});
        for(int i=0;i<events.Length;i++)
        {
            var decision=Kernel.Project(events,i,single);Assert.AreEqual(decision,Kernel.Project(events,i,single));
            Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);
            if(events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            Assert.IsNotNull(decision.Dispatch,decision.Reason);cues++;Assert.AreEqual(311,decision.Dispatch.SourceMoveId);Assert.AreEqual(flags,decision.Dispatch.AnimationWeather);Assert.AreEqual(hit.Sequence,decision.Dispatch.ResultSequence);
        }
        Assert.AreEqual(1,cues);Assert.AreEqual(before,JsonSerializer.Serialize(events));
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)][DataRow(4)]
    public void TemporaryWeatherIsCapturedBeforeBodyOrSubstituteDamageForAllPositions(int weather)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var frame=Frame(single,actor,weather);if(substitute)frame=Change.Body(frame,actor^1,b=>b with{CalledVolatile=new(){SubstituteHp=1}});
            string before=JsonSerializer.Serialize(frame);var result=Kernel.Ordered(frame,actor,actor^1);Check(result.Events,single,Bits(weather));
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Kernel.Ordered(frame,actor,actor^1)));
        }
    }
    [TestMethod]
    public void PermanentWeatherKeepsBothSourceTemporaryAndPermanentBits()
    {
        foreach(var (kind,bits) in new[]{(SourcePermanentWeatherKind.Sun,(ushort)96),(SourcePermanentWeatherKind.Rain,(ushort)5),(SourcePermanentWeatherKind.Sandstorm,(ushort)24)})
        {
            var frame=Frame(true,0,0);frame=frame with{Field=frame.Field with{PermanentWeather=new(kind)}};
            Check(Envelope.Resolve(frame).Events,true,bits);
        }
    }
    [TestMethod]
    public void ControllerSuppressionIncludesFaintedCloudNineAndAirLockBodies()
    {
        foreach(bool fainted in new[]{false,true})foreach(string species in new[]{"PSYDUCK","RAYQUAZA"})
        {
            var suppressor=Kernel.Mon(species,species=="PSYDUCK"?"SCRATCH":"TWISTER",100,personality:species=="PSYDUCK"?1u:0u);
            if(fainted)suppressor=suppressor with{Pokemon=suppressor.Pokemon with{CurrentHp=0}};
            var frame=Change.Body(Frame(false,0,2),3,_=>suppressor);Assert.IsTrue(SourceWeatherRules.IsSuppressed(frame,includeFainted:true));
            Check(Envelope.Resolve(frame).Events,false,0);
        }
    }
    [TestMethod]
    public void MissingOrOutOfRangePreparedWeatherCannotBorrowTheCurrentField()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Frame(single,0,1)).Events;var hit=events.OfType<SourceHpChangedEvent>().Single();
            foreach(ushort? value in new ushort?[]{null,256,65535})
            {
                var broken=events.SetItem(events.IndexOf(hit),hit with{PreparedAnimationWeather=value});
                for(int i=0;i<broken.Length;i++)Assert.IsNull(Kernel.Project(broken,i,single).Dispatch);
            }
        }
    }
    [TestMethod]
    public void ActualCalledLeafKeepsControllerWeatherAndCallerPayment()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Caller.Frame(118,single);frame=frame with{Rng=new(Caller.MetronomeSeed(311)),Field=frame.Field with{HailTurnsRemaining=5}};
            var result=Envelope.Resolve(frame);var cue=Called.Check(result,single,Kernel.Id("METRONOME"),311);
            Assert.AreEqual((ushort)128,cue.AnimationWeather);
        }
    }
}
