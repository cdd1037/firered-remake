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
public sealed class SaveTests
{
    private (string Id, global::System.Func<EngineeringFixture, global::Game.Application.EngineeringState> Corrupt)[] corruptions = default !;
    [TestInitialize]
    public void SetUp()
    {
        corruptions = new (string Id, Func<EngineeringFixture, EngineeringState> Corrupt)[]
        {
            ("null-world", f => f.State with { World = null ! }),
            ("duplicate-battle-side", f => f.State with { Battle = f.Battle with { Battlers = f.Battle.Battlers.SetItem(1, f.Battle.Battlers[1] with { Side = BattleSide.Player }) } }),
            ("pending-transaction", f => f.State with { Battle = BattleRules.Apply(f.Battle, new(new("audit:player-choice"), 0, new("player"), 0, new("opponent")), f.Content).State }),
            ("ended-harness-battle", f => f.State with { Battle = f.Battle with { Phase = BattlePhase.Ended, Outcome = BattleOutcome.PlayerWon, Battlers = f.Battle.Battlers.SetItem(1, f.Battle.Battlers[1] with { Hp = 0 }) } }),
            ("unknown-rng", f => f.State with { World = f.State.World with { Rng = f.State.World.Rng with { AlgorithmId = "audit:unsupported-rng" } } }),
            ("invalid-facing", f => f.State with { World = f.State.World with { Player = f.State.World.Player with { Facing = (Direction)99 } } })
        };
    }

    [TestMethod]
    [DataRow("null-world")]
    [DataRow("duplicate-battle-side")]
    [DataRow("pending-transaction")]
    [DataRow("ended-harness-battle")]
    [DataRow("unknown-rng")]
    [DataRow("invalid-facing")]
    public void save_semantic_backup(string corruptionId)
    {
        var corruption = corruptions.Single(c => c.Id == corruptionId);
        var fixture = new EngineeringFixture();
        using var storage = new IsolatedFileStorage();
        var service = new SaveService(storage);
        fixture.SaveTwoGenerations(service, storage.Slot);
        var bad = corruption.Corrupt(fixture);
        File.WriteAllBytes(storage.Slot, SaveCodec.Encode(bad, 3, EngineeringSession.Rules, EngineeringSession.Content));
        var recovered = service.Load<EngineeringState>(storage.Slot, EngineeringSession.Rules, EngineeringSession.Content, fixture.Validate);
        Assert.IsTrue(recovered.Source == storage.Slot + ".bak", "Corrupt semantic primary did not select the backup.");
        Assert.IsTrue(recovered.Generation == 1 && CanonicalJson.Hash(recovered.State) == CanonicalJson.Hash(fixture.State), "Recovery changed the last verified backup.");
        Assert.IsTrue(File.Exists(storage.Slot), "Read-only fallback unexpectedly removed the primary.");
        return;
    }

