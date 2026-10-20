using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class SolidInteractionCollisionTests
{
    private static readonly MapId MapId = new("map.furniture");
    private static (WorldSnapshot World, Dictionary<MapId, ConnectedFieldMap> Maps) Fixture(int behavior, int collision, Direction direction)
    {
        var cells = Enumerable.Repeat(new ConnectedFieldCell(0, 0, 3), 25).ToImmutableArray();
        var (dx, dy) = direction switch { Direction.North => (0, -1), Direction.South => (0, 1), Direction.East => (1, 0), _ => (-1, 0) };
        cells = cells.SetItem((2 + dy) * 5 + 2 + dx, new(behavior, collision, 0));
        var world = new WorldSnapshot(ConnectedFieldRules.RulesVersion, 7, 19,
            new(MapId, 2, 2, 3, Direction.South), new(123));
        return (world, new() { [MapId] = new(MapId, 5, 5, cells, []) });
    }
    private static ConnectedFieldTransition Walk(WorldSnapshot world, Dictionary<MapId, ConnectedFieldMap> maps,
        Direction direction, bool bicycle = false) => ConnectedFieldRules.Complete(world,
            new(new("furniture:move"), world.Revision, ConnectedFieldAction.Walk, direction), maps, biking: bicycle, enableCardinalLedges: true);

    [TestMethod]
    [DataRow(1, false)] [DataRow(2, false)] [DataRow(3, false)] [DataRow(1, true)]
    public void EverySourceSolidInteractionTypeUsesCollisionWithoutTakingAStep(int collision, bool bicycle)
    {
        // Complete contiguous source declaration group MB_COUNTER..MB_TRAINER_TOWER_MONITOR.
        // Not a region list, and not permission to stand on these behaviors.
        int checkedCases = 0;
        for (int behavior = 0x80; behavior <= 0xa3; behavior++)
        foreach (var direction in Enum.GetValues<Direction>())
        {
            var (world, maps) = Fixture(behavior, collision, direction);
            var result = Walk(world, maps, direction, bicycle);
            Assert.AreEqual(TransitionStatus.Accepted, result.Status, $"{behavior:x2}/{direction}: {result.Diagnostic?.Code}");
            Assert.AreEqual(world.Player with { Facing = direction }, result.State.Player);
            Assert.AreEqual(world.StepCount, result.State.StepCount); Assert.AreEqual(world.Rng, result.State.Rng);
            Assert.AreEqual(world.Revision + 1, result.State.Revision);
            Assert.AreEqual(ConnectedFieldEventKind.Blocked, result.Events.Single().Kind);
            Assert.AreEqual("collision", result.Events.Single().Reason);
            Assert.IsFalse(result.RequiresEncounterCheck); Assert.IsFalse(result.MapChanged);
            checkedCases++;
        }
        Assert.AreEqual(144, checkedCases);
    }

    [TestMethod]
    [DataRow(0x7f, 0)] [DataRow(0x7f, 1)]
    [DataRow(0xa4, 0)] [DataRow(0xa4, 1)]
    [DataRow(0xff, 0)] [DataRow(0xff, 1)]
    public void UnknownBehaviorsRemainFaultsEvenWhenSolid(int behavior, int collision)
    {
        var (world, maps) = Fixture(behavior, collision, Direction.West);
        var result = Walk(world, maps, Direction.West);
        Assert.AreEqual(TransitionStatus.Fault, result.Status);
        Assert.AreEqual("unsupported-source-behavior", result.Diagnostic?.Code);
        Assert.AreSame(world, result.State); Assert.IsTrue(result.Events.IsEmpty);
    }

    [TestMethod]
    [DataRow(0x81)] [DataRow(0x86)] [DataRow(0x8b)] [DataRow(0xa3)]
    public void UnsupportedPassableFurnitureIsNotMadeWalkable(int behavior)
    {
        var (world, maps) = Fixture(behavior, 0, Direction.West);
        var result = Walk(world, maps, Direction.West);
        Assert.AreEqual(TransitionStatus.Fault, result.Status);
        Assert.AreEqual("unsupported-source-behavior", result.Diagnostic?.Code);
        Assert.AreSame(world, result.State);
    }

    [TestMethod]
    [DataRow(0x86, 4)] [DataRow(256, 1)]
    public void MalformedTargetCellsStillFailValidation(int behavior, int collision)
    {
        var (world, maps) = Fixture(behavior, collision, Direction.West);
        var result = Walk(world, maps, Direction.West);
        Assert.AreEqual(TransitionStatus.Fault, result.Status);
        Assert.AreEqual("invalid-connected-target-cell", result.Diagnostic?.Code);
        Assert.AreSame(world, result.State);
    }

    [TestMethod]
    public void DoorDispatchAndMatchingLedgeOverrideAreNotReordered()
    {
        var (world, maps) = Fixture(0x69, 1, Direction.North);
        world = world with { Player = world.Player with { Facing = Direction.North } };
        Assert.AreEqual("field-warp-handler-required", Walk(world, maps, Direction.North).Diagnostic?.Code);
        (world, maps) = Fixture(0x3b, 1, Direction.South);
        var jumped = Walk(world, maps, Direction.South);
        Assert.AreEqual(TransitionStatus.Accepted, jumped.Status, jumped.Diagnostic?.Code);
        Assert.AreEqual(ConnectedFieldEventKind.JumpedLedge, jumped.Events.Single().Kind);
        Assert.AreEqual((2, 4), (jumped.State.Player.X, jumped.State.Player.Y));
        Assert.AreEqual(world.StepCount + 1, jumped.State.StepCount);
    }
}
