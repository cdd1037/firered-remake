using Game.Domain.Ids;
using Game.Domain.WildBattle;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class AbilityPreventionPresentationTests
{
    [TestMethod]
    [DataRow("ABILITY_OWN_TEMPO","sText_PkmnPreventsConfusionWith")]
    [DataRow("ABILITY_HYPER_CUTTER","sText_PkmnsXPreventsYLoss")]
    [DataRow("ABILITY_KEEN_EYE","sText_PkmnsXPreventsYLoss")]
    [DataRow("ABILITY_WHITE_SMOKE","sText_PkmnPreventsStatLossWith")]
    [DataRow("ABILITY_CLEAR_BODY","sText_PkmnPreventsStatLossWith")]
    public void SinglesUsesSharedSourceAbilityMessageAndAffectedParticipant(string ability,string symbol)
    {
        foreach(var target in new[]{WildBattleSide.Player,WildBattleSide.Opponent})
        {
            var actor=target==WildBattleSide.Player?WildBattleSide.Opponent:WildBattleSide.Player;
            WildBattleEvent[] events=[new WildBattleAbilityPreventedEvent(1,1,actor,target,new MoveId("pret:MOVE_SWAGGER"),ability)];
            var display=SourceTrainerPresentationModel.Describe(events,0,s=>s.ToString(),m=>m.Value,(_,_)=>"party");
            Assert.IsNotNull(display);Assert.IsFalse(display.Unsupported);Assert.IsFalse(display.Silent);
            var text=display.Texts.Single();Assert.AreEqual(symbol,text.Symbol);
            Assert.AreEqual(symbol,SourceTrainerPresentationModel.AbilityPreventionSymbol(ability));
            var fields=text.Values.ToDictionary(x=>x.Key,x=>x.Value);
            Assert.AreEqual(target.ToString(),fields["B_DEF_NAME_WITH_PREFIX"]);
            Assert.AreEqual(target.ToString(),fields["B_SCR_ACTIVE_NAME_WITH_PREFIX"]);
            Assert.AreEqual(ability[8..].Replace('_',' '),fields["B_SCR_ACTIVE_ABILITY"]);
            Assert.AreEqual(actor.ToString(),fields["B_ATK_NAME_WITH_PREFIX"]);
            Assert.AreEqual(ability=="ABILITY_HYPER_CUTTER"?"ATTACK":ability=="ABILITY_KEEN_EYE"?"accuracy":"",fields["B_BUFF1"]);
        }
    }
    [TestMethod][DataRow("ABILITY_VOLT_ABSORB")][DataRow("ABILITY_WATER_ABSORB")]
    public void AbsorbPreventionRemainsSilentUntilItsMatchingRecovery(string ability)
    {
        var move=new MoveId("pret:MOVE_THUNDERBOLT");
        WildBattleEvent[] events=[new WildBattleAbilityPreventedEvent(1,1,WildBattleSide.Player,WildBattleSide.Opponent,move,ability),new WildBattleRecoveredEvent(2,1,WildBattleSide.Opponent,move,5,10,5)];
        var before=SourceTrainerPresentationModel.Describe(events,0,s=>s.ToString(),m=>m.Value,(_,_)=>"party");
        Assert.IsTrue(before!.Silent);
        var recovered=SourceTrainerPresentationModel.Describe(events,1,s=>s.ToString(),m=>m.Value,(_,_)=>"party")!.Texts.Single();
        Assert.AreEqual("sText_PkmnRestoredHPUsing",recovered.Symbol);
        Assert.AreEqual(ability[8..].Replace('_',' '),recovered.Values.Single(x=>x.Key=="B_DEF_ABILITY").Value);
    }
}
