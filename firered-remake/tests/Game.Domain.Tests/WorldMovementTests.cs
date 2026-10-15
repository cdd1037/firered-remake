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
public sealed class WorldMovementTests : DomainFixture
{

    [TestMethod]
    public void world_orthogonal_move()
    {
        var s = World();
        var t = WorldRules.Move(s, Walk(s, Direction.North), Map());
        Assert.AreEqual(TransitionStatus.Accepted, t.Status);
        Assert.AreEqual(1, t.State.Player.X);
        Assert.AreEqual(0, t.State.Player.Y);
        Assert.AreEqual(Direction.North, t.State.Player.Facing);
        Assert.AreEqual(1L, t.State.StepCount);
        Assert.AreEqual(s.Rng, t.State.Rng);
        Assert.AreEqual(WorldEventKind.Moved, t.Events[0].Kind);
    }

    [TestMethod]
    public void world_signpost_blocks_but_faces()
    {
        var s = World();
        var m = Map();
        var t = WorldRules.Move(s, Walk(s, Direction.North), new GridMap(m.Id, 3, 3, m.Cells.SetItem(1, new(true, 0, TileBehavior.Sign))));
        Assert.AreEqual(TransitionStatus.Accepted, t.Status);
        Assert.AreEqual(Direction.North, t.State.Player.Facing);
        Assert.AreEqual(0L, t.State.StepCount);
        Assert.AreEqual(s.Rng, t.State.Rng);
        Assert.AreEqual("collision", t.Events[0].Reason);
    }

    [TestMethod]
    public void world_blocked_turns_facing_only()
    {
        var s = World();
        var t = WorldRules.Move(s, Walk(s, Direction.East), Map());
        Assert.AreEqual(TransitionStatus.Accepted, t.Status);
        Assert.AreEqual(1, t.State.Player.X);
        Assert.AreEqual(1, t.State.Player.Y);
        Assert.AreEqual(Direction.East, t.State.Player.Facing);
        Assert.AreEqual(0L, t.State.StepCount);
        Assert.AreEqual(1L, t.State.Revision);
        Assert.AreEqual(s.Rng, t.State.Rng);
        Assert.AreEqual("collision", t.Events[0].Reason);
    }

    [TestMethod]
    public void world_boundary_blocked()
    {
        var s = World();
        s = s with
        {
            Player = s.Player with
            {
                Y = 0
            }
        };
        var t = WorldRules.Move(s, Walk(s, Direction.North), Map());
        Assert.AreEqual("map-boundary", t.Events[0].Reason);
        Assert.AreEqual(0, t.State.Player.Y);
    }

    [TestMethod]
    public void world_stale_rejected_unchanged()
    {
        var s = World();
        var t = WorldRules.Move(s, Walk(s, Direction.North)with { ExpectedRevision = 9 }, Map());
        Assert.AreEqual(TransitionStatus.Rejected, t.Status);
        Assert.IsTrue(ReferenceEquals(s, t.State));
        Assert.AreEqual(0, t.Events.Length);
    }

    [TestMethod]
    public void world_elevation_blocked()
    {
        var s = World();
        var m = Map();
        var t = WorldRules.Move(s, Walk(s, Direction.North), new GridMap(m.Id, 3, 3, m.Cells.SetItem(1, new(false, 1))));
        Assert.AreEqual("elevation", t.Events[0].Reason);
        Assert.AreEqual(1, t.State.Player.Y);
    }

    [TestMethod]
    public void world_unsupported_grass_fault_atomic()
    {
        var s = World();
        var m = Map();
        var t = WorldRules.Move(s, Walk(s, Direction.North), new GridMap(m.Id, 3, 3, m.Cells.SetItem(1, new(false, 0, TileBehavior.Grass))));
        Assert.AreEqual(TransitionStatus.Fault, t.Status);
        Assert.IsTrue(ReferenceEquals(s, t.State));
        Assert.AreEqual(0, t.Events.Length);
    }

    [TestMethod]
    public void world_invalid_direction_rejected()
    {
        var s = World();
        var t = WorldRules.Move(s, Walk(s, (Direction)99), Map());
        Assert.AreEqual(TransitionStatus.Rejected, t.Status);
        Assert.IsTrue(ReferenceEquals(s, t.State));
    }

    [TestMethod]
    public void world_mismatched_map_fault()
    {
        var s = World();
        s = s with
        {
            Player = s.Player with
            {
                MapId = new("other/map")
            }
        };
        Assert.AreEqual(TransitionStatus.Fault, WorldRules.Move(s, Walk(s, Direction.North), Map()).Status);
    }

    [TestMethod]
    public void world_cave_door_source_elevation_wildcards()
    {
        foreach (var(actorElevation, doorElevation, allowed)in new[]
        {
            (3, 0, true),
            (0, 3, true),
            (3, 3, true),
            (3, 15, true),
            (3, 4, false)
        }

        )
        {
            var before = World()with
            {
                Player = World().Player with
                {
                    Elevation = actorElevation,
                    Facing = Direction.North
                }
            };
            var source = new CaveWarpMap(false, actorElevation, doorElevation);
            var destination = new CaveWarpMap(true, 3, 3);
            var maps = new Dictionary<MapId, IFieldMap>
            {
                {
                    source.Id,
                    source
                },
                {
                    destination.Id,
                    destination
                }
            };
            var result = FieldMovementRules.Move(before, new(new("fixture/cave-elevation"), 0, Direction.North), maps, enableCaveDoors: true);
            Assert.AreEqual(allowed, result.MapChanged);
            Assert.AreEqual(before.Rng, result.State.Rng);
            if (allowed)
            {
                Assert.AreEqual(TransitionStatus.Accepted, result.Status);
                Assert.AreEqual(destination.Id, result.State.Player.MapId);
                Assert.AreEqual(1L, result.State.StepCount);
            }
            else
            {
                Assert.AreEqual(TransitionStatus.Fault, result.Status);
                Assert.AreEqual(Json(before), Json(result.State));
            }
        }
    }

    [TestMethod]
    public void world_invalid_grid_fails()
    {
        Assert.Throws<ArgumentException>(() => new GridMap(new("map"), 2, 2, [new(false)]));
    }

    [TestMethod]
    public void world_deterministic_replay()
    {
        var a = World();
        var b = World();
        foreach (var direction in new[]
        {
            Direction.North,
            Direction.West,
            Direction.West,
            Direction.South,
            Direction.East
        }

        )
        {
            var x = WorldRules.Move(a, Walk(a, direction), Map());
            var y = WorldRules.Move(b, Walk(b, direction), Map());
            Assert.AreEqual(Json(x), Json(y));
            a = x.State;
            b = y.State;
        }

        Assert.AreEqual(4L, a.StepCount);
        Assert.AreEqual(5L, a.Revision);
        Assert.AreEqual(a.Rng, World().Rng);
    }

    [TestMethod]
    public void world_unsupported_current_tile_fault()
    {
        var s = World();
        var m = Map();
        m = new(m.Id, 3, 3, m.Cells.SetItem(4, new(false, 0, TileBehavior.Grass)));
        var t = WorldRules.Move(s, Walk(s, Direction.North), m);
        Assert.AreEqual(TransitionStatus.Fault, t.Status);
        Assert.IsTrue(ReferenceEquals(s, t.State));
    }

    [TestMethod]
    public void world_rng_algorithm_fault()
    {
        var s = World()with
        {
            Rng = new(0, 0, "unknown")
        };
        Assert.AreEqual(TransitionStatus.Fault, WorldRules.Move(s, Walk(s, Direction.North), Map()).Status);
    }
}
