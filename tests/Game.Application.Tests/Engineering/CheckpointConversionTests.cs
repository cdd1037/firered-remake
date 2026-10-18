using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Tooling;

namespace FireRed.Tests.Application.Engineering;

[TestClass, TestCategory("Game")]
public sealed class CheckpointConversionTests
{
    private const string V2 = "firered-current-campaign-v2";
    private const string V3 = "firered-current-campaign-v3";
    private const string V4 = "firered-current-campaign-v4";
    private const string Stat = "GAME_STAT_HATCHED_EGGS";
    private const long Generation = 81234;

    [TestMethod]
    public void V3TowerCompactionChangesOnlyHistoryAndPreservesFinalReceiptAndGameplay()
    {
        using var directory = TestAssets.TemporaryOutput();
        var source = LegacyTower();
        var payload = Payload(source);
        // Permitted nested optional fields are not permission to erase data during compaction.
        // The root schema still rejects unknown properties.
        payload["World"]!["OperatorNote"] = "preserve this unrelated optional field";
        payload["TrainerTowerHistory"]![2]!["ReceiptNote"] = "preserve the retained receipt verbatim";
        var original = SaveCodec.Encode(payload, Generation, V3, CurrentCampaignFixture.Catalog.ContentVersion);
        var (input, output) = Paths(directory.Path, original);

        Assert.AreEqual(0, CurrentCheckpointConversion.Run(TestAssets.Root, input, output));

        var current = Decode(File.ReadAllBytes(output), V4);
        Assert.AreEqual(Generation, current.Generation);
        Assert.AreEqual(source.World.Rng, current.State.World.Rng);
        Assert.AreEqual(source.NextPokemonId, current.State.NextPokemonId);
        Assert.AreEqual("trainer-tower-battle:3", current.State.TrainerTowerHistory!.Value.Single().Id);
        Assert.AreEqual(CanonicalJson.Hash(source), CanonicalJson.Hash(current.State with { TrainerTowerHistory = source.TrainerTowerHistory }));
        var expected = payload.DeepClone().AsObject();
        expected["TrainerTowerHistory"] = new JsonArray(payload["TrainerTowerHistory"]![2]!.DeepClone());
        Assert.AreEqual(CanonicalJson.Hash(expected), CanonicalJson.Hash(RawPayload(File.ReadAllBytes(output), V4)));
        Assert.IsNull(Validator().ValidatePlayerSave(current.State));
        Assert.AreEqual(CanonicalJson.Hash(current.State), CanonicalJson.Hash(
            CampaignSession.RestoreCurrent(CurrentCampaignFixture.Catalog, current.State, Generation).State));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        AssertNoTemporaryFiles(directory.Path);
        Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(original, V4,
            CurrentCampaignFixture.Catalog.ContentVersion, validate: Validator().ValidatePlayerSave));
    }

    [TestMethod]
    public void V2ConversionRunsBothDeclaredCompactionsWithoutChangingGameplay()
    {
        using var directory = TestAssets.TemporaryOutput();
        var basis = LegacyTower();
        long retiredId = basis.NextPokemonId;
        var source = basis with
        {
            NextPokemonId = retiredId + 1,
            PrizePokemon = basis.PrizePokemon!.Value.Add(basis.PrizePokemon!.Value[0] with { InstanceId = retiredId }),
            EvolutionHistory = basis.EvolutionHistory!.Value.Add(basis.EvolutionHistory!.Value[0] with { InstanceId = retiredId }),
            Exploration = basis.Exploration! with
            {
                Captured = basis.Exploration!.Captured!.Value.Add(basis.Exploration!.Captured!.Value[0] with { InstanceId = retiredId })
            }
        };
        var payload = Payload(source);
        payload["ReleasedPokemon"] = new JsonArray(new JsonObject { ["InstanceId"] = retiredId });
        payload["HallOfFame"]!["RibbonRecipients"] = new JsonArray(retiredId);
        payload["World"]!["OperatorNote"] = "unrelated fields survive both explicit steps";
        payload["PrizePokemon"]![0]!["ReceiptNote"] = "retained v2 source row stays intact";
        var original = SaveCodec.Encode(payload, Generation, V2, CurrentCampaignFixture.Catalog.ContentVersion);
        var (input, output) = Paths(directory.Path, original);

        Assert.AreEqual(0, CurrentCheckpointConversion.Run(TestAssets.Root, input, output));

        var loaded = Decode(File.ReadAllBytes(output), V4);
        var expected = basis with { NextPokemonId = source.NextPokemonId, TrainerTowerHistory = [source.TrainerTowerHistory!.Value[^1]] };
        Assert.AreEqual(Generation, loaded.Generation);
        Assert.AreEqual(source.World.Rng, loaded.State.World.Rng);
        Assert.AreEqual(CanonicalJson.Hash(expected), CanonicalJson.Hash(loaded.State));
        var raw = RawPayload(File.ReadAllBytes(output), V4);
        Assert.IsFalse(raw.ContainsKey("ReleasedPokemon"));
        Assert.IsFalse(raw["HallOfFame"]!.AsObject().ContainsKey("RibbonRecipients"));
        Assert.AreEqual(payload["World"]!["OperatorNote"]!.GetValue<string>(), raw["World"]!["OperatorNote"]!.GetValue<string>());
        Assert.AreEqual("retained v2 source row stays intact", raw["PrizePokemon"]![0]!["ReceiptNote"]!.GetValue<string>());
        Assert.IsNull(Validator().ValidatePlayerSave(loaded.State));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    [DataRow(V2, "identity")]
    [DataRow(V3, "identity")]
    [DataRow(V3, "course")]
    [DataRow(V3, "floor")]
    [DataRow(V3, "trainer")]
    [DataRow(V3, "level")]
    [DataRow(V3, "outcome")]
    [DataRow(V3, "seen")]
    public void DiscardedLegacyReceiptMustBeValidBeforeConversion(string rules, string corruption)
    {
        using var directory = TestAssets.TemporaryOutput();
        var source = LegacyTower();
        var history = source.TrainerTowerHistory!.Value;
        int index = corruption == "course" ? 1 : 0; // Also check a discarded interior receipt.
        var bad = corruption switch
        {
            "identity" => history[0] with { Id = "trainer-tower-battle:01" },
            "course" => history[index] with { Course = (TrainerTowerChallengeKind)99 },
            "floor" => history[0] with { FloorIndex = 8 },
            "trainer" => history[0] with { TrainerIndex = 99 },
            "level" => history[0] with { Level = 0 },
            "outcome" => history[0] with { Outcome = (WildBattleOutcome)99 },
            "seen" => history[0] with { OpponentsSeen = [0, 0] },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        var original = Encode(source with { TrainerTowerHistory = history.SetItem(index, bad) }, rules);
        var (input, output) = Paths(directory.Path, original);

        Assert.ThrowsExactly<InvalidDataException>(() => CurrentCheckpointConversion.Run(TestAssets.Root, input, output));

        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    [DataRow(V2)]
    [DataRow(V3)]
    public void TowerConversionNeverInventsUnknownLifetimeHatchCount(string rules)
    {
        using var directory = TestAssets.TemporaryOutput();
        var source = LegacyTower();
        var original = Encode(source with { GameStats = source.GameStats!.Remove(Stat) }, rules);
        var (input, output) = Paths(directory.Path, original);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => CurrentCheckpointConversion.Run(TestAssets.Root, input, output));

        StringAssert.Contains(error.Message, "save_hatched_egg_count_missing_migration_required");
        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(V3)]
    [DataRow(V4)]
    public void KnownHatchCorrectionPreservesDeclaredRulesAndEveryOtherPayloadField(string rules)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = rules == V4 ? Validator().ConvertLegacyTrainerTowerCheckpoint(LegacyTower()) : LegacyTower();
        var payload = Payload(state with { GameStats = state.GameStats!.Remove(Stat) });
        payload["World"]!["OperatorNote"] = "known total is an assertion, not an inferred history";
        var original = SaveCodec.Encode(payload, Generation, rules, CurrentCampaignFixture.Catalog.ContentVersion);
        var (input, output) = Paths(directory.Path, original);

        Assert.AreEqual(0, KnownHatchCountConversion.Run(TestAssets.Root, input, output, 1000));

        var corrected = Decode(File.ReadAllBytes(output), rules);
        Assert.AreEqual(1000, corrected.State.GameStats![Stat]);
        Assert.AreEqual(Generation, corrected.Generation);
        Assert.AreEqual(state.World.Rng, corrected.State.World.Rng);
        Assert.AreEqual(CanonicalJson.Hash(state.TrainerTowerHistory), CanonicalJson.Hash(corrected.State.TrainerTowerHistory));
        var expected = payload.DeepClone().AsObject();
        expected["GameStats"]![Stat] = 1000;
        Assert.AreEqual(CanonicalJson.Hash(expected), CanonicalJson.Hash(RawPayload(File.ReadAllBytes(output), rules)));
        Assert.IsNull(rules == V3 ? Validator().ValidateLegacyTrainerTowerCheckpoint(corrected.State)
            : Validator().ValidatePlayerSave(corrected.State));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        AssertNoTemporaryFiles(directory.Path);
        if (rules == V3)
        {
            string v4 = Path.Combine(directory.Path, "explicit-v4.json");
            Assert.AreEqual(0, CurrentCheckpointConversion.Run(TestAssets.Root, output, v4));
            Assert.AreEqual(1, Decode(File.ReadAllBytes(v4), V4).State.TrainerTowerHistory!.Value.Length);
            Assert.AreEqual(3, Decode(File.ReadAllBytes(output), V3).State.TrainerTowerHistory!.Value.Length);
        }
    }

    [TestMethod]
    public void V2KnownHatchCorrectionKeepsRetiredPayloadThenAllowsSeparateVersionConversion()
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = LegacyTower();
        long retiredId = state.NextPokemonId;
        state = state with
        {
            NextPokemonId = retiredId + 1,
            GameStats = state.GameStats!.Remove(Stat),
            PrizePokemon = state.PrizePokemon!.Value.Add(state.PrizePokemon!.Value[0] with { InstanceId = retiredId })
        };
        var payload = Payload(state);
        payload["ReleasedPokemon"] = new JsonArray(new JsonObject { ["InstanceId"] = retiredId });
        payload["HallOfFame"]!["RibbonRecipients"] = new JsonArray(retiredId);
        payload["World"]!["OperatorNote"] = "v2 count correction retains retired fields and history";
        var original = SaveCodec.Encode(payload, Generation, V2, CurrentCampaignFixture.Catalog.ContentVersion);
        var (input, output) = Paths(directory.Path, original);

        Assert.AreEqual(0, KnownHatchCountConversion.Run(TestAssets.Root, input, output, 1000));

        var correctedBytes = File.ReadAllBytes(output);
        var expected = payload.DeepClone().AsObject();
        expected["GameStats"]![Stat] = 1000;
        Assert.AreEqual(CanonicalJson.Hash(expected), CanonicalJson.Hash(RawPayload(correctedBytes, V2)));
        Assert.AreEqual(Generation, SaveCodec.Decode<JsonElement>(correctedBytes, V2,
            CurrentCampaignFixture.Catalog.ContentVersion).Generation);
        string convertedPath = Path.Combine(directory.Path, "separate-v4.json");
        Assert.AreEqual(0, CurrentCheckpointConversion.Run(TestAssets.Root, output, convertedPath));
        var converted = Decode(File.ReadAllBytes(convertedPath), V4);
        Assert.AreEqual(Generation, converted.Generation);
        Assert.AreEqual(state.World.Rng, converted.State.World.Rng);
        Assert.AreEqual(state.NextPokemonId, converted.State.NextPokemonId);
        Assert.AreEqual(1000, converted.State.GameStats![Stat]);
        Assert.AreEqual(1, converted.State.TrainerTowerHistory!.Value.Length);
        Assert.IsFalse(converted.State.PrizePokemon!.Value.Any(p => p.InstanceId == retiredId));
        Assert.IsNull(Validator().ValidatePlayerSave(converted.State));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        CollectionAssert.AreEqual(correctedBytes, File.ReadAllBytes(output));
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    [DataRow(V2)]
    [DataRow(V3)]
    public void KnownHatchCorrectionStillValidatesDiscardedLegacyTowerReceipts(string rules)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = LegacyTower();
        var original = Encode(state with
        {
            GameStats = state.GameStats!.Remove(Stat),
            TrainerTowerHistory = state.TrainerTowerHistory!.Value.SetItem(0,
                state.TrainerTowerHistory!.Value[0] with { OpponentsSeen = [] })
        }, rules);
        var (input, output) = Paths(directory.Path, original);

        Assert.ThrowsExactly<InvalidDataException>(() => KnownHatchCountConversion.Run(TestAssets.Root, input, output, 1000));

        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BothCommandsRejectUnrelatedGameplayCorruption(bool knownCount)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = LegacyTower();
        state = state with { Money = -1, GameStats = knownCount ? state.GameStats!.Remove(Stat) : state.GameStats };
        var original = Encode(state, V3);
        var (input, output) = Paths(directory.Path, original);

        Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, output));

        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BothCommandsRequireNewDestinationAndKeepExistingFiles(bool knownCount)
    {
        using var directory = TestAssets.TemporaryOutput();
        var source = LegacyTower();
        var original = Encode(source with { GameStats = source.GameStats!.Remove(Stat) }, V3);
        var (input, output) = Paths(directory.Path, original);
        byte[] sentinel = [17, 29, 83];
        File.WriteAllBytes(output, sentinel);

        Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, input));
        Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, output));
        Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, directory.Path));

        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        CollectionAssert.AreEqual(sentinel, File.ReadAllBytes(output));
        AssertNoTemporaryFiles(directory.Path);
    }

    [TestMethod]
    [DataRow(false, V4)]
    [DataRow(false, "firered-current-campaign-v1")]
    [DataRow(false, "firered-current-campaign-v5")]
    [DataRow(true, "firered-current-campaign-v1")]
    [DataRow(true, "firered-current-campaign-v5")]
    public void OnlyDeclaredVersionRoutesAreAccepted(bool knownCount, string rules)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = LegacyTower();
        var original = Encode(state with { GameStats = state.GameStats!.Remove(Stat) }, rules);
        var (input, output) = Paths(directory.Path, original);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, output));

        StringAssert.Contains(error.Message, "unsupported_route");
        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NeitherCommandSilentlyDropsUnrecognizedRootData(bool knownCount)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = LegacyTower();
        var payload = Payload(state with { GameStats = knownCount ? state.GameStats!.Remove(Stat) : state.GameStats });
        payload["UnrecognizedGameplayField"] = 123;
        var original = SaveCodec.Encode(payload, Generation, V3, CurrentCampaignFixture.Catalog.ContentVersion);
        var (input, output) = Paths(directory.Path, original);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, output));

        Assert.AreEqual("save_decode_invalid: JsonException", error.Message);
        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(false, "checksum")]
    [DataRow(true, "checksum")]
    [DataRow(false, "content")]
    [DataRow(true, "content")]
    public void ExactContentAndChecksumRemainRequired(bool knownCount, string fault)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = LegacyTower();
        var envelope = JsonNode.Parse(Encode(state with { GameStats = state.GameStats!.Remove(Stat) }, V3))!.AsObject();
        if (fault == "checksum") envelope["Payload"]!["Money"] = state.Money + 1;
        else envelope["ContentVersion"] = "firered-current-content-v3";
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var (input, output) = Paths(directory.Path, original);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => Run(knownCount, input, output));

        Assert.AreEqual(fault == "checksum" ? "save_checksum_mismatch" : "save_version_mismatch", error.Message);
        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(V2)]
    [DataRow(V3)]
    [DataRow(V4)]
    public void KnownHatchCountCannotReplaceExistingStatistic(string rules)
    {
        using var directory = TestAssets.TemporaryOutput();
        var state = rules == V4 ? Validator().ConvertLegacyTrainerTowerCheckpoint(LegacyTower()) : LegacyTower();
        var original = Encode(state, rules);
        var (input, output) = Paths(directory.Path, original);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => KnownHatchCountConversion.Run(TestAssets.Root, input, output, 1234));

        Assert.AreEqual("hatch_count_already_present", error.Message);
        Assert.IsFalse(File.Exists(output));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [DataRow(V2, -1)]
    [DataRow(V2, 16777216)]
    [DataRow(V3, -1)]
    [DataRow(V4, 16777216)]
    public void KnownHatchCountRejectsOutOfRangeOperatorInput(string rules, int count)
    {
        using var directory = TestAssets.TemporaryOutput();
        var (input, output) = Paths(directory.Path, Encode(LegacyTower(), rules));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KnownHatchCountConversion.Run(TestAssets.Root, input, output, count));
        Assert.IsFalse(File.Exists(output));
    }

    // Deliberately synthetic receipts exercise conversion invariants; they are not
    // additional claimed public-command playthroughs or the player's hatch history.
    private static CampaignState LegacyTower()
    {
        var basis = CurrentCampaignFixture.Checkpoint.State;
        var descriptor = CurrentCampaignFixture.Catalog.TrainerTowerCatalog!
            .DescribeBattle(TrainerTowerChallengeKind.Single, 0, 0, 71);
        var seen = Enumerable.Range(0, descriptor.Members.Length).ToImmutableArray();
        var history = Enumerable.Range(1, 3).Select(i => new CampaignTrainerTowerHistory(
            "trainer-tower-battle:" + i, TrainerTowerChallengeKind.Single, 0, 0, 71,
            WildBattleOutcome.Won, seen)).ToImmutableArray();
        return basis with { TrainerTower = basis.TrainerTower ?? TrainerTowerProgressRules.Create(), TrainerTowerHistory = history };
    }

    private static CampaignSession Validator() => CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
    private static JsonObject Payload(CampaignState state) => JsonSerializer.SerializeToNode(state)!.AsObject();
    private static byte[] Encode(CampaignState state, string rules) =>
        SaveCodec.Encode(state, Generation, rules, CurrentCampaignFixture.Catalog.ContentVersion);
    private static LoadedSave<CampaignState> Decode(byte[] bytes, string rules) =>
        SaveCodec.Decode<CampaignState>(bytes, rules, CurrentCampaignFixture.Catalog.ContentVersion);
    private static JsonObject RawPayload(byte[] bytes, string rules) => JsonNode.Parse(
        SaveCodec.Decode<JsonElement>(bytes, rules, CurrentCampaignFixture.Catalog.ContentVersion).State.GetRawText())!.AsObject();
    private static (string Input, string Output) Paths(string directory, byte[] original)
    {
        string input = Path.Combine(directory, "source.json"), output = Path.Combine(directory, "new-destination.json");
        File.WriteAllBytes(input, original);
        return (input, output);
    }
    private static int Run(bool knownCount, string input, string output) => knownCount
        ? KnownHatchCountConversion.Run(TestAssets.Root, input, output, 1000)
        : CurrentCheckpointConversion.Run(TestAssets.Root, input, output);
    private static void AssertNoTemporaryFiles(string directory) =>
        Assert.AreEqual(0, Directory.GetFiles(directory, ".firered-checkpoint-*.tmp").Length);
}
