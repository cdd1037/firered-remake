using Game.Application;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SceneOptionBattleTests : WildBattleFixture
{
    [TestMethod]
    public void SceneOffKeepsPublicEncounterOutcomePpExperienceAndRngIdentical()
    {
        var basis = FieldFixture();
        var on = CampaignSession.RestoreCurrent(content, basis, checkpoint.Generation);
        var off = CampaignSession.RestoreCurrent(content, basis, checkpoint.Generation);
        Send(on, new("options_save") { Options = SourceGameOptions.Defaults });
        Send(off, new("options_save") { Options = SourceGameOptions.Defaults with { BattleScene = SourceBattleScene.Off } });
        foreach (var session in new[] { on, off })
        {
            Send(session, new("turn", Direction: Direction.North));
            Drain(session);
            Assert.IsNotNull(session.State.WildBattle);
            Assert.AreEqual(CampaignBattlePhase.ChooseMove, session.State.WildBattle.Phase);
            Send(session, new("battle_move", MoveSlot: 0));
            var battle = session.State.WildBattle!;
            string beforeProjection = CanonicalJson.Hash(session.State);
            for (int index = 0; index < battle.Events.Length; index++)
            {
                var decision = AnimationDispatchFixture.AtAttackText(battle with { EventIndex = index }, true, SourceBattleScene.Off);
                Assert.IsNull(decision.Dispatch);
                Assert.AreEqual("animations-disabled", decision.Reason);
            }
            Assert.AreEqual(beforeProjection, CanonicalJson.Hash(session.State));
            Drain(session);
            Assert.IsNull(session.State.WildBattle, "The controlled public encounter must complete.");
            Assert.AreEqual(WildBattleOutcome.Won, session.State.Exploration!.Battles[^1].Outcome);
        }
        Assert.AreEqual(on.Revision, off.Revision);
        Assert.AreEqual(CanonicalJson.Hash(on.State with { Options = null }), CanonicalJson.Hash(off.State with { Options = null }));
    }
}
