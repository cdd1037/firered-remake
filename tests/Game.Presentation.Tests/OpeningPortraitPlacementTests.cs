namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class OpeningPortraitPlacementTests
{
    [TestMethod]
    [DataRow("red", "rival")]
    [DataRow("leaf", "rival")]
    [DataRow("rival", "red")]
    [DataRow("rival", "leaf")]
    public void NamedTrainerKeepsItsShiftUntilHiddenAndNextTrainerIsCentered(string previous, string next)
    {
        var transition = new OpeningPortraitTransition(previous, next, 40, 60, 0);
        for (int frame = 0; frame < 49; frame++)
        {
            Assert.AreEqual(previous, transition.Portrait);
            Assert.AreEqual(60, transition.OffsetX, $"Outgoing portrait/platform moved at frame {frame}.");
            transition.AdvanceFrames(1);
        }
        Assert.AreEqual(0f, transition.Alpha);
        Assert.IsFalse(transition.PlatformVisible);
        Assert.AreEqual(next, transition.Portrait);
        Assert.AreEqual(0, transition.OffsetX);
        for (int frame = 49; !transition.Complete; frame++)
        {
            Assert.AreEqual(0, transition.OffsetX, $"Incoming portrait/platform moved at frame {frame}.");
            transition.AdvanceFrames(1);
        }
        Assert.AreEqual(1f, transition.Alpha);
        Assert.IsTrue(transition.PlatformVisible);
    }

    [TestMethod]
    public void CenteredAndIncomingOnlyTransitionsRetainExistingLayout()
    {
        var centered = new OpeningPortraitTransition("oak", null, 48);
        var incoming = new OpeningPortraitTransition(null, "leaf");
        for (int frame = 0; frame < 120; frame++)
        {
            Assert.AreEqual(0, centered.OffsetX);
            Assert.AreEqual(0, incoming.OffsetX);
            centered.AdvanceFrames(1); incoming.AdvanceFrames(1);
        }
    }

    [TestMethod]
    public void OffsetPhaseIsIndependentOfFrameChunking()
    {
        var single = new OpeningPortraitTransition("rival", "red", 40, 60, 0);
        var batched = new OpeningPortraitTransition("rival", "red", 40, 60, 0);
        for (int i = 0; i < 102; i++) single.AdvanceFrames(1);
        batched.AdvanceFrames(102);
        Assert.AreEqual(single.Portrait, batched.Portrait);
        Assert.AreEqual(single.OffsetX, batched.OffsetX);
        Assert.AreEqual(single.Alpha, batched.Alpha);
        Assert.AreEqual(single.PlatformVisible, batched.PlatformVisible);
    }
}
