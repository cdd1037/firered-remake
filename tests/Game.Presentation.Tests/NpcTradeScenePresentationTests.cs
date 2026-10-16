using Game.Content;
namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class NpcTradeScenePresentationTests
{
    [TestMethod]
    public void EveryPhaseIsReachableAndFinalAcknowledgementCannotOccurAtOldSixSecondLimit()
    {
        foreach(var phase in Enum.GetValues<NpcTradeScenePhase>())
        {
            var frame=NpcTradeSceneTimeline.StartFrame(phase);
            Assert.AreEqual(phase,NpcTradeSceneTimeline.At(frame).Phase);
            Assert.AreEqual(0,NpcTradeSceneTimeline.At(frame).Frame);
            if(frame>0)Assert.AreNotEqual(phase,NpcTradeSceneTimeline.At(frame-1).Phase);
        }
        Assert.IsTrue(NpcTradeSceneTimeline.ReadyFrame>360);
        Assert.AreNotEqual(NpcTradeScenePhase.Ready,NpcTradeSceneTimeline.At(360).Phase);
        Assert.AreEqual(NpcTradeScenePhase.Ready,NpcTradeSceneTimeline.At(int.MaxValue).Phase);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>NpcTradeSceneTimeline.At(-1));
    }
    [TestMethod]
    public void SourceMessageOrderDoesNotLeakTextIntoWirelessTransfer()
    {
        Assert.AreEqual(-1,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.SlideIn));
        Assert.AreEqual(0,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.SendMessage));
        Assert.AreEqual(1,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.DepartBounce));
        Assert.AreEqual(-1,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.SendSignal));
        Assert.AreEqual(-1,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.CrossingPortraits));
        Assert.AreEqual(2,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.Release));
        Assert.AreEqual(3,NpcTradeSceneTimeline.TextStage(NpcTradeScenePhase.Ready));
    }
    [TestMethod]
    public void AudioAnchorsRemainOrderedAndFanfareFollowsIncomingCryGate()
    {
        var cues=NpcTradeSceneTimeline.SoundCues;
        Assert.IsTrue(cues.Count>20);
        Assert.IsTrue(cues.Select(c=>c.Frame).SequenceEqual(cues.Select(c=>c.Frame).Order()));
        Assert.IsTrue(cues.All(c=>c.Frame>=0 && c.Frame<NpcTradeSceneTimeline.ReadyFrame));
        Assert.AreEqual((0,"MUS_EVOLUTION"),cues[0]);
        Assert.AreEqual(1,cues.Count(c=>c.Symbol=="MUS_EVOLVED"));
        Assert.AreEqual(NpcTradeSceneTimeline.StartFrame(NpcTradeScenePhase.ReceivedMessage)+10,
            cues.Single(c=>c.Symbol=="MUS_EVOLVED").Frame);
        Assert.AreEqual(4,cues.Count(c=>c.Symbol=="SE_M_HEAL_BELL"));
    }
    [TestMethod]
    public void BallCallbacksUseExactSignedSourceVelocityAndBoundTheirIndexes()
    {
        // The production table comes from generated content. Build a length-valid deterministic
        // vector here to isolate callback indexing from the importer's source parity check.
        int[] v=Enumerable.Range(0,108).Select(i=>i<22?2:i<44?-2:0).ToArray();
        Assert.AreEqual(32,NpcTradeSceneTimeline.DepartBallY(0,v));
        Assert.AreEqual(76,NpcTradeSceneTimeline.DepartBallY(22,v));
        Assert.AreEqual(32,NpcTradeSceneTimeline.DepartBallY(44,v));
        Assert.AreEqual(32,NpcTradeSceneTimeline.DepartBallY(64,v));
        Assert.AreEqual(-8,NpcTradeSceneTimeline.ArriveBallY(0,v));
        Assert.AreEqual(76,NpcTradeSceneTimeline.ArriveBallY(21,v));
        Assert.AreEqual(74,NpcTradeSceneTimeline.ArriveBallY(22,v));
        _=NpcTradeSceneTimeline.DepartBallY(int.MaxValue,v);
        _=NpcTradeSceneTimeline.ArriveBallY(int.MaxValue,v);
        Assert.ThrowsExactly<ArgumentException>(()=>NpcTradeSceneTimeline.DepartBallY(0,[]));
    }
}
