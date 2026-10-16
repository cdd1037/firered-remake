using System.Collections.Immutable;
using Game.Domain.Capture;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class CaptureBallPresentationTests
{
    private static CaptureBallPresentationAssets Assets => CaptureBallPresentationAssets.Load(TestAssets.Root);
    private static CaptureBallPresentation Playback(int code, CaptureBall ball = CaptureBall.Poke) =>
        new(ball, code == 4 ? CaptureOutcome.Caught : CaptureOutcome.Escaped, code, Assets.Sine);

    [TestMethod]
    public void EveryNativeBallUsesItsOwnAtlasColumn()
    {
        var assets=Assets;
        Assert.IsTrue(File.Exists(assets.TexturePath));
        foreach(CaptureBall ball in Enum.GetValues<CaptureBall>())
        {
            Assert.AreEqual(((int)ball-1)*16,assets.Column(ball));
            var playback=Playback(4,ball);
            Assert.AreEqual(ball,playback.Ball);
            Assert.AreEqual(3,playback.Cues.Count(c=>c.Symbol=="SE_BALL"));
        }
    }

    [TestMethod]
    public void OutcomeDrivesZeroThroughThreeShakesAndThreeShakeSuccess()
    {
        for(int code=0;code<=4;code++)
        {
            var playback=Playback(code);
            Assert.AreEqual(code==4?3:code,playback.ShakeCount);
            Assert.AreEqual(playback.ShakeCount,playback.Cues.Count(c=>c.Symbol=="SE_BALL"));
            Assert.AreEqual(CaptureBallPhase.Complete,playback.Sample(int.MaxValue).Phase);
            Assert.IsFalse(playback.Sample(int.MaxValue).BallVisible);
            Assert.AreEqual(code==4?0f:1f,playback.Sample(int.MaxValue).TargetScale);
            Assert.IsFalse(playback.Complete(playback.DurationTicks-1));
            Assert.IsTrue(playback.Complete(playback.DurationTicks));
        }
    }

    [TestMethod]
    public void NativeThrowUsesFixedPointArcThenShrinkBeforeHidingTarget()
    {
        var playback=Playback(0);
        Assert.AreEqual((32,80),(playback.Sample(0).X,playback.Sample(0).Y));
        Assert.AreEqual((36,76),(playback.Sample(1).X,playback.Sample(1).Y));
        Assert.AreEqual(1f,playback.Sample(34).TargetScale);
        Assert.AreEqual(CaptureBallPhase.Open,playback.Sample(35).Phase);
        Assert.AreEqual(1,playback.Sample(35).TileFrame);
        Assert.AreEqual(2,playback.Sample(40).TileFrame);
        var shrink=playback.Frames.Where(f=>f.Phase==CaptureBallPhase.Shrink).ToArray();
        Assert.AreEqual(30,shrink.Length);
        Assert.AreEqual(1f,shrink[0].TargetScale);
        Assert.AreEqual(256f/1152,shrink[^2].TargetScale);
        Assert.AreEqual(0f,shrink[^1].TargetScale);
        CollectionAssert.AreEqual(new[]{"SE_BALL_BOUNCE_1","SE_BALL_BOUNCE_2","SE_BALL_BOUNCE_3","SE_BALL_BOUNCE_4"},
            playback.Cues.Where(c=>c.Symbol.StartsWith("SE_BALL_BOUNCE_",StringComparison.Ordinal)).Select(c=>c.Symbol).ToArray());
    }

    [TestMethod]
    public void ClickAndCaughtIntroAreTimedFromClickCallbackWithoutFourthShake()
    {
        var playback=Playback(4);
        int start=Enumerable.Range(0,playback.Frames.Length).First(i=>playback.Frames[i].Phase==CaptureBallPhase.Click);
        Assert.AreEqual(start+40,playback.Cues.Single(c=>c.Symbol=="SE_BALL_CLICK").Tick);
        Assert.AreEqual(start+95,playback.Cues.Single(c=>c.Symbol=="MUS_CAUGHT_INTRO").Tick);
        Assert.IsFalse(playback.Frames.Any(f=>f.Phase==CaptureBallPhase.BreakOut));
        var fail=Playback(3);
        Assert.IsFalse(fail.Cues.Any(c=>c.Symbol=="SE_BALL_CLICK"));
        Assert.IsTrue(fail.Frames.Any(f=>f.Phase==CaptureBallPhase.BreakOut));
    }

    [TestMethod]
    public void RepeatedSamplingCannotRollOrMutateAnOutcome()
    {
        var playback=Playback(4,CaptureBall.Master);
        var before=playback.Frames;
        for(int i=0;i<1000;i++)Assert.AreSame(playback.Sample(87),playback.Sample(87));
        Assert.AreEqual(before,playback.Frames);
        Assert.ThrowsExactly<ArgumentException>(()=>new CaptureBallPresentation(CaptureBall.Poke,CaptureOutcome.Caught,3,Assets.Sine));
        Assert.ThrowsExactly<ArgumentException>(()=>new CaptureBallPresentation(CaptureBall.Poke,CaptureOutcome.Escaped,4,Assets.Sine));
    }
    [TestMethod]
    public void NativeBurstsAndSuccessStarsUseImportedParticleTiles()
    {
        foreach(CaptureBall ball in Enum.GetValues<CaptureBall>())
        {
            var playback=Playback(4,ball);
            var particles=playback.Particles(40);
            Assert.IsFalse(particles.IsEmpty,ball.ToString());
            Assert.IsTrue(particles.All(p=>p.TileFrame is >=0 and <=7));
            int click=playback.Cues.Single(c=>c.Symbol=="SE_BALL_CLICK").Tick;
            Assert.AreEqual(0,playback.Particles(click).Length);
            Assert.AreEqual(3,playback.Particles(click+1).Length);
            Assert.IsTrue(playback.Particles(click+1).All(p=>p.TileFrame==3));
            Assert.AreEqual(0,playback.Particles(click+24).Length);
        }
    }

    [TestMethod]
    public void FailureRestoresNativeTargetPaletteBeforeResultCanAdvance()
    {
        var playback=Playback(1);
        Assert.AreEqual(0,playback.TargetBlend(0));
        Assert.AreEqual(16,playback.TargetBlend(60));
        Assert.AreEqual(0,playback.TargetBlend(playback.DurationTicks));
    }

}
