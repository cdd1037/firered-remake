namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class PokedexTransitionClockTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void HelpFreezesEveryTurnAndZoomThenResumesFromSameFrame(int kind)
    {
        var clock = new PokedexTransitionClock();
        clock.Begin(kind);
        clock.Advance(3d / 60, false);
        Assert.AreEqual(3, clock.Frame);
        clock.Advance(60, true);
        Assert.AreEqual(3, clock.Frame);
        Assert.AreEqual(kind, clock.Kind);
        clock.Advance(1d / 60, false);
        Assert.AreEqual(4, clock.Frame);
    }

    [TestMethod]
    public void ClosingMidTurnCancelsTheProgramAndNextVisitStartsAtZero()
    {
        var clock = new PokedexTransitionClock();
        clock.Begin(2);
        clock.Advance(5d / 60, false);
        clock.Reset();
        clock.Advance(60, false);
        Assert.IsFalse(clock.Active);
        Assert.AreEqual(0, clock.Kind);
        Assert.AreEqual(0, clock.Frame);
        clock.Begin(1);
        Assert.AreEqual(0, clock.Frame);
        clock.Advance(1d / 60, false);
        Assert.AreEqual(1, clock.Frame);
    }

    [TestMethod]
    public void LogicalFramesAreChunkIndependentAndDoNotNeedDrawCallbacks()
    {
        var whole = new PokedexTransitionClock();
        var chunks = new PokedexTransitionClock();
        whole.Begin(3); chunks.Begin(3);
        whole.Advance(7d / 60, false);
        for (int frame = 0; frame < 7; frame++) chunks.Advance(1d / 60, false);
        Assert.AreEqual(7, whole.Frame);
        Assert.AreEqual(whole.Frame, chunks.Frame);
        whole.Begin(4);
        Assert.AreEqual(0, whole.Frame);
    }

    [TestMethod]
    public void InvalidProgramsAndNonfiniteTimesFailClosed()
    {
        var clock = new PokedexTransitionClock();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Begin(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Begin(5));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN, false));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Advance(double.PositiveInfinity, false));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clock.Advance(-1, false));
    }
}
