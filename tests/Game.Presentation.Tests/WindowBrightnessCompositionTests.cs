using System.Buffers.Binary;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WindowBrightnessCompositionTests
{
    private const ushort Blue=0xfc00,Green=0x83e0;
    private static Dictionary<string,SourcePixelAsset> Assets()=>new()
    { ["background"]=new(240,112,Enumerable.Repeat(Blue,240*112).ToArray()),["impact"]=new(32,32,new ushort[1024]),["body"]=new(64,64,Enumerable.Repeat(Green,4096).ToArray()) };
    private static ushort Pixel(byte[] rgba,int x,int y=0)
    {int i=(y*240+x)*4;return (ushort)(0x8000|((rgba[i]*31+127)/255)|(((rgba[i+1]*31+127)/255)<<5)|(((rgba[i+2]*31+127)/255)<<10));}
    private static SourceAnimationFrame Frame()=>new(0,true,new PixelPoint[4],[],0,0,[],[])
    {Brightness=new(SourceBrightnessMode.Darken,16),BlendFirstTargets=8,BlendSecondTargets=0};
    private static SourceAnimationBackgroundSurface Surface()
    {
        var tiles=new byte[64];Array.Fill(tiles,(byte)0x11,32,32);
        var map=new byte[4096];for(int i=0;i<map.Length;i+=2)BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(i),0x2001);
        BinaryPrimitives.WriteUInt16LittleEndian(map,0x2000); // Equal-colored transparent backdrop.
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(4),0x0001); // Opaque BG3 using bank zero.
        return new(new(tiles,map,Enumerable.Repeat((ushort)(Blue&0x7fff),48).ToArray()),Enumerable.Repeat((ushort)(Blue&0x7fff),32).ToArray());
    }
    [TestMethod]
    public void RectangleEdgesAreExclusiveEqualEdgesEmptyAndDisabledWindowDoesNotGateColor()
    {
        var full=new SourceWindow0Frame(true,240,160,31,63);full.Validate();
        Assert.IsFalse(full.ColorEffectsAt(0,0));Assert.IsFalse(full.ColorEffectsAt(239,159));
        var inset=new SourceWindow0Frame(true,(13<<8)|227,(10<<8)|100,31,63);inset.Validate();
        foreach(var p in new[]{(13,10),(226,99)})Assert.IsFalse(inset.ColorEffectsAt(p.Item1,p.Item2));
        foreach(var p in new[]{(12,10),(227,10),(13,9),(13,100)})Assert.IsTrue(inset.ColorEffectsAt(p.Item1,p.Item2));
        foreach(var empty in new[]{full with{Horizontal=0},full with{Vertical=0},full with{Horizontal=(37<<8)|37},full with{Vertical=(99<<8)|99}})
        {empty.Validate();for(int y=0;y<160;y++)for(int x=0;x<240;x++)Assert.IsTrue(empty.ColorEffectsAt(x,y));}
        Assert.IsTrue((full with{Enabled=false,OutsideMask=31}).ColorEffectsAt(0,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>full.ColorEffectsAt(240,0));Assert.Throws<ArgumentOutOfRangeException>(()=>full.ColorEffectsAt(0,160));
    }
    [TestMethod]
    public void UnprovenInvertedOffscreenAndLayerDisablingMasksFailClosed()
    {
        var full=new SourceWindow0Frame(true,240,160,31,63);
        foreach(var invalid in new[]{full with{Horizontal=(240<<8)|10},full with{Vertical=(150<<8)|10},full with{Horizontal=241},full with{Vertical=161},full with{InsideMask=30},full with{OutsideMask=64}})
            Assert.Throws<InvalidDataException>(invalid.Validate);
    }
    [TestMethod]
    public void BldyClampsFiveBitsAndPreservesHardwareRoundBeforeSubtractionForEveryChannel()
    {
        foreach(var mode in new[]{SourceBrightnessMode.Lighten,SourceBrightnessMode.Darken})for(int y=0;y<32;y++)for(int channel=0;channel<32;channel++)
        {
            int amount=Math.Min(y,16),expected=mode==SourceBrightnessMode.Lighten?channel+(31-channel)*amount/16:channel-channel*amount/16;
            var effect=new SourceBrightnessFrame(mode,y);effect.Validate();
            foreach(int shift in new[]{0,5,10})
            {
                ushort color=(ushort)(channel<<shift);ushort result=effect.Apply(color);
                Assert.AreEqual(expected,(result>>shift)&31);Assert.AreEqual((ushort)(result|0x8000),effect.Apply((ushort)(color|0x8000)));
            }
        }
        Assert.Throws<InvalidDataException>(()=>new SourceBrightnessFrame((SourceBrightnessMode)1,16).Validate());
        Assert.Throws<InvalidDataException>(()=>new SourceBrightnessFrame(SourceBrightnessMode.Darken,32).Validate());
    }
    [TestMethod]
    public void Bg3BrightnessDoesNotTouchEqualColoredBackdropOrAnOccludingObject()
    {
        Assert.Throws<InvalidDataException>(()=>new SourceBattleFrameComposer(Assets(),[]).ComposeField(Frame()));
        var composer=new SourceBattleFrameComposer(Assets(),[],backgroundSurface:Surface());
        var dark=composer.ComposeField(Frame());Assert.AreEqual(Blue,Pixel(dark,0));Assert.AreEqual((ushort)0x8000,Pixel(dark,8));Assert.AreEqual((ushort)0x8000,Pixel(dark,16));
        var light=composer.ComposeField(Frame() with{Brightness=new(SourceBrightnessMode.Lighten,16)});
        Assert.AreEqual(Blue,Pixel(light,0));Assert.AreEqual((ushort)0xffff,Pixel(light,8));Assert.AreEqual((ushort)0xffff,Pixel(light,16));
        var inverse=composer.ComposeField(Frame() with{BlendFirstTargets=32});Assert.AreEqual((ushort)0x8000,Pixel(inverse,0));Assert.AreEqual(Blue,Pixel(inverse,8));
        var gated=composer.ComposeField(Frame() with{Window0=new(true,16,160,31,63)});
        Assert.AreEqual(Blue,Pixel(gated,8));Assert.AreEqual((ushort)0x8000,Pixel(gated,16));
        AnimationBattler[] bodies=[new(new(0),"body",0,0,64,64){DisplayCenter=new(40,40)}];
        var objects=new SourceBattleFrameComposer(Assets(),bodies,backgroundSurface:Surface());
        Assert.AreEqual(Green,Pixel(objects.ComposeField(Frame()),40,40));
        Assert.AreEqual((ushort)0x8000,Pixel(objects.ComposeField(Frame()),200,40));
        var bg1=Frame() with{BackgroundSlots=[0]};Assert.AreEqual(Green,Pixel(objects.ComposeField(bg1),40,40));
    }
    [TestMethod]
    public void WindowColorBitAlsoGatesExistingAlphaAndLeavesNullStateFastPathEquivalent()
    {
        AnimationBattler[] bodies=[new(new(0),"body",0,0,64,64){DisplayCenter=new(40,40)}];
        var composer=new SourceBattleFrameComposer(Assets(),bodies);
        var alpha=new SourceAnimationFrame(0,true,new PixelPoint[4],[],8,8,[],[]){BlendEnabled=true,BlendedObjectSlots=[0]};
        var window=alpha with{Window0=new(true,40,160,31,63)};var pixels=composer.ComposeField(window);
        Assert.AreEqual(Green,Pixel(pixels,39,40));Assert.AreEqual((ushort)0xbde0,Pixel(pixels,40,40));
        CollectionAssert.AreEqual(composer.ComposeField(alpha),composer.ComposeField(alpha with{Window0=new(true,0,0,63,63)}));
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(alpha with{Brightness=new(SourceBrightnessMode.Darken,16)}));
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(Frame() with{MoveBackground=new(null,0,0,1)}));
    }
    [TestMethod]
    public void RectanglesTakePriorityOverAffineObjectColorMasksWithoutPaintingThem()
    {
        var composer=new SourceBattleFrameComposer(Assets(),[],backgroundSurface:Surface());
        ushort[] mask=Enumerable.Range(0,64).Select(i=>(ushort)(i%8<5?0x8000:0)).ToArray();
        var sprite=new SourceVisualSpriteFrame(4,"mask",new(10,12),default,0,true,false,false,256,"mask",0)
            {Width=8,Height=8,Affine=true,DoubleSize=true,MatrixA=128,MatrixD=128,ObjectWindow=true,Material=new(8,8,mask)};
        var f=Frame() with{BlendFirstTargets=56,Sprites=[sprite],ObjectWindow=new(true,31,63),Window0=new(true,(7<<8)|9,(8<<8)|16,63,63),Window1=new(true,(8<<8)|12,(9<<8)|15,31,63)};
        byte[] result=composer.ComposeField(f);
        for(int y=4;y<21;y++)for(int x=2;x<20;x++)
        {
            int sx=((x-10)*128>>8)+4,sy=((y-12)*128>>8)+4;
            bool insideMask=x>=2&&x<18&&y>=4&&y<20&&sx>=0&&sx<8&&sy>=0&&sy<8&&mask[sy*8+sx]!=0;
            bool win0=x>=7&&x<9&&y>=8&&y<16,win1=x>=8&&x<12&&y>=9&&y<15;
            bool effects=win0||(!win1&&!insideMask);
            Assert.AreEqual(effects?(ushort)0x8000:Blue,Pixel(result,x,y),$"{x},{y}");
        }
        CollectionAssert.AreEqual(composer.ComposeField(Frame()),composer.ComposeField(Frame() with{Window1=new(false,0,0,31,63)}));
        var hud=new SourceWindow0Frame(true,240,(120<<8)|160,31,63);hud.Validate();
        Assert.IsTrue(hud.ColorEffectsAt(0,119));Assert.IsFalse(hud.ColorEffectsAt(239,120));
    }

}
