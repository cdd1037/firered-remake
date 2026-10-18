using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class TrainerTowerReceiptBoundaryTests
{
    private CampaignSession session = null!;
    private CampaignState basis = null!;

    [TestInitialize]
    public void Initialize()
    {
        session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        basis = session.State with { TrainerTower = TrainerTowerProgressRules.Create() };
        Assert.IsNull(session.ValidatePlayerSave(basis));
    }

    private static CampaignTrainerTowerHistory Receipt(long serial, TrainerTowerChallengeKind course = TrainerTowerChallengeKind.Single,
        int floor = 0, int trainer = 0, WildBattleOutcome outcome = WildBattleOutcome.Won)
    {
        var descriptor = CurrentCampaignFixture.Catalog.TrainerTowerCatalog!.DescribeBattle(course, floor, trainer, 50);
        return new("trainer-tower-battle:" + serial.ToString(CultureInfo.InvariantCulture), course, floor, trainer, 50,
            outcome, Enumerable.Range(0, descriptor.Members.Length).ToImmutableArray());
    }

    private object? Invoke(string method, params object?[] args) => typeof(CampaignSession)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, args);

    [TestMethod]
    public void CurrentBoundaryRetainsOneGlobalReceiptWithoutCouplingSelectedCourseOrChangingRecords()
    {
        var tower = TrainerTowerProgressRules.Start(basis.TrainerTower!, TrainerTowerChallengeKind.Mixed);
        tower = tower with { Records = tower.Records.Select((record, index) => record with
        { TimerFrames = index + 100, BestTimeFrames = index + 1000, ReceivedPrize = index % 2 == 0 }).ToImmutableArray() };
        var state = basis with { TrainerTower = tower, TrainerTowerHistory = [Receipt(9001, TrainerTowerChallengeKind.Double)] };
        Assert.IsNull(session.ValidatePlayerSave(state));
        var bytes = SaveCodec.Encode(state, 27, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion,
            validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(CurrentCampaignFixture.Catalog, loaded.State, loaded.Generation);
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(continued.State));
        Assert.AreEqual(CanonicalJson.Hash(tower), CanonicalJson.Hash(continued.State.TrainerTower));
        Assert.AreEqual("trainer-tower-battle:9002", Invoke("NextTrainerTowerBattleId", state.TrainerTowerHistory));
    }

    [TestMethod]
    public void CurrentBoundaryRejectsUnboundedHistoryRatherThanSilentlyTrimmingIt()
    {
        var state = basis with { TrainerTowerHistory = [Receipt(1), Receipt(2)] };
        var hash = CanonicalJson.Hash(state);
        Assert.AreEqual("save_trainer_tower_history", session.ValidatePlayerSave(state));
        Assert.ThrowsExactly<InvalidDataException>(() => CampaignSession.RestoreCurrent(CurrentCampaignFixture.Catalog, state, 0));
        var bytes = SaveCodec.Encode(state, 0, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion, validate: session.ValidatePlayerSave));
        Assert.AreEqual(hash, CanonicalJson.Hash(state));
        Assert.AreEqual(2, state.TrainerTowerHistory!.Value.Length);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("trainer-tower-battle:0")]
    [DataRow("trainer-tower-battle:-1")]
    [DataRow("trainer-tower-battle:+1")]
    [DataRow("trainer-tower-battle:01")]
    [DataRow("trainer-tower-battle: 1")]
    [DataRow("trainer-tower-battle:1 ")]
    [DataRow("trainer-tower-battle:1.0")]
    [DataRow("trainer-tower-battle:1e1")]
    [DataRow("trainer-tower-battle:9223372036854775808")]
    [DataRow("trainer-tower-battle:١")]
    [DataRow("trainer-Tower-battle:1")]
    public void RetainedIdentityRequiresCanonicalPositiveInt64(string id)
    {
        var state = basis with { TrainerTowerHistory = [Receipt(1) with { Id = id }] };
        Assert.AreEqual("save_trainer_tower_history", session.ValidatePlayerSave(state));
    }

    [TestMethod]
    public void NullEmptyAndLargeLastReceiptsAllocateWithoutASecondSavedCounter()
    {
        Assert.AreEqual("trainer-tower-battle:1", Invoke("NextTrainerTowerBattleId", new object?[] { null }));
        Assert.AreEqual("trainer-tower-battle:1", Invoke("NextTrainerTowerBattleId", ImmutableArray<CampaignTrainerTowerHistory>.Empty));
        Assert.AreEqual("trainer-tower-battle:2147483649", Invoke("NextTrainerTowerBattleId",
            ImmutableArray.Create(Receipt(2147483648L))));
        var last = basis with { TrainerTowerHistory = [Receipt(long.MaxValue)] };
        Assert.IsNull(session.ValidatePlayerSave(last), "The last representable settled battle remains saveable.");
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Invoke("NextTrainerTowerBattleId", last.TrainerTowerHistory));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        Assert.AreEqual("trainer_tower_battle_serial_exhausted", error.InnerException!.Message);
    }

    [TestMethod]
    public void ExhaustionRejectsBattleEntryBeforeFloorPartyOrRngWork()
    {
        // Deliberately no party or Tower floor: ID exhaustion must precede either consumer.
        var state = basis with { TrainerTowerHistory = [Receipt(long.MaxValue)],
            Script = basis.Script with { Wait = new(StoryWaitKind.Battle, "tower-exhaustion") } };
        var before = CanonicalJson.Hash(state);
        var effect = new StoryEffect(1, "special", ["TrainerTowerBattle"], new("data/scripts/trainer_tower.inc", "test", 1));
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Invoke("BeginTrainerTowerBattle", state, effect));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        Assert.AreEqual("trainer_tower_battle_serial_exhausted", error.InnerException!.Message);
        Assert.AreEqual(before, CanonicalJson.Hash(state));
        Assert.AreEqual(basis.World.Rng, state.World.Rng);
        Assert.IsNull(state.TrainerTower!.FloorChain);
    }

    [TestMethod]
    public void EveryLegacyReceiptIsValidatedBeforeOnlyTheLastIsRetained()
    {
        var history = Enumerable.Range(1, 10000).Select(i => Receipt(i,
            (TrainerTowerChallengeKind)(i % 4), floor: i % 8)).ToImmutableArray();
        var source = basis with { TrainerTowerHistory = history };
        var hash = CanonicalJson.Hash(source);
        Assert.IsNull(session.ValidateLegacyTrainerTowerCheckpoint(source));
        var converted = session.ConvertLegacyTrainerTowerCheckpoint(source);
        Assert.IsNull(session.ValidatePlayerSave(converted));
        Assert.AreEqual(1, converted.TrainerTowerHistory!.Value.Length);
        Assert.AreSame(history[^1], converted.TrainerTowerHistory.Value[0]);
        Assert.AreEqual("trainer-tower-battle:10001", Invoke("NextTrainerTowerBattleId", converted.TrainerTowerHistory));
        Assert.AreEqual(hash, CanonicalJson.Hash(converted with { TrainerTowerHistory = history }));
        Assert.AreEqual(hash, CanonicalJson.Hash(source));
        Assert.AreEqual(10000, source.TrainerTowerHistory!.Value.Length);
    }

    [TestMethod]
    public void LegacyConversionRejectsCorruptDiscardedAndRetainedReceipts()
    {
        var valid = Receipt(1);
        CampaignTrainerTowerHistory[] malformed =
        [
            null!, valid with { Id = null! }, valid with { Id = "trainer-tower-battle:01" },
            valid with { Course = (TrainerTowerChallengeKind)99 }, valid with { FloorIndex = -1 },
            valid with { FloorIndex = 8 }, valid with { TrainerIndex = 1 }, valid with { Level = 0 },
            valid with { Level = 101 }, valid with { Outcome = WildBattleOutcome.Ongoing },
            valid with { Outcome = WildBattleOutcome.Caught }, valid with { OpponentsSeen = default },
            valid with { OpponentsSeen = [] }, valid with { OpponentsSeen = [0, 0] },
            valid with { OpponentsSeen = [-1] }, valid with { OpponentsSeen = [2] },
            valid with { OpponentsSeen = [0] }
        ];
        foreach (int index in new[] { 0, 1, 2 })
        foreach (var bad in malformed)
        {
            ImmutableArray<CampaignTrainerTowerHistory> history = [Receipt(1), Receipt(2), Receipt(3)];
            var receipt = bad is not null && bad.Id == valid.Id ? bad with { Id = Receipt(index + 1).Id } : bad;
            var source = basis with { TrainerTowerHistory = history.SetItem(index, receipt!) };
            Assert.IsNotNull(session.ValidateLegacyTrainerTowerCheckpoint(source), $"Bad receipt at index {index} must not be discarded.");
            Assert.ThrowsExactly<InvalidDataException>(() => session.ConvertLegacyTrainerTowerCheckpoint(source));
        }
    }

    [TestMethod]
    [DataRow(WildBattleOutcome.Lost)]
    [DataRow(WildBattleOutcome.Draw)]
    public void TerminalLossOrDrawMayRetainOnlyTheActuallySeenOpponent(WildBattleOutcome outcome)
    {
        var receipt = Receipt(1, outcome: outcome) with { OpponentsSeen = [0] };
        var state = basis with { TrainerTowerHistory = [receipt] };
        Assert.IsNull(session.ValidatePlayerSave(state));
        Assert.IsNull(session.ValidateLegacyTrainerTowerCheckpoint(state));
        Assert.AreSame(state, session.ConvertLegacyTrainerTowerCheckpoint(state));
        Assert.AreEqual("save_trainer_tower_seen", session.ValidatePlayerSave(state with
        { TrainerTowerHistory = [receipt with { Outcome = WildBattleOutcome.Won }] }));
    }

    [TestMethod]
    public void LegacyIdentityStillRequiresEverySequentialReceiptStartingAtOne()
    {
        ImmutableArray<CampaignTrainerTowerHistory>[] bad =
        [
            [Receipt(2)], [Receipt(1), Receipt(3)], [Receipt(1), Receipt(1)],
            [Receipt(2), Receipt(1)], [Receipt(1), Receipt(2), Receipt(2)]
        ];
        foreach (var history in bad)
            Assert.AreEqual("save_trainer_tower_history", session.ValidateLegacyTrainerTowerCheckpoint(basis with { TrainerTowerHistory = history }));
    }

    [TestMethod]
    public void ConversionPreservesNullAndEmptyAndRejectsDefaultOrUnrelatedInvalidState()
    {
        var absent = basis with { TrainerTowerHistory = null };
        var empty = basis with { TrainerTowerHistory = [] };
        Assert.AreSame(absent, session.ConvertLegacyTrainerTowerCheckpoint(absent));
        Assert.AreSame(empty, session.ConvertLegacyTrainerTowerCheckpoint(empty));
        Assert.AreEqual("save_trainer_tower_history", session.ValidateLegacyTrainerTowerCheckpoint(
            basis with { TrainerTowerHistory = default(ImmutableArray<CampaignTrainerTowerHistory>) }));
        var source = basis with { TrainerTowerHistory = [Receipt(1), Receipt(2)] };
        CampaignState[] bad =
        [
            source with { Money = -1 }, source with { TrainerTower = null },
            source with { TrainerTower = basis.TrainerTower! with { Records = [] } },
            source with { Script = source.Script with { Wait = new(StoryWaitKind.Battle, "active") } },
            source with { GameStats = source.GameStats!.Remove("GAME_STAT_HATCHED_EGGS") }
        ];
        foreach (var state in bad)
        {
            Assert.IsNotNull(session.ValidateLegacyTrainerTowerCheckpoint(state));
            Assert.ThrowsExactly<InvalidDataException>(() => session.ConvertLegacyTrainerTowerCheckpoint(state));
        }
    }

    [TestMethod]
    public void OrdinaryContinueDoesNotAcceptTheLegacyRulesEnvelope()
    {
        var legacy = SaveCodec.Encode(basis, 5, "firered-current-campaign-v3", CurrentCampaignFixture.Catalog.ContentVersion);
        var before = legacy.ToArray();
        var error = Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(legacy,
            CurrentGameProfile.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, validate: session.ValidatePlayerSave));
        Assert.AreEqual("save_version_mismatch", error.Message);
        CollectionAssert.AreEqual(before, legacy);
    }
}
