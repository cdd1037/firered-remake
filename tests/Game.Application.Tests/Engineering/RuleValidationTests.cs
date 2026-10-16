using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Rules;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Application.Engineering;
[TestClass, TestCategory("Game")]
public sealed class RuleValidationTests
{
    [TestMethod]
    public void battle_zero_accuracy_is_not_ordinary_always_hit()
    {
        var fixture = new EngineeringFixture();
        var result = BattleRules.Apply(fixture.Battle, new(new("audit:zero"), 0, new("player"), 0, new("opponent")), new AuditBattleContent(0));
        Assert.IsTrue(result.Status == TransitionStatus.Fault && result.Diagnostic?.Code == "unsupported-move", "Zero accuracy was accepted as ordinary always-hit damage.");
        Assert.IsTrue(ReferenceEquals(result.State, fixture.Battle) && result.Events.IsEmpty && result.RandomTrace.IsEmpty, "Unsupported move consumed state, events, or RNG.");
        return;
    }

    [TestMethod]
    public void world_unsupported_current_grass_is_atomic_fault()
    {
        var fixture = new EngineeringFixture();
        var map = new GridMap(fixture.Map.Id, 5, 5, fixture.Map.Cells.SetItem(12, new(false, 0, TileBehavior.Grass)));
        var result = WorldRules.Move(fixture.State.World, new(new("audit:walk"), 0, Direction.East), map);
        Assert.IsTrue(result.Status == TransitionStatus.Fault && result.Diagnostic?.Code == "unsupported-current-tile-behavior", "An unsupported current tile escaped profile validation.");
        Assert.IsTrue(ReferenceEquals(result.State, fixture.State.World) && result.Events.IsEmpty, "Unsupported tile changed authoritative world state.");
        return;
    }
}
