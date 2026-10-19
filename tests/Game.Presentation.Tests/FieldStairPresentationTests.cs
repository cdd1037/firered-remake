using Game.Application;
using Game.Domain.World;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldStairPresentationTests
{
    [TestMethod]
    public void EveryDirectionalStairPhaseOffsetAndPaletteBankMatchesExtractedSource()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(
            "tests/Game.Presentation.Tests/Fixtures/field-stair-source-vectors.json",
            "python tests/Reference/generate_field_stair_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-stair-source-vectors.json")));
        int traces=0,rows=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray())
        {
            traces++;int behavior=trace.GetProperty("behavior").GetInt32();
            string name=trace.GetProperty("name").GetString()!;
            Assert.IsTrue(name is "departureReady" or "departureDelayed" or "arrival");
            var departure=new FieldStairPresentation.Departure(behavior);
            var arrival=new FieldStairPresentation.Arrival(behavior);
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                rows++;var v=row.EnumerateArray().Select(value=>value.GetInt32()).ToArray();
                Assert.AreEqual(10,v.Length);int frame=v[0];
                if(frame>0)
                {
                    if(name=="arrival")arrival.Advance();
                    else departure.Advance(name=="departureReady"||frame>=65);
                }
                var expected=new FieldStairPresentation.PhaseSample(new(frame,v[1],v[2],v[3],v[4]!=0,v[5]!=0),
                    v[6],v[7],v[8],v[9]!=0);
                Assert.AreEqual(expected,name=="arrival"?arrival.Sample:departure.Sample,$"{behavior:x} {name} callback {frame}");
            }
            Assert.IsTrue(name=="arrival"?arrival.Complete:departure.Complete);
        }
        Assert.AreEqual(12,traces);Assert.AreEqual(516,rows);
    }

    [TestMethod]
    [DataRow(0x6c,0x6f)]
    [DataRow(0x6d,0x6e)]
    [DataRow(0x6e,0x6d)]
    [DataRow(0x6f,0x6c)]
    public void RecordedBoundariesReuseEveryLiveStairCallback(int from,int to)
    {
        foreach(int change in new[]{36,66})
        {
            var departure=new FieldStairPresentation.Departure(from);
            for(int frame=0;frame<change;frame++)
            {
                Assert.AreEqual(departure.Sample,FieldStairPresentation.SampleRecorded(from,to,change,frame));
                Assert.AreEqual(departure.Sample,FieldStairPresentation.SampleDeparture(from,change,frame));
                Assert.AreEqual(FieldDoorPresentation.FromPlain(departure.Sample.Phase),
                    FieldDoorPresentation.SampleRecorded(FieldTransitionCause.StairWarp,FieldTransitionArrival.Stair,change,frame,from,to));
                departure.Advance(frame+1>=change-1);
            }
            Assert.IsTrue(departure.Complete);
            var final=departure.Sample;departure.Advance(false);Assert.AreEqual(final,departure.Sample);
            var arrival=new FieldStairPresentation.Arrival(to);
            for(int frame=0;frame<=27;frame++)
            {
                Assert.AreEqual(arrival.Sample,FieldStairPresentation.SampleRecorded(from,to,change,change+frame));
                Assert.AreEqual(arrival.Sample,FieldStairPresentation.SampleArrival(to,frame));
                Assert.AreEqual(FieldDoorPresentation.FromPlain(arrival.Sample.Phase),
                    FieldDoorPresentation.SampleRecorded(FieldTransitionCause.StairWarp,FieldTransitionArrival.Stair,change,change+frame,from,to));
                arrival.Advance();
            }
            Assert.IsTrue(arrival.Complete);
        }
    }

    [TestMethod]
    public void DepartureDefersFadeTwelveDriftsAndContinuesMovingWhileMusicWaits()
    {
        var departure=new FieldStairPresentation.Departure(0x6d);
        Assert.AreEqual(new FieldStairPresentation.PhaseSample(new(0,1,0,0,false,false),0,0,0,false),departure.Sample);
        for(int frame=1;frame<=64;frame++)
        {
            departure.Advance(false);var sample=departure.Sample;
            Assert.IsTrue(sample.MusicFadeRequested);Assert.IsFalse(sample.Phase.Complete);
            Assert.AreEqual(frame is >=13 and <34,sample.Phase.PaletteActive,$"palette {frame}");
            Assert.AreEqual((-17*Math.Max(0,frame-1))>>5,sample.OffsetX,$"x {frame}");
            Assert.AreEqual((-10*Math.Max(0,frame-8))>>5,sample.OffsetY,$"y {frame}");
            Assert.AreEqual(frame<=1?2:frame<13?2:3,sample.Phase.TaskPhase);
        }
        departure.Advance(true);Assert.AreEqual(4,departure.Sample.Phase.TaskPhase);Assert.IsFalse(departure.Complete);
        var ready=departure.Sample;
        departure.Advance(false);Assert.IsTrue(departure.Complete);
        Assert.AreEqual(ready.OffsetX,departure.Sample.OffsetX);Assert.AreEqual(ready.OffsetY,departure.Sample.OffsetY);
        Assert.AreEqual(66,departure.Frames);
    }

    [TestMethod]
    [DataRow(0x6c,8,-5)]
    [DataRow(0x6d,-9,-5)]
    [DataRow(0x6e,8,1)]
    [DataRow(0x6f,-9,1)]
    public void ArrivalKeepsSignedOffsetsAndWaitsForWeatherAfterItsSixteenDrifts(int behavior,int firstX,int firstY)
    {
        var arrival=new FieldStairPresentation.Arrival(behavior);
        Assert.AreEqual(0,arrival.Sample.OffsetX);Assert.AreEqual(0,arrival.Sample.OffsetY);
        for(int frame=1;frame<=24;frame++)
        {
            arrival.Advance();var sample=arrival.Sample;
            if(frame==1){Assert.AreEqual(firstX,sample.OffsetX);Assert.AreEqual(firstY,sample.OffsetY);}
            if(frame>=17){Assert.AreEqual(0,sample.OffsetX);Assert.AreEqual(0,sample.OffsetY);}
            Assert.AreEqual(frame<=4?1:0,sample.WalkFrame);
            Assert.AreEqual(frame<18?1:2,sample.Phase.TaskPhase);
            Assert.AreEqual(frame<22,sample.Phase.PaletteActive);
            Assert.AreEqual(frame==24,sample.Phase.Complete);
        }
        var final=arrival.Sample;arrival.Advance();Assert.AreEqual(final,arrival.Sample);
    }

    [TestMethod]
    public void UnsupportedBehaviorsAndInvalidRecordedBoundsFailClosed()
    {
        foreach(int behavior in new[]{0,0x60,0x61,0x62,0x69,0x6b,0x70,256})
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldStairPresentation.Departure(behavior));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldStairPresentation.Arrival(behavior));
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldStairPresentation.SampleDeparture(0x6c,35,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldStairPresentation.SampleDeparture(0x6c,36,-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldStairPresentation.SampleArrival(0x6f,-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldStairPresentation.SampleRecorded(0x6c,0x6f,35,100));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldStairPresentation.SampleRecorded(0x6c,0x6f,36,-1));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldDoorPresentation.SampleRecorded(FieldTransitionCause.StairWarp,FieldTransitionArrival.Stair,36,0));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldDoorPresentation.SampleRecorded(FieldTransitionCause.StairWarp,FieldTransitionArrival.Stair,36,36,0x6c));
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void HostCadencePreservesDiscreteSourceBoundaries(int hz)
    {
        var departure=new FieldStairPresentation.Departure(0x6c);var arrival=new FieldStairPresentation.Arrival(0x6f);
        double elapsed=0;int processed=0;
        for(int host=0;host<hz;host++)
        {
            elapsed+=60d/hz;
            while(processed<(int)Math.Floor(elapsed+1e-7)){processed++;departure.Advance(true);arrival.Advance();}
        }
        Assert.IsTrue(departure.Complete);Assert.AreEqual(36,departure.Frames);
        Assert.IsTrue(arrival.Complete);Assert.AreEqual(24,arrival.Frames);
    }
}
