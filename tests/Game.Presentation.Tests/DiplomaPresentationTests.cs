namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class DiplomaPresentationTests
{
    [TestMethod]
    public void FadeFanfareFreshAAndFieldReturnOccurInOrder()
    {
        var screen = new DiplomaPresentation(16, 340);
        int fanfares = 0; screen.FanfareRequested += () => fanfares++;
        screen.Open();
        Assert.AreEqual(16, screen.FadeCoefficient);
        screen.AdvanceFrames(15, true); Assert.AreEqual(1, screen.FadeCoefficient);
        Assert.AreEqual(0, fanfares);
        screen.AdvanceFrames(1, true);
        Assert.AreEqual(1, fanfares); Assert.AreEqual(DiplomaPhase.Fanfare, screen.Phase);
        screen.AdvanceFrames(340, true); Assert.AreEqual(DiplomaPhase.Fanfare, screen.Phase);
        screen.AdvanceFrames(1, false); Assert.AreEqual(DiplomaPhase.Fanfare, screen.Phase);
        screen.AdvanceFrames(1, true); Assert.AreEqual(DiplomaPhase.AwaitingConfirm, screen.Phase);
        screen.Confirm(true); Assert.AreEqual(DiplomaPhase.FadeOut, screen.Phase);
        screen.AdvanceFrames(15, true); Assert.IsTrue(screen.CertificateVisible); Assert.AreEqual(15, screen.FadeCoefficient);
        screen.AdvanceFrames(1, true); Assert.IsFalse(screen.CertificateVisible); Assert.IsTrue(screen.FieldVisible);
        Assert.AreEqual(16, screen.FadeCoefficient);
        screen.AdvanceFrames(15, true); Assert.AreEqual(DiplomaPhase.ReturnFadeIn, screen.Phase);
        screen.AdvanceFrames(1, true); Assert.AreEqual(DiplomaPhase.Complete, screen.Phase);
        Assert.AreEqual(0, screen.FadeCoefficient); Assert.AreEqual(1, fanfares);
        screen.Close(); Assert.IsFalse(screen.Active);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public void EarlyOrAlreadyHeldAIsNotQueued(bool alreadyHeld)
    {
        var screen = new DiplomaPresentation(16, 340); screen.Open(alreadyHeld);
        if (!alreadyHeld) screen.Confirm(true);
        screen.AdvanceFrames(357, true);
        Assert.AreEqual(DiplomaPhase.AwaitingConfirm, screen.Phase);
        screen.Confirm(true); screen.Confirm(true, echo: true);
        Assert.AreEqual(DiplomaPhase.AwaitingConfirm, screen.Phase);
        screen.Confirm(false); screen.Confirm(true);
        Assert.AreEqual(DiplomaPhase.FadeOut, screen.Phase);
    }

    [TestMethod]
    public void PressReleasedDuringFanfareDoesNotBecomeAQueuedExit()
    {
        var screen = new DiplomaPresentation(16, 340); screen.Open();
        screen.AdvanceFrames(30, true); screen.Confirm(true); screen.Confirm(false);
        screen.AdvanceFrames(500, true); Assert.AreEqual(DiplomaPhase.AwaitingConfirm, screen.Phase);
        screen.ReleaseInput(); Assert.AreEqual(DiplomaPhase.AwaitingConfirm, screen.Phase);
        screen.Confirm(true, echo: true); Assert.AreEqual(DiplomaPhase.AwaitingConfirm, screen.Phase);
        screen.Confirm(false); screen.Confirm(true); screen.AdvanceFrames(32, true);
        Assert.AreEqual(DiplomaPhase.Complete, screen.Phase);
        screen.Open(); Assert.AreEqual(DiplomaPhase.FadeIn, screen.Phase);
    }

    [TestMethod]
    public void DeltaSteppingAndFrameSteppingAgreeAndRejectInvalidTime()
    {
        var clocked = new DiplomaPresentation(16, 340); var frames = new DiplomaPresentation(16, 340);
        clocked.Open(); frames.Open();
        for (int n = 0; n < 357; n++) clocked.Advance(1d / 60, true);
        frames.AdvanceFrames(357, true); Assert.AreEqual(frames.Phase, clocked.Phase);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => clocked.Advance(double.NaN, true));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => frames.AdvanceFrames(-1, true));
    }
}
