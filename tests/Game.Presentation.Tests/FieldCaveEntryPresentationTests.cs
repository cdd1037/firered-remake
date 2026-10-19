using Game.Application;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldCaveEntryPresentationTests
{
    private const string GenerationCommand="python tests/Reference/generate_field_cave_entry_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-cave-entry-source-vectors.json";

    [TestMethod]
    public void EveryGenericEnterCallbackPaletteAndPixelMatchesExtractedSource()
    {
        using var source=ReadSource();int traces=0,rows=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray().Take(2))
        {
            traces++;ushort[] palette=Colors(trace,"transitionPalette"),white=Colors(trace,"whitePalette"),black=Colors(trace,"blackPalette");
            ushort backdrop=trace.GetProperty("initialBackdrop").GetUInt16();
            var enter=new FieldCaveTransitionPresentation.Enter(palette,white,black,backdrop);
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                int frame=row.GetProperty("state")[0].GetInt32();
                Assert.AreEqual(rows%29,frame);rows++;
                if(frame!=0)enter.Advance();
                AssertCaveRow(row,enter.Sample);
                var replay=FieldCaveTransitionPresentation.SampleEnter(palette,white,black,frame,backdrop);
                Assert.AreEqual(enter.Sample with {Palette=replay.Palette},replay);
                CollectionAssert.AreEqual(enter.Sample.Palette.ToArray(),replay.Palette.ToArray());
            }
            Assert.IsTrue(enter.Complete);var terminal=enter.Sample;enter.Advance();Assert.AreEqual(terminal,enter.Sample);
            var clamped=FieldCaveTransitionPresentation.SampleEnter(palette,white,black,int.MaxValue,backdrop);
            Assert.AreEqual(terminal with {Palette=clamped.Palette},clamped);
        }
        Assert.AreEqual(2,traces);Assert.AreEqual(58,rows);
    }

    [TestMethod]
    public void EveryPreviewCallbackMatchesActualTransferGatedPaletteSchedulerAndEnterTail()
    {
        using var source=ReadSource();int traces=0,rows=0;
        int[] completions=[177,97,57,177,185,98];
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray().Skip(2))
        {
            var preview=new FieldCaveTransitionPresentation.CavePreview(Colors(trace,"transitionPalette"),
                Colors(trace,"whitePalette"),Colors(trace,"blackPalette"),trace.GetProperty("visited").GetBoolean());
            int graphicsFrom=trace.GetProperty("graphicsReadyFrom").GetInt32();
            int copyFrom=trace.GetProperty("windowCopyReadyFrom").GetInt32();
            int bFrom=trace.GetProperty("bHeldFrom").GetInt32(),bThrough=trace.GetProperty("bHeldThrough").GetInt32();
            int nextFrame=0,wrapperFrames=-1;
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                rows++;int[] expected=Ints(row,"state");int frame=expected[0];Assert.AreEqual(nextFrame++,frame);
                if(frame!=0)preview.Advance(frame>=graphicsFrom,frame>=copyFrom,frame>=bFrom&&frame<=bThrough);
                var sample=preview.Sample;
                int[] actual=[sample.Frames,(int)sample.Phase,sample.HoldFrames,sample.HoldDuration,
                    sample.NameWindowCreated?1:0,sample.BackgroundBlend,sample.ObjectBlend,sample.FadeColor,
                    sample.PaletteActive?1:0,sample.Cave?.Frames??0,sample.Complete?1:0];
                CollectionAssert.AreEqual(expected,actual,$"{trace.GetProperty("name").GetString()} callback {frame}");
                Assert.AreEqual(sample.Frames,preview.TotalFrames);Assert.AreEqual(sample.Cave,preview.EnterSample);
                if(sample.Cave is not null&&wrapperFrames<0)wrapperFrames=frame;
                Assert.AreEqual(wrapperFrames<0?frame:wrapperFrames,preview.PreviewFrames);
                Assert.AreEqual(preview.PreviewFrames,sample.PreviewFrames);
                Assert.AreEqual(frame!=0,sample.GraphicsRequested);
                Assert.AreEqual(expected[1] is >=1 and <=5,sample.PreviewVisible);
                if(row.TryGetProperty("cave",out var cave))AssertCaveRow(cave,sample.Cave!.Value);
            }
            Assert.IsTrue(preview.Complete);Assert.AreEqual(completions[traces],preview.Frames);
            Assert.AreEqual(preview.PreviewFrames+FieldCaveTransitionPresentation.PreviewEnterFrames,preview.Frames);
            var terminal=preview.Sample;preview.Advance(false,false,false);Assert.AreEqual(terminal,preview.Sample);traces++;
        }
        Assert.AreEqual(6,traces);Assert.AreEqual(797,rows);
    }

    [TestMethod]
    public void EnterPaletteExpansionHasEightOddLengthWritesBeforeSixteenToZeroAlpha()
    {
        using var source=ReadSource();int writes=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray().Take(2))
        {
            ushort[] transition=Colors(trace,"transitionPalette"),white=Colors(trace,"whitePalette"),black=Colors(trace,"blackPalette");
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                int frame=row.GetProperty("state")[0].GetInt32();var paletteWrites=row.GetProperty("writes").EnumerateArray().ToArray();
                Assert.AreEqual(frame==2?2:frame is >=3 and <=10||frame==28?1:0,paletteWrites.Length);
                for(int index=0;index<paletteWrites.Length;index++)
                {
                    writes++;bool backdropWrite=frame==28||frame==2&&index==1;
                    Assert.AreEqual(backdropWrite?0:224,paletteWrites[index].GetProperty("offset").GetInt32());
                    ushort[] expected=backdropWrite?black:frame==2?white:transition[(15-(frame-3)*2)..];
                    CollectionAssert.AreEqual(expected,Colors(paletteWrites[index],"colors"));
                }
                if(frame>=2)CollectionAssert.AreEqual(black,Colors(row,"palette0"));
            }
        }
        Assert.AreEqual(22,writes);
    }

    [TestMethod]
    [DataRow(false,120,150,177)]
    [DataRow(true,40,70,97)]
    public void PreviewWaitUsesStrictlyGreaterThanDuration(bool visited,int duration,int wrapper,int total)
    {
        var preview=Preview(visited);
        for(int frame=1;frame<=18+duration;frame++)preview.Advance(true,true,false);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.Hold,preview.Sample.Phase);
        Assert.AreEqual(duration,preview.Sample.HoldFrames);Assert.AreEqual(duration,preview.HoldDuration);
        preview.Advance(true,true,false);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.FadeToWhite,preview.Sample.Phase);
        Assert.AreEqual(duration+1,preview.Sample.HoldFrames);
        while(!preview.Complete)preview.Advance(true,true,false);
        Assert.AreEqual(wrapper,preview.PreviewFrames);Assert.AreEqual(total,preview.Frames);
    }

    [TestMethod]
    public void BAlreadyHeldSkipsOnFirstHoldCallbackButEarlyReleasedBIsNotLatched()
    {
        var held=Preview(false);var released=Preview(false);
        for(int frame=1;frame<=18;frame++)
        {held.Advance(true,true,true);released.Advance(true,true,true);}
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.Hold,held.Sample.Phase);
        held.Advance(true,true,true);released.Advance(true,true,false);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.FadeToWhite,held.Sample.Phase);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.Hold,released.Sample.Phase);
        while(!held.Complete)held.Advance(true,true,true);
        Assert.AreEqual(FieldCaveTransitionPresentation.MinimumPreviewFrames,held.PreviewFrames);
        Assert.AreEqual(57,held.Frames);Assert.AreEqual(1,held.Sample.HoldFrames);
    }

    [TestMethod]
    public void GraphicsAndWindowCopyReadinessAreIndependentUnboundedObservations()
    {
        var preview=Preview(false);
        for(int frame=0;frame<100;frame++)preview.Advance(false,true,true);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.WaitGraphics,preview.Sample.Phase);
        Assert.IsFalse(preview.Sample.NameWindowCreated);Assert.AreEqual(0,preview.Sample.HoldFrames);
        preview.Advance(true,false,true);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.WaitWindowCopy,preview.Sample.Phase);
        Assert.IsTrue(preview.Sample.NameWindowCreated);
        for(int frame=0;frame<100;frame++)preview.Advance(true,false,true);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.WaitWindowCopy,preview.Sample.Phase);
        Assert.IsFalse(preview.Sample.PaletteActive);
        preview.Advance(false,true,true);
        Assert.AreEqual(FieldCaveTransitionPresentation.CavePreviewPhase.FadeFromBlack,preview.Sample.Phase);
        while(!preview.Complete)preview.Advance(false,false,true);
        Assert.AreEqual(229,preview.PreviewFrames);Assert.AreEqual(256,preview.Frames);
    }

    [TestMethod]
    [DataRow(-1,true,18,13)]
    [DataRow(-1,false,18,13)]
    [DataRow(-2,true,14,9)]
    [DataRow(-2,false,14,9)]
    public void NegativeDelaysAccelerateBothBanksWithClampingAndFiveFinishingUpdates(int delay,bool fromBlack,int finished,int selectionEnded)
    {
        var palette=new FieldWarpPresentation.NormalPalette(fromBlack,delay);
        for(int frame=0;frame<=finished+2;frame++)
        {
            int bg=Math.Min(16,(frame/2)*(2-delay)),obj=Math.Min(16,(Math.Max(0,frame-1)/2)*(2-delay));
            Assert.AreEqual(fromBlack?16-bg:bg,palette.BackgroundBlend,$"BG {frame}");
            Assert.AreEqual(fromBlack?16-obj:obj,palette.ObjectBlend,$"OBJ {frame}");
            Assert.AreEqual(frame<finished,palette.Active,$"active {frame}");
            Assert.AreEqual(frame<selectionEnded,palette.HasSelectedPalettes,$"selected {frame}");
            palette.Advance();
        }
    }

    [TestMethod]
    public void EnterInputsAndPreviousSnapshotsRemainDetached()
    {
        ushort[] palette=Enumerable.Range(0,16).Select(v=>(ushort)(v*1057)).ToArray();
        ushort[] white=Enumerable.Repeat((ushort)0x7fff,16).ToArray(),black=Enumerable.Repeat((ushort)0x1234,16).ToArray();
        ushort[] original=palette.ToArray();var enter=new FieldCaveTransitionPresentation.Enter(palette,white,black);
        Array.Fill(palette,(ushort)0);Array.Fill(white,(ushort)0);Array.Fill(black,(ushort)0);
        enter.Advance();enter.Advance();var setup=enter.Sample;
        Assert.IsTrue(setup.Palette.All(color=>color==0x7fff));Assert.AreEqual((ushort)0x1234,setup.BackdropColor);
        Assert.ThrowsExactly<NotSupportedException>(()=>((IList<ushort>)setup.Palette)[0]=0);
        while(enter.Frames<10)enter.Advance();
        CollectionAssert.AreEqual(original[1..].Append((ushort)0x7fff).ToArray(),enter.Sample.Palette.ToArray());
        while(!enter.Complete)enter.Advance();
        Assert.IsTrue(setup.Palette.All(color=>color==0x7fff));Assert.AreEqual((ushort)0x1234,enter.Sample.BackdropColor);
    }

    [TestMethod]
    public void PreviewPixelsUseBothPaletteBanksTransparentNibblesAndSignedFadeRounding()
    {
        ushort[] palette=Enumerable.Range(0,32).Select(v=>(ushort)(v|((31-v)<<5)|((v/2)<<10))).ToArray();
        palette[0]=palette[16]=0x7fff;
        byte[] indices=Enumerable.Range(0,32).Select(v=>(byte)v).ToArray(),rgba=new byte[128];
        var sample=Preview(false).Sample;
        foreach(ushort target in new ushort[]{0,0x7fff,0x1234})
        for(int blend=0;blend<=16;blend++)
        {
            var faded=sample with {BackgroundBlend=blend,FadeColor=target};
            FieldCaveTransitionPresentation.CompositePreviewRgba(indices,palette,faded,rgba);
            for(int index=0;index<32;index++)
            {
                ushort original=(index&15)==0?(ushort)0:palette[index];
                for(int channel=0;channel<3;channel++)
                {
                    int from=(original>>(channel*5))&31,to=(target>>(channel*5))&31;
                    // Independent real-number floor, rather than repeating signed >>.
                    int value=from+(int)Math.Floor((to-from)*blend/16d);
                    Assert.AreEqual((byte)((value<<3)|(value>>2)),rgba[index*4+channel]);
                }
                Assert.AreEqual((byte)255,rgba[index*4+3]);
            }
        }
    }

    [TestMethod]
    public void PreviewPixelAdapterRejectsMalformedBanksIndicesAndFadeValues()
    {
        ushort[] palette=new ushort[32],invalid=new ushort[32];invalid[31]=0x8000;
        var sample=Preview(false).Sample;
        Assert.ThrowsExactly<ArgumentException>(()=>FieldCaveTransitionPresentation.CompositePreviewRgba(new byte[1],new ushort[16],sample,new byte[4]));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldCaveTransitionPresentation.CompositePreviewRgba(new byte[1],invalid,sample,new byte[4]));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldCaveTransitionPresentation.CompositePreviewRgba(new byte[1],palette,sample,new byte[3]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldCaveTransitionPresentation.CompositePreviewRgba(new byte[]{32},palette,sample,new byte[4]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldCaveTransitionPresentation.CompositePreviewRgba(new byte[1],palette,sample with {BackgroundBlend=17},new byte[4]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldCaveTransitionPresentation.CompositePreviewRgba(new byte[1],palette,sample with {FadeColor=0x8000},new byte[4]));
    }

    [TestMethod]
    public void InvalidEnterAndUnsupportedAcceleratedPaletteInputsFailClosed()
    {
        ushort[] palette=new ushort[16],invalid=new ushort[16];invalid[15]=0x8000;
        Assert.ThrowsExactly<ArgumentNullException>(()=>new FieldCaveTransitionPresentation.Enter(null!,palette,palette));
        Assert.ThrowsExactly<ArgumentNullException>(()=>new FieldCaveTransitionPresentation.Enter(palette,null!,palette));
        Assert.ThrowsExactly<ArgumentNullException>(()=>new FieldCaveTransitionPresentation.Enter(palette,palette,null!));
        Assert.ThrowsExactly<ArgumentException>(()=>new FieldCaveTransitionPresentation.Enter(palette,palette,[]));
        Assert.ThrowsExactly<ArgumentException>(()=>new FieldCaveTransitionPresentation.Enter(palette,palette,invalid));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldCaveTransitionPresentation.Enter(palette,palette,palette,0x8000));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldCaveTransitionPresentation.SampleEnter(palette,palette,palette,-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldWarpPresentation.NormalPalette(true,-3));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldWarpPresentation.NormalPalette(true,128));
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void HostCadenceRetainsPreviewAndEnterCallbacks(int hz)
    {
        var preview=Preview(true);double elapsed=0;int processed=0;
        for(int host=0;host<hz*2;host++)
        {
            elapsed+=60d/hz;
            while(processed<(int)Math.Floor(elapsed+1e-7)){processed++;preview.Advance(true,true,false);}
        }
        Assert.IsTrue(preview.Complete);Assert.AreEqual(70,preview.PreviewFrames);Assert.AreEqual(97,preview.Frames);
    }

    private static FieldCaveTransitionPresentation.CavePreview Preview(bool visited)=>new(new ushort[16],
        Enumerable.Repeat((ushort)0x7fff,16).ToArray(),new ushort[16],visited);
    private static JsonDocument ReadSource()=>JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(
        "tests/Game.Presentation.Tests/Fixtures/field-cave-entry-source-vectors.json",GenerationCommand)));
    private static ushort[] Colors(JsonElement element,string property)=>element.GetProperty(property).EnumerateArray().Select(v=>v.GetUInt16()).ToArray();
    private static int[] Ints(JsonElement element,string property)=>element.GetProperty(property).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static void AssertCaveRow(JsonElement row,FieldCaveTransitionPresentation.PhaseSample sample)
    {
        int[] state=[sample.Frames,sample.TaskPhase,sample.BackgroundEnabled?1:0,sample.BlendEnabled?1:0,
            sample.AlphaA,sample.AlphaB,sample.BackdropColor,sample.Complete?1:0];
        CollectionAssert.AreEqual(Ints(row,"state"),state,$"ENTER callback {sample.Frames}");
        CollectionAssert.AreEqual(Colors(row,"palette"),sample.Palette.ToArray(),$"ENTER palette {sample.Frames}");
        byte[] indices=Enumerable.Range(0,16).Select(v=>(byte)v).ToArray(),rgba=new byte[64];
        FieldCaveTransitionPresentation.CompositeRgba(indices,sample,rgba);
        CollectionAssert.AreEqual(row.GetProperty("rgba").EnumerateArray().Select(v=>v.GetByte()).ToArray(),rgba);
    }
}
