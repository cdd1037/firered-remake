using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class IntroMovieContinuityTests
{
    [TestMethod]
    public void FightEntranceToWaitKeepsBackgroundAndForegroundPositions()
    {
        var movie = At(IntroMovieStage.Entrance, 56);
        Assert.AreEqual(-224f, movie.FightBackgroundScrollX);
        Assert.AreEqual(91, movie.FightGrassX);
        Assert.IsTrue(movie.FightGrassVisible);
        movie.Advance(1d / 60);
        Assert.AreEqual(IntroMovieStage.WaitCry, movie.Stage);
        Assert.AreEqual(0, movie.Frame);
        Assert.AreEqual(-228f, movie.FightBackgroundScrollX);
        Assert.AreEqual(86, movie.FightGrassX);
        Assert.IsTrue(movie.FightGrassVisible);
    }

    [TestMethod]
    public void GrassCallbackChangesScrollSpeedWithoutRebasingEitherLayer()
    {
        var movie = At(IntroMovieStage.WaitCry, 6);
        Assert.AreEqual(56, movie.FightGrassX);
        Assert.AreEqual(-252f, movie.FightBackgroundScrollX);
        movie.Advance(1d / 60);
        Assert.AreEqual(51, movie.FightGrassX);
        Assert.AreEqual(-256f, movie.FightBackgroundScrollX);
        movie.Advance(1d / 60);
        Assert.AreEqual(50, movie.FightGrassX);
        Assert.AreEqual(-256.125f, movie.FightBackgroundScrollX);
        Assert.AreEqual(-257, movie.FightBackgroundPixelX);
        movie.Advance(7d / 60);
        Assert.AreEqual(-257f, movie.FightBackgroundScrollX);
        Assert.AreEqual(-257, movie.FightBackgroundPixelX);
        movie.Advance(1d / 60);
        Assert.AreEqual(-258, movie.FightBackgroundPixelX);
        while (movie.FightGrassVisible) movie.Advance(1d / 60);
        Assert.AreEqual(-32, movie.FightGrassX);
        float finalScroll = movie.FightBackgroundScrollX;
        movie.Advance(1d / 60);
        Assert.AreEqual(finalScroll - .125f, movie.FightBackgroundScrollX);
        Assert.AreEqual(-32, movie.FightGrassX);
    }

    [TestMethod]
    [DataRow(0, 64, 2)]
    [DataRow(16, 32, 2)]
    [DataRow(32, 32, 2)]
    [DataRow(33, 40, 2)]
    [DataRow(36, 64, 3)]
    [DataRow(40, 96, 3)]
    [DataRow(41, 88, 3)]
    [DataRow(43, 72, 3)]
    [DataRow(44, 64, 0)]
    [DataRow(45, 64, 0)]
    [DataRow(46, 64, 0)]
    public void GengarSwipeReturnsToSourceRestEndpointAndStaysThere(int frame, int sineIndex, int pose)
    {
        var swipe = IntroMoviePresentation.GengarSwipeAt(frame);
        Assert.AreEqual(sineIndex, swipe.SineIndex);
        Assert.AreEqual(pose, swipe.AnimationFrame);
        Assert.AreEqual(frame < 36 ? 32 : 48, swipe.AmplitudeX);
        Assert.AreEqual(frame < 36 ? 6 : 32, swipe.AmplitudeY);
    }

    [TestMethod]
    public void GengarIdleRemainsPausedAfterSwipeUntilRecoilFinishes()
    {
        var movie = At(IntroMovieStage.Swipe, 46);
        Assert.AreEqual(64, IntroMoviePresentation.GengarSwipeAt(movie.Frame).SineIndex);
        movie.Advance(1d / 60);
        Assert.AreEqual(IntroMovieStage.Recoil, movie.Stage);
        do
        {
            Assert.AreEqual(0, movie.GengarFrame);
            Assert.IsFalse(movie.GengarAsSprite);
            movie.Advance(1d / 60);
        } while (movie.Stage == IntroMovieStage.Recoil);
        Assert.AreEqual(IntroMovieStage.WaitHop, movie.Stage);
        Assert.AreEqual(0, movie.GengarFrame);
    }

    [TestMethod]
    public void GengarSpriteReplacementUsesNormalPoseAndRetainsItThroughZoom()
    {
        var movie = At(IntroMovieStage.WaitCharge, 0);
        Assert.IsFalse(movie.GengarAsSprite);
        while (!movie.GengarAsSprite)
        {
            Assert.IsTrue(movie.Stage <= IntroMovieStage.Charge);
            movie.Advance(1d / 60);
        }
        Assert.AreEqual(IntroMovieStage.Charge, movie.Stage);
        Assert.AreEqual(0, movie.GengarFrame);
        while (movie.Stage <= IntroMovieStage.Fade)
        {
            Assert.IsTrue(movie.GengarAsSprite);
            Assert.AreEqual(0, movie.GengarFrame);
            movie.Advance(1d / 60);
        }
    }

    [TestMethod]
    public void FightStateIsIndependentOfDeltaPartitionAndNewMovieHasNoOffsets()
    {
        var single = new IntroMoviePresentation();
        var batched = new IntroMoviePresentation();
        for (int i = 0; i < 1180; i++) single.Advance(1d / 60);
        batched.Advance(1180d / 60);
        Assert.AreEqual(single.Stage, batched.Stage);
        Assert.AreEqual(single.Frame, batched.Frame);
        Assert.AreEqual(single.FightBackgroundScrollX, batched.FightBackgroundScrollX);
        Assert.AreEqual(single.FightGrassX, batched.FightGrassX);
        Assert.AreEqual(single.FightGrassVisible, batched.FightGrassVisible);
        Assert.AreEqual(single.GengarFrame, batched.GengarFrame);
        Assert.AreEqual(single.GengarAsSprite, batched.GengarAsSprite);
        var fresh = new IntroMoviePresentation();
        Assert.AreEqual(0f, fresh.FightBackgroundScrollX);
        Assert.AreEqual(296, fresh.FightGrassX);
        Assert.AreEqual(0, fresh.GengarFrame);
        Assert.IsFalse(fresh.FightGrassVisible);
        Assert.IsFalse(fresh.GengarAsSprite);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IntroMoviePresentation.GengarSwipeAt(-1));
    }

    private static IntroMoviePresentation At(IntroMovieStage stage, int frame)
    {
        var movie = new IntroMoviePresentation();
        while (movie.Stage < stage || movie.Stage == stage && movie.Frame < frame)
        {
            Assert.IsFalse(movie.Complete, "Requested movie frame was never reached.");
            movie.Advance(1d / 60);
        }
        Assert.AreEqual(stage, movie.Stage);
        Assert.AreEqual(frame, movie.Frame);
        return movie;
    }
}
