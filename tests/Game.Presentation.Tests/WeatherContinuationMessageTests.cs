using Game.Domain.WildBattle;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WeatherContinuationMessageTests
{
    [TestMethod]
    [DataRow("rain","sText_RainContinues")]
    [DataRow("sun","sText_SunlightStrong")]
    [DataRow("sandstorm","sText_SandstormRages")]
    [DataRow("hail","sText_HailContinues")]
    public void PermanentWeatherHasOneTextAndTemporaryCompanionIsSilent(string weather,string symbol)
    {
        var continued=new WildBattleWeatherContinuedEvent(11,3,WildBattleSide.Player,weather);
        var permanent=Describe([continued],0);
        Assert.IsNotNull(permanent);Assert.IsFalse(permanent.Unsupported);
        Assert.AreEqual(symbol,permanent.Texts.Single().Symbol);
        var timer=new WildBattleMtMoonVolatileEvent(10,3,WildBattleSide.Player,weather,3,2,"countdown");
        var companion=Describe([timer,continued],1);
        Assert.IsNotNull(companion);Assert.IsTrue(companion.Silent);Assert.IsFalse(companion.Unsupported);
    }
    private static SourceTrainerEventDisplay? Describe(WildBattleEvent[] events,int index)=>
        SourceTrainerPresentationModel.Describe(events,index,s=>s.ToString(),m=>m.Value,(s,i)=>$"{s}:{i}");
}
