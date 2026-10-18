using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class TeleportResultPageTests : WildBattleFixture
{
    [TestMethod, DataRow(false), DataRow(true)]
    public void RealTeleportKeepsItsTerminalTextPageUntilAcknowledged(bool opponent)
    {
        var player = Owned(opponent ? "SNORLAX" : "ABRA", 100, 101, opponent ? "DEFENSE_CURL" : "TELEPORT");
        var enemy = Owned(opponent ? "ABRA" : "MAGIKARP", 100, 201, opponent ? "TELEPORT" : "SPLASH");
        var session = Controlled([player], enemy);
        int history = session.State.Exploration!.Battles.Length;
        string battleId = session.State.WildBattle!.Id;
        Send(session, new("battle_move", MoveSlot: 0));
        bool sawAttack = false;
        for (int i = 0; i < 32; i++)
        {
            var current = session.State.WildBattle;
            Assert.IsNotNull(current, "The successful escape must not settle before its result text.");
            Assert.AreEqual(CampaignBattlePhase.Events, current.Phase);
            var receipt = current.Events[current.EventIndex];
            if (receipt is WildBattleEndedEvent ended)
            {
                Assert.IsTrue(sawAttack, "The move announcement and dedicated animation boundary precede the fled page.");
                Assert.AreEqual(WildBattleOutcome.Escaped, ended.Outcome);
                Assert.IsNotNull(ended.MoveEscape);
                Assert.AreEqual(opponent ? WildBattleSide.Opponent : WildBattleSide.Player, ended.MoveEscape.Actor);
                Assert.AreEqual("pret:MOVE_TELEPORT", ended.MoveEscape.Move.Value);
                Assert.AreEqual(history, session.State.Exploration!.Battles.Length);
                Assert.IsFalse(session.State.Exploration.Battles.Any(b => b.Id == battleId));
                Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
                Assert.AreEqual(1, current.Events.OfType<WildBattleEndedEvent>().Count(e => e.MoveEscape is not null));
                var source = current.SourceEvents.OfType<SourceEndedEvent>().Single();
                Assert.AreEqual(ended.Sequence, source.Sequence);
                Assert.AreEqual((int)ended.MoveEscape.Actor, source.MoveEscape!.Actor.Value);
                var rng = session.State.World.Rng;
                string party = JsonSerializer.Serialize(session.State.Party);
                var trailing = current.Events.Skip(current.EventIndex + 1).ToArray();
                Assert.AreEqual(1, trailing.Length, "This escape's only trailing receipt is its PP party synchronization.");
                Assert.IsTrue(trailing[0] is WildBattleCalledPartyChangedEvent { Reason: "active-sync" } sync &&
                    sync.SourceSlot == ended.MoveEscape.Actor && sync.Side == ended.MoveEscape.Actor,
                    "No user-visible result may be silently dismissed after the fled page.");
                Send(session, new("ack"));
                // The real view auto-ACKs this existing silent bookkeeping receipt.
                foreach (var diagnostic in trailing)
                {
                    var pending = session.State.WildBattle;
                    Assert.IsNotNull(pending);
                    Assert.AreEqual(CampaignBattlePhase.Events, pending.Phase);
                    Assert.AreEqual(diagnostic, pending.Events[pending.EventIndex]);
                    Send(session, new("ack"));
                }
                Assert.IsNull(session.State.WildBattle);
                Assert.AreEqual(rng, session.State.World.Rng, "Dismissing the page draws no battle randomness.");
                Assert.AreEqual(party, JsonSerializer.Serialize(session.State.Party), "The terminal page does not reapply PP or party writes.");
                Assert.AreEqual(1, session.State.Exploration!.Battles.Count(b => b.Id == battleId));
                Assert.AreEqual(battleId, session.State.Exploration.Battles[^1].Id);
                Assert.AreEqual(WildBattleOutcome.Escaped, session.State.Exploration.Battles[^1].Outcome);
                return;
            }
            if (receipt is WildBattleMoveUsedEvent { Move.Value: "pret:MOVE_TELEPORT" }) sawAttack = true;
            Send(session, new("ack"));
        }
        Assert.Fail("The real escape result was not reached within its bounded presentation batch.");
    }

    [TestMethod]
    public void OrdinaryRunKeepsItsExistingRunReceiptAndNoExtraTerminalPage()
    {
        var session = Controlled([Owned("ABRA", 100, 101, "TELEPORT")], Owned("MAGIKARP", 5, 201, "SPLASH"));
        Send(session, new("battle_menu", Text: "run"));
        var battle = session.State.WildBattle;
        Assert.IsNotNull(battle);
        Assert.IsTrue(battle.Events.OfType<WildBattleRunAttemptEvent>().Single().Escaped);
        Assert.IsTrue(battle.Events.OfType<WildBattleEndedEvent>().All(e => e.MoveEscape is null));
        for (int i = 0; i < 32 && session.State.WildBattle is { } pending; i++)
        {
            Assert.IsFalse(pending.Events[pending.EventIndex] is WildBattleEndedEvent,
                "The ordinary Run result remains its original page, with no extra terminal confirmation.");
            Send(session, new("ack"));
        }
        Assert.IsNull(session.State.WildBattle);
    }
}
