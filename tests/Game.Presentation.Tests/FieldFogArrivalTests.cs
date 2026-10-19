using Game.Application;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldFogArrivalTests
{
    private const string Fixture="tests/Game.Presentation.Tests/Fixtures/field-fog-source-vectors.json";
    private const string Generate="python tests/Reference/generate_field_fog_vectors.py --reference content/raw/pokefirered/037335f4 --output "+Fixture;

    [TestMethod]
    public void PlainFogArrivalMatchesEverySourcePhaseBankAndWeatherReadinessCallback()
    {
        var source=Trace("plainArrival");
        var arrival=new FieldWarpPresentation.PlainArrival(FieldArrivalPalette.FogHorizontal);
        Assert.AreEqual(19,arrival.Palette.BaseArrivalFrames);
        foreach(var row in source.Rows)
        {
            if(row.Frame>0)arrival.Advance();
            Assert.AreEqual(row.Phase,arrival.Sample,$"plain callback {row.Frame}");
            AssertPalette(row,arrival.Palette,"plain");
        }
        Assert.AreEqual(20,source.Rows.Length);Assert.IsTrue(arrival.Complete);
        var terminal=arrival.Sample;arrival.Advance();Assert.AreEqual(terminal,arrival.Sample);
    }

    [TestMethod]
    [DataRow(0x6c)]
    [DataRow(0x6d)]
    [DataRow(0x6e)]
    [DataRow(0x6f)]
    public void FogStairArrivalMatchesEverySourcePhaseOffsetBankAndWeatherGate(int behavior)
    {
        var source=Trace("stairArrival",behavior);
        var arrival=new FieldStairPresentation.Arrival(behavior,FieldArrivalPalette.FogHorizontal);
        Assert.AreEqual(19,FieldStairPresentation.ArrivalDuration(FieldArrivalPalette.FogHorizontal));
        foreach(var row in source.Rows)
        {
            if(row.Frame>0)arrival.Advance();
            var sample=arrival.Sample;string context=$"stair {behavior:x} callback {row.Frame}";
            Assert.AreEqual(row.Phase,sample.Phase,context);
            Assert.AreEqual(row.OffsetX,sample.OffsetX,context+" X");
            Assert.AreEqual(row.OffsetY,sample.OffsetY,context+" Y");
            AssertPalette(row,arrival.Palette,context);
            Assert.AreEqual(sample,FieldStairPresentation.SampleArrival(behavior,row.Frame,FieldArrivalPalette.FogHorizontal),context+" sampled");
            // This fog oracle extracts the stair offsets, but stubs held-walk
            // animation. Its rows intentionally make no fresh-gait claim.
        }
        Assert.AreEqual(20,source.Rows.Length);Assert.IsTrue(arrival.Complete);
        var terminal=arrival.Sample;arrival.Advance();Assert.AreEqual(terminal,arrival.Sample);
    }

    [TestMethod]
    [DataRow("directFogFade")]
    [DataRow("repeatFogFade")]
    public void InitializedAndRepeatedFogFadeFromBlackMatchEverySourceCoefficient(string name)
    {
        var source=Trace(name);
        var palette=new FieldArrivalPalettePresentation(FieldArrivalPalette.FogHorizontal);
        // These source traces begin after fresh InitAll. Consume the one
        // initialization callback before setting their explicit fade boundary.
        palette.Advance();palette.FadeFromBlack();
        foreach(var row in source.Rows)
        {
            if(row.Frame>0)
            {
                if(name=="repeatFogFade"&&row.Frame==8)palette.FadeFromBlack();
                palette.Advance();
            }
            AssertPalette(row,palette,name);
        }
        Assert.AreEqual(name=="directFogFade"?19:26,source.Rows.Length);
        Assert.IsTrue(palette.WeatherReady);Assert.IsFalse(palette.Active);
        Assert.AreEqual(0,palette.BackgroundBlend);Assert.AreEqual(0,palette.ObjectBlend);
    }

    [TestMethod]
    [DataRow("plainDeparture",24)]
    [DataRow("plainDepartureDelayed",66)]
    public void FogDepartureUsesTheSourceSplitBanksAndActualMusicReadiness(string name,int duration)
    {
        var source=Trace(name);var departure=new FieldWarpPresentation.Departure();
        foreach(var row in source.Rows)
        {
            if(row.Frame>0)departure.Advance(name=="plainDeparture"||row.Frame>=65);
            Assert.AreEqual(row.Phase,departure.Sample,$"{name} callback {row.Frame}");
            Assert.AreEqual(1,row.PaletteCopies,"Source fog departure captures its already weather-treated palette once.");
        }
        Assert.AreEqual(duration+1,source.Rows.Length);Assert.IsTrue(departure.Complete);
        var terminal=departure.Sample;departure.Advance(false);Assert.AreEqual(terminal,departure.Sample);
    }

    [TestMethod]
    [DataRow(0x6c)]
    [DataRow(0x6d)]
    [DataRow(0x6e)]
    [DataRow(0x6f)]
    public void FogStairDeparturePreservesEverySourceBankAndSignedOffset(int behavior)
    {
        var source=Trace("stairDeparture",behavior);var departure=new FieldStairPresentation.Departure(behavior);
        foreach(var row in source.Rows)
        {
            if(row.Frame>0)departure.Advance(true);
            var sample=departure.Sample;string context=$"departure {behavior:x} callback {row.Frame}";
            Assert.AreEqual(row.Phase,sample.Phase,context);
            Assert.AreEqual(row.OffsetX,sample.OffsetX,context+" X");
            Assert.AreEqual(row.OffsetY,sample.OffsetY,context+" Y");
            Assert.AreEqual(row.Frame<13?0:1,row.PaletteCopies,context+" source palette copy");
        }
        Assert.AreEqual(37,source.Rows.Length);Assert.IsTrue(departure.Complete);
        var terminal=departure.Sample;departure.Advance(false);Assert.AreEqual(terminal,departure.Sample);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void CallerCallbackBatchesPreserveEveryFogRowWithoutAWallOrGameplayClock(int hz)
    {
        var plainRows=Trace("plainArrival").Rows;var stairRows=Trace("stairArrival",0x6c).Rows;
        var plain=new FieldWarpPresentation.PlainArrival(FieldArrivalPalette.FogHorizontal);
        var stair=new FieldStairPresentation.Arrival(0x6c,FieldArrivalPalette.FogHorizontal);
        Assert.AreEqual(plainRows[0].Phase,plain.Sample);Assert.AreEqual(stairRows[0].Phase,stair.Sample.Phase);
        double callbacks=0;int processed=0,checkedRows=1;
        for(int host=0;host<hz;host++)
        {
            callbacks+=60d/hz;
            while(processed<(int)Math.Floor(callbacks+1e-7))
            {
                processed++;plain.Advance();stair.Advance();int row=Math.Min(processed,19);
                Assert.AreEqual(plainRows[row].Phase,plain.Sample,$"{hz}Hz plain callback {processed}");
                Assert.AreEqual(stairRows[row].Phase,stair.Sample.Phase,$"{hz}Hz stair callback {processed}");
                Assert.AreEqual(stairRows[row].OffsetX,stair.Sample.OffsetX);
                Assert.AreEqual(stairRows[row].OffsetY,stair.Sample.OffsetY);checkedRows++;
            }
        }
        Assert.AreEqual(60,processed);Assert.AreEqual(61,checkedRows);
        Assert.AreEqual(19,plain.Frames);Assert.AreEqual(19,stair.Frames);
    }

    [TestMethod]
    public void ReadingOrReplayingSnapshotsDoesNotAdvanceTheLivePresentation()
    {
        var plain=new FieldWarpPresentation.PlainArrival(FieldArrivalPalette.FogHorizontal);
        var stair=new FieldStairPresentation.Arrival(0x6f,FieldArrivalPalette.FogHorizontal);
        var plainBefore=plain.Sample;var stairBefore=stair.Sample;
        for(int read=0;read<1000;read++)
        {
            _=FieldStairPresentation.SampleArrival(0x6f,read,FieldArrivalPalette.FogHorizontal);
            Assert.AreEqual(plainBefore,plain.Sample);Assert.AreEqual(stairBefore,stair.Sample);
        }
        Assert.AreEqual(0,plain.Frames);Assert.AreEqual(0,stair.Frames);
        Assert.IsFalse(plain.Palette.WeatherReady);Assert.IsFalse(stair.Palette.WeatherReady);
        plain.Advance();stair.Advance();
        Assert.AreEqual(1,plain.Frames);Assert.AreEqual(1,stair.Frames);
        Assert.AreEqual(16,plain.Palette.BackgroundBlend);Assert.AreEqual(16,stair.Palette.BackgroundBlend);
    }

    private static void AssertPalette(SourceRow row,FieldArrivalPalettePresentation actual,string context)
    {
        Assert.AreEqual(FieldArrivalPalette.FogHorizontal,actual.Kind);
        Assert.AreEqual(row.BackgroundBlend,actual.BackgroundBlend,$"{context}/{row.Frame} BG");
        Assert.AreEqual(row.ObjectBlend,actual.ObjectBlend,$"{context}/{row.Frame} OBJ");
        Assert.AreEqual(row.PaletteActive,actual.Active,$"{context}/{row.Frame} palette active");
        Assert.AreEqual(row.WeatherReady,actual.WeatherReady,$"{context}/{row.Frame} weather ready");
    }

    private static SourceTrace Trace(string name,int behavior=0)=>ReadSource().Single(t=>t.Name==name&&t.Behavior==behavior);

    private static SourceTrace[] ReadSource()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        Assert.AreEqual("037335f4c725d7c9aecdac87066f2002b4bd7e14",source.RootElement.GetProperty("sourceCommit").GetString());
        string[] columns=source.RootElement.GetProperty("columns").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        string[] expected=["frame","mainCalls","initStep","gfxLoaded","scrollOffset","scrollCounter","scrollPosX","eva","evb",
            "blendFrameCounter","blendUpdateCounter","fadeCounter","palState","fadeInActive","backgroundBlend","objectBlend",
            "paletteActive","taskPhase","complete","offsetX","offsetY","fadeCalls","paletteCopies","targetEva","targetEvb","cameraX","cameraY"];
        CollectionAssert.AreEqual(expected,columns);
        var traces=new List<SourceTrace>();int totalRows=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray())
        {
            string name=trace.GetProperty("name").GetString()!;int behavior=trace.GetProperty("behavior").GetInt32();
            var rows=new List<SourceRow>();
            foreach(var value in trace.GetProperty("frames").EnumerateArray())
            {
                int[] row=value.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(expected.Length,row.Length,name);
                Assert.IsTrue(row[16] is 0 or 1);Assert.IsTrue(row[18] is 0 or 1);
                rows.Add(new(row[0],row[17],row[14],row[15],row[16]!=0,row[18]!=0,row[19],row[20],row[12]!=1,row[22]));
            }
            Assert.IsGreaterThan(0,rows.Count,name);totalRows+=rows.Count;
            traces.Add(new(name,behavior,rows.ToArray()));
        }
        Assert.AreEqual(19,traces.Count);Assert.AreEqual(19,traces.Select(t=>(t.Name,t.Behavior)).Distinct().Count());
        Assert.AreEqual(627,totalRows);
        return traces.ToArray();
    }

    private sealed record SourceTrace(string Name,int Behavior,SourceRow[] Rows);
    private readonly record struct SourceRow(int Frame,int TaskPhase,int BackgroundBlend,int ObjectBlend,
        bool PaletteActive,bool Complete,int OffsetX,int OffsetY,bool WeatherReady,int PaletteCopies)
    {
        public FieldWarpPresentation.PhaseSample Phase=>new(Frame,TaskPhase,BackgroundBlend,ObjectBlend,PaletteActive,Complete);
    }
}
