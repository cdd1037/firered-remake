using Game.Application;
using Game.Presentation;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldHorizontalFogPresentationTests
{
    private const string Fixture="tests/Game.Presentation.Tests/Fixtures/field-fog-source-vectors.json";
    private const string Generate="python tests/Reference/generate_field_fog_vectors.py --reference content/raw/pokefirered/037335f4 --output "+Fixture;

    [TestMethod]
    public void SourceInitializationSteadyScrollAndU16WrapMatchEveryOracleSample()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        string[] names=["freshInitAll","repeatInitAll","stableScroll","u16ScrollWrap","signedCameraScroll"];
        var seen=new List<string>();int samples=0;
        foreach(var trace in source.RootElement.GetProperty("traces").EnumerateArray())
        {
            string name=trace.GetProperty("name").GetString()!;
            if(!names.Contains(name))continue;
            seen.Add(name);int count=0;
            foreach(var value in trace.GetProperty("frames").EnumerateArray())
            {
                int[] row=value.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                // Seeded wrap/camera probes change input after the last callback;
                // their initial X deliberately remains the previous sample.
                if((name is "u16ScrollWrap" or "signedCameraScroll")&&row[0]==0)continue;
                long elapsed=name=="u16ScrollWrap"?262050L+row[0]:row[1]-93L;
                var actual=FieldHorizontalFogPresentation.Sample(elapsed,row[25]);
                Assert.AreEqual(row[4],actual.ScrollOffset,$"{name}/{row[0]} offset");
                Assert.AreEqual(row[5],actual.ScrollCounter,$"{name}/{row[0]} counter");
                Assert.AreEqual(row[6],actual.PositionX,$"{name}/{row[0]} position");
                int actualEva=FieldHorizontalFogPresentation.Eva,actualEvb=FieldHorizontalFogPresentation.Evb;
                Assert.AreEqual(row[7],actualEva,$"{name}/{row[0]} EVA");
                Assert.AreEqual(row[8],actualEvb,$"{name}/{row[0]} EVB");
                Assert.AreEqual(2,row[2]);Assert.AreEqual(1,row[3]);count++;samples++;
            }
            Assert.IsGreaterThan(0,count,name);
        }
        CollectionAssert.AreEqual(names,seen);Assert.AreEqual(146,samples);
    }

    [TestMethod]
    public void EverySourceSpriteColumnUsesHardwareWrapAndAllFourRows()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        var rows=source.RootElement.GetProperty("spritePositions");Assert.AreEqual(12,rows.GetArrayLength());
        foreach(var value in rows.EnumerateArray())
        {
            int[] row=value.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(7,row.Length);
            // A raw camera delta of position+23 selects this source scroll X
            // at the post-initialization phase; signed -9 is source y2=247.
            var actual=FieldHorizontalFogPresentation.Sprites(0,row[0]+23,-9);
            for(int i=0;i<20;i++)
            {
                int x=(row[1+i%5]-32)&511;if(x>=240)x-=512;
                int y=(i/5*64+row[6])&255;if(y>=160)y-=256;
                Assert.AreEqual(new FieldHorizontalFogPresentation.FogSprite(x,y),actual[i],$"X {row[0]}, sprite {i}");
            }
        }
    }

    [TestMethod]
    public void FreshInitializationHasFinishedTheBlendAndKeepsPreincrementScrollPhase()
    {
        var fresh=FieldHorizontalFogPresentation.Sample(0);
        Assert.AreEqual(new FieldHorizontalFogPresentation.PhaseSample(0,23,1,233),fresh);
        for(int frame=0;frame<4;frame++)
            Assert.AreEqual(233,FieldHorizontalFogPresentation.Sample(frame).PositionX,$"callback {frame}");
        Assert.AreEqual(new FieldHorizontalFogPresentation.PhaseSample(3,24,0,233),FieldHorizontalFogPresentation.Sample(3));
        Assert.AreEqual(new FieldHorizontalFogPresentation.PhaseSample(4,24,1,232),FieldHorizontalFogPresentation.Sample(4));
    }

    [TestMethod]
    public void ScrollAndHardwareCoordinatesWrapWithoutDependingOnAbsoluteMapCamera()
    {
        foreach(long frame in new long[]{0,1,3,4,931,932,1023,1024,262051,262052,262143,262144,long.MaxValue})
        {
            var sample=FieldHorizontalFogPresentation.Sample(frame,int.MinValue);
            Assert.IsTrue(sample.ScrollOffset is >=0 and <=65535);
            Assert.IsTrue(sample.ScrollCounter is >=0 and <=3);
            Assert.IsTrue(sample.PositionX is >=0 and <=255);
            Assert.AreEqual(frame,sample.Frames);
            CollectionAssert.AreEqual(FieldHorizontalFogPresentation.Sprites(frame,0,0),
                FieldHorizontalFogPresentation.Sprites(frame,256,-256),$"callback {frame}");
            if(frame<=long.MaxValue-262144)
            {
                var wrapped=FieldHorizontalFogPresentation.Sample(frame+262144);
                var normal=FieldHorizontalFogPresentation.Sample(frame);
                Assert.AreEqual(normal.ScrollOffset,wrapped.ScrollOffset);
                Assert.AreEqual(normal.ScrollCounter,wrapped.ScrollCounter);
                Assert.AreEqual(normal.PositionX,wrapped.PositionX);
            }
        }
        var sprites=FieldHorizontalFogPresentation.Sprites(0,0,0);
        Assert.AreEqual(20,sprites.Length);
        int[] columns=[233,-23,41,105,169],rows=[0,64,128,-64];
        for(int i=0;i<sprites.Length;i++)
            Assert.AreEqual(new FieldHorizontalFogPresentation.FogSprite(columns[i%5],rows[i/5]),sprites[i]);
        var shifted=FieldHorizontalFogPresentation.Sprites(0,-19,37);
        for(int i=0;i<shifted.Length;i++)
        {
            Assert.AreEqual(shifted[i%5].X,shifted[i].X);
            Assert.AreEqual(shifted[i/5*5].Y,shifted[i].Y);
        }
        sprites[0]=new(999,999);
        Assert.AreEqual(new FieldHorizontalFogPresentation.FogSprite(233,0),FieldHorizontalFogPresentation.Sprites(0,0,0)[0]);
    }

    [TestMethod]
    public void IndependentTwelveEightCoefficientsSaturateFiveBitChannels()
    {
        for(int fog=0;fog<32;fog++)
        for(int background=0;background<32;background++)
        {
            ushort front=(ushort)(fog|((31-fog)<<5)|((fog/2)<<10));
            ushort back=(ushort)(background|((background/2)<<5)|((31-background)<<10));
            ushort actual=FieldHorizontalFogPresentation.AlphaBlendRgb555(front,back);
            for(int shift=0;shift<=10;shift+=5)
            {
                int expected=Math.Min(31,(((front>>shift)&31)*12+((back>>shift)&31)*8)/16);
                Assert.AreEqual(expected,(actual>>shift)&31,$"fog {fog}, background {background}, channel {shift/5}");
            }
        }
        Assert.AreEqual((ushort)0x7fff,FieldHorizontalFogPresentation.AlphaBlendRgb555(0x7fff,0x7fff));
        Assert.AreEqual((ushort)23,FieldHorizontalFogPresentation.AlphaBlendRgb555(31,0));
        Assert.AreEqual((ushort)15,FieldHorizontalFogPresentation.AlphaBlendRgb555(0,31));
    }

    [TestMethod]
    public void SharedPaletteFadeAndRecapToneApplyToEachLayerBeforeCompositionWithoutMutatingOriginalPixels()
    {
        byte[] fog=[248,184,120,255,3,17,91,0],field=[88,216,40,255,201,7,52,0];
        byte[] originalFog=fog.ToArray(),originalField=field.ToArray();
        int differencesFromPostFade=0,differencesFromPostGrayscale=0;
        foreach(int grayscale in new[]{0,1,8,15,16})
        foreach(int backgroundFade in new[]{0,2,9,16})
        foreach(int objectFade in new[]{0,3,9,16})
        foreach(ushort target in new ushort[]{0,0x7fff,0x32ad})
        {
            byte[] fogFrame=fog.ToArray(),fieldFrame=field.ToArray();
            QuestLogPalettePresentation.TransformRgba(fogFrame,grayscale,objectFade,target);
            QuestLogPalettePresentation.TransformRgba(fieldFrame,grayscale,backgroundFade,target);
            ushort expectedFog=PaletteOracle(Pack(fog),grayscale,objectFade,target);
            ushort expectedField=PaletteOracle(Pack(field),grayscale,backgroundFade,target);
            Assert.AreEqual(expectedFog,Pack(fogFrame));Assert.AreEqual(expectedField,Pack(fieldFrame));
            ushort actual=FieldHorizontalFogPresentation.AlphaBlendRgb555(Pack(fogFrame),Pack(fieldFrame));
            Assert.AreEqual(AlphaOracle(expectedFog,expectedField),actual);
            Assert.AreEqual((byte)255,fogFrame[3]);Assert.AreEqual((byte)255,fieldFrame[3]);
            CollectionAssert.AreEqual(fog[4..],fogFrame[4..]);CollectionAssert.AreEqual(field[4..],fieldFrame[4..]);
            if(backgroundFade==objectFade&&grayscale==0)
            {
                ushort post=PaletteOracle(AlphaOracle(Pack(fog),Pack(field)),0,objectFade,target);
                if(post!=actual)differencesFromPostFade++;
            }
            if(backgroundFade==0&&objectFade==0)
            {
                ushort post=PaletteOracle(AlphaOracle(Pack(fog),Pack(field)),grayscale,0,target);
                if(post!=actual)differencesFromPostGrayscale++;
            }
        }
        Assert.IsGreaterThan(0,differencesFromPostFade);
        Assert.IsGreaterThan(0,differencesFromPostGrayscale);
        CollectionAssert.AreEqual(originalFog,fog);CollectionAssert.AreEqual(originalField,field);
    }

    [TestMethod]
    public void SceneCameraLagMatchesOriginalPanningCallbackAndFirstInitialization()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        var rows=source.RootElement.GetProperty("cameraLag");Assert.AreEqual(5,rows.GetArrayLength());
        var scene=new FieldHorizontalFogPresentation.Scene(120,80);
        int cameraX=120,cameraY=80;
        foreach(var value in rows.EnumerateArray())
        {
            int[] row=value.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(12,row.Length);
            scene=scene.Advance(1,cameraX,cameraY);
            Assert.AreEqual((double)row[0],scene.Frames);
            Assert.AreEqual(row[1],scene.SpriteCoordOffsetX,$"callback {row[0]} camera X");
            Assert.AreEqual(row[2],scene.SpriteCoordOffsetY,$"callback {row[0]} camera Y");
            Assert.AreEqual(row[5],scene.Sample.ScrollOffset);
            Assert.AreEqual(row[6],scene.Sample.ScrollCounter);
            Assert.AreEqual(row[7],scene.Sample.PositionX);
            int x=(row[8]-32)&511;if(x>=240)x-=512;
            int y=row[9]&255;if(y>=160)y-=256;
            Assert.AreEqual(new FieldHorizontalFogPresentation.FogSprite(x,y),scene.Sprites()[0]);
            cameraX+=row[10];cameraY+=row[11];
            Assert.AreEqual(row[3],scene.AnchorCameraX-cameraX);
            Assert.AreEqual(row[4],scene.AnchorCameraY-cameraY-40);
        }
    }

    [TestMethod]
    public void SceneInitializationAndFractionalCallbacksKeepPriorCameraOffsets()
    {
        var scene=new FieldHorizontalFogPresentation.Scene(120,80);
        Assert.IsFalse(scene.Initialized);Assert.AreEqual(0,scene.Sprites().Length);
        var fractional=scene.Advance(.5,124,86);
        Assert.IsFalse(fractional.Initialized);Assert.AreEqual(0,fractional.SpriteCoordOffsetX);
        var initialized=fractional.Advance(.5,124,86);
        Assert.IsTrue(initialized.Initialized);Assert.AreEqual(0L,initialized.SourceElapsedFrames);
        Assert.AreEqual(0,initialized.SpriteCoordOffsetX);Assert.AreEqual(0,initialized.SpriteCoordOffsetY);
        var pending=initialized.Advance(.75,124,86);
        Assert.AreEqual(initialized.Sample,pending.Sample);
        Assert.AreEqual(0,pending.SpriteCoordOffsetY);
        var moved=pending.Advance(.25,124,86);
        Assert.AreEqual(1L,moved.SourceElapsedFrames);
        Assert.AreEqual(-4,moved.SpriteCoordOffsetX);Assert.AreEqual(-46,moved.SpriteCoordOffsetY);
        CollectionAssert.AreEqual(FieldHorizontalFogPresentation.Sprites(1,-4,-46),moved.Sprites());
        Assert.AreEqual(0d,scene.Frames);Assert.AreEqual(1d,initialized.Frames);
    }

    [TestMethod]
    public void FreshSameMapDestinationAndDetachedDepartureResumeTheirOwnPhase()
    {
        var live=new FieldHorizontalFogPresentation.Scene(120,80).Advance(45,124,82);
        var departure=live;
        live=live.Advance(9,125,83);
        Assert.AreEqual(45d,departure.Frames);Assert.AreEqual(54d,live.Frames);
        // Reconstructing even the same map is a fresh owner and camera anchor.
        var destination=new FieldHorizontalFogPresentation.Scene(120,80);
        Assert.AreEqual(0d,destination.Frames);Assert.AreEqual(0L,destination.SourceElapsedFrames);
        var arrival=destination.Advance(19,120,80);
        var capturedArrival=arrival;
        destination=arrival.Advance(4,121,80);
        Assert.AreEqual(19d,capturedArrival.Frames);Assert.AreEqual(18L,capturedArrival.SourceElapsedFrames);
        Assert.AreEqual(23d,destination.Frames);Assert.AreEqual(22L,destination.SourceElapsedFrames);
        Assert.AreEqual(-40,capturedArrival.SpriteCoordOffsetY);
        Assert.AreEqual(0,capturedArrival.SpriteCoordOffsetX);Assert.AreEqual(-1,destination.SpriteCoordOffsetX);
        Assert.AreEqual(departure.Sample,FieldHorizontalFogPresentation.Sample(44,-4));
        var pixels=capturedArrival.Sprites();pixels[0]=new(999,999);
        Assert.AreNotEqual(pixels[0],capturedArrival.Sprites()[0]);
    }

    [TestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(144)]
    public void SceneCallbackPhaseIsIndependentOfHostRate(int hz)
    {
        var scene=new FieldHorizontalFogPresentation.Scene(73,41);
        for(int frame=0;frame<hz*10;frame++)scene=scene.Advance(60d/hz,73,41);
        Assert.AreEqual(599L,scene.SourceElapsedFrames);
        CollectionAssert.AreEqual(FieldHorizontalFogPresentation.Sprites(599,0,-40),scene.Sprites());
    }

    [TestMethod]
    public void NegativeTimeAndNonRgb555InputAreRejected()
    {
        var scene=new FieldHorizontalFogPresentation.Scene(0,0);
        foreach(double frames in new[]{-1d,double.NaN,double.NegativeInfinity,double.PositiveInfinity,double.MaxValue})
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>scene.Advance(frames,0,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldHorizontalFogPresentation.Sample(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldHorizontalFogPresentation.Sprites(-1,0,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldHorizontalFogPresentation.AlphaBlendRgb555(0x8000,0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldHorizontalFogPresentation.AlphaBlendRgb555(0,0xffff));
    }

    private static ushort Pack(byte[] rgba)=>(ushort)((rgba[0]>>3)|((rgba[1]>>3)<<5)|((rgba[2]>>3)<<10));
    private static ushort AlphaOracle(ushort front,ushort back)
    {
        int result=0;
        for(int shift=0;shift<=10;shift+=5)
            result|=Math.Min(31,(((front>>shift)&31)*12+((back>>shift)&31)*8)/16)<<shift;
        return (ushort)result;
    }
    private static ushort PaletteOracle(ushort color,int tone,int fade,ushort target)
    {
        int gray=(76*(color&31)+151*((color>>5)&31)+29*((color>>10)&31))/256,result=0;
        for(int shift=0;shift<=10;shift+=5)
        {
            int channel=(color>>shift)&31;
            channel+=(int)Math.Floor((gray-channel)*tone/16d);
            channel+=(int)Math.Floor((((target>>shift)&31)-channel)*fade/16d);
            result|=channel<<shift;
        }
        return (ushort)result;
    }
}
