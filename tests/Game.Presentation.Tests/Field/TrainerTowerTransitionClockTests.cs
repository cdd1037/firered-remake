using Game.Application;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class TrainerTowerTransitionClockTests
{
    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void ArrivalKeepsRunningTimerFramesUntilTheOrdinaryCommitBoundary(int rate)
    {
        var clock=new TrainerTowerFrameAccumulator();
        var departure=new FieldWarpPresentation.Departure();
        var arrival=new FieldDoorPresentation.AnimatedArrival();
        int duration=FieldWarpPresentation.MinimumDepartureFrames+FieldDoorPresentation.AnimatedArrivalFrames;
        double elapsed=0;
        int presented=0;
        while(presented<duration)
        {
            double delta=Math.Min(1.0/rate,duration/60.0-elapsed);
            clock.Advance(delta,true);
            elapsed+=delta;
            int next=(int)Math.Floor(elapsed*60+1e-7);
            for(;presented<next;presented++)
                if(!departure.Complete)departure.Advance(true);else arrival.Advance();
            // No Take/command is issued while the detached transition owns input.
        }
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(arrival.Complete);
        Assert.AreEqual(duration,clock.Take(false));
        Assert.AreEqual(0,clock.Take(true));
        clock.Advance(1.0/60,true);
        Assert.AreEqual(1,clock.Take(true));
    }

    [TestMethod]
    public void StoppedTimerAndExcludedSaveTimeDoNotBecomeWarpElapsedTime()
    {
        var clock=new TrainerTowerFrameAccumulator();
        clock.Advance(2,false);
        Assert.AreEqual(0,clock.Take(true));
        clock.ExcludeSaveWrite(.5);
        clock.Advance(.75,true);
        Assert.AreEqual(15,clock.Take(true));
    }
}
