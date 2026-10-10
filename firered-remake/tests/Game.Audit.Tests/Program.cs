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

// Independent audit regressions. No reference to another test project's private helpers.
// --hash-probe starts only from the explicit cold-process test below, never recursively.
if (args.SequenceEqual(new[] { "--hash-probe" }))
{
    Console.WriteLine(JsonSerializer.Serialize(AuditFixture.HashProbe()));
    return Environment.Version.Major == 10 ? 0 : 1;
}

string? filter = null;
if (args.Length == 2 && args[0] == "--filter") filter = args[1];
else if (args.Length != 0)
{
    Console.WriteLine(JsonSerializer.Serialize(new { suite = "Game.Audit.Tests", passed = false, error = "Usage: [--filter substring]" }));
    return 2;
}

var tests = new List<(string Id, Func<object?> Run)>();
void Test(string id, Func<object?> run) => tests.Add((id, run));
void Check(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }

// The first eleven cases preserve the original independent audit checks.
var corruptions = new (string Id, Func<AuditFixture, EngineeringState> Corrupt)[]
{
    ("null-world", f => f.State with { World = null! }),
    ("duplicate-battle-side", f => f.State with { Battle = f.Battle with
        { Battlers = f.Battle.Battlers.SetItem(1, f.Battle.Battlers[1] with { Side = BattleSide.Player }) } }),
    ("pending-transaction", f => f.State with { Battle = BattleRules.Apply(f.Battle,
        new(new("audit:player-choice"), 0, new("player"), 0, new("opponent")), f.Content).State }),
    ("ended-harness-battle", f => f.State with { Battle = f.Battle with
        { Phase = BattlePhase.Ended, Outcome = BattleOutcome.PlayerWon,
          Battlers = f.Battle.Battlers.SetItem(1, f.Battle.Battlers[1] with { Hp = 0 }) } }),
    ("unknown-rng", f => f.State with { World = f.State.World with
        { Rng = f.State.World.Rng with { AlgorithmId = "audit:unsupported-rng" } } }),
    ("invalid-facing", f => f.State with { World = f.State.World with
        { Player = f.State.World.Player with { Facing = (Direction)99 } } })
};
foreach (var corruption in corruptions)
{
    Test("save.semantic." + corruption.Id + ".backup", () =>
    {
        var fixture = new AuditFixture();
        using var storage = new IsolatedFileStorage();
        var service = new SaveService(storage);
        fixture.SaveTwoGenerations(service, storage.Slot);
        var bad = corruption.Corrupt(fixture);
        // Checksum is intentionally valid: this exercises semantic validation, not byte corruption.
        File.WriteAllBytes(storage.Slot, SaveCodec.Encode(bad, 3, EngineeringSession.Rules, EngineeringSession.Content));
        var recovered = service.Load<EngineeringState>(storage.Slot, EngineeringSession.Rules, EngineeringSession.Content, fixture.Validate);
        Check(recovered.Source == storage.Slot + ".bak", "Corrupt semantic primary did not select the backup.");
        Check(recovered.Generation == 1 && CanonicalJson.Hash(recovered.State) == CanonicalJson.Hash(fixture.State), "Recovery changed the last verified backup.");
        Check(File.Exists(storage.Slot), "Read-only fallback unexpectedly removed the primary.");
        return new { recovered.Generation, usedBackup = true };
    });
}

Test("command.harness.retry-after-intervening-command", () =>
{
    var fixture = new AuditFixture();
    var session = fixture.Session();
    var first = session.Execute("audit:first", 0, new("move", Direction.East));
    var second = session.Execute("audit:second", 1, new("move", Direction.West));
    var duplicate = session.Execute("audit:first", 0, new("move", Direction.East));
    Check(first.Status == CommandStatus.Accepted && second.Status == CommandStatus.Accepted, "Setup moves failed.");
    Check(duplicate.Status == CommandStatus.Rejected && duplicate.Error == "stale_revision" && duplicate.StateHash == second.StateHash && ReferenceEquals(duplicate.State, session.State) && duplicate.Revision == 2, "Delayed stale retry did not preserve the current committed outcome.");
    Check(session.Revision == 2 && session.State.World.StepCount == 2 && session.State.World.Player.X == 2, "Duplicate rewound or advanced authoritative state.");
    var conflict = session.Execute("audit:first", 0, new("move", Direction.North));
    Check(conflict.Status == CommandStatus.Rejected && conflict.Error == "stale_revision" && conflict.StateHash == second.StateHash && session.Revision == 2, "Changed payload bypassed the stale revision guard.");
    return new { session.Revision, session.State.World.StepCount };
});

