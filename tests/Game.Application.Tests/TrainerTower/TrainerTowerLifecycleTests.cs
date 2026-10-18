using Game.Domain.Trainers;

namespace FireRed.Tests.Application.TrainerTower;

[TestClass, TestCategory("Game")]
public sealed class TrainerTowerLifecycleTests
{
    // Mixed floor 1 is an authentic Singles floor; higher heterogeneous Mixed
    // floors are not claimed by this scoped lifecycle fixture.
    [TestMethod]
    [DataRow(TrainerTowerChallengeKind.Single, false)]
    [DataRow(TrainerTowerChallengeKind.Double, false)]
    [DataRow(TrainerTowerChallengeKind.Knockout, false)]
    [DataRow(TrainerTowerChallengeKind.Mixed, false)]
    [DataRow(TrainerTowerChallengeKind.Single, true)]
    [DataRow(TrainerTowerChallengeKind.Double, true)]
    public void PublicFirstFloorCommandsSettleOnceAcknowledgeReturnAndRoundtrip(
        TrainerTowerChallengeKind course, bool lose)
    {
        _ = new TrainerTowerLifecycleFixture().Course(course, lose);
    }

    [TestMethod]
    [DataRow(TrainerTowerChallengeKind.Single)]
    [DataRow(TrainerTowerChallengeKind.Double)]
    public void RealFinishedBattleRejectsDuplicateSettlementAndMismatchedReturnReceipt(TrainerTowerChallengeKind course)
    {
        new TrainerTowerLifecycleFixture().RejectMismatchedFinishedCallbacks(course);
    }

    [TestMethod]
    public void ContinueAfterSingleFloorThenStartDoublePreservesGlobalLastIdAndIndependentRecords()
    {
        var fixture = new TrainerTowerLifecycleFixture();
        var single = fixture.Course(TrainerTowerChallengeKind.Single);
        var retainedSingle = single.State.TrainerTowerHistory!.Value.Single();
        var doubleRun = fixture.Course(TrainerTowerChallengeKind.Double, origin: single);
        var retainedDouble = doubleRun.State.TrainerTowerHistory!.Value.Single();
        Assert.AreEqual(TrainerTowerChallengeKind.Single, retainedSingle.Course);
        Assert.AreEqual(TrainerTowerChallengeKind.Double, retainedDouble.Course);
        Assert.AreEqual(single.State.TrainerTower!.Records[(int)TrainerTowerChallengeKind.Single],
            doubleRun.State.TrainerTower!.Records[(int)TrainerTowerChallengeKind.Single]);
    }
}
