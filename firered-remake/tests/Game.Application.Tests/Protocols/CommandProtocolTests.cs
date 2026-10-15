using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Battle;
using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Application.Protocols;
[TestClass, TestCategory("Game")]
public sealed class CommandProtocolTests : ProtocolFixture
{

    [TestMethod]
    public void command_stale_retry_does_not_execute()
    {
        var s = new CommandSession<Counter, int>(new(0), "r1", "c1");
        var c = new CommandEnvelope<int>("one", 0, "r1", "c1", 5);
        var first = s.Execute(c, (x, n) => new(CommandStatus.Accepted, x with { Value = x.Value + n }));
        var again = s.Execute(c, (_, _) => throw new Exception("duplicate executed"));
        Assert.IsTrue(first.State.Value == 5 && again.Status == CommandStatus.Rejected && again.Error == "stale_revision" && ReferenceEquals(again.State, s.State) && again.StateHash == first.StateHash && s.Revision == 1, "duplicated state change");
        Assert.IsTrue(s.Execute(c with { Payload = 6 }, (x, n) => new(CommandStatus.Accepted, x)).Error == "stale_revision", "old revision with changed payload accepted");
        var next = s.Execute(c with { ExpectedRevision = 1, Payload = 6 }, (x, n) => new(CommandStatus.Accepted, x with { Value = x.Value + n }));
        Assert.IsTrue(next.Status == CommandStatus.Accepted && next.State.Value == 11 && s.Revision == 2, "trace ID incorrectly blocked a new current-revision request");
    }

    [TestMethod]
    public void command_reject_atomic()
    {
        var s = new CommandSession<Counter, int>(new(2), "r1", "c1");
        var a = s.Execute(new("a", 0, "r1", "c1", 1), (x, _) => new(CommandStatus.Rejected, new(999), "no"));
        Assert.IsTrue(a.State.Value == 2 && s.Revision == 0, "rejection mutated state");
        var b = s.Execute(new("b", 0, "r1", "c1", 1), (_, _) => throw new InvalidOperationException("invariant"));
        Assert.IsTrue(b.Status == CommandStatus.Fault && s.State.Value == 2 && s.Revision == 0, "fault mutated state");
    }

    [TestMethod]
    public void command_stale_version()
    {
        var s = new CommandSession<Counter, int>(new(2), "r1", "c1");
        Assert.IsTrue(s.Execute(new("a", 1, "r1", "c1", 1), (x, _) => new(CommandStatus.Accepted, x)).Error == "stale_revision", "stale accepted");
        Assert.IsTrue(s.Execute(new("a", 0, "r2", "c1", 1), (x, _) => new(CommandStatus.Accepted, x)).Error == "version_mismatch", "version accepted");
    }

    [TestMethod]
    public void command_revision_exhaustion()
    {
        var session = new CommandSession<Counter, int>(new(1), "r", "c", long.MaxValue);
        var r = session.Execute(new("a", long.MaxValue, "r", "c", 1), (_, _) => throw new Exception("must not execute"));
        Assert.IsTrue(r.Status == CommandStatus.Fault && r.Error == "revision_exhausted", "overflow escaped transaction");
    }

    [TestMethod]
    public void harness_command_retry()
    {
        var map = new GridMap(new("fixture:lab"), 5, 5, Enumerable.Repeat(new TileCell(false), 25).ToImmutableArray());
        var session = new EngineeringSession(map, new EmptyBattleContent(), BattleSnapshot.Create(new("b"), EngineeringSession.Content, new(new("p"), new("s"), BattleSide.Player, 5, new(20, 10, 10, 10, 10, 10), Game.Domain.Rules.ElementType.Normal, null, 20, []), new(new("o"), new("s"), BattleSide.Opponent, 5, new(20, 10, 10, 10, 10, 10), Game.Domain.Rules.ElementType.Normal, null, 20, []), 1));
        var c = new EngineeringCommand("move", Direction.East);
        var first = session.Execute("same", 0, c);
        var second = session.Execute("same", 0, c);
        Assert.IsTrue(second.Status == CommandStatus.Rejected && second.Error == "stale_revision" && second.StateHash == first.StateHash && ReferenceEquals(second.State, session.State) && session.State.World.StepCount == 1, "harness stale retry changed state");
    }
}
