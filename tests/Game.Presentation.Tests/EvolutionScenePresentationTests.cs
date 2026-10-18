namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class EvolutionScenePresentationTests
{
    [TestMethod]
    public void OriginalMorphCallbackAlternatesAndAcceleratesWithoutSkippingFrames()
    {
        var scene=Reach(EvolutionScenePhase.Morph);
        Assert.AreEqual(EvolutionScenePhase.Morph,scene.Phase);
        Assert.AreEqual(256,scene.PreviousScale);Assert.AreEqual(16,scene.TargetScale);
        AdvanceFrames(scene, 1); // choose, speed 10
        Assert.AreEqual(256,scene.PreviousScale);
        AdvanceFrames(scene, 1);
        Assert.AreEqual(246,scene.PreviousScale);Assert.AreEqual(26,scene.TargetScale);
        AdvanceFrames(scene, 23);
        Assert.AreEqual(16,scene.PreviousScale);Assert.AreEqual(256,scene.TargetScale);
        AdvanceFrames(scene, 2); // choose speed 12, then reverse
        Assert.AreEqual(28,scene.PreviousScale);Assert.AreEqual(244,scene.TargetScale);
        AdvanceFrames(scene, 2000);
        Assert.AreEqual(EvolutionScenePhase.Complete,scene.Phase);
        Assert.AreEqual(0,scene.PreviousScale);Assert.AreEqual(256,scene.TargetScale);
        Assert.IsFalse(scene.Animating);Assert.IsFalse(scene.Cancel());
    }
    [TestMethod]
    public void CancellationRestoresOldSpriteAndStoneCannotCancel()
    {
        var ordinary=Reach(EvolutionScenePhase.Morph);
        Assert.IsTrue(ordinary.Cancel());AdvanceFrames(ordinary, 2000);
        Assert.AreEqual(EvolutionScenePhase.Cancelled,ordinary.Phase);
        Assert.AreEqual(256,ordinary.PreviousScale);Assert.AreEqual(0,ordinary.TargetScale);
        var stone=Reach(EvolutionScenePhase.Morph, canCancel:false);
        Assert.IsFalse(stone.Cancel());Assert.AreEqual(EvolutionScenePhase.Morph,stone.Phase);
        AdvanceFrames(stone, 2000);Assert.AreEqual(EvolutionScenePhase.Complete,stone.Phase);
    }
    [TestMethod]
    public void TimingIsFrameRateIndependentAndHostStallsPreserveCancellationWindow()
    {
        var sixty=Reach(EvolutionScenePhase.Spiral);var oneTwenty=Reach(EvolutionScenePhase.Spiral);
        for(int i=0;i<400;i++)Advance(sixty, 1d/60);
        for(int i=0;i<800;i++)Advance(oneTwenty, 1d/120);
        Assert.AreEqual(sixty.Phase,oneTwenty.Phase);Assert.AreEqual(sixty.PhaseFrame,oneTwenty.PhaseFrame);
        Assert.AreEqual(sixty.PreviousScale,oneTwenty.PreviousScale);
        var stalled=NewScene();Advance(stalled, 500);
        Assert.AreEqual(15,stalled.PhaseFrame);Assert.IsFalse(stalled.Cancel());
        for(int i=0;i<1000&&stalled.Phase!=EvolutionScenePhase.Morph;i++)AdvanceFrames(stalled, 1);
        Assert.IsTrue(stalled.CanCancel);
        Advance(stalled, 500);Assert.IsTrue(stalled.Cancel());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>Advance(sixty, double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>Advance(sixty, -1));
    }

    // Later-phase regressions explicitly drive the intro with an unavailable backend.
    // They never rely on a constructor bypass or an elapsed intro duration.
    private static EvolutionScenePresentation NewScene(bool canCancel = true, uint seed = 0x45564F) =>
        new(canCancel, "What? TEST\nis evolving!", 1, seed);
    private static void AdvanceFrames(EvolutionScenePresentation scene, int frames) =>
        scene.AdvanceFrames(frames, () => EvolutionIntroSignals.Unavailable, _ => { });
    private static void Advance(EvolutionScenePresentation scene, double seconds) =>
        scene.Advance(seconds, () => EvolutionIntroSignals.Unavailable, _ => { });

    private static EvolutionScenePresentation Reach(EvolutionScenePhase phase, uint seed = 0x45564F, bool canCancel = true)
    {
        var scene = NewScene(canCancel, seed);
        for (int i = 0; i < 2000 && scene.Phase != phase; i++) AdvanceFrames(scene, 1);
        Assert.AreEqual(phase, scene.Phase);
        return scene;
    }
    [TestMethod]
    public void CancellationIsOnlyAcceptedDuringTheSourceMorphWindow()
    {
        foreach (var phase in Enum.GetValues<EvolutionScenePhase>().Where(p => p != EvolutionScenePhase.Cancelled))
        {
            var scene = Reach(phase);
            Assert.AreEqual(phase == EvolutionScenePhase.Morph, scene.CanCancel, phase.ToString());
            Assert.AreEqual(phase == EvolutionScenePhase.Morph, scene.Cancel(), phase.ToString());
        }
    }
    [TestMethod]
    public void PaletteSchedulerUsesSeparateBackgroundAndObjectPassesAndFinishingCallbacks()
    {
        var fade = new EvolutionPaletteFade(0, 16, 10);
        Assert.AreEqual(0, fade.BackgroundBlend); Assert.AreEqual(0, fade.ObjectBlend);
        for (int i = 0; i < 11; i++) fade.AdvanceFrame();
        Assert.AreEqual(0, fade.BackgroundBlend);
        fade.AdvanceFrame(); Assert.AreEqual(2, fade.BackgroundBlend); Assert.AreEqual(0, fade.ObjectBlend);
        fade.AdvanceFrame(); Assert.AreEqual(2, fade.ObjectBlend);
        for (int i = 13; i < 97; i++) fade.AdvanceFrame();
        Assert.AreEqual(16, fade.BackgroundBlend); Assert.AreEqual(16, fade.ObjectBlend); Assert.IsTrue(fade.Active);
        for (int i = 0; i < 4; i++) fade.AdvanceFrame();
        Assert.IsTrue(fade.Active); fade.AdvanceFrame(); Assert.IsFalse(fade.Active);
    }
    [TestMethod]
    public void RevealFreezesMovingPaletteAndUsesSourceSteppedWhiteFlash()
    {
        var scene = Reach(EvolutionScenePhase.Reveal); int paletteFrame = scene.BackgroundFrame;
        Assert.IsFalse(scene.CanCancel); Assert.IsFalse(scene.MovingBackgroundVisible);
        Assert.AreEqual(0, scene.BackgroundWhiteCoefficient); Assert.AreEqual(16, scene.PortraitWhiteCoefficient);
        AdvanceFrames(scene, 2); Assert.AreEqual(2, scene.BackgroundWhiteCoefficient);
        AdvanceFrames(scene, 31); Assert.AreEqual(16, scene.BackgroundWhiteCoefficient); Assert.AreEqual(16, scene.PortraitWhiteCoefficient);
        AdvanceFrames(scene, 18); Assert.AreEqual(14, scene.BackgroundWhiteCoefficient); Assert.AreEqual(16, scene.PortraitWhiteCoefficient);
        AdvanceFrames(scene, 1); Assert.AreEqual(14, scene.PortraitWhiteCoefficient);
        Assert.AreEqual(paletteFrame, scene.BackgroundFrame);
        AdvanceFrames(scene, 130); Assert.AreEqual(EvolutionScenePhase.Reveal, scene.Phase);
        Assert.AreEqual(0, scene.PortraitWhiteCoefficient); AdvanceFrames(scene, 1);
        Assert.AreEqual(EvolutionScenePhase.Complete, scene.Phase);
    }
    [TestMethod]
    public void PaletteMasksKeepPortraitSparkleAndBackgroundCoefficientsIndependent()
    {
        static void Check(EvolutionScenePresentation scene, int portrait, int sparkle, int background)
        {
            Assert.AreEqual(portrait, scene.PortraitWhiteCoefficient, $"{scene.Phase}/{scene.PhaseFrame}: portrait");
            Assert.AreEqual(sparkle, scene.SparkleWhiteCoefficient, $"{scene.Phase}/{scene.PhaseFrame}: sparkle");
            Assert.AreEqual(background, scene.BackgroundWhiteCoefficient, $"{scene.Phase}/{scene.PhaseFrame}: background");
        }
        var spiral = Reach(EvolutionScenePhase.Spiral); Check(spiral, 0, 0, 0);
        AdvanceFrames(spiral, 12); Check(spiral, 0, 0, 0); // BG pass is outside the portrait mask.
        AdvanceFrames(spiral, 1); Check(spiral, 2, 0, 0);
        AdvanceFrames(spiral, 84); Check(spiral, 16, 0, 0);
        foreach (var phase in new[] { EvolutionScenePhase.Arc, EvolutionScenePhase.Morph, EvolutionScenePhase.Circle })
            Check(Reach(phase), 16, 0, 0);
        var reveal = Reach(EvolutionScenePhase.Reveal); Check(reveal, 16, 0, 0);
        AdvanceFrames(reveal, 2); Check(reveal, 16, 0, 2);
        AdvanceFrames(reveal, 1); Check(reveal, 16, 2, 2);
        AdvanceFrames(reveal, 13); Check(reveal, 16, 14, 16);
        AdvanceFrames(reveal, 1); Check(reveal, 16, 16, 16);
        AdvanceFrames(reveal, 16); Check(reveal, 16, 16, 16); // Return fade begins at frame 33.
        AdvanceFrames(reveal, 18); Check(reveal, 16, 16, 14);
        AdvanceFrames(reveal, 1); Check(reveal, 14, 14, 14);
        AdvanceFrames(reveal, 125); Check(reveal, 2, 2, 0);
        AdvanceFrames(reveal, 1); Check(reveal, 0, 0, 0);
        AdvanceFrames(reveal, 5); Assert.AreEqual(EvolutionScenePhase.Complete, reveal.Phase); Check(reveal, 0, 0, 0);
        var cancelled = Reach(EvolutionScenePhase.Morph); Assert.IsTrue(cancelled.Cancel()); Check(cancelled, 0, 0, 0);
        Check(Reach(EvolutionScenePhase.Intro), 0, 0, 0);
    }
    [TestMethod]
    public void SourceSprayHasBurstSkippedFadeFrameAffineSizeAndBoundedLifetimes()
    {
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var scene = Reach(EvolutionScenePhase.Reveal);
        Assert.IsTrue(scene.Sparkles(sine).IsEmpty);
        AdvanceFrames(scene, 1); var first = scene.Sparkles(sine);
        Assert.AreEqual(8, first.Length); Assert.IsTrue(first.All(p => p.X == 120 && p.Y == 57 && p.Scale == 1));
        AdvanceFrames(scene, 31); Assert.AreEqual(39, scene.Sparkles(sine).Length);
        AdvanceFrames(scene, 1); Assert.AreEqual(39, scene.Sparkles(sine).Length);
        AdvanceFrames(scene, 17); Assert.AreEqual(56, scene.Sparkles(sine).Length);
        AdvanceFrames(scene, 78); Assert.AreEqual(56, scene.Sparkles(sine).Length);
        Assert.IsTrue(scene.Sparkles(sine).Any(p => p.MatrixIndex == 0 && p.Subpriority == 1));
        AdvanceFrames(scene, 1); Assert.AreEqual(48, scene.Sparkles(sine).Length);
        AdvanceFrames(scene, 49); Assert.IsTrue(scene.Sparkles(sine).IsEmpty);
    }
    [TestMethod]
    public void ParticleSamplingAndChunkingDoNotConsumeOrAlterVisualRandomness()
    {
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var first = Reach(EvolutionScenePhase.Reveal, 42); var second = Reach(EvolutionScenePhase.Reveal, 42);
        AdvanceFrames(first, 60);
        for (int i = 0; i < 60; i++) { AdvanceFrames(second, 1); _ = second.Sparkles(sine); }
        CollectionAssert.AreEqual(first.Sparkles(sine).ToArray(), second.Sparkles(sine).ToArray());
        var different = Reach(EvolutionScenePhase.Reveal, 43); AdvanceFrames(different, 60);
        Assert.IsFalse(first.Sparkles(sine).SequenceEqual(different.Sparkles(sine)));
        Assert.ThrowsExactly<ArgumentException>(() => first.Sparkles([]));
    }
}
