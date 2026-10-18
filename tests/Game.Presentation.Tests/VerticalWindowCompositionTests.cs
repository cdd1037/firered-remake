using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class VerticalWindowCompositionTests
{
    private static ushort Pixel(byte[] rgba,int x,int y)=>(ushort)((rgba[(y*240+x)*4]>>3)|((rgba[(y*240+x)*4+1]>>3)<<5)|((rgba[(y*240+x)*4+2]>>3)<<10));
    private static AnimationBattler[] Bodies()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){DisplayCenter=new(-100,-100),SourceVisibility=new(true,false,true)}).ToArray();
    private static Dictionary<string,SourcePixelAsset> Assets()
    {
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])}};
        foreach(var b in Bodies())assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(0x8000|((i%64)&31)|(((i/64)&31)<<5))).ToArray());return assets;
    }
    private static SourceAnimationFrame Frame(int[] slots)=>new(0,true,new PixelPoint[4],slots,16,0,[],[])
        {Battlers=Bodies(),Doubles=true,BackgroundScrollOffsets=[new(0,0),new(0,0)],BattlerBackgroundPriorities=[1,2],BattlerVisibility=Enumerable.Range(0,4).Select(i=>new SourceBattlerVisibilityFrame(i,true,false,true,true)).ToArray()};
    [TestMethod,DataRow(1),DataRow(2)]
    public void VerticalDmaPreservesHorizontalRegisterAndSamplesOriginalRowsBeforeClipping(int rank)
    {
        int slot=rank==1?1:0;var composer=new SourceBattleFrameComposer(Assets(),Bodies());var words=Enumerable.Range(0,161).Select(y=>unchecked((ushort)((y%2==0?5:9)-y))).ToArray();
        var frame=Frame([slot]) with{BackgroundScrollOffsets=[new(65535,200),new(65535,200)],Scanline=new(rank==1?SourceScanlineDestination.Bg1Vertical:SourceScanlineDestination.Bg2Vertical,false,words)};
        var pixels=composer.ComposeField(frame);
        for(int y=0;y<112;y++){Assert.AreEqual((ushort)0x7c00,Pixel(pixels,0,y));Assert.AreEqual((ushort)((y%2==0?5:9)<<5),Pixel(pixels,1,y));Assert.AreEqual((ushort)(7|((y%2==0?5:9)<<5)),Pixel(pixels,8,y));}
        Assert.Throws<ArgumentException>(()=>(frame.Scanline! with{Word32=true,Words=new ushort[322]}).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SourceScanlineState().SetParams(frame.Scanline!.Destination,true));
    }
    [TestMethod]
    public void RectangularMasksSelectBgPlanesBeforeAlphaAndWin0WinsOverWin1AndObjectWindow()
    {
        var assets=Assets();assets["b1"]=new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray());assets["b0"]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
        var frame=Frame([0,1]) with{Window0=new(true,(10<<8)|30,(10<<8)|30,61,59),Window1=new(true,50,50,63,63),ObjectWindow=new(true,63,61)};
        var composer=new SourceBattleFrameComposer(assets,Bodies());var pixels=composer.ComposeField(frame);
        Assert.AreEqual((ushort)0x03e0,Pixel(pixels,20,20),"WIN0 hides BG1 even though WIN1 covers it.");
        Assert.AreEqual((ushort)0x001f,Pixel(pixels,5,20),"WIN1 wins outside WIN0 and enables both planes.");
        Assert.AreEqual((ushort)0x03e0,Pixel(pixels,55,20),"Outside OBJ mask has precedence over rectangular outside mask.");
        pixels=composer.ComposeField(frame with{ObjectWindow=null,Window1=null});Assert.AreEqual((ushort)0x001f,Pixel(pixels,55,20),"WIN0 outside mask hides BG2.");
        pixels=composer.ComposeField(frame with{ObjectWindow=null,Window1=null,Window0=new(true,0,0,63,57)});Assert.AreEqual((ushort)0x7c00,Pixel(pixels,20,20),"Both BG planes can be suppressed.");
        foreach(int mask in new[]{62,55,47})Assert.Throws<InvalidDataException>(()=>new SourceWindow0Frame(true,240,160,mask,63).Validate());
        Assert.Throws<InvalidDataException>(()=>new SourceWindow0Frame(true,(30<<8)|10,160,63,63).Validate());
    }
    [TestMethod]
    public void VerticalBg1DmaUsesTheSameIndexedOverlaySampler()
    {
        var tiles=new byte[64];for(int row=0;row<8;row++)for(int col=0;col<4;col++)tiles[row*4+col]=(byte)((row+1)|((row+1)<<4));
        var map=new byte[2048];for(int i=0;i<1024;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(i*2),0x8000);
        var palette=Enumerable.Range(0,16).Select(i=>(ushort)i).ToArray();var overlay=new SourceOverlayAsset(tiles,map,palette);var words=Enumerable.Range(0,161).Select(y=>unchecked((ushort)(3-y))).ToArray();var output=new ushort[240*112];
        overlay.Render(output,240,112,0,70,null,[],[],scanline:new(SourceScanlineDestination.Bg1Vertical,false,words));Assert.IsTrue(output.All(p=>p==0x8004));
    }
}
