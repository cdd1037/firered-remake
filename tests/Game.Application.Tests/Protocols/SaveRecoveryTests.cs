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
public sealed class SaveRecoveryTests : ProtocolFixture
{

    [TestMethod]
    public void save_roundtrip()
    {
        var b = SaveCodec.Encode(new Counter(8), 4, "r1", "c1");
        var a = SaveCodec.Decode<Counter>(b, "r1", "c1");
        Assert.IsTrue(a.State.Value == 8 && a.Generation == 4, "roundtrip mismatch");
    }

    [TestMethod]
    public void save_canonical_polymorphic_metadata_order()
    {
        Game.Domain.Battle.SourceBattleEvent[] events = [new Game.Domain.Battle.SourceMedicineAppliedEvent(4, 2, new("pret:ITEM_ANTIDOTE"), 7, 1, false, 16, 16, 8, 0)];
        var bytes = SaveCodec.Encode(events, 5, "diagnostic", "content");
        var text = Encoding.UTF8.GetString(bytes);
        Assert.IsTrue(text.IndexOf("\"ActiveTarget\"", StringComparison.Ordinal) < text.IndexOf("\"kind\"", StringComparison.Ordinal), "fixture must exercise canonical metadata ordering");
        var restored = SaveCodec.Decode<Game.Domain.Battle.SourceBattleEvent[]>(bytes, "diagnostic", "content");
        Assert.IsTrue(CanonicalJson.Hash(restored.State) == CanonicalJson.Hash(events) && restored.State[0] is Game.Domain.Battle.SourceMedicineAppliedEvent, "typed canonical payload did not roundtrip");
        Throws(() => SaveCodec.Decode<Game.Domain.Battle.SourceBattleEvent[]>(bytes, "diagnostic", "content", validate: _ => "save_battle_active"));
        var fs = new MemoryStorage();
        Throws(() => new SaveService(fs).Save("slot", events, 5, "diagnostic", "content", _ => "save_battle_active"));
        Assert.IsTrue(fs.Files.Count == 0, "codec support must not bypass player save policy");
    }

    [TestMethod]
    public void save_corrupt_future_reject()
    {
        var b = Encoding.UTF8.GetString(SaveCodec.Encode(new Counter(8), 4, "r1", "c1"));
        Throws(() => SaveCodec.Decode<Counter>(Encoding.UTF8.GetBytes(b.Replace("\"Value\":8", "\"Value\":9")), "r1", "c1"));
        Throws(() => SaveCodec.Decode<Counter>(Encoding.UTF8.GetBytes(b.Replace($"\"SchemaVersion\":{SaveCodec.CurrentSchema}", $"\"SchemaVersion\":{SaveCodec.CurrentSchema + 1}", StringComparison.Ordinal)), "r1", "c1"));
        Throws(() => SaveCodec.Decode<Counter>(Encoding.UTF8.GetBytes(b), "r1", "different"));
    }

