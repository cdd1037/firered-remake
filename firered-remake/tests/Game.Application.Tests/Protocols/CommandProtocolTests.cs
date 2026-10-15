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
        Assert.IsTrue(first.State.Value == 5 && again.Status == CommandStatus.Rejected && again.Error == "stale_revision" && ReferenceEquals(again.State, s.State) && ReferenceEquals(again.State, first.State) && s.Revision == 1, "duplicated state change");
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
        Assert.IsTrue(second.Status == CommandStatus.Rejected && second.Error == "stale_revision" && ReferenceEquals(second.State, first.State) && ReferenceEquals(second.State, session.State) && session.State.World.StepCount == 1, "harness stale retry changed state");
    }

    [TestMethod]
    [DataRow("accepted", CommandStatus.Accepted)]
    [DataRow("rejected", CommandStatus.Rejected)]
    [DataRow("fault", CommandStatus.Fault)]
    [DataRow("throw", CommandStatus.Fault)]
    [DataRow("missing_id", CommandStatus.Rejected)]
    [DataRow("stale_revision", CommandStatus.Rejected)]
    [DataRow("version_mismatch", CommandStatus.Rejected)]
    [DataRow("revision_exhausted", CommandStatus.Fault)]
    public void command_transaction_never_serializes_the_state(string path, CommandStatus expected)
    {
        // Serialization is deliberately unsupported. Command correctness depends on
        // the immutable snapshot and revision, not on diagnostic JSON availability.
        var original = new NonSerializableState();
        var proposed = new NonSerializableState();
        long revision = path == "revision_exhausted" ? long.MaxValue : 0;
        var session = new CommandSession<NonSerializableState, int>(original, "r", "c", revision);
        var envelope = new CommandEnvelope<int>(path == "missing_id" ? "" : "command",
            path == "stale_revision" ? 1 : revision, path == "version_mismatch" ? "wrong" : "r", "c", 1);
        int calls = 0;
        var outcome = session.Execute(envelope, (_, _) =>
        {
            calls++;
            return path switch
            {
                "accepted" => new(CommandStatus.Accepted, proposed),
                "rejected" => new(CommandStatus.Rejected, proposed, "rule_rejected"),
                "fault" => new(CommandStatus.Fault, proposed, "rule_fault"),
                "throw" => throw new InvalidOperationException("rule_threw"),
                _ => throw new AssertFailedException("A guard failure must not execute rules.")
            };
        });
        bool accepted = expected == CommandStatus.Accepted;
        Assert.AreEqual(expected, outcome.Status);
        Assert.AreEqual(path is "accepted" or "rejected" or "fault" or "throw" ? 1 : 0, calls);
        Assert.AreSame(accepted ? proposed : original, outcome.State);
        Assert.AreSame(outcome.State, session.State);
        Assert.AreEqual(accepted ? revision + 1 : revision, outcome.Revision);
        Assert.AreEqual(outcome.Revision, session.Revision);
        Assert.AreEqual(path switch
        {
            "accepted" => null,
            "rejected" => "rule_rejected",
            "fault" => "rule_fault",
            "throw" => "InvalidOperationException: rule_threw",
            "missing_id" => "command_id_required",
            _ => path
        }, outcome.Error);
    }

    [TestMethod]
    public void outcome_serialization_does_not_invoke_an_implicit_hash()
    {
        var state = new ObservedSerializationState();
        var session = new CommandSession<ObservedSerializationState, int>(state, "r", "c");
        var result = session.Execute(new("one", 0, "r", "c", 0), (current, _) => new(CommandStatus.Accepted, current));
        Assert.AreEqual(0, state.SerializationReads);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.AreEqual(1, state.SerializationReads, "Serializing an outcome must not serialize its state again through a lazy hash property.");
        Assert.IsFalse(json.RootElement.TryGetProperty("StateHash", out _));
        Assert.AreEqual(7, json.RootElement.GetProperty("State").GetProperty("Value").GetInt32());
        Assert.AreEqual(1L, json.RootElement.GetProperty("Revision").GetInt64());
    }

    private sealed class NonSerializableState
    {
        public int Value => throw new InvalidOperationException("State was serialized inside a command transaction.");
    }

    private sealed class ObservedSerializationState
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public int SerializationReads { get; private set; }
        public int Value { get { SerializationReads++; return 7; } }
    }

}
