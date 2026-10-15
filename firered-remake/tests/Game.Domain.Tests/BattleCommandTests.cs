using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Battle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Domain;
[TestClass, TestCategory("Game")]
public sealed class BattleCommandTests : DomainFixture
{

    [TestMethod]
    public void battle_queue_without_rng_pp_hp()
    {
        var s = Battle();
        var t = BattleRules.Apply(s, Player(s), catalog);
        Assert.AreEqual(TransitionStatus.Accepted, t.Status);
        Assert.AreEqual(1L, t.State.Revision);
        Assert.AreEqual(1, t.State.Turn);
        Assert.AreEqual(s.Rng, t.State.Rng);
        Assert.AreEqual(1, t.State.PendingMoves.Length);
        Assert.AreEqual(35, t.State.Battlers[0].Moves[0].Pp);
        Assert.AreEqual(100, t.State.Battlers[0].Hp);
        Assert.AreEqual(1, t.Events.Length);
        Assert.AreEqual(0, t.RandomTrace.Length);
    }

    [TestMethod]
    public void battle_controlled_turn_golden()
    {
        var t = Turn(Battle());
        Assert.AreEqual(TransitionStatus.Accepted, t.Status);
        Assert.AreEqual(2L, t.State.Revision);
        Assert.AreEqual(2, t.State.Turn);
        Assert.AreEqual(8L, t.State.EventSequence);
        Assert.AreEqual(new RngState(3805062638, 6), t.State.Rng);
        Assert.AreEqual(94, t.State.Battlers[0].Hp);
        Assert.AreEqual(94, t.State.Battlers[1].Hp);
        Assert.AreEqual(34, t.State.Battlers[0].Moves[0].Pp);
        Assert.AreEqual(6, t.RandomTrace.Length);
        Assert.AreEqual("MoveChosenEvent,TurnOrderEvent,MoveUsedEvent,HpChangedEvent,MoveUsedEvent,HpChangedEvent,TurnEndedEvent", string.Join(',', t.Events.Select(x => x.GetType().Name)));
    }

    [TestMethod]
    public void battle_event_causes_sequence()
    {
        var t = Turn(Battle());
        Assert.AreEqual(2L, t.Events[0].Metadata.Sequence);
        Assert.AreEqual(8L, t.Events[^1].Metadata.Sequence);
        Assert.AreEqual(new CommandId("cmd/player"), t.Events.OfType<HpChangedEvent>().First().Metadata.CausedByCommandId);
        Assert.IsTrue(t.Events.All(x => x.Metadata.BattleId == t.State.BattleId));
    }