Test("command.revision-exhaustion-is-atomic-fault", () =>
{
    var session = new CommandSession<Counter, int>(new(7), "audit-r1", "audit-c1", long.MaxValue);
    var result = session.Execute(new("audit:overflow", long.MaxValue, "audit-r1", "audit-c1", 1),
        (_, _) => throw new InvalidOperationException("Exhausted session must not invoke rules."));
    Check(result.Status == CommandStatus.Fault && result.Error == "revision_exhausted", "Overflow escaped as an exception or another result.");
    Check(session.State.Value == 7 && session.Revision == long.MaxValue, "Overflow changed committed state.");
    return null;
});

Test("battle.zero-accuracy-is-not-ordinary-always-hit", () =>
{
    var fixture = new AuditFixture();
    var result = BattleRules.Apply(fixture.Battle, new(new("audit:zero"), 0, new("player"), 0, new("opponent")), new AuditBattleContent(0));
    Check(result.Status == TransitionStatus.Fault && result.Diagnostic?.Code == "unsupported-move", "Zero accuracy was accepted as ordinary always-hit damage.");
    Check(ReferenceEquals(result.State, fixture.Battle) && result.Events.IsEmpty && result.RandomTrace.IsEmpty, "Unsupported move consumed state, events, or RNG.");
    return new { source = "AccuracyCalcHelper/ Cmd_accuracycheck", sourceCommit = RuleVersions.SourceCommit };
});

Test("world.unsupported-current-grass-is-atomic-fault", () =>
{
    var fixture = new AuditFixture();
    var map = new GridMap(fixture.Map.Id, 5, 5, fixture.Map.Cells.SetItem(12, new(false, 0, TileBehavior.Grass)));
    var result = WorldRules.Move(fixture.State.World, new(new("audit:walk"), 0, Direction.East), map);
    Check(result.Status == TransitionStatus.Fault && result.Diagnostic?.Code == "unsupported-current-tile-behavior", "An unsupported current tile escaped profile validation.");
    Check(ReferenceEquals(result.State, fixture.State.World) && result.Events.IsEmpty, "Unsupported tile changed authoritative world state.");
    return null;
});