    [TestMethod]
    public void save_cross_service_64_writes_serialize()
    {
        var storage = new ObservedMemoryStorage();
        using var start = new Barrier(8);
        var workers = Enumerable.Range(0, 8).Select(worker => Task.Factory.StartNew(() =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Save workers failed to start.");
            for (var index = 0; index < 8; index++)
            {
                var value = worker * 8 + index + 1;
                new SaveService(storage).Save("audit-slot", new Counter(value), value, "audit-r1", "audit-c1");
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        Assert.IsTrue(Task.WaitAll(workers, TimeSpan.FromSeconds(30)), "Concurrent saves did not complete.");
        var loaded = new SaveService(storage).Load<Counter>("audit-slot", "audit-r1", "audit-c1");
        Assert.IsTrue(storage.CommitCount == 64 && storage.WrongCommitCount == 0 && storage.MaxWriters == 1, "A save published another caller's staged state or writes overlapped.");
        Assert.IsTrue(!storage.Files.ContainsKey("audit-slot.tmp") && loaded.State.Value is >= 1 and <= 64, "Final primary or staged-file state is invalid.");
        Assert.IsTrue(SaveCodec.Decode<Counter>(storage.Files["audit-slot.bak"], "audit-r1", "audit-c1").State.Value is >= 1 and <= 64, "Backup was corrupted by concurrent services.");
        return;
    }

    [TestMethod]
    public void save_missing_payload_recovers()
    {
        using var storage = new IsolatedFileStorage();
        File.WriteAllBytes(storage.Slot + ".bak", SaveCodec.Encode(new Counter(4), 1, "audit-r1", "audit-c1"));
        File.WriteAllText(storage.Slot, "{\"SchemaVersion\":" + SaveCodec.CurrentSchema + ",\"RulesVersion\":\"audit-r1\",\"ContentVersion\":\"audit-c1\",\"GameEdition\":\"firered\",\"Generation\":2,\"PayloadHash\":\"missing\"}", Encoding.UTF8);
        var loaded = new SaveService(storage).Load<Counter>(storage.Slot, "audit-r1", "audit-c1");
        Assert.IsTrue(loaded.Source == storage.Slot + ".bak" && loaded.State.Value == 4, "Undefined JsonElement bypassed backup recovery.");
        return;
    }

    [TestMethod]
    public void save_invalid_stable_id_recovers()
    {
        using var storage = new IsolatedFileStorage();
        File.WriteAllBytes(storage.Slot + ".bak", SaveCodec.Encode(new IdHolder(new("audit:valid")), 1, "audit-r1", "audit-c1"));
        var payload = JsonSerializer.SerializeToElement(new { Map = new { Value = "invalid id" } });
        File.WriteAllBytes(storage.Slot, SaveCodec.Encode(payload, 2, "audit-r1", "audit-c1"));
        var loaded = new SaveService(storage).Load<IdHolder>(storage.Slot, "audit-r1", "audit-c1");
        Assert.IsTrue(loaded.Source == storage.Slot + ".bak" && loaded.State.Map.Value == "audit:valid", "Stable-ID constructor exception bypassed backup recovery.");
        return;
    }

    [TestMethod]
    public void save_valid_but_substituted_stage_is_rejected()
    {
        using var storage = new IsolatedFileStorage();
        var service = new SaveService(storage);
        service.Save(storage.Slot, new Counter(1), 1, "audit-r1", "audit-c1");
        storage.SubstituteStage = SaveCodec.Encode(new Counter(999), 999, "audit-r1", "audit-c1");
        var rejected = false;
        try
        {
            service.Save(storage.Slot, new Counter(2), 2, "audit-r1", "audit-c1");
        }
        catch (InvalidDataException ex)when (ex.Message == "save_stage_changed")
        {
            rejected = true;
        }

        Assert.IsTrue(rejected && service.Load<Counter>(storage.Slot, "audit-r1", "audit-c1").State.Value == 1, "A different checksum-valid stage was committed as this save.");
        return;
    }

    [TestMethod]
    public void save_player_policy_rejects_stable_battle_before_write()
    {
        var fixture = new EngineeringFixture();
        var battleState = fixture.State with
        {
            Battle = fixture.Battle
        };
        Assert.IsTrue(fixture.Validate(battleState)is null, "Internal diagnostic checkpoint was unexpectedly invalid.");
        using var storage = new IsolatedFileStorage();
        var rejected = false;
        try
        {
            new SaveService(storage).Save(storage.Slot, battleState, 1, EngineeringSession.Rules, EngineeringSession.Content, state => EngineeringSession.ValidatePlayerSave(state, fixture.Map, fixture.Content));
        }
        catch (InvalidDataException ex)when (ex.Message.Contains("save_battle_active", StringComparison.Ordinal))
        {
            rejected = true;
        }

        Assert.IsTrue(rejected && !File.Exists(storage.Slot) && !File.Exists(storage.Slot + ".tmp"), "Player battle save was written or stage-created before rejection.");
        return;
    }
}
