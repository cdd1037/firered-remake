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
public sealed class CommandTests
{
    [TestMethod]
    public void command_harness_retry_after_intervening_command()
    {
        var fixture = new EngineeringFixture();
        var session = fixture.Session();
        var first = session.Execute("audit:first", 0, new("move", Direction.East));
        var second = session.Execute("audit:second", 1, new("move", Direction.West));
        var duplicate = session.Execute("audit:first", 0, new("move", Direction.East));
        Assert.IsTrue(first.Status == CommandStatus.Accepted && second.Status == CommandStatus.Accepted, "Setup moves failed.");
        Assert.IsTrue(duplicate.Status == CommandStatus.Rejected && duplicate.Error == "stale_revision" && ReferenceEquals(duplicate.State, second.State) && ReferenceEquals(duplicate.State, session.State) && duplicate.Revision == 2, "Delayed stale retry did not preserve the current committed outcome.");
        Assert.IsTrue(session.Revision == 2 && session.State.World.StepCount == 2 && session.State.World.Player.X == 2, "Duplicate rewound or advanced authoritative state.");
        var conflict = session.Execute("audit:first", 0, new("move", Direction.North));
        Assert.IsTrue(conflict.Status == CommandStatus.Rejected && conflict.Error == "stale_revision" && ReferenceEquals(conflict.State, second.State) && session.Revision == 2, "Changed payload bypassed the stale revision guard.");
        return;
    }

    [TestMethod]
    public void command_revision_exhaustion_is_atomic_fault()
    {
        var session = new CommandSession<Counter, int>(new(7), "audit-r1", "audit-c1", long.MaxValue);
        var result = session.Execute(new("audit:overflow", long.MaxValue, "audit-r1", "audit-c1", 1), (_, _) => throw new InvalidOperationException("Exhausted session must not invoke rules."));
        Assert.IsTrue(result.Status == CommandStatus.Fault && result.Error == "revision_exhausted", "Overflow escaped as an exception or another result.");
        Assert.IsTrue(session.State.Value == 7 && session.Revision == long.MaxValue, "Overflow changed committed state.");
        return;
    }

    [TestMethod]
    public void command_100_concurrent_stale_envelopes()
    {
        var session = new CommandSession<Counter, int>(new(0), "audit-r1", "audit-c1");
        var outcomes = new ConcurrentBag<CommandOutcome<Counter>>();
        Parallel.For(0, 100, index => outcomes.Add(session.Execute(new("audit:" + index, 0, "audit-r1", "audit-c1", 1), (state, delta) => new(CommandStatus.Accepted, new(state.Value + delta)))));
        var accepted = outcomes.Count(o => o.Status == CommandStatus.Accepted);
        var stale = outcomes.Count(o => o.Status == CommandStatus.Rejected && o.Error == "stale_revision");
        Assert.IsTrue(accepted == 1 && stale == 99 && session.Revision == 1 && session.State.Value == 1, "Concurrent stale commands lost an update or committed more than once.");
        return;
    }

    [TestMethod]
    public void command_100_concurrent_identical_envelopes()
    {
        var session = new CommandSession<Counter, int>(new(0), "audit-r1", "audit-c1");
        var outcomes = new ConcurrentBag<CommandOutcome<Counter>>();
        var ruleCalls = 0;
        Parallel.For(0, 100, _ => outcomes.Add(session.Execute(new("audit:duplicate", 0, "audit-r1", "audit-c1", 1), (state, delta) =>
        {
            Interlocked.Increment(ref ruleCalls);
            return new(CommandStatus.Accepted, new(state.Value + delta));
        })));
        var stale = outcomes.Count(o => o.Status == CommandStatus.Rejected && o.Error == "stale_revision");
        Assert.IsTrue(ruleCalls == 1 && stale == 99 && outcomes.Count(o => o.Status == CommandStatus.Accepted) == 1 && outcomes.All(o => ReferenceEquals(o.State, session.State) && o.Revision == session.Revision) && session.Revision == 1 && session.State.Value == 1, "Concurrent duplicate executed rules repeatedly or bypassed the revision guard.");
        return;
    }
}