Test("save.cross-service-64-writes-serialize", () =>
{
    var storage = new ObservedMemoryStorage();
    using var start = new Barrier(8);
    // Dedicated workers avoid a thread-pool starvation false positive at the start barrier.
    var workers = Enumerable.Range(0, 8).Select(worker => Task.Factory.StartNew(() =>
    {
        if (!start.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Save workers failed to start.");
        for (var index = 0; index < 8; index++)
        {
            var value = worker * 8 + index + 1;
            new SaveService(storage).Save("audit-slot", new Counter(value), value, "audit-r1", "audit-c1");
        }
    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
    Check(Task.WaitAll(workers, TimeSpan.FromSeconds(30)), "Concurrent saves did not complete.");
    var loaded = new SaveService(storage).Load<Counter>("audit-slot", "audit-r1", "audit-c1");
    Check(storage.CommitCount == 64 && storage.WrongCommitCount == 0 && storage.MaxWriters == 1, "A save published another caller's staged state or writes overlapped.");
    Check(!storage.Files.ContainsKey("audit-slot.tmp") && loaded.State.Value is >= 1 and <= 64, "Final primary or staged-file state is invalid.");
    Check(SaveCodec.Decode<Counter>(storage.Files["audit-slot.bak"], "audit-r1", "audit-c1").State.Value is >= 1 and <= 64, "Backup was corrupted by concurrent services.");
    return new { storage.CommitCount, storage.WrongCommitCount, storage.MaxWriters };
});

Test("canonical.cold-process-hashes-match", () =>
{
    var parent = AuditFixture.HashProbe();
    var probes = Enumerable.Range(0, 3).Select(_ => AuditFixture.StartHashProbe()).ToArray();
    Check(probes.Select(p => p.ProcessId).Distinct().Count() == 3 && probes.All(p => p.ProcessId != Environment.ProcessId), "Hash test did not use distinct child processes.");
    Check(probes.All(p => p.RuntimeMajor == 10 && p.StateHash == parent.StateHash && p.RoundTripHash == parent.StateHash && p.NormalizedSetHash == parent.NormalizedSetHash), "Cold processes or save round-trip changed a canonical semantic hash.");
    return new { parent.StateHash, parent.NormalizedSetHash, children = probes };
});

Test("command.100-concurrent-stale-envelopes", () =>
{
    var session = new CommandSession<Counter, int>(new(0), "audit-r1", "audit-c1");
    var outcomes = new ConcurrentBag<CommandOutcome<Counter>>();
    Parallel.For(0, 100, index => outcomes.Add(session.Execute(new("audit:" + index, 0, "audit-r1", "audit-c1", 1),
        (state, delta) => new(CommandStatus.Accepted, new(state.Value + delta)))));
    var accepted = outcomes.Count(o => o.Status == CommandStatus.Accepted);
    var stale = outcomes.Count(o => o.Status == CommandStatus.Rejected && o.Error == "stale_revision");
    Check(accepted == 1 && stale == 99 && session.Revision == 1 && session.State.Value == 1, "Concurrent stale commands lost an update or committed more than once.");
    return new { accepted, stale, session.Revision };
});

Test("command.100-concurrent-identical-envelopes", () =>
{
    var session = new CommandSession<Counter, int>(new(0), "audit-r1", "audit-c1");
    var outcomes = new ConcurrentBag<CommandOutcome<Counter>>();
    var ruleCalls = 0;
    Parallel.For(0, 100, _ => outcomes.Add(session.Execute(new("audit:duplicate", 0, "audit-r1", "audit-c1", 1),
        (state, delta) => { Interlocked.Increment(ref ruleCalls); return new(CommandStatus.Accepted, new(state.Value + delta)); })));
    var stale = outcomes.Count(o => o.Status == CommandStatus.Rejected && o.Error == "stale_revision");
    Check(ruleCalls == 1 && stale == 99 && outcomes.Count(o => o.Status == CommandStatus.Accepted) == 1 && outcomes.All(o => o.StateHash == CanonicalJson.Hash(session.State)) && session.Revision == 1 && session.State.Value == 1, "Concurrent duplicate executed rules repeatedly or bypassed the revision guard.");
    return new { ruleCalls, stale, session.Revision };
});

Test("save.missing-payload-recovers", () =>
{
    using var storage = new IsolatedFileStorage();
    File.WriteAllBytes(storage.Slot + ".bak", SaveCodec.Encode(new Counter(4), 1, "audit-r1", "audit-c1"));
    File.WriteAllText(storage.Slot, "{\"SchemaVersion\":1,\"RulesVersion\":\"audit-r1\",\"ContentVersion\":\"audit-c1\",\"GameEdition\":\"firered\",\"Generation\":2,\"PayloadHash\":\"missing\"}", Encoding.UTF8);
    var loaded = new SaveService(storage).Load<Counter>(storage.Slot, "audit-r1", "audit-c1");
    Check(loaded.Source == storage.Slot + ".bak" && loaded.State.Value == 4, "Undefined JsonElement bypassed backup recovery.");
    return null;
});

Test("save.invalid-stable-id-recovers", () =>
{
    using var storage = new IsolatedFileStorage();
    File.WriteAllBytes(storage.Slot + ".bak", SaveCodec.Encode(new IdHolder(new("audit:valid")), 1, "audit-r1", "audit-c1"));
    var payload = JsonSerializer.SerializeToElement(new { Map = new { Value = "invalid id" } });
    File.WriteAllBytes(storage.Slot, SaveCodec.Encode(payload, 2, "audit-r1", "audit-c1"));
    var loaded = new SaveService(storage).Load<IdHolder>(storage.Slot, "audit-r1", "audit-c1");
    Check(loaded.Source == storage.Slot + ".bak" && loaded.State.Map.Value == "audit:valid", "Stable-ID constructor exception bypassed backup recovery.");
    return null;
});

Test("save.valid-but-substituted-stage-is-rejected", () =>
{
    using var storage = new IsolatedFileStorage();
    var service = new SaveService(storage);
    service.Save(storage.Slot, new Counter(1), 1, "audit-r1", "audit-c1");
    storage.SubstituteStage = SaveCodec.Encode(new Counter(999), 999, "audit-r1", "audit-c1");
    var rejected = false;
    try { service.Save(storage.Slot, new Counter(2), 2, "audit-r1", "audit-c1"); }
    catch (InvalidDataException ex) when (ex.Message == "save_stage_changed") { rejected = true; }
    Check(rejected && service.Load<Counter>(storage.Slot, "audit-r1", "audit-c1").State.Value == 1, "A different checksum-valid stage was committed as this save.");
    return null;
});

Test("save.player-policy-rejects-stable-battle-before-write", () =>
{
    var fixture = new AuditFixture();
    var battleState = fixture.State with { Battle = fixture.Battle };
    Check(fixture.Validate(battleState) is null, "Internal diagnostic checkpoint was unexpectedly invalid.");
    using var storage = new IsolatedFileStorage();
    var rejected = false;
    try
    {
        new SaveService(storage).Save(storage.Slot, battleState, 1, EngineeringSession.Rules, EngineeringSession.Content,
            state => EngineeringSession.ValidatePlayerSave(state, fixture.Map, fixture.Content));
    }
    catch (InvalidDataException ex) when (ex.Message.Contains("save_battle_active", StringComparison.Ordinal)) { rejected = true; }
    Check(rejected && !File.Exists(storage.Slot) && !File.Exists(storage.Slot + ".tmp"), "Player battle save was written or stage-created before rejection.");
    return null;
});

var selected = tests.Where(test => filter is null || test.Id.Contains(filter, StringComparison.Ordinal)).ToArray();
var results = new List<AuditResult>();
foreach (var test in selected)
{
    try { results.Add(new(test.Id, true, test.Run(), null)); }
    catch (Exception error) { results.Add(new(test.Id, false, null, error.ToString())); }
}
var failed = results.Count(result => !result.Passed);
var passed = selected.Length > 0 && failed == 0 && Environment.Version.Major == 10;
Console.WriteLine(JsonSerializer.Serialize(new
{
    suite = "Game.Audit.Tests", runtime = Environment.Version.ToString(), runtimeMajor = Environment.Version.Major,
    total = selected.Length, passed, failed, emptySuite = selected.Length == 0, results
}, new JsonSerializerOptions { WriteIndented = true }));
return selected.Length == 0 ? 2 : passed ? 0 : 1;

sealed record AuditResult(string Id, bool Passed, object? Evidence, string? Error);
sealed record Counter(int Value);
sealed record IdHolder(MapId Map);
sealed record HashProbeResult(int ProcessId, int RuntimeMajor, string StateHash, string RoundTripHash, string NormalizedSetHash);

sealed class AuditFixture
{
    public GridMap Map { get; } = new(new("fixture:lab"), 5, 5, Enumerable.Repeat(new TileCell(false), 25).ToImmutableArray());
    public AuditBattleContent Content { get; } = new();
    public BattleSnapshot Battle { get; } = BattleSnapshot.Create(new("battle"), EngineeringSession.Content,
        Actor("player", BattleSide.Player), Actor("opponent", BattleSide.Opponent), 1);
    public EngineeringState State { get; }
    public AuditFixture() => State = Session().State;
    public EngineeringSession Session() => new(Map, Content, Battle);
    public string? Validate(EngineeringState state) => EngineeringSession.ValidateState(state, Map, Content);
    private static CombatantSnapshot Actor(string id, BattleSide side) => new(new(id), new("species"), side, 5,
        new(20, 10, 10, 10, 10, 10), ElementType.Normal, null, 20, [new(new("strike"), 35, 35)]);
    public void SaveTwoGenerations(SaveService service, string slot)
    {
        service.Save(slot, State, 1, EngineeringSession.Rules, EngineeringSession.Content, Validate);
        service.Save(slot, State, 2, EngineeringSession.Rules, EngineeringSession.Content, Validate);
    }
    public static HashProbeResult HashProbe()
    {
        var session = new AuditFixture().Session();
        var reward = session.Execute("audit:reward", 0, new("reward"));
        if (reward.Status != CommandStatus.Accepted) throw new InvalidOperationException("Hash probe reward failed.");
        var state = session.State;
        var bytes = SaveCodec.Encode(state, 1, EngineeringSession.Rules, EngineeringSession.Content);
        var loaded = SaveCodec.Decode<EngineeringState>(bytes, EngineeringSession.Rules, EngineeringSession.Content);
        // Set order is normalized explicitly before canonical JSON; array order is otherwise semantic.
        var ids = ImmutableHashSet.Create("reward:one", "reward:two", "reward:three", "reward:four")
            .Order(StringComparer.Ordinal).ToImmutableArray();
        return new(Environment.ProcessId, Environment.Version.Major, CanonicalJson.Hash(state), CanonicalJson.Hash(loaded.State), CanonicalJson.Hash(ids));
    }
    public static HashProbeResult StartHashProbe()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Current executable path is missing.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--hash-probe");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start the hash probe.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Cold-process hash probe timed out.");
        }
        if (process.ExitCode != 0) throw new InvalidOperationException("Hash probe failed: " + stderr.GetAwaiter().GetResult());
        return JsonSerializer.Deserialize<HashProbeResult>(stdout.GetAwaiter().GetResult()) ?? throw new InvalidDataException("Hash probe returned no evidence.");
    }
}

sealed class AuditBattleContent(int accuracy = 100) : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => id.Value == "strike" ? new(id, ElementType.Normal, 40, accuracy, 35) : null;
}

