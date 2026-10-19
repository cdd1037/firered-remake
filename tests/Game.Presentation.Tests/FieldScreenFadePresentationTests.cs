using Game.Application;
using Game.Presentation.BattleAnimations;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldScreenFadePresentationTests
{
    private const string Fixture="field-inplace-white-fade-source-vectors.json";
    private const string Generate="python tests/Reference/generate_field_screen_fade_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/"+Fixture;

    [TestMethod]
    public void BothDirectionsMatchEverySourceEndFrameAndSeparateNativeFromBytecodeReadiness()
    {
        int compared=0;
        foreach(var trace in Read())
        {
            int departure=trace.Mark("toWhiteFrame"),arrival=trace.Mark("fromWhiteFrame"),message=trace.Mark("messageFrame");
            var outgoing=new FieldScreenFadePresentation(false,FieldArrivalPalette.FogHorizontal);
            for(int frame=departure;frame<arrival;frame++)
            {
                if(frame>departure)outgoing.Advance();
                AssertFade(outgoing,trace.At(frame,3),false,frame-departure,trace.Name);compared++;
            }
            Assert.IsFalse(outgoing.ScriptReady);
            Assert.AreEqual(1,trace.At(arrival-1,1)["mode"],"The source native wait has switched mode, but no next bytecode ran.");
            Assert.AreEqual(-1,trace.At(arrival-1,1)["healFrame"]);
            outgoing.Advance();Assert.IsTrue(outgoing.ScriptReady);Assert.AreEqual(23,outgoing.Frames);
            Assert.AreEqual(arrival,trace.At(arrival,1)["healFrame"]);

            var incoming=new FieldScreenFadePresentation(true,FieldArrivalPalette.FogHorizontal);
            for(int frame=arrival;frame<=trace.LastFrame;frame++)
            {
                if(frame>arrival)incoming.Advance();
                AssertFade(incoming,trace.At(frame,3),frame>=message,frame-arrival,trace.Name);compared++;
            }
            Assert.AreEqual(1,trace.At(arrival+1,1)["mode"]);
            Assert.AreEqual(-1,trace.At(arrival+1,1)["messageFrame"]);
            Assert.AreEqual(message,trace.At(message,1)["messageFrame"]);
            Assert.AreEqual(14,trace.At(message,1)["backgroundBlend"],"Message admission precedes this frame's weather update.");
            Assert.AreEqual(13,trace.At(message,3)["backgroundBlend"]);
            Assert.AreEqual(0,trace.At(message,3)["weatherReady"]);
            Assert.AreEqual(0,trace.At(arrival+15,3)["backgroundBlend"]);
            Assert.AreEqual(0,trace.At(arrival+15,3)["weatherReady"]);
            Assert.AreEqual(1,trace.At(arrival+16,3)["weatherReady"]);
        }
        Assert.AreEqual(164,compared);
        Assert.AreEqual(23,FieldScreenFadePresentation.ScriptWaitFrames(false,FieldArrivalPalette.FogHorizontal));
        Assert.AreEqual(2,FieldScreenFadePresentation.ScriptWaitFrames(true,FieldArrivalPalette.FogHorizontal));
    }

    [TestMethod]
    public void LockallWaitsForTaskThenNativeThenBytecodeWithoutRecreatingTheExistingFog()
    {
        int phaseRows=0;
        foreach(var trace in Read())
        {
            int start=trace.Mark("toWhiteFrame"),resume=trace.Mark("fromWhiteFrame");
            var timeline=new FieldScreenFadePresentation.Timeline(false,FieldArrivalPalette.FogHorizontal,trace.MovementFrames,true);
            Assert.AreEqual(start,timeline.DelayFrames);
            long initialElapsed=trace.InitialElapsed;
            var fog=new FieldHorizontalFogPresentation.Scene(0,0).Advance(initialElapsed+1,0,0);
            var retained=fog;
            for(int frame=0;frame<=trace.LastFrame;frame++)
            {
                if(frame>0)timeline.Advance(1);
                fog=fog.Advance(1,0,0);var row=trace.At(frame,3);
                Assert.AreEqual(frame>=start,timeline.Fade is not null,$"{trace.Name}/{frame} lockall gate");
                Assert.AreEqual(frame>=resume,timeline.ScriptReady,$"{trace.Name}/{frame} script gate");
                if(frame>=start&&frame<resume)AssertFade(timeline.Fade!,row,false,frame-start,trace.Name);
                Assert.AreEqual(row["scrollOffset"],fog.Sample.ScrollOffset);
                Assert.AreEqual(row["scrollCounter"],fog.Sample.ScrollCounter);
                Assert.AreEqual(row["scrollX"],fog.Sample.PositionX);
                Assert.AreEqual(initialElapsed+frame+1,fog.SourceElapsedFrames);
                Assert.AreEqual(initialElapsed,retained.SourceElapsedFrames);
                Assert.AreEqual(FieldHorizontalFogPresentation.Eva,row["eva"]);
                Assert.AreEqual(FieldHorizontalFogPresentation.Evb,row["evb"]);
            }
            int taskClear=trace.MovementFrames==0?0:trace.MovementFrames+1;
            Assert.AreEqual(1,trace.At(taskClear,1)["lockTaskActive"]);
            Assert.AreEqual(0,trace.At(taskClear,2)["lockTaskActive"]);
            Assert.AreEqual(1,trace.At(taskClear+1,1)["mode"]);
            Assert.AreEqual(1,trace.At(taskClear+1,1)["stopPlayerCalls"]);
            Assert.AreEqual(taskClear+2,start);
            foreach(var row in trace.Rows)
            {
                Assert.AreEqual(0,row["scriptEnableCalls"]);
                Assert.AreEqual(0,row["contextStatus"],"This prefix remains RUNNING, including native waits.");
                Assert.AreEqual(1,row["fogCreates"]);Assert.AreEqual(0,row["fogDestroys"]);
                Assert.AreEqual(2,row["initStep"]);phaseRows++;
            }
        }
        Assert.AreEqual(708,phaseRows);
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void FractionalAndBatchedHostFramesPreserveSourceLockFadeAndMessageGates(int hz)
    {
        foreach(var trace in Read())
        {
            int start=trace.Mark("toWhiteFrame"),resume=trace.Mark("fromWhiteFrame"),message=trace.Mark("messageFrame");
            var outgoing=new FieldScreenFadePresentation.Timeline(false,FieldArrivalPalette.FogHorizontal,trace.MovementFrames,true);
            var incoming=new FieldScreenFadePresentation.Timeline(true,FieldArrivalPalette.FogHorizontal,0,false);
            double elapsed=0;
            for(int host=0;host<hz;host++)
            {
                double delta=60d/hz;elapsed+=delta;outgoing.Advance(delta);incoming.Advance(delta);
                int frame=(int)Math.Floor(elapsed+1e-7);
                Assert.AreEqual(frame,outgoing.Frames);Assert.AreEqual(frame,incoming.Frames);
                Assert.AreEqual(frame>=start,outgoing.Fade is not null);
                Assert.AreEqual(frame>=resume,outgoing.ScriptReady);
                Assert.AreEqual(frame>=message-resume,incoming.ScriptReady);
                if(frame>=start)
                {
                    var row=trace.At(Math.Min(frame,resume-1),3);
                    Assert.AreEqual(row["backgroundBlend"],outgoing.Fade!.BackgroundBlend);
                    Assert.AreEqual(row["objectBlend"],outgoing.Fade.ObjectBlend);
                    Assert.AreEqual(row["paletteActive"]!=0,outgoing.Fade.PaletteActive);
                }
                var incomingRow=trace.At(Math.Min(resume+frame,trace.LastFrame),3);
                Assert.AreEqual(incomingRow["backgroundBlend"],incoming.Fade!.BackgroundBlend);
                Assert.AreEqual(incomingRow["objectBlend"],incoming.Fade.ObjectBlend);
                Assert.AreEqual(incomingRow["weatherReady"]!=0,incoming.Fade.VisualComplete);
            }
            Assert.AreEqual(60,outgoing.Frames);Assert.AreEqual(60,incoming.Frames);
        }
    }

    [TestMethod]
    public void SourceCopiedBaselineAndEveryRgb555ProbeUseSharedFiveBitPaletteMath()
    {
        int checkedRows=0;
        foreach(var trace in Read())
        {
            int start=trace.Mark("toWhiteFrame"),arrival=trace.Mark("fromWhiteFrame");
            var before=trace.At(start,0);var copied=trace.At(start,1);
            string[] sampleNames=["bgSample","objectSample","lightenedObjectSample"];
            string[] baselineNames=["unfadedBgSample","unfadedObjectSample","unfadedLightenedObjectSample"];
            for(int index=0;index<3;index++)Assert.AreEqual(before[sampleNames[index]],copied[baselineNames[index]],"TO_WHITE copies current weather-faded colors.");
            Assert.AreNotEqual(before["unfadedLightenedObjectSample"],copied["unfadedLightenedObjectSample"],"The marked-bank probe distinguishes the original unfaded palette from the captured weather baseline.");
            foreach(var row in trace.Rows.Where(r=>r["frame"]>start||r["frame"]==start&&r["phase"]>=1))
            {
                bool weatherBlend=row["frame"]>arrival||row["frame"]==arrival&&row["phase"]>=2;
                for(int index=0;index<3;index++)
                {
                    ushort baseline=(ushort)copied[baselineNames[index]];
                    Assert.AreEqual((int)baseline,row[baselineNames[index]]);
                    // Actual source marks this synthetic bank. This verifies its arithmetic,
                    // not unsupported runtime sprite-palette membership or marked-slot rendering.
                    if(index==2&&weatherBlend)baseline=SourcePaletteMath.Blend(baseline,12,(ushort)(28|(31<<5)|(28<<10)));
                    int coefficient=row[index==0?"backgroundBlend":"objectBlend"];
                    Assert.AreEqual((ushort)row[sampleNames[index]],SourcePaletteMath.Blend(baseline,coefficient,0x7fff),$"{trace.Name}/{row["frame"]}/{row["phase"]} RGB probe {index}");
                }
                Assert.AreEqual(1,row["baselineCopies"]);checkedRows++;
            }
            Assert.AreEqual(copied["unfadedBgSample"],trace.At(arrival+15,3)["bgSample"]);
            Assert.AreEqual(copied["unfadedObjectSample"],trace.At(arrival+15,3)["objectSample"]);
            Assert.AreNotEqual(copied["unfadedLightenedObjectSample"],trace.At(arrival+15,3)["lightenedObjectSample"],"Source marked-bank FROM_WHITE lightening is reapplied to the copied baseline.");
        }
        Assert.AreEqual(652,checkedRows);
    }

    private static void AssertFade(FieldScreenFadePresentation fade,Row row,bool ready,int frames,string context)
    {
        Assert.AreEqual(frames,fade.Frames,context+" frame");
        Assert.AreEqual(row["backgroundBlend"],fade.BackgroundBlend,context+" BG");
        Assert.AreEqual(row["objectBlend"],fade.ObjectBlend,context+" OBJ");
        Assert.AreEqual(row["paletteActive"]!=0,fade.PaletteActive,context+" normal palette active");
        Assert.AreEqual(ready,fade.ScriptReady,context+" next bytecode ready");
        Assert.AreEqual(fade.FromWhite?row["weatherReady"]!=0:row["paletteActive"]==0,fade.VisualComplete,context+" source terminal gate");
    }

    private sealed record Row(Dictionary<string,int> Values)
    {
        public int this[string name]=>Values[name];
    }
    private sealed record Trace(string Name,int MovementFrames,int[] InitialFog,Row[] Rows)
    {
        public int LastFrame=>Rows[^1]["frame"];
        public int Mark(string name)=>Rows[^1][name];
        public Row At(int frame,int phase)=>Rows.Single(r=>r["frame"]==frame&&r["phase"]==phase);
        public long InitialElapsed=>(InitialFog[1]*4L+InitialFog[2]-93+262144)%262144;
    }
    private static Trace[] Read()
    {
        using var source=SourceVectorFixture.Read(Fixture,Generate);
        Assert.AreEqual("037335f4c725d7c9aecdac87066f2002b4bd7e14",source.RootElement.GetProperty("sourceCommit").GetString());
        CollectionAssert.AreEqual(new[]{"O0","O2"},source.RootElement.GetProperty("optimizationParity").EnumerateArray().Select(v=>v.GetString()!).ToArray());
        string[] columns=source.RootElement.GetProperty("columns").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        Assert.AreEqual(40,columns.Length);Assert.AreEqual(columns.Length,columns.Distinct().Count());
        var traces=source.RootElement.GetProperty("traces").EnumerateArray().Select(t=>new Trace(
            t.GetProperty("name").GetString()!,t.GetProperty("movementStopsAfterSpriteFrame").GetInt32(),
            t.GetProperty("initialFog").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),
            t.GetProperty("rows").EnumerateArray().Select(r=>
            {
                int[] values=r.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(columns.Length,values.Length);
                return new Row(columns.Zip(values).ToDictionary(v=>v.First,v=>v.Second));
            }).ToArray())).ToArray();
        Assert.AreEqual(4,traces.Length);Assert.AreEqual(4,traces.Select(t=>t.Name).Distinct().Count());
        Assert.AreEqual(708,traces.Sum(t=>t.Rows.Length));
        return traces;
    }
}
