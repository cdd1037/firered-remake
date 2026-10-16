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
        Assert.AreEqual(15,stalled.PhaseFrame);Assert.IsTrue(stalled.Cancel());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>sixty.Advance(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>sixty.Advance(-1));
    }
}