// This is an isolated test adapter, not evidence for Godot or power-loss durability.
sealed class IsolatedFileStorage : ISaveStorage, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "firered-core-audit-" + Guid.NewGuid().ToString("N"));
    public string Slot { get; }
    public byte[]? SubstituteStage { get; set; }
    public IsolatedFileStorage() { Directory.CreateDirectory(_root); Slot = Path.Combine(_root, "slot.json"); }
    public byte[]? Read(string slot) => File.Exists(slot) ? File.ReadAllBytes(slot) : null;
    public void WriteStaged(string slot, byte[] bytes) => File.WriteAllBytes(slot + ".tmp", SubstituteStage ?? bytes);
    public void Commit(string slot, bool preservePrimary)
    {
        if (preservePrimary) File.Copy(slot, slot + ".bak", true);
        File.Move(slot + ".tmp", slot, true);
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}

sealed class ObservedMemoryStorage : ISaveStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
    private readonly ThreadLocal<byte[]?> _expectedStage = new();
    private int _writers;
    public int MaxWriters { get; private set; }
    public int CommitCount { get; private set; }
    public int WrongCommitCount { get; private set; }
    public byte[]? Read(string slot) => Files.GetValueOrDefault(slot);
    public void WriteStaged(string slot, byte[] bytes)
    {
        lock (this) { _writers++; MaxWriters = Math.Max(MaxWriters, _writers); }
        _expectedStage.Value = bytes;
        Files[slot + ".tmp"] = bytes;
        Thread.Sleep(2); // Widen the historical overwrite window without dictating ordering.
    }
    public void Commit(string slot, bool preservePrimary)
    {
        var staged = Files[slot + ".tmp"];
        lock (this)
        {
            if (!staged.AsSpan().SequenceEqual(_expectedStage.Value)) WrongCommitCount++;
            if (preservePrimary) Files[slot + ".bak"] = Files[slot];
            Files[slot] = staged;
            Files.TryRemove(slot + ".tmp", out _);
            _writers--; CommitCount++;
        }
    }
}
