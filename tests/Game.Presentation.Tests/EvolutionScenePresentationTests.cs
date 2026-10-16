namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class EvolutionScenePresentationTests
{
    [TestMethod]
    public void OriginalMorphCallbackAlternatesAndAcceleratesWithoutSkippingFrames()
    {
        var scene=new EvolutionScenePresentation(true);
        scene.AdvanceFrames(90+162+99);
        Assert.AreEqual(EvolutionScenePhase.Morph,scene.Phase);
        Assert.AreEqual(256,scene.PreviousScale);Assert.AreEqual(16,scene.TargetScale);
        scene.AdvanceFrames(1); // choose, speed 10
        Assert.AreEqual(256,scene.PreviousScale);
        scene.AdvanceFrames(1);
        Assert.AreEqual(246,scene.PreviousScale);Assert.AreEqual(26,scene.TargetScale);
        scene.AdvanceFrames(23);
        Assert.AreEqual(16,scene.PreviousScale);Assert.AreEqual(256,scene.TargetScale);
        scene.AdvanceFrames(2); // choose speed 12, then reverse
        Assert.AreEqual(28,scene.PreviousScale);Assert.AreEqual(244,scene.TargetScale);
        scene.AdvanceFrames(2000);
        Assert.AreEqual(EvolutionScenePhase.Complete,scene.Phase);
        Assert.AreEqual(0,scene.PreviousScale);Assert.AreEqual(256,scene.TargetScale);
        Assert.IsFalse(scene.Animating);Assert.IsFalse(scene.Cancel());
    }
    [TestMethod]
    public void CancellationRestoresOldSpriteAndStoneCannotCancel()
    {
        var ordinary=new EvolutionScenePresentation(true);ordinary.AdvanceFrames(400);
        Assert.IsTrue(ordinary.Cancel());ordinary.AdvanceFrames(2000);
        Assert.AreEqual(EvolutionScenePhase.Cancelled,ordinary.Phase);
        Assert.AreEqual(256,ordinary.PreviousScale);Assert.AreEqual(0,ordinary.TargetScale);
        var stone=new EvolutionScenePresentation(false);stone.AdvanceFrames(400);
        Assert.IsFalse(stone.Cancel());Assert.AreEqual(EvolutionScenePhase.Morph,stone.Phase);
        stone.AdvanceFrames(2000);Assert.AreEqual(EvolutionScenePhase.Complete,stone.Phase);
    }
    [TestMethod]
    public void TimingIsFrameRateIndependentAndHostStallsPreserveCancellationWindow()
    {
        var sixty=new EvolutionScenePresentation(true);var oneTwenty=new EvolutionScenePresentation(true);
        for(int i=0;i<400;i++)sixty.Advance(1d/60);
        for(int i=0;i<800;i++)oneTwenty.Advance(1d/120);
        Assert.AreEqual(sixty.Phase,oneTwenty.Phase);Assert.AreEqual(sixty.PhaseFrame,oneTwenty.PhaseFrame);
        Assert.AreEqual(sixty.PreviousScale,oneTwenty.PreviousScale);
        var stalled=new EvolutionScenePresentation(true);stalled.Advance(500);
        Assert.AreEqual(15,stalled.PhaseFrame);Assert.IsFalse(stalled.Cancel());
        stalled.AdvanceFrames(90+162+99-15);Assert.IsTrue(stalled.CanCancel);
        stalled.Advance(500);Assert.IsTrue(stalled.Cancel());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>sixty.Advance(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>sixty.Advance(-1));
    }

    private static EvolutionScenePresentation Reach(EvolutionScenePhase phase, uint seed = 0x45564F)
    {
        var scene = new EvolutionScenePresentation(true, seed);
        for (int i = 0; i < 2000 && scene.Phase != phase; i++) scene.AdvanceFrames(1);
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
        Assert.AreEqual(0f, scene.BackgroundWhiteAmount); Assert.AreEqual(1f, scene.WhiteAmount);
        scene.AdvanceFrames(2); Assert.AreEqual(.125f, scene.BackgroundWhiteAmount);
        scene.AdvanceFrames(31); Assert.AreEqual(1f, scene.BackgroundWhiteAmount); Assert.AreEqual(1f, scene.WhiteAmount);
        scene.AdvanceFrames(18); Assert.AreEqual(.875f, scene.BackgroundWhiteAmount); Assert.AreEqual(1f, scene.WhiteAmount);
        scene.AdvanceFrames(1); Assert.AreEqual(.875f, scene.WhiteAmount);
        Assert.AreEqual(paletteFrame, scene.BackgroundFrame);
        scene.AdvanceFrames(130); Assert.AreEqual(EvolutionScenePhase.Reveal, scene.Phase);
        Assert.AreEqual(0f, scene.WhiteAmount); scene.AdvanceFrames(1);
        Assert.AreEqual(EvolutionScenePhase.Complete, scene.Phase);
    }
    [TestMethod]
    public void SourceSprayHasBurstSkippedFadeFrameAffineSizeAndBoundedLifetimes()
    {
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var scene = Reach(EvolutionScenePhase.Reveal);
        Assert.IsTrue(scene.Sparkles(sine).IsEmpty);
        scene.AdvanceFrames(1); var first = scene.Sparkles(sine);
        Assert.AreEqual(8, first.Length); Assert.IsTrue(first.All(p => p.X == 120 && p.Y == 57 && p.Scale == 1));
        scene.AdvanceFrames(31); Assert.AreEqual(39, scene.Sparkles(sine).Length);
        scene.AdvanceFrames(1); Assert.AreEqual(39, scene.Sparkles(sine).Length);
        scene.AdvanceFrames(17); Assert.AreEqual(56, scene.Sparkles(sine).Length);
        scene.AdvanceFrames(78); Assert.AreEqual(56, scene.Sparkles(sine).Length);
        Assert.IsTrue(scene.Sparkles(sine).Any(p => p.MatrixIndex == 0 && p.Subpriority == 1));
        scene.AdvanceFrames(1); Assert.AreEqual(48, scene.Sparkles(sine).Length);
        scene.AdvanceFrames(49); Assert.IsTrue(scene.Sparkles(sine).IsEmpty);
    }
    [TestMethod]
    public void ParticleSamplingAndChunkingDoNotConsumeOrAlterVisualRandomness()
    {
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var first = Reach(EvolutionScenePhase.Reveal, 42); var second = Reach(EvolutionScenePhase.Reveal, 42);
        first.AdvanceFrames(60);
        for (int i = 0; i < 60; i++) { second.AdvanceFrames(1); _ = second.Sparkles(sine); }
        CollectionAssert.AreEqual(first.Sparkles(sine).ToArray(), second.Sparkles(sine).ToArray());
        var different = Reach(EvolutionScenePhase.Reveal, 43); different.AdvanceFrames(60);
        Assert.IsFalse(first.Sparkles(sine).SequenceEqual(different.Sparkles(sine)));
        Assert.ThrowsExactly<ArgumentException>(() => first.Sparkles([]));
    }
}
