using Game.Application;
using Game.Presentation.BattleAnimations;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldForestPreviewPresentationTests
{
    private const string Fixture="tests/Game.Presentation.Tests/Fixtures/field-forest-preview-source-vectors.json";
    private const string Generate="python tests/Reference/generate_field_forest_preview_vectors.py --reference content/raw/pokefirered/037335f4 --output "+Fixture;

    [TestMethod]
    public void AllSourceForestArrivalWeatherPaletteAndCleanupCallbacksMatch()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        string[] expectedNames=["normalFirst","normalVisited","shadeFirst","shadeVisited",
            "normalDelayedCopy","normalFinishingRefill","normalRestart","shadeRestart","shadeCleanupWait",
            "readyFirst","readyVisited","delayedReadiness","heldButton","plainNormal","plainShade"];
        var names=new List<string>();int rows=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray())
        {
            string name=trace.GetProperty("name").GetString()!;names.Add(name);
            int duration=trace.GetProperty("duration").GetInt32();
            bool plain=trace.GetProperty("plain").GetInt32()!=0;
            bool external=trace.GetProperty("externalWeather").GetInt32()!=0;
            var kind=trace.GetProperty("shade").GetInt32()!=0?FieldArrivalPalette.Shade:FieldArrivalPalette.Normal;
            var arrival=plain?null:new FieldForestPreviewPresentation.ForestArrival(duration,kind);
            var plainArrival=plain?new FieldWarpPresentation.PlainArrival(kind):null;
            var task=external?new FieldForestPreviewPresentation.ForestPreview(duration):null;
            int frame=0;
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                rows++;
                Assert.AreEqual(frame,row.GetProperty("frame").GetInt32(),name);
                int[] input=Numbers(row,"input");Assert.AreEqual(5,input.Length);
                if(frame>0)
                {
                    if(plain)plainArrival!.Advance();
                    else if(external)task!.AdvanceTask(input[0]!=0,input[1]!=0,input[2]!=0,input[3]!=0);
                    else arrival!.Advance(input[0]!=0,input[1]!=0,input[3]!=0);
                }
                if(!plain)
                {
                    var sample=external?task!.Sample:arrival!.Preview.Sample;
                    CollectionAssert.AreEqual(Numbers(row,"forest"),ForestValues(sample),$"{name} forest callback {frame}");
                    Assert.AreEqual(!sample.Complete,row.GetProperty("controlsLocked").GetInt32()!=0,$"{name} controls callback {frame}");
                    Assert.AreEqual(sample.Complete,row.GetProperty("restored").GetInt32()!=0,$"{name} registers callback {frame}");
                }
                if(!external)
                {
                    var sample=plain?plainArrival!.Sample:arrival!.Sample;
                    int[] actual=[sample.Frames,sample.TaskPhase,sample.BackgroundBlend,sample.ObjectBlend,
                        sample.PaletteActive?1:0,sample.Complete?1:0];
                    CollectionAssert.AreEqual(Numbers(row,"arrival"),actual,$"{name} arrival callback {frame}");
                    var palette=plain?plainArrival!.Palette:arrival!.Palette;
                    Assert.AreEqual(row.GetProperty("weatherReady").GetInt32()!=0,palette.WeatherReady,
                        $"{name} weather callback {frame}");
                }
                frame++;
            }
            Assert.IsTrue(plain?plainArrival!.Complete:external?task!.Complete:arrival!.Complete,name);
            if(arrival is not null&&!external)
            {
                Assert.AreEqual(arrival.Frames-arrival.BaseArrivalFrames,arrival.ExtraWaitFrames);
                var terminal=arrival.Sample;var overlay=arrival.Preview.Sample;
                arrival.Advance(false,false,false);
                Assert.AreEqual(terminal,arrival.Sample);Assert.AreEqual(overlay,arrival.Preview.Sample);
            }
        }
        CollectionAssert.AreEqual(expectedNames,names);
        Assert.AreEqual(1773,rows);
    }

    [TestMethod]
    [DataRow(40,FieldArrivalPalette.Normal,113,89)]
    [DataRow(120,FieldArrivalPalette.Normal,193,169)]
    [DataRow(40,FieldArrivalPalette.Shade,107,89)]
    [DataRow(120,FieldArrivalPalette.Shade,187,169)]
    public void ArrivalWaitIncludesFullHoldAlternatingAlphaAndSeparateCleanup(
        int duration,FieldArrivalPalette kind,int total,int extra)
    {
        var arrival=new FieldForestPreviewPresentation.ForestArrival(duration,kind);
        Assert.ThrowsExactly<InvalidOperationException>(()=>_=arrival.ExtraWaitFrames);
        int holds=0,blends=0,cleanups=0,musicRequests=0,copyRequests=0,fadeRequests=0;
        for(int frame=1;frame<=total;frame++)
        {
            int phase=arrival.Preview.Sample.TaskPhase;
            if(phase==3)holds++;
            if(phase==4)blends++;
            if(phase==5)cleanups++;
            arrival.Advance();
            var sample=arrival.Preview.Sample;
            if(sample.MusicStartRequested)musicRequests++;
            if(sample.WindowCopyRequested)copyRequests++;
            if(sample.FadeFromBlackRequested)fadeRequests++;
            Assert.AreEqual(frame==total,arrival.Complete,$"callback {frame}");
            // Priority0 destroys the forest before priority10 checks its gate.
            Assert.AreEqual(arrival.Preview.Complete,arrival.Complete,$"priority callback {frame}");
        }
        Assert.AreEqual(duration+1,holds);Assert.AreEqual(47,blends);Assert.AreEqual(1,cleanups);
        Assert.AreEqual(1,musicRequests);Assert.AreEqual(1,copyRequests);Assert.AreEqual(1,fadeRequests);
        Assert.AreEqual(extra,arrival.ExtraWaitFrames);
    }

    [TestMethod]
    public void ReadinessMustPassBothSetupGatesWeatherAndFinalCopy()
    {
        var task=new FieldForestPreviewPresentation.ForestPreview(40);
        for(int frame=0;frame<5;frame++)task.AdvanceTask(false,true,true,true);
        Assert.AreEqual(0,task.Sample.TaskPhase);
        task.AdvanceTask(true,false,true,true);Assert.AreEqual(0,task.Sample.TaskPhase);
        task.AdvanceTask(true,true,false,false);
        Assert.AreEqual(1,task.Sample.TaskPhase);Assert.IsTrue(task.Sample.WindowCopyRequested);
        task.AdvanceTask(false,false,true,true);
        Assert.AreEqual(1,task.Sample.TaskPhase);Assert.IsFalse(task.Sample.FadeFromBlackRequested);
        task.AdvanceTask(false,true,false,false);
        Assert.AreEqual(2,task.Sample.TaskPhase);Assert.IsTrue(task.Sample.FadeFromBlackRequested);
        for(int frame=0;frame<5;frame++)task.AdvanceTask(false,false,false,false);
        Assert.AreEqual(2,task.Sample.TaskPhase);Assert.AreEqual(0,task.Sample.Counter);
        task.AdvanceTask(false,false,true,false);Assert.IsTrue(task.Sample.MusicStartRequested);
        for(int frame=0;frame<41+47;frame++)task.AdvanceTask(false,false,false,false);
        Assert.AreEqual(5,task.Sample.TaskPhase);Assert.IsTrue(task.Sample.CleanupCopyRequested);
        Assert.IsFalse(task.Sample.PreviewVisible);Assert.IsFalse(task.Complete);
        for(int frame=0;frame<5;frame++)task.AdvanceTask(false,false,false,false);
        Assert.IsFalse(task.Complete);Assert.IsFalse(task.Sample.CleanupCopyRequested);
        task.AdvanceTask(false,false,false,true);Assert.IsTrue(task.Complete);
        var terminal=task.Sample;task.AdvanceTask(true,true,true,true);Assert.AreEqual(terminal,task.Sample);
    }

    [TestMethod]
    public void BlendRaisesBackgroundThenLowersPreviewThenWaitsAndEndsOnCallback47()
    {
        var task=new FieldForestPreviewPresentation.ForestPreview(40);
        for(int i=0;i<44;i++)task.AdvanceTask(true,true,true,true);
        Assert.AreEqual(4,task.Sample.TaskPhase);
        for(int frame=1;frame<=47;frame++)
        {
            task.AdvanceTask(true,true,true,true);
            Assert.AreEqual(16-(frame+1)/3,task.Sample.AlphaA,$"EVA callback {frame}");
            Assert.AreEqual((frame+2)/3,task.Sample.AlphaB,$"EVB callback {frame}");
            Assert.AreEqual(frame%3,task.Sample.Counter);
            Assert.AreEqual(frame==47?5:4,task.Sample.TaskPhase);
        }
        Assert.AreEqual(0,task.Sample.AlphaA);Assert.AreEqual(16,task.Sample.AlphaB);
    }

    [TestMethod]
    public void HeldBSourceTraceCannotShortenTheForestHold()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        var traces=source.RootElement.GetProperty("traces").EnumerateArray().ToArray();
        var held=traces.Single(t=>t.GetProperty("name").GetString()=="heldButton").GetProperty("frames").EnumerateArray().ToArray();
        var ordinary=traces.Single(t=>t.GetProperty("name").GetString()=="readyVisited").GetProperty("frames").EnumerateArray().ToArray();
        Assert.AreEqual(93,held.Length);Assert.AreEqual(ordinary.Length,held.Length);
        for(int frame=0;frame<held.Length;frame++)
        {
            Assert.AreEqual(1,Numbers(held[frame],"input")[4]);
            Assert.AreEqual(0,Numbers(ordinary[frame],"input")[4]);
            CollectionAssert.AreEqual(Numbers(ordinary[frame],"forest"),Numbers(held[frame],"forest"),$"callback {frame}");
        }
    }

    [TestMethod]
    public void RepeatedNormalFadeDoesNotRestartAnActiveCursorButAlwaysFillsBothBanks()
    {
        var palette=new FieldArrivalPalettePresentation();
        for(int frame=0;frame<10;frame++)palette.Advance();
        Assert.AreEqual(6,palette.BackgroundBlend);Assert.AreEqual(8,palette.ObjectBlend);
        palette.FadeFromBlack();
        Assert.AreEqual(16,palette.BackgroundBlend);Assert.AreEqual(16,palette.ObjectBlend);
        palette.Advance();
        Assert.AreEqual(16,palette.BackgroundBlend);Assert.AreEqual(6,palette.ObjectBlend);
        for(int frame=0;frame<11;frame++)palette.Advance();
        Assert.IsFalse(palette.Active);Assert.IsFalse(palette.WeatherReady);
        palette.Advance();Assert.IsTrue(palette.WeatherReady);
        palette.FadeFromBlack();Assert.IsTrue(palette.Active);Assert.IsFalse(palette.WeatherReady);
        Assert.AreEqual(16,palette.BackgroundBlend);Assert.AreEqual(16,palette.ObjectBlend);
    }

    [TestMethod]
    public void RepeatedShadeFadeUsesExistingWeatherTaskAndBothBanksTogether()
    {
        var palette=new FieldArrivalPalettePresentation(FieldArrivalPalette.Shade);
        palette.Advance();Assert.AreEqual(16,palette.BackgroundBlend);
        palette.Advance();Assert.AreEqual(15,palette.BackgroundBlend);
        for(int frame=0;frame<15;frame++)palette.Advance();
        Assert.IsTrue(palette.WeatherReady);Assert.AreEqual(0,palette.ObjectBlend);Assert.IsFalse(palette.Active);
        palette.FadeFromBlack();Assert.IsFalse(palette.WeatherReady);
        Assert.AreEqual(16,palette.BackgroundBlend);Assert.AreEqual(16,palette.ObjectBlend);
        palette.Advance();
        Assert.AreEqual(15,palette.BackgroundBlend);Assert.AreEqual(15,palette.ObjectBlend);
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void HostCadencePreservesEverySourceCallback(int hz)
    {
        var arrival=new FieldForestPreviewPresentation.ForestArrival(120,FieldArrivalPalette.Shade);
        double elapsed=0;int processed=0;
        for(int hostFrame=0;hostFrame<hz*4;hostFrame++)
        {
            elapsed+=60d/hz;
            while(processed<(int)Math.Floor(elapsed+1e-7)){processed++;arrival.Advance();}
        }
        Assert.IsTrue(arrival.Complete);Assert.AreEqual(187,arrival.Frames);Assert.AreEqual(169,arrival.ExtraWaitFrames);
    }

    [TestMethod]
    public void CompositePreservesTransparentIndicesAndUsesSaturatedFiveBitAddition()
    {
        ushort[] palette=new ushort[32];Array.Fill(palette,(ushort)0x7fff);
        byte[] indices=[0,1,16,17];ushort[] field=[0x1234,0x1234,0x4321,0x4321];
        ushort[] output=new ushort[4];
        FieldForestPreviewPresentation.CompositeRgb555(indices,palette,field,16,16,output);
        for(int i=0;i<4;i++)Assert.AreEqual((ushort)((i%2)==0?field[i]:0x7fff),output[i]);
        FieldForestPreviewPresentation.CompositeRgb555(indices,palette,field,0,16,output);
        CollectionAssert.AreEqual(field,output);
        int[] factors=[-1,0,1,8,15,16,17,31];
        foreach(int foreground in Enumerable.Range(0,32))
        foreach(int background in Enumerable.Range(0,32))
        foreach(int alphaA in factors)
        foreach(int alphaB in factors)
        {
            ushort first=(ushort)(foreground|((31-foreground)<<5)|((foreground/2)<<10));
            ushort second=(ushort)(background|((background/2)<<5)|((31-background)<<10));
            ushort actual=FieldForestPreviewPresentation.AlphaBlendRgb555(first,second,alphaA,alphaB);
            // Existing shared battle compositor carries an independent opacity bit.
            Assert.AreEqual((ushort)(SourcePaletteMath.AlphaBlend(first,second,alphaA,alphaB)&0x7fff),actual);
            for(int shift=0;shift<=10;shift+=5)
            {
                int expected=Math.Min(31,(((first>>shift)&31)*Math.Clamp(alphaA,0,16)+
                    ((second>>shift)&31)*Math.Clamp(alphaB,0,16))/16);
                Assert.AreEqual(expected,(actual>>shift)&31);
            }
        }
    }

    [TestMethod]
    public void ShadeChannelsAndUnshadedAlternateBanksMatchSourceGammaTables()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        var tables=source.RootElement.GetProperty("shadeGamma");
        int[] normal=Numbers(tables,"normal"),alternate=Numbers(tables,"alternate");
        Assert.AreEqual(32,normal.Length);Assert.AreEqual(32,alternate.Length);
        byte[] pixels=new byte[32*8],expected=new byte[32*8];
        for(int channel=0;channel<32;channel++)
        {
            Assert.AreEqual(normal[channel],FieldShadePresentation.Channel(channel));
            Assert.AreEqual(channel,alternate[channel]);
            int offset=channel*8;
            for(int component=0;component<3;component++)
            {
                pixels[offset+component]=(byte)((channel<<3)|(channel>>2));
                expected[offset+component]=(byte)((normal[channel]<<3)|(normal[channel]>>2));
                // Hidden pixel bytes are preserved, even when nonzero.
                pixels[offset+4+component]=expected[offset+4+component]=(byte)(255-channel);
            }
            pixels[offset+3]=expected[offset+3]=255;
        }
        FieldShadePresentation.TransformRgba(pixels);CollectionAssert.AreEqual(expected,pixels);
        int[] banks=Numbers(tables,"paletteTypes");Assert.AreEqual(32,banks.Length);
        for(int bank=13;bank<=15;bank++)Assert.AreEqual(1,banks[bank],$"reserved BG bank {bank}");
        Assert.AreEqual(2,banks[16]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldShadePresentation.Channel(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldShadePresentation.Channel(32));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldShadePresentation.TransformRgba(new byte[3]));
    }

    [TestMethod]
    public void UnsupportedDurationsWeatherAndPixelDataFailExplicitly()
    {
        foreach(int duration in new[]{-1,0,39,41,119,121})
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldForestPreviewPresentation.ForestPreview(duration));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldArrivalPalettePresentation((FieldArrivalPalette)99));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldForestPreviewPresentation.AlphaBlendRgb555(0x8000,0,16,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldForestPreviewPresentation.AlphaBlendRgb555(0,0xffff,16,0));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldForestPreviewPresentation.CompositeRgb555([0],new ushort[16],[0],16,0,new ushort[1]));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldForestPreviewPresentation.CompositeRgb555([0],new ushort[32],[],16,0,new ushort[1]));
        ushort[] invalidPalette=new ushort[32];invalidPalette[31]=0x8000;
        Assert.ThrowsExactly<ArgumentException>(()=>FieldForestPreviewPresentation.CompositeRgb555([0],invalidPalette,[0],16,0,new ushort[1]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldForestPreviewPresentation.CompositeRgb555([32],new ushort[32],[0],16,0,new ushort[1]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldForestPreviewPresentation.CompositeRgb555([0],new ushort[32],[0x8000],16,0,new ushort[1]));
        var normal=new FieldWarpPresentation.NormalPalette(true);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>normal.FillBanks(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>normal.FillBanks(17));
    }

    private static int[] Numbers(JsonElement value,string property)=>value.GetProperty(property).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static int[] ForestValues(FieldForestPreviewPresentation.PhaseSample sample)=>
        [sample.Frames,sample.TaskPhase,sample.Counter,sample.HoldDuration,sample.AlphaA,sample.AlphaB,
        sample.WindowCopyRequested?1:0,sample.FadeFromBlackRequested?1:0,sample.MusicStartRequested?1:0,
        sample.CleanupCopyRequested?1:0,sample.Complete?1:0];
}
