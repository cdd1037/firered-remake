using System.Buffers.Binary;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OverlayCompositionTests
{
    private const ushort Red=0x801f,Green=0x83e0,Blue=0xfc00;
    private static SourceOverlayAsset Material(byte packed=0x11)
    {
        var map=new byte[2048];for(int i=0;i<map.Length;i+=2)BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(i),0x8000);
        var colors=new ushort[16];colors[1]=31;colors[2]=31<<5;
        return new(Enumerable.Repeat(packed,32).ToArray(),map,colors);
    }
    private static Dictionary<string,SourcePixelAsset> Assets()=>new()
    { ["background"]=new(240,112,Enumerable.Repeat(Green,240*112).ToArray()),["impact"]=new(32,32,new ushort[1024]),
      ["body"]=new(64,64,Enumerable.Repeat(Blue,4096).ToArray()) };
    private static ushort Pixel(byte[] rgba,int x=40,int y=40)
    {int i=(y*240+x)*4;return (ushort)(0x8000|((rgba[i]*31+127)/255)|(((rgba[i+1]*31+127)/255)<<5)|(((rgba[i+2]*31+127)/255)<<10));}
    private static SourceAnimationFrame Frame(int priority)=>new(0,true,new PixelPoint[4],[],8,8,[],[])
    {Overlay=new("overlay",0,0),BlendEnabled=true,BlendFirstTargets=2,BlendSecondTargets=63,BattlerBackgroundPriorities=[priority,1]};
    [TestMethod]
    [DataRow(1,0xbc0f)] [DataRow(2,0xfc00)] [DataRow(3,0xfc00)]
    public void OverlayAndObjectUseHardwarePriorityIncludingObjectTieBreak(int priority,int expected)
    {
        AnimationBattler[] battlers=[new(new(0),"body",0,0,64,64){DisplayCenter=new(40,40)}];
        var composer=new SourceBattleFrameComposer(Assets(),battlers,overlays:new Dictionary<string,SourceOverlayAsset>{{"overlay",Material()}});
        Assert.AreEqual((ushort)expected,Pixel(composer.ComposeField(Frame(priority))));
        Assert.AreEqual((ushort)0x81ef,Pixel(composer.ComposeField(Frame(priority)),200,100));
    }
    [TestMethod]
    public void ImmediateUnselectedSecondLayerBlocksBlendAndSemiTransparentObjectUsesOverlay()
    {
        AnimationBattler[] battlers=[new(new(0),"body",0,0,64,64){DisplayCenter=new(40,40)}];
        var composer=new SourceBattleFrameComposer(Assets(),battlers,overlays:new Dictionary<string,SourceOverlayAsset>{{"overlay",Material()}});
        Assert.AreEqual(Red,Pixel(composer.ComposeField(Frame(1) with{BlendSecondTargets=8})));
        Assert.AreEqual((ushort)0xbc0f,Pixel(composer.ComposeField(Frame(2) with{BlendedObjectSlots=[0]})));
        Assert.AreEqual(Red,Pixel(composer.ComposeField(Frame(1) with{BlendEnabled=false})));
    }
    [TestMethod]
    public void Bg1TieWinsBg2AndReplacesItsOwnBattlerCopy()
    {
        AnimationBattler[] battlers=[new(new(0),"body",0,0,64,64){DisplayCenter=new(40,40)},new(new(1),"body",0,0,64,64){DisplayCenter=new(180,40)}];
        var composer=new SourceBattleFrameComposer(Assets(),battlers,overlays:new Dictionary<string,SourceOverlayAsset>{{"overlay",Material()}});
        var frame=Frame(1) with{BackgroundSlots=[0,1]};
        Assert.AreEqual((ushort)0xbc0f,Pixel(composer.ComposeField(frame)));
        Assert.AreEqual((ushort)0x81ef,Pixel(composer.ComposeField(frame),180,40));
    }
    [TestMethod]
    public void OverlayUsesTheExistingScreenAlignedBgMosaicBeforeScrollSampling()
    {
        var composer=new SourceBattleFrameComposer(Assets(),[],overlays:new Dictionary<string,SourceOverlayAsset>{{"overlay",Material(0x21)}});
        var frame=Frame(1) with{BlendEnabled=false};
        Assert.AreEqual(Red,Pixel(composer.ComposeField(frame),0,0));Assert.AreEqual(Green,Pixel(composer.ComposeField(frame),1,0));
        var mosaic=frame with{BattlerBackgroundMosaic=[true,false],BackgroundMosaicHorizontal=1};
        Assert.AreEqual(Red,Pixel(composer.ComposeField(mosaic),1,0));
        Assert.AreEqual(Green,Pixel(composer.ComposeField(mosaic with{Overlay=new("overlay",1,0)}),1,0));
    }
    [TestMethod]
    public void OrdinaryObjBlendFastPathMatchesGeneralLayerSelection()
    {
        var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"body",0,0,64,64){DisplayCenter=new(30+i*8,30+i*5)}).ToArray();
        var composer=new SourceBattleFrameComposer(Assets(),battlers);
        foreach(int p1 in Enumerable.Range(0,4))foreach(int p2 in Enumerable.Range(0,4))foreach(bool blend in new[]{false,true})
        {
            var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[0,1],7,12,[],[])
                {Doubles=true,BattlerBackgroundPriorities=[p1,p2],BlendEnabled=blend,BlendedObjectSlots=[2,3]};
            // BG0 is outside this field surface. Removing only its second-target
            // bit selects the general path without changing any visible target.
            CollectionAssert.AreEqual(composer.ComposeField(frame),composer.ComposeField(frame with{BlendSecondTargets=62}));
        }
    }
    [TestMethod]
    public void IndexedMapRetainsBanksFlipsScrollTransparencyAndPaletteTransforms()
    {
        var map=new byte[2048];for(int i=0;i<map.Length;i+=2)BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(i),0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(2),0x8400);
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(4),0x1000);
        var tile=new byte[32];tile[0]=0x21;tile[3]=0x10;tile[28]=0x12;
        ushort[] palette=new ushort[16];palette[1]=31;palette[2]=31<<5;
        var material=new SourceOverlayAsset(tile,map,palette);
        Assert.AreEqual(new SourceBackgroundPixel(8,1),material.Sample(0,0));
        Assert.AreEqual(new SourceBackgroundPixel(8,1),material.Sample(256,256));
        Assert.AreEqual(new SourceBackgroundPixel(8,1),material.Sample(8,0));
        Assert.AreEqual(new SourceBackgroundPixel(1,1),material.Sample(16,0));
        Assert.AreEqual(0,material.Sample(2,0).Index);
        var pixels=new ushort[8];material.Render(pixels,8,1,0,0,null,[new(8,8,0)],[]);
        Assert.AreEqual((ushort)0x800f,pixels[0]);Assert.AreEqual((ushort)0,pixels[2]);
        Assert.Throws<InvalidDataException>(()=>material.Render(new ushort[1],1,1,16,0,null,[],[]));
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(0),0x9000);
        Assert.Throws<InvalidDataException>(()=>new SourceOverlayAsset(tile,map,palette));
    }
    [TestMethod]
    public void Bg1AlphaDistinguishesTransparentBackdropFromOpaqueBg3BySourceIndex()
    {
        // Tile zero is transparent even when its map entry selects bank two.
        // Tile one is opaque BG3, including when its map entry selects bank zero.
        var tiles=new byte[64];Array.Fill(tiles,(byte)0x11,32,32);
        var map=new byte[4096];
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(0),0x2000);
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(2),0x2001);
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(4),0x0001);
        var palette=new ushort[48];palette[1]=(ushort)(Blue&0x7fff);
        var basePalette=new ushort[32];basePalette[0]=(ushort)(Green&0x7fff);basePalette[1]=(ushort)(Blue&0x7fff);
        var surface=new SourceAnimationBackgroundSurface(new(tiles,map,palette),basePalette);
        var colors=new ushort[24];var masks=new byte[24];
        surface.Render(colors,24,1,layerMasks:masks);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)32,8).Concat(Enumerable.Repeat((byte)8,16)).ToArray(),masks);
        Assert.AreEqual(Green,colors[0]);Assert.AreEqual(Blue,colors[8]);Assert.AreEqual(Blue,colors[16]);
        var composer=new SourceBattleFrameComposer(Assets(),[],backgroundSurface:surface,
            overlays:new Dictionary<string,SourceOverlayAsset>{{"overlay",Material()}});
        var bg3Only=composer.ComposeField(Frame(1) with{BlendSecondTargets=8});
        Assert.AreEqual(Red,Pixel(bg3Only,0,0));
        Assert.AreEqual((ushort)0xbc0f,Pixel(bg3Only,8,0));
        Assert.AreEqual((ushort)0xbc0f,Pixel(bg3Only,16,0));
        var backdropOnly=composer.ComposeField(Frame(1) with{BlendSecondTargets=32});
        Assert.AreEqual((ushort)0x81ef,Pixel(backdropOnly,0,0));
        Assert.AreEqual(Red,Pixel(backdropOnly,8,0));Assert.AreEqual(Red,Pixel(backdropOnly,16,0));
        var all=composer.ComposeField(Frame(1));
        Assert.AreEqual((ushort)0x81ef,Pixel(all,0,0));Assert.AreEqual((ushort)0xbc0f,Pixel(all,8,0));
        // The existing flattened terrain path still represents opaque BG3.
        var flattened=new SourceBattleFrameComposer(Assets(),[],
            overlays:new Dictionary<string,SourceOverlayAsset>{{"overlay",Material()}});
        Assert.AreEqual((ushort)0x81ef,Pixel(flattened.ComposeField(Frame(1) with{BlendSecondTargets=8}),0,0));
        Assert.AreEqual(Red,Pixel(flattened.ComposeField(Frame(1) with{BlendSecondTargets=32}),0,0));
    }
}