    [TestMethod]
    public void battle_stale_revision_rejected()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s)with { ExpectedRevision = 9 }, catalog), TransitionStatus.Rejected, "stale-revision");
    }

    [TestMethod]
    public void battle_no_pp_rejected()
    {
        var s = Battle();
        s = s with
        {
            Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Moves = [new(fixtureMove.Id, 0, 35)] })
        };
        Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Rejected, "no-pp");
    }

    [TestMethod]
    public void battle_invalid_target_rejected()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s)with { TargetId = new("player") }, catalog), TransitionStatus.Rejected, "invalid-target");
    }

    [TestMethod]
    public void battle_unknown_actor_rejected()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s)with { ActorId = new("stranger") }, catalog), TransitionStatus.Rejected, "unknown-actor");
    }

    [TestMethod]
    public void battle_invalid_slot_rejected()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s)with { MoveSlot = -1 }, catalog), TransitionStatus.Rejected, "invalid-move-slot");
    }

    [TestMethod]
    public void battle_already_chosen_rejected()
    {
        var s = Battle();
        s = BattleRules.Apply(s, Player(s), catalog).State;
        Unchanged(s, BattleRules.Apply(s, Player(s, "cmd/other"), catalog), TransitionStatus.Rejected, "already-selected");
    }

    [TestMethod]
    public void battle_duplicate_pending_id_rejected()
    {
        var s = Battle();
        s = BattleRules.Apply(s, Player(s), catalog).State;
        Unchanged(s, BattleRules.Apply(s, Opponent(s, "cmd/player"), catalog), TransitionStatus.Rejected, "duplicate-command-id");
    }

    [TestMethod]
    public void battle_missing_content_fault()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s), new Catalog()), TransitionStatus.Fault, "missing-move-content");
    }

    [TestMethod]
    public void battle_unsupported_effect_fault()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s), new Catalog(fixtureMove with { Effect = MoveEffect.Unsupported })), TransitionStatus.Fault, "unsupported-move");
    }

    [TestMethod]
    public void battle_double_mode_fault()
    {
        var s = Battle()with
        {
            Mode = BattleMode.Doubles
        };
        Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "unsupported-rules-profile");
    }

    [TestMethod]
    public void battle_badge_profile_fault()
    {
        var s = Battle()with
        {
            ActiveMechanics = UnsupportedBattleMechanics.Badges
        };
        Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "unsupported-rules-profile");
    }

    [TestMethod]
    public void battle_invalid_hp_fault()
    {
        var s = Battle();
        s = s with
        {
            Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Hp = 101 })
        };
        Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "invalid-combatant");
    }

    [TestMethod]
    public void battle_corrupt_pending_fault()
    {
        var s = Battle();
        s = s with
        {
            PendingMoves = [new(new("cmd/tamper"), new("player"), 3, new("opponent"), fixtureMove.Id)]
        };
        Unchanged(s, BattleRules.Apply(s, Opponent(s), catalog), TransitionStatus.Fault, "invalid-pending-move");
    }

    [TestMethod]
    public void battle_fast_player_first()
    {
        Assert.AreEqual(new CombatantId("player"), Turn(Battle()).Events.OfType<TurnOrderEvent>().Single().Actors[0]);
    }

    [TestMethod]
    public void battle_priority_before_speed()
    {
        var s = Battle();
        var quick = fixtureMove with
        {
            Id = new("fixture/quick"),
            Priority = 1
        };
        s = s with
        {
            Battlers = s.Battlers.SetItem(1, s.Battlers[1] with { Moves = [new(quick.Id, 35, 35)] })
        };
        Assert.AreEqual(new CombatantId("opponent"), Turn(s, new Catalog(fixtureMove, quick)).Events.OfType<TurnOrderEvent>().Single().Actors[0]);
    }

    [TestMethod]
    public void battle_speed_tie_draw()
    {
        var s = Battle();
        s = s with
        {
            Battlers = s.Battlers.SetItem(1, s.Battlers[1] with { Stats = s.Battlers[1].Stats with { Speed = 20 } })
        };
        var t = Turn(s);
        Assert.AreEqual("turn-order:speed-tie", t.RandomTrace[0].Purpose);
        Assert.AreEqual(new CombatantId("player"), t.Events.OfType<TurnOrderEvent>().Single().Actors[0]);
        Assert.AreEqual(7, t.RandomTrace.Length);
    }

    [TestMethod]
    public void battle_zero_accuracy_not_assumed_always_hit()
    {
        var s = Battle();
        Unchanged(s, BattleRules.Apply(s, Player(s), new Catalog(fixtureMove with { Accuracy = 0 })), TransitionStatus.Fault, "unsupported-move");
    }

    [TestMethod]
    public void battle_miss_spends_pp_only()
    {
        var t = Turn(Battle(1), new Catalog(fixtureMove with { Accuracy = 1 }));
        Assert.IsTrue(t.Events.OfType<MoveMissedEvent>().Any());
        Assert.AreEqual(34, t.State.Battlers[0].Moves[0].Pp);
        Assert.IsTrue(t.RandomTrace.Length < 6);
    }

    [TestMethod]
    public void battle_ko_stops_opponent_rng_pp()
    {
        var s = Battle();
        s = s with
        {
            Battlers = s.Battlers.SetItem(1, s.Battlers[1] with { Hp = 1 })
        };
        var t = Turn(s);
        Assert.AreEqual(BattleOutcome.PlayerWon, t.State.Outcome);
        Assert.AreEqual(BattlePhase.Ended, t.State.Phase);
        Assert.AreEqual(0, t.State.Battlers[1].Hp);
        Assert.AreEqual(35, t.State.Battlers[1].Moves[0].Pp);
        Assert.AreEqual(3, t.RandomTrace.Length);
        Assert.AreEqual(1, t.Events.OfType<FaintedEvent>().Count());
        Unchanged(t.State, BattleRules.Apply(t.State, Player(t.State, "cmd/late"), catalog), TransitionStatus.Rejected, "battle-ended");
    }

    [TestMethod]
    public void battle_opponent_victory()
    {
        var s = Battle();
        s = s with
        {
            Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Hp = 1, Stats = s.Battlers[0].Stats with { Speed = 1 } })
        };
        Assert.AreEqual(BattleOutcome.OpponentWon, Turn(s).State.Outcome);
    }

    [TestMethod]
    public void battle_input_snapshot_immutable()
    {
        var s = Battle();
        string before = Json(s);
        Turn(s);
        Assert.AreEqual(before, Json(s));
    }

    [TestMethod]
    public void battle_json_snapshot_roundtrip()
    {
        var s = Battle();
        s = BattleRules.Apply(s, Player(s), catalog).State;
        var copy = JsonSerializer.Deserialize<BattleSnapshot>(Json(s))!;
        Assert.AreEqual(Json(s), Json(copy));
        Assert.AreEqual(Json(BattleRules.Apply(s, Opponent(s), catalog)), Json(BattleRules.Apply(copy, Opponent(copy), catalog)));
    }

    [TestMethod]
    public void battle_deterministic_command_replay()
    {
        var initial = Battle(0x12345678);
        var a = initial;
        var b = initial;
        for (int i = 0; i < 5 && a.Outcome == BattleOutcome.Ongoing; i++)
        {
            var x = Turn(a, prefix: $"turn/{i}");
            var y = Turn(b, prefix: $"turn/{i}");
            Assert.AreEqual(Json(x), Json(y));
            a = x.State;
            b = y.State;
        }

        Assert.AreEqual(Json(a), Json(b));
    }

    [TestMethod]
    public void battle_selection_order_semantic_invariance()
    {
        var s = Battle();
        var normal = Turn(s);
        var queued = BattleRules.Apply(s, Opponent(s), catalog);
        var reverse = BattleRules.Apply(queued.State, Player(queued.State), catalog);
        Assert.AreEqual(Json(normal.State), Json(reverse.State));
        Assert.AreEqual(Json(normal.RandomTrace), Json(reverse.RandomTrace));
    }

    [TestMethod]
    public void battle_rejected_input_does_not_perturb_replay()
    {
        var s = Battle();
        var rejected = BattleRules.Apply(s, Player(s)with { MoveSlot = 8 }, catalog);
        Assert.AreEqual(Json(Turn(s)), Json(Turn(rejected.State)));
    }

    [TestMethod]
    public void battle_bad_rng_algorithm_fault()
    {
        var s = Battle()with
        {
            Rng = new(0, 0, "unknown")
        };
        Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "invalid-battle-state");
    }

    [TestMethod]
    public void battle_second_selection_content_fault_atomic()
    {
        var s = Battle();
        var missing = fixtureMove with
        {
            Id = new("fixture/missing-later")
        };
        s = s with
        {
            Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Moves = [new(missing.Id, 35, 35)] })
        };
        s = BattleRules.Apply(s, Player(s), new Catalog(fixtureMove, missing)).State;
        Unchanged(s, BattleRules.Apply(s, Opponent(s), catalog), TransitionStatus.Fault, "missing-move-content");
    }

    [TestMethod]
    public void battle_controlled_turn_trace_and_effects()
    {
        var result = Turn(Battle());
        RandomTrace[] expected = [new(1, "move:player:accuracy", 0, 24691, 0, 100, 0), new(2, "move:player:critical", 24691, 3917380458, 59774, 16, 14), new(3, "move:player:damage-drop", 3917380458, 1383151765, 21105, 16, 1), new(4, "move:opponent:accuracy", 1383151765, 833674724, 12720, 100, 20), new(5, "move:opponent:critical", 833674724, 2386711175, 36418, 16, 2), new(6, "move:opponent:damage-drop", 2386711175, 3805062638, 58060, 16, 12)];
        Assert.IsTrue(result.RandomTrace.SequenceEqual(expected), "Controlled turn must preserve every RNG draw and purpose.");
        Assert.AreEqual(TransitionStatus.Accepted, result.Status);
        Assert.IsTrue(result.Diagnostic is null);
        Assert.AreEqual(BattlePhase.AwaitCommands, result.State.Phase);
        Assert.AreEqual(BattleOutcome.Ongoing, result.State.Outcome);
        Assert.AreEqual(0, result.State.PendingMoves.Length);
        Assert.AreEqual(new RngState(3805062638, 6), result.State.Rng);
        Assert.AreEqual(2, result.State.Battlers.Length);
        foreach (var battler in result.State.Battlers)
        {
            Assert.AreEqual(94, battler.Hp);
            Assert.AreEqual(new MoveSlot(fixtureMove.Id, 34, 35), battler.Moves.Single());
        }

        var changes = result.Events.OfType<HpChangedEvent>().ToArray();
        Assert.AreEqual(2, changes.Length);
        for (int i = 0; i < changes.Length; i++)
        {
            var change = changes[i];
            Assert.AreEqual(new CombatantId(i == 0 ? "player" : "opponent"), change.ActorId);
            Assert.AreEqual(new CombatantId(i == 0 ? "opponent" : "player"), change.TargetId);
            Assert.AreEqual(fixtureMove.Id, change.MoveId);
            Assert.AreEqual(100, change.Before);
            Assert.AreEqual(94, change.After);
            Assert.AreEqual(6, change.Calculation.Damage);
            Assert.AreEqual(DamageCategory.Physical, change.Calculation.Category);
            Assert.AreEqual(5, change.Calculation.Steps.Single(s => s.Operation == "critical").Value);
            Assert.AreEqual(7, change.Calculation.Steps.Single(s => s.Operation == "stab").Value);
        }

        Assert.AreEqual(1, result.Events.OfType<TurnEndedEvent>().Single().CompletedTurn);
    }
}
