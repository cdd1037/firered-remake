using Game.Application;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldCaveTransitionPresentationTests
{
    private const string GenerationCommand="python tests/Reference/generate_field_cave_exit_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-cave-exit-source-vectors.json";

    [TestMethod]
    public void EveryExitCallbackPaletteByteAndCompositePixelMatchesExtractedSource()
    {
        using var source=ReadSource();int traces=0,rows=0;
        byte[] indices=Enumerable.Range(0,16).Select(index=>(byte)index).ToArray();
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray())
        {
            traces++;ushort[] palette=Colors(trace,"transitionPalette"),white=Colors(trace,"whitePalette");
            ushort backdrop=trace.GetProperty("initialBackdrop").GetUInt16();
            var exit=new FieldCaveTransitionPresentation.Exit(palette,white,backdrop);
            int expectedFrame=0;
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                rows++;int[] state=row.GetProperty("state").EnumerateArray().Select(value=>value.GetInt32()).ToArray();
                Assert.AreEqual(8,state.Length);Assert.AreEqual(expectedFrame++,state[0]);
                if(state[0]>0)exit.Advance();
                var sample=exit.Sample;
                int[] actual=[sample.Frames,sample.TaskPhase,sample.BackgroundEnabled?1:0,sample.BlendEnabled?1:0,
                    sample.AlphaA,sample.AlphaB,sample.BackdropColor,sample.Complete?1:0];
                CollectionAssert.AreEqual(state,actual,$"state callback{state[0]}");
                CollectionAssert.AreEqual(Colors(row,"palette"),sample.Palette.ToArray(),$"palette callback{state[0]}");
                byte[] rgba=new byte[64];FieldCaveTransitionPresentation.CompositeRgba(indices,sample,rgba);
                CollectionAssert.AreEqual(row.GetProperty("rgba").EnumerateArray().Select(value=>value.GetByte()).ToArray(),
                    rgba,$"composite callback{state[0]}");
                AssertSampleEqual(sample,FieldCaveTransitionPresentation.SampleExit(palette,white,state[0],backdrop));
            }
            Assert.AreEqual(39,expectedFrame);Assert.IsTrue(exit.Complete);
            var terminal=exit.Sample;exit.Advance();Assert.AreEqual(terminal,exit.Sample);
            AssertSampleEqual(terminal,FieldCaveTransitionPresentation.SampleExit(palette,white,int.MaxValue,backdrop));
        }
        Assert.AreEqual(2,traces);Assert.AreEqual(78,rows);
    }

    [TestMethod]
    public void OriginalPaletteWritesPreserveSlot14AndOverwriteSlotZeroOnlyAt29()
    {
        using var source=ReadSource();int traces=0,writes=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray())
        {
            traces++;ushort[] palette=Colors(trace,"transitionPalette"),white=Colors(trace,"whitePalette");
            foreach(var row in trace.GetProperty("frames").EnumerateArray())
            {
                int frame=row.GetProperty("state")[0].GetInt32();
                var actual=row.GetProperty("writes").EnumerateArray().ToArray();
                Assert.AreEqual(frame==2?2:frame is >=21 and <=29?1:0,actual.Length,$"writes callback{frame}");
                for(int index=0;index<actual.Length;index++)
                {
                    writes++;Assert.AreEqual(frame==29?0:224,actual[index].GetProperty("offset").GetInt32());
                    ushort[] expected=frame==29||frame==2&&index==0?white:palette[(frame==2?8:frame-13)..];
                    CollectionAssert.AreEqual(expected,Colors(actual[index],"colors"),$"bytes callback{frame}, write{index}");
                }
                ushort[] palette0=Colors(row,"palette0");Assert.AreEqual(16,palette0.Length);
                if(frame>=29)CollectionAssert.AreEqual(white,palette0);
                else
                {
                    Assert.AreEqual(trace.GetProperty("initialBackdrop").GetUInt16(),palette0[0]);
                    Assert.IsTrue(palette0.Skip(1).All(color=>color==0));
                }
            }
        }
        Assert.AreEqual(2,traces);Assert.AreEqual(22,writes);
    }

    [TestMethod]
    public void BoundaryFramesRetainAlpha17AndEightFullWaitCallbacks()
    {
        ushort[] palette=Enumerable.Range(0,16).Select(value=>(ushort)(value*1057)).ToArray();
        ushort[] white=Enumerable.Repeat((ushort)0x7fff,16).ToArray();
        var exit=new FieldCaveTransitionPresentation.Exit(palette,white);
        for(int frame=0;frame<=38;frame++)
        {
            if(frame!=0)exit.Advance();var sample=exit.Sample;
            Assert.AreEqual(frame,sample.Frames);
            Assert.AreEqual(frame==0?0:frame==1?1:frame<20?2:frame<29?3:4,sample.TaskPhase);
            Assert.AreEqual(frame>=2,sample.BackgroundEnabled);Assert.AreEqual(frame>=2,sample.BlendEnabled);
            Assert.AreEqual(frame<3?0:frame<=20?frame-3:16,sample.AlphaA);
            Assert.AreEqual(frame<3?0:16,sample.AlphaB);
            Assert.AreEqual((ushort)(frame<29?0:0x7fff),sample.BackdropColor);
            Assert.AreEqual(frame==38,sample.Complete);
        }
    }

    [TestMethod]
    public void InputsAndReturnedPaletteSnapshotsCannotChangeLaterFrames()
    {
        ushort[] palette=Enumerable.Range(0,16).Select(value=>(ushort)(value*1057)).ToArray();
        ushort[] white=Enumerable.Repeat((ushort)0x7fff,16).ToArray();
        ushort[] originalPalette=palette.ToArray(),originalWhite=white.ToArray();
        var exit=new FieldCaveTransitionPresentation.Exit(palette,white);
        Array.Fill(palette,(ushort)0);Array.Fill(white,(ushort)0);
        exit.Advance();exit.Advance();var setup=exit.Sample;
        CollectionAssert.AreEqual(originalPalette[8..].Concat(originalWhite[8..]).ToArray(),setup.Palette.ToArray());
        ushort[] setupColors=setup.Palette.ToArray();
        Assert.ThrowsExactly<NotSupportedException>(()=>((IList<ushort>)setup.Palette)[1]=0);
        while(!exit.Complete)exit.Advance();
        CollectionAssert.AreEqual(setupColors,setup.Palette.ToArray());
        Assert.AreEqual((ushort)0x7fff,exit.Sample.BackdropColor);
        Assert.IsTrue(exit.Sample.Palette.Take(8).All(value=>value==originalPalette[15]));
    }

    [TestMethod]
    public void TransparentIndexAndHardwareAlphaClampUseBackdropWithIntegerSaturation()
    {
        ushort[] palette=new ushort[16];palette[0]=0x7fff;
        var bank=Array.AsReadOnly(palette);byte[] rgba=new byte[8];byte[] indices=[0,1];
        int[] factors=[0,1,8,15,16,17,31];
        foreach(int foreground in Enumerable.Range(0,32))
        foreach(int background in Enumerable.Range(0,32))
        foreach(int alphaA in factors)
        foreach(int alphaB in factors)
        {
            palette[1]=(ushort)(foreground|((31-foreground)<<5)|((foreground/2)<<10));
            ushort backdrop=(ushort)(background|((background/2)<<5)|((31-background)<<10));
            var sample=new FieldCaveTransitionPresentation.PhaseSample(0,3,true,true,alphaA,alphaB,backdrop,false,bank);
            FieldCaveTransitionPresentation.CompositeRgba(indices,sample,rgba);
            for(int channel=0;channel<3;channel++)
            {
                int bg=(backdrop>>(channel*5))&31,fg=(palette[1]>>(channel*5))&31;
                Assert.AreEqual(Expand(bg),rgba[channel]);
                int mixed=Math.Min(31,(fg*Math.Min(alphaA,16)+bg*Math.Min(alphaB,16))/16);
                Assert.AreEqual(Expand(mixed),rgba[4+channel]);
            }
            Assert.AreEqual((byte)255,rgba[3]);Assert.AreEqual((byte)255,rgba[7]);
        }
        var disabled=new FieldCaveTransitionPresentation.PhaseSample(0,0,false,true,16,16,0x1234,false,bank);
        FieldCaveTransitionPresentation.CompositeRgba(indices,disabled,rgba);
        CollectionAssert.AreEqual(rgba[..4],rgba[4..]);
        var opaque=disabled with {BackgroundEnabled=true,BlendEnabled=false};
        FieldCaveTransitionPresentation.CompositeRgba(indices,opaque,rgba);
        Assert.AreEqual(Expand(palette[1]&31),rgba[4]);
    }

    [TestMethod]
    public void InvalidPaletteIndicesAndRecordedBoundsFailClosed()
    {
        ushort[] palette=new ushort[16],invalid=new ushort[16];invalid[15]=0x8000;
        Assert.ThrowsExactly<ArgumentNullException>(()=>new FieldCaveTransitionPresentation.Exit(null!,palette));
        Assert.ThrowsExactly<ArgumentNullException>(()=>new FieldCaveTransitionPresentation.Exit(palette,null!));
        Assert.ThrowsExactly<ArgumentException>(()=>new FieldCaveTransitionPresentation.Exit([],palette));
        Assert.ThrowsExactly<ArgumentException>(()=>new FieldCaveTransitionPresentation.Exit(palette,new ushort[17]));
        Assert.ThrowsExactly<ArgumentException>(()=>new FieldCaveTransitionPresentation.Exit(invalid,palette));
        Assert.ThrowsExactly<ArgumentException>(()=>new FieldCaveTransitionPresentation.Exit(palette,invalid));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new FieldCaveTransitionPresentation.Exit(palette,palette,0x8000));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldCaveTransitionPresentation.SampleExit(palette,palette,-1));
        var sample=new FieldCaveTransitionPresentation.Exit(palette,palette).Sample;
        Assert.ThrowsExactly<ArgumentException>(()=>FieldCaveTransitionPresentation.CompositeRgba(new byte[2],sample,new byte[7]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldCaveTransitionPresentation.CompositeRgba(new byte[]{16},sample,new byte[4]));
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(120)]
    public void HostCadenceRetainsThe38SourceCallbacks(int hz)
    {
        var exit=new FieldCaveTransitionPresentation.Exit(new ushort[16],Enumerable.Repeat((ushort)0x7fff,16).ToArray());
        double elapsed=0;int processed=0;
        for(int host=0;host<hz;host++)
        {
            elapsed+=60d/hz;
            while(processed<(int)Math.Floor(elapsed+1e-7)){processed++;exit.Advance();}
        }
        Assert.IsTrue(exit.Complete);Assert.AreEqual(38,exit.Frames);
    }

    private static JsonDocument ReadSource()=>JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(
        "tests/Game.Presentation.Tests/Fixtures/field-cave-exit-source-vectors.json",GenerationCommand)));
    private static ushort[] Colors(JsonElement element,string property)=>element.GetProperty(property).EnumerateArray().Select(value=>value.GetUInt16()).ToArray();
    private static byte Expand(int value)=>(byte)((value<<3)|(value>>2));

    private static void AssertSampleEqual(FieldCaveTransitionPresentation.PhaseSample expected,FieldCaveTransitionPresentation.PhaseSample actual)
    {
        Assert.AreEqual(expected with {Palette=actual.Palette},actual);
        CollectionAssert.AreEqual(expected.Palette.ToArray(),actual.Palette.ToArray());
    }
}
