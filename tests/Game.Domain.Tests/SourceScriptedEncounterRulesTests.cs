using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests;

[TestClass, TestCategory("Game")]
public sealed class SourceScriptedEncounterRulesTests
{
    [TestMethod]
    public void ScriptedOriginRequiresRegisteredMapSlotAndRulesVersion()
    {
        var definition = SourceScriptedEncounterRegistry.Definitions.Single(d => d.Id == "static.moltres");
        var origin = SourceScriptedEncounterRegistry.Origin(definition);
        Assert.AreEqual(new SourceTableWildSlot(new("pret:SPECIES_MOLTRES"), 50, 50), SourceTableWildBattleContract.SourceSlot(origin));
        Assert.IsNull(SourceTableWildBattleContract.SourceSlot(origin with { Slot = 1 }));
        Assert.IsNull(SourceTableWildBattleContract.SourceSlot(origin with { Slot = -1 }));
        Assert.IsNull(SourceTableWildBattleContract.SourceSlot(origin with { Map = new("map.route-1") }));
        Assert.IsNull(SourceTableWildBattleContract.SourceSlot(origin with { RulesVersion = "unsupported" }));
        Assert.IsNull(SourceTableWildBattleContract.SourceSlot(origin with { Kind = SourceTableEncounterKind.Land }));
    }

    [TestMethod]
    public void MountainTopWalkUsesOrdinaryCollisionAndElevationOnAnyMap()
    {
        var map = new ConnectedFieldMap(new("map.test-mountain-ground"), 1, 2, [new(12, 0, 3), new(12, 0, 3)], []);
        var state = new WorldSnapshot(ConnectedFieldRules.RulesVersion, 0, 0, new(map.Id, 0, 0, 3, Direction.South), new(42));
        var command = new ConnectedFieldIntent(new("test/mountain-ground"), 0, ConnectedFieldAction.Walk, Direction.South);
        var maps = new Dictionary<MapId, ConnectedFieldMap> { [map.Id] = map };
        var step = ConnectedFieldRules.Complete(state, command, maps);
        Assert.AreEqual(TransitionStatus.Accepted, step.Status);
        Assert.AreEqual(1, step.State.Player.Y);
        Assert.AreEqual(state.Rng, step.State.Rng);
        maps[map.Id] = map with { Cells = [new(12, 0, 3), new(12, 1, 3)] };
        var blocked = ConnectedFieldRules.Complete(state, command, maps);
        Assert.AreEqual(0, blocked.State.Player.Y);
        Assert.AreEqual("collision", blocked.Events.Single().Reason);
    }
}
