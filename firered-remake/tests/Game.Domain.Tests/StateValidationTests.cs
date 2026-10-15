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
public sealed class StateValidationTests : DomainFixture
{

    [TestMethod]
    public void validation_valid_battle_and_world_pure()
    {
        var b = Battle();
        var w = World();
        string beforeBattle = Json(b), beforeWorld = Json(w);
        Assert.IsTrue(BattleRules.ValidateState(b, catalog)is null);
        Assert.IsTrue(WorldRules.ValidateState(w, Map())is null);
        Assert.AreEqual(beforeBattle, Json(b));
        Assert.AreEqual(beforeWorld, Json(w));
    }

    [TestMethod]
    public void validation_duplicate_combatant_id()
    {
        var b = Battle();
        b = b with
        {
            Battlers = b.Battlers.SetItem(1, b.Battlers[1] with { Id = b.Battlers[0].Id })
        };
        Assert.AreEqual("invalid-roster", BattleRules.ValidateState(b, catalog)!.Code);
    }

    [TestMethod]
    public void validation_two_player_sides()
    {
        var b = Battle();
        b = b with
        {
            Battlers = b.Battlers.SetItem(1, b.Battlers[1] with { Side = BattleSide.Player })
        };
        Assert.AreEqual("invalid-roster", BattleRules.ValidateState(b, catalog)!.Code);
    }

    [TestMethod]
    public void validation_outcome_hp_disagreement()
    {
        var b = Battle()with
        {
            Phase = BattlePhase.Ended,
            Outcome = BattleOutcome.PlayerWon
        };
        Assert.AreEqual("invalid-battle-outcome", BattleRules.ValidateState(b, catalog)!.Code);
    }

    [TestMethod]
    public void validation_unselected_move_reference()
    {
        var b = Battle();
        b = b with
        {
            Battlers = b.Battlers.SetItem(0, b.Battlers[0] with { Moves = b.Battlers[0].Moves.Add(new(new("missing/unselected"), 10, 10)) })
        };
        Assert.AreEqual("missing-move-content", BattleRules.ValidateState(b, catalog)!.Code);
    }

    [TestMethod]
    public void validation_valid_pending_recovery()
    {
        var b = Battle();
        b = BattleRules.Apply(b, Player(b), catalog).State;
        Assert.IsTrue(BattleRules.ValidateState(b, catalog)is null);
    }

    [TestMethod]
    public void validation_invalid_world_facing()
    {
        var w = World();
        w = w with
        {
            Player = w.Player with
            {
                Facing = (Direction)99
            }
        };
        Assert.AreEqual("invalid-world-state", WorldRules.ValidateState(w, Map())!.Code);
    }

    [TestMethod]
    public void validation_invalid_world_steps()
    {
        var w = World()with
        {
            StepCount = -1
        };
        Assert.AreEqual("invalid-world-state", WorldRules.ValidateState(w, Map())!.Code);
    }
}