    [TestMethod, DataRow(1), DataRow(2)]
    public void old_save_schema_is_rejected_before_payload_admission(int schema)
    {
        var payload = JsonSerializer.SerializeToElement(new Counter(8));
        byte[] old = CanonicalJson.Encode(new SaveEnvelope(schema, "r1", "c1", "firered", 4,
            CanonicalJson.Hash(payload), payload));
        bool validatorCalled = false;
        var error = Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<Counter>(old, "r1", "c1",
            validate: _ => { validatorCalled = true; return null; }));
        Assert.AreEqual("save_schema_unsupported", error.Message);
        Assert.IsFalse(validatorCalled, "Old nickname or Mail identity cannot be admitted by a payload validator.");
        Assert.AreEqual(SaveOperationStatus.IncompatibleVersion,
            SaveOperationResult.Failure(SaveOperation.Load, error).Status);
    }

    [TestMethod, DataRow(1), DataRow(2)]
    public void old_save_primary_and_backup_are_preserved_when_loading_fails(int schema)
    {
        var payload = JsonSerializer.SerializeToElement(new Counter(8));
        byte[] old = CanonicalJson.Encode(new SaveEnvelope(schema, "r", "c", "firered", 4,
            CanonicalJson.Hash(payload), payload));
        var fs = new MemoryStorage();
        fs.Files["s"] = old.ToArray();
        fs.Files["s.bak"] = old.ToArray();
        var result = PlayerSaveOperations.Load(() => new SaveService(fs).Load<Counter>("s", "r", "c"));
        Assert.AreEqual(SaveOperationStatus.IncompatibleVersion, result.Result.Status);
        Assert.AreEqual(SaveFileOutcome.Preserved, result.Result.Files);
        Assert.IsNull(result.Saved);
        Assert.AreEqual(2, fs.Files.Count);
        CollectionAssert.AreEqual(old, fs.Files["s"]);
        CollectionAssert.AreEqual(old, fs.Files["s.bak"]);
    }

    [TestMethod]
    public void save_backup_recover()
    {
        var fs = new MemoryStorage();
        var service = new SaveService(fs);
        service.Save("s", new Counter(1), 1, "r", "c");
        service.Save("s", new Counter(2), 2, "r", "c");
        fs.Files["s"] = Encoding.UTF8.GetBytes("corrupt");
        var restored = service.Load<Counter>("s", "r", "c");
        Assert.IsTrue(restored.State.Value == 1 && restored.Source == "s.bak", "backup failed");
        service.Save("s", new Counter(3), 3, "r", "c");
        Assert.IsTrue(SaveCodec.Decode<Counter>(fs.Files["s.bak"], "r", "c").State.Value == 1, "corrupt primary overwrote backup");
    }

    [TestMethod]
    public void save_stage_failure_recover()
    {
        var fs = new MemoryStorage();
        var service = new SaveService(fs);
        service.Save("s", new Counter(1), 1, "r", "c");
        fs.FailCommit = true;
        try
        {
            service.Save("s", new Counter(2), 2, "r", "c");
        }
        catch (IOException)
        {
        }

        Assert.IsTrue(service.Load<Counter>("s", "r", "c").State.Value == 1, "commit fault lost primary");
        fs.FailCommit = false;
        fs.CorruptStage = true;
        Throws(() => service.Save("s", new Counter(3), 3, "r", "c"));
        Assert.IsTrue(service.Load<Counter>("s", "r", "c").State.Value == 1, "stage fault lost primary");
    }

    [TestMethod]
    public void save_semantic_corruption_recover()
    {
        var fs = new MemoryStorage();
        var service = new SaveService(fs);
        service.Save("s", new Counter(1), 1, "r", "c");
        service.Save("s", new Counter(2), 2, "r", "c");
        fs.Files["s"] = SaveCodec.Encode(new Counter(-99), 3, "r", "c");
        var restored = service.Load<Counter>("s", "r", "c", x => x.Value < 0 ? "negative_counter" : null);
        Assert.IsTrue(restored.State.Value == 1 && restored.Source == "s.bak", "semantic corruption did not recover from backup");
    }

    [TestMethod]
    public void save_missing_payload_recover()
    {
        var fs = new MemoryStorage();
        fs.Files["s.bak"] = SaveCodec.Encode(new Counter(4), 1, "r", "c");
        fs.Files["s"] = Encoding.UTF8.GetBytes("{\"SchemaVersion\":" + SaveCodec.CurrentSchema + ",\"RulesVersion\":\"r\",\"ContentVersion\":\"c\",\"GameEdition\":\"firered\",\"Generation\":2,\"PayloadHash\":\"missing\"}");
        Assert.IsTrue(new SaveService(fs).Load<Counter>("s", "r", "c").State.Value == 4, "missing payload blocked backup");
    }

    [TestMethod]
    public void save_invalid_id_recover()
    {
        var fs = new MemoryStorage();
        fs.Files["s.bak"] = SaveCodec.Encode(new IdHolder(new("valid:map")), 1, "r", "c");
        var payload = JsonSerializer.SerializeToElement(new { Map = new { Value = "bad id" } });
        fs.Files["s"] = CanonicalJson.Encode(new SaveEnvelope(SaveCodec.CurrentSchema, "r", "c", "firered", 2, CanonicalJson.Hash(payload), payload));
        Assert.IsTrue(new SaveService(fs).Load<IdHolder>("s", "r", "c").State.Map.Value == "valid:map", "invalid id blocked backup");
    }

    [TestMethod]
    public void save_concurrent_services_serialize()
    {
        var fs = new MemoryStorage();
        Task.WaitAll(Enumerable.Range(1, 32).Select(i => Task.Run(() => new SaveService(fs).Save("same", new Counter(i), i, "r", "c"))).ToArray());
        var loaded = new SaveService(fs).Load<Counter>("same", "r", "c");
        Assert.IsTrue(loaded.State.Value is >= 1 and <= 32 && !fs.Files.ContainsKey("same.tmp"), "concurrent writes corrupted save");
        Assert.IsTrue(SaveCodec.Decode<Counter>(fs.Files["same.bak"], "r", "c").State.Value is >= 1 and <= 32, "backup corrupted");
    }

    [TestMethod]
    public void harness_bad_semantic_saves_recover()
    {
        var map = new GridMap(new("fixture:lab"), 5, 5, Enumerable.Repeat(new TileCell(false), 25).ToImmutableArray());
        var catalog = new IntegrationBattleContent();
        CombatantSnapshot mon(string id, BattleSide side) => new(new(id), new("species:test"), side, 5, new(20, 10, 10, 10, 10, 10), Game.Domain.Rules.ElementType.Normal, null, 20, [new(new("move:test"), 30, 30)]);
        var battle = BattleSnapshot.Create(new("battle:test"), EngineeringSession.Content, mon("actor:p", BattleSide.Player), mon("actor:o", BattleSide.Opponent), 1);
        var state = new EngineeringSession(map, catalog, battle).State;
        var badStates = new[]
        {
            state with
            {
                World = null !
            },
            state with
            {
                World = state.World with
                {
                    Player = state.World.Player with
                    {
                        Facing = (Direction)999
                    }
                }
            },
            state with
            {
                World = state.World with
                {
                    Rng = state.World.Rng with
                    {
                        AlgorithmId = "bad-rng"
                    }
                }
            },
            state with
            {
                Battle = battle with
                {
                    Battlers = [battle.Battlers[0], battle.Battlers[1] with
                    {
                        Side = BattleSide.Player
                    }

                    ]
                }
            },
            state with
            {
                Battle = battle with
                {
                    Phase = BattlePhase.Ended,
                    Outcome = BattleOutcome.PlayerWon,
                    Battlers = [battle.Battlers[0], battle.Battlers[1] with
                    {
                        Hp = 0
                    }

                    ]
                }
            },
            state with
            {
                Battle = BattleRules.Apply(battle, new(new("pick"), 0, battle.Battlers[0].Id, 0, battle.Battlers[1].Id), catalog).State
            }
        };
        foreach (var bad in badStates)
        {
            var fs = new MemoryStorage();
            fs.Files["s.bak"] = SaveCodec.Encode(state, 1, EngineeringSession.Rules, EngineeringSession.Content);
            fs.Files["s"] = SaveCodec.Encode(bad, 2, EngineeringSession.Rules, EngineeringSession.Content);
            var loaded = new SaveService(fs).Load<EngineeringState>("s", EngineeringSession.Rules, EngineeringSession.Content, x => EngineeringSession.ValidateState(x, map, catalog));
            Assert.IsTrue(loaded.Source == "s.bak", "unsafe harness state loaded");
        }
    }
}
