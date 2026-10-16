using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class OpeningTitlePresentationTests
{
    [TestMethod]
    public void SourceBlinkSlashAndRestartBoundaries()
    {
        var title = new OpeningTitlePresentation();
        Assert.IsTrue(title.PressStartVisible);
        title.AdvanceFrames(59); Assert.IsTrue(title.PressStartVisible);
        title.AdvanceFrames(1); Assert.IsFalse(title.PressStartVisible);
        title.AdvanceFrames(30); Assert.IsTrue(title.PressStartVisible);
        title.AdvanceFrames(449); Assert.IsFalse(title.SlashVisible);
        title.AdvanceFrames(1); Assert.IsTrue(title.SlashVisible); Assert.AreEqual(-32, title.SlashX);
        title.AdvanceFrames(11); Assert.AreEqual(67, title.SlashX); Assert.AreEqual(20, title.SlashY);
        title.AdvanceFrames(9); Assert.AreEqual(148, title.SlashX); Assert.AreEqual(27, title.SlashY);
        title.AdvanceFrames(14); Assert.IsFalse(title.SlashVisible);
        title.AdvanceFrames(2699 - title.Frame); Assert.IsFalse(title.RestartDue);
        title.AdvanceFrames(1); Assert.IsTrue(title.RestartDue);
    }

    [TestMethod]
    public void FixedLocalRandomSequenceIsFrameChunkIndependentAndBounded()
    {
        var a = new OpeningTitlePresentation(); var b = new OpeningTitlePresentation();
        a.AdvanceFrames(2); Assert.AreEqual(16, a.Flames.Count);
        Assert.AreEqual(new OpeningTitlePresentation.Flame(24 * 16, 118 * 16, -2, -10, 0, false), a.Flames[0]);
        a.AdvanceFrames(898);
        for (int i = 0; i < 900; i++) b.AdvanceFrames(1);
        CollectionAssert.AreEqual(a.Flames.ToArray(), b.Flames.ToArray());
        Assert.IsTrue(a.Flames.Count is > 0 and <= 64);
        a.Reset(); b.Reset(); a.AdvanceFrames(20); b.AdvanceFrames(20);
        CollectionAssert.AreEqual(a.Flames.ToArray(), b.Flames.ToArray());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => a.AdvanceFrames(-1));
    }

    [TestMethod]
    public void CompactSourceAnimationUsesDurationsAndLoopBoundaries()
    {
        OpeningAnimationFrame[] track = [new(0, 30), new(1, 30)];
        Assert.AreEqual(0, OpeningTitlePresentation.AnimationFrame(track, 29));
        Assert.AreEqual(1, OpeningTitlePresentation.AnimationFrame(track, 30));
        Assert.AreEqual(0, OpeningTitlePresentation.AnimationFrame(track, 60));
        Assert.AreEqual(-1, OpeningTitlePresentation.AnimationFrame(track, 60, false));
        Assert.ThrowsExactly<ArgumentException>(() => OpeningTitlePresentation.AnimationFrame([], 0));
    }
    [TestMethod]
    public void RevealAndPlayerShrinkCompleteWithSourceStepBoundaries()
    {
        var reveal = new OpeningTitleRevealPresentation();
        Assert.AreEqual(128, reveal.Scanline);
        reveal.AdvanceFrame(); Assert.AreEqual(124, reveal.Scanline);
        for (int i = 0; i < 400; i++) reveal.AdvanceFrame();
        Assert.IsTrue(reveal.Complete);
        reveal.Reset(); reveal.Skip(); Assert.IsTrue(reveal.Complete);
        var exit = new OpeningExitPresentation();
        for (int i = 0; i < 19; i++) exit.AdvanceFrame();
        Assert.AreEqual(1f, exit.ScaleX);
        exit.AdvanceFrame(); Assert.AreEqual(248 / 256f, exit.ScaleX); Assert.AreEqual(208 / 256f, exit.ScaleY);
        for (int i = 20; i < 100; i++) exit.AdvanceFrame();
        Assert.AreEqual(120 / 256f, exit.ScaleX); Assert.AreEqual(80 / 256f, exit.ScaleY);
        for (int i = 100; i < 184; i++) exit.AdvanceFrame();
        Assert.IsTrue(exit.Complete); Assert.AreEqual(1f, exit.PlayerBlackFade);
    }

    [TestMethod]
    public void NidoranReleaseAndReturnUseSharedBallSineAndBoundedParticles()
    {
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var release = new OpeningNidoranPresentation(false, sine);
        for (int i = 0; i < 32; i++) release.AdvanceFrame();
        Assert.IsFalse(release.TargetVisible);
        release.AdvanceFrame(); Assert.IsTrue(release.TargetVisible); Assert.AreEqual(100, release.X); Assert.AreEqual(66, release.Y);
        for (int i = 33; i < 96; i++) release.AdvanceFrame();
        Assert.IsTrue(release.Complete); Assert.AreEqual(96, release.X); Assert.AreEqual(96, release.Y); Assert.AreEqual(1f, release.Scale);
        Assert.AreEqual(0f, release.BackgroundFlash); Assert.AreEqual(0, release.TintBlend);
        var back = new OpeningNidoranPresentation(true, sine);
        for (int i = 0; i < 66; i++) back.AdvanceFrame();
        Assert.IsFalse(back.TargetVisible); Assert.AreEqual(0f, back.Scale);
        for (int i = 66; i < 112; i++) back.AdvanceFrame();
        Assert.IsTrue(back.Complete);
        Assert.AreEqual(0, CaptureBallPresentation.PokeBallBurst(-1, 100, 61, sine).Length);
        Assert.AreEqual(1, CaptureBallPresentation.PokeBallBurst(1, 100, 61, sine).Length);
        Assert.AreEqual(16, CaptureBallPresentation.PokeBallBurst(16, 100, 61, sine).Length);
        Assert.AreEqual(0, CaptureBallPresentation.PokeBallBurst(41, 100, 61, sine).Length);
    }

    [TestMethod]
    public void TitleExitKeepsFlamesMovingAndFinishesOnlyTheActiveSlash()
    {
        var title = new OpeningTitlePresentation();
        title.AdvanceFrames(550); Assert.IsTrue(title.SlashVisible);
        var before = title.Flames.ToArray();
        title.BeginExit(); title.AdvanceFrames(12);
        Assert.AreEqual(562, title.Frame); Assert.IsTrue(title.SlashVisible);
        Assert.IsFalse(before.SequenceEqual(title.Flames));
        title.AdvanceFrames(12); Assert.IsFalse(title.SlashVisible);
        title.AdvanceFrames(540); Assert.IsFalse(title.SlashVisible);
        title.AdvanceFrames(2700); Assert.IsFalse(title.RestartDue);
        title.Reset(); title.AdvanceFrames(530); title.BeginExit(); title.AdvanceFrames(10);
        Assert.IsFalse(title.SlashVisible); // Do not start another slash during the cry hold.
    }

}
