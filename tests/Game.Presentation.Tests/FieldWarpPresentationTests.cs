using Game.Application;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldWarpPresentationTests
{
    [TestMethod]
    [DataRow(24)]
    [DataRow(66)]
    public void RecordedBoundaryReplaysEveryLivePhaseWithoutConsultingAudio(int switchFrame)
    {
        var departure=new FieldWarpPresentation.Departure();
        for(int frame=0;frame<switchFrame;frame++)
        {
            Assert.AreEqual(departure.Sample,FieldWarpPresentation.SampleRecorded(switchFrame,frame));
            departure.Advance(frame+1>=switchFrame-1);
        }
        Assert.IsTrue(departure.Complete);
        var arrival=new FieldWarpPresentation.PlainArrival();
        for(int frame=0;frame<=FieldWarpPresentation.PlainArrivalFrames+3;frame++)
        {
            Assert.AreEqual(arrival.Sample,FieldWarpPresentation.SampleRecorded(switchFrame,switchFrame+frame));
            arrival.Advance();
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldWarpPresentation.SampleRecorded(23,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldWarpPresentation.SampleRecorded(24,-1));
    }

    [TestMethod]
    public void EveryPhaseAndBothPaletteBanksMatchExtractedSourceCallbacks()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(
            "tests/Game.Presentation.Tests/Fixtures/field-warp-source-vectors.json",
            "python tests/Reference/generate_field_warp_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-warp-source-vectors.json")));
        int traces=0,rows=0;
        foreach(var trace in document.RootElement.GetProperty("traces").EnumerateArray())
        {
            traces++;string name=trace.GetProperty("name").GetString()!;
            Assert.IsTrue(name is "departureReady" or "departureDelayed" or "plainArrival");
            var departure=new FieldWarpPresentation.Departure();
            var arrival=new FieldWarpPresentation.PlainArrival();
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                rows++;var values=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                Assert.AreEqual(6,values.Length);
                int frame=values[0];
                if(frame>0)
                {
                    if(name=="plainArrival")arrival.Advance();
                    else departure.Advance(name=="departureReady"||frame>64);
                }
                var actual=name=="plainArrival"?arrival.Sample:departure.Sample;
                var expected=new FieldWarpPresentation.PhaseSample(frame,values[1],values[2],values[3],values[4]!=0,values[5]!=0);
                Assert.AreEqual(expected,actual,$"{name} callback {frame}");
            }
            Assert.IsTrue(name=="plainArrival"?arrival.Complete:departure.Complete);
        }
        Assert.AreEqual(3,traces);Assert.AreEqual(117,rows);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NormalPalettePreservesSplitBanksAndFiveFinishingCallbacks(bool fromBlack)
    {
        var palette=new FieldWarpPresentation.NormalPalette(fromBlack);
        for(int frame=0;frame<=24;frame++)
        {
            int background=Math.Min(16,frame/2*2),objects=Math.Min(16,Math.Max(0,frame-1)/2*2);
            Assert.AreEqual(fromBlack?16-background:background,palette.BackgroundBlend,$"BG callback {frame}");
            Assert.AreEqual(fromBlack?16-objects:objects,palette.ObjectBlend,$"OBJ callback {frame}");
            Assert.AreEqual(frame<22,palette.Active,$"active callback {frame}");
            palette.Advance();
        }
    }

    [TestMethod]
    public void ReadyMusicDepartureWaitsForPaletteThenTheSeparateMapLoadCallback()
    {
        var departure=new FieldWarpPresentation.Departure();
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(0,1,0,0,true,false),departure.Sample);
        for(int frame=1;frame<=24;frame++)
        {
            departure.Advance(true);
            Assert.AreEqual(frame,departure.Frames);
            Assert.AreEqual(frame<23?1:2,departure.Phase);
            Assert.AreEqual(frame<22,departure.Palette.Active);
            Assert.AreEqual(frame==24,departure.Complete);
        }
        var final=departure.Sample;departure.Advance(false);Assert.AreEqual(final,departure.Sample);
    }

    [TestMethod]
    public void DelayedMusicCannotBeBypassedByACompletedPalette()
    {
        var departure=new FieldWarpPresentation.Departure();
        for(int frame=1;frame<=64;frame++)departure.Advance(false);
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(64,1,16,16,false,false),departure.Sample);
        departure.Advance(true);
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(65,2,16,16,false,false),departure.Sample);
        departure.Advance(true);
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(66,2,16,16,false,true),departure.Sample);
    }

    [TestMethod]
    public void MapLoadCallbackDoesNotRestartTheCompletedReadinessGate()
    {
        var departure=new FieldWarpPresentation.Departure();
        for(int frame=1;frame<=23;frame++)departure.Advance(true);
        Assert.AreEqual(2,departure.Phase);Assert.IsFalse(departure.Complete);
        // Source Task_Teleport2Warp case2 loads the map unconditionally. A later
        // change to the caller's observation does not send it back to case1.
        departure.Advance(false);
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(24,2,16,16,false,true),departure.Sample);
    }

    [TestMethod]
    public void PlainArrivalUnlocksOnlyAfterWeatherObservesPaletteCompletion()
    {
        var arrival=new FieldWarpPresentation.PlainArrival();
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(0,0,16,16,true,false),arrival.Sample);
        for(int frame=1;frame<=24;frame++)
        {
            arrival.Advance();
            Assert.AreEqual(frame,arrival.Frames);Assert.AreEqual(1,arrival.Phase);
            Assert.AreEqual(frame<22,arrival.Palette.Active);
            Assert.AreEqual(frame==24,arrival.Complete,$"arrival callback {frame}");
        }
        var final=arrival.Sample;arrival.Advance();Assert.AreEqual(final,arrival.Sample);
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void HostCadenceDoesNotChangeDiscreteDepartureOrArrival(int hz)
    {
        var departure=new FieldWarpPresentation.Departure();
        var arrival=new FieldWarpPresentation.PlainArrival();
        double elapsed=0;int processed=0;
        for(int hostFrame=0;hostFrame<hz;hostFrame++)
        {
            elapsed+=60d/hz;
            while(processed<(int)Math.Floor(elapsed+1e-7))
            {
                processed++;departure.Advance(true);arrival.Advance();
            }
        }
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(24,2,16,16,false,true),departure.Sample);
        Assert.AreEqual(new FieldWarpPresentation.PhaseSample(24,1,0,0,false,true),arrival.Sample);
    }
}
