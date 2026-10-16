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

namespace FireRed.Tests.Application.Engineering;
sealed class EngineeringFixture
{
    public GridMap Map { get; } = new(new("fixture:lab"), 5, 5, Enumerable.Repeat(new TileCell(false), 25).ToImmutableArray());
    public AuditBattleContent Content { get; } = new();
    public BattleSnapshot Battle { get; } = BattleSnapshot.Create(new("battle"), EngineeringSession.Content, Actor("player", BattleSide.Player), Actor("opponent", BattleSide.Opponent), 1);
    public EngineeringState State { get; }

    public EngineeringFixture() => State = Session().State;
    public EngineeringSession Session() => new(Map, Content, Battle);
    public string? Validate(EngineeringState state) => EngineeringSession.ValidateState(state, Map, Content);
    private static CombatantSnapshot Actor(string id, BattleSide side) => new(new(id), new("species"), side, 5, new(20, 10, 10, 10, 10, 10), ElementType.Normal, null, 20, [new(new("strike"), 35, 35)]);
    public void SaveTwoGenerations(SaveService service, string slot)
    {
        service.Save(slot, State, 1, EngineeringSession.Rules, EngineeringSession.Content, Validate);
        service.Save(slot, State, 2, EngineeringSession.Rules, EngineeringSession.Content, Validate);
    }
}

sealed class AuditBattleContent(int accuracy = 100) : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => id.Value == "strike" ? new(id, ElementType.Normal, 40, accuracy, 35) : null;
}

sealed class IsolatedFileStorage : ISaveStorage, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "firered-core-audit-" + Guid.NewGuid().ToString("N"));
    public string Slot { get; }
    public byte[]? SubstituteStage { get; set; }

    public IsolatedFileStorage()
    {
        Directory.CreateDirectory(_root);
        Slot = Path.Combine(_root, "slot.json");
    }

    public byte[]? Read(string slot) => File.Exists(slot) ? File.ReadAllBytes(slot) : null;
    public void WriteStaged(string slot, ReadOnlySpan<byte> bytes) => File.WriteAllBytes(slot + ".tmp", SubstituteStage is {} replacement ? replacement : bytes);
    public void Commit(string slot, bool preservePrimary)
    {
        if (preservePrimary)
            File.Copy(slot, slot + ".bak", true);
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
    public void WriteStaged(string slot, ReadOnlySpan<byte> bytes)
    {
        lock (this)
        {
            _writers++;
            MaxWriters = Math.Max(MaxWriters, _writers);
        }

        _expectedStage.Value = bytes.ToArray();
        Files[slot + ".tmp"] = bytes.ToArray();
        Thread.Sleep(2); // Widen the historical overwrite window without dictating ordering.
    }

    public void Commit(string slot, bool preservePrimary)
    {
        var staged = Files[slot + ".tmp"];
        lock (this)
        {
            if (!staged.AsSpan().SequenceEqual(_expectedStage.Value))
                WrongCommitCount++;
            if (preservePrimary)
                Files[slot + ".bak"] = Files[slot];
            Files[slot] = staged;
            Files.TryRemove(slot + ".tmp", out _);
            _writers--;
            CommitCount++;
        }
    }
}


sealed record Counter(int Value);
sealed record IdHolder(MapId Map);
