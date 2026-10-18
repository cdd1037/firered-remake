using System.Reflection;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ScanlineCompositionTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object? Call(SourceBattleAnimation owner,string method,params object[] args)=>typeof(SourceBattleAnimation).GetMethod(method,Private)!.Invoke(owner,args);
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceBattleAnimation Runtime(SourceBattleAnimation.SpriteScene scene)=>new(new(1,"Move_POUND",[new("delay",["30"],1),new("end",[],2)]),new("scanline",1,1,new(0),new(1)),Battlers(),true,new Audio(),spriteScene:scene);
    private static SourceScanlineState State(SourceBattleAnimation owner)=>(SourceScanlineState)typeof(SourceBattleAnimation).GetProperty("ScanlineState",Private)!.GetValue(owner)!;
    private static Dictionary<string,SourcePixelAsset> Assets()
    {
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])}};
        foreach(var b in Battlers())assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(0x8000|((i%64)&31)|(((i/64)&31)<<5))).ToArray());
        return assets;
    }
    private static SourceAnimationFrame Frame(AnimationBattler[] battlers,int slot,SourceScanlineFrame scanline)=>new(0,true,new PixelPoint[4],[slot],16,0,[],[])
    {Battlers=battlers,Doubles=true,Scanline=scanline,BackgroundScrollOffsets=[new(400,240),new(400,240)],BattlerBackgroundPriorities=[1,1]};
    private static ushort Pixel(byte[] rgba,int x,int y)=>(ushort)((rgba[(y*240+x)*4]>>3)|((rgba[(y*240+x)*4+1]>>3)<<5)|((rgba[(y*240+x)*4+2]>>3)<<10));

    [TestMethod,DataRow(1),DataRow(2)]
    public void PairedScanlinesSampleOriginalBattlerTilesBeforeScreenClipping(int rank)
    {
        int slot=rank==1?1:0;var battlers=Battlers().Select(b=>b with{Visible=b.Slot.Value==slot,DisplayCenter=new(-90,-90)}).ToArray();
        var words=new ushort[322];for(int y=0;y<161;y++){words[y*2]=(ushort)(y%2==0?0:65535);words[y*2+1]=unchecked((ushort)(5-y));}
        var scanline=new SourceScanlineFrame(rank==1?SourceScanlineDestination.Bg1Horizontal:SourceScanlineDestination.Bg2Horizontal,true,words);
        var composer=new SourceBattleFrameComposer(Assets(),battlers);var pixels=composer.ComposeField(Frame(battlers,slot,scanline));
        for(int y=0;y<112;y++)
        {
            Assert.AreEqual((ushort)(5<<5),Pixel(pixels,y%2, y),"Original tile (0,5) enters from a previously clipped body.");
            Assert.AreEqual((ushort)(7|(5<<5)),Pixel(pixels,7+y%2,y));
            if(y%2==1)Assert.AreEqual((ushort)0x7c00,Pixel(pixels,0,y),"BG HOFS wraps at512, leaving tile511 transparent.");
        }
    }
    [TestMethod]
    public void AlphaScanlinesSelectEachRowsHardwareCoefficientsAndKeepOtherLayers()
    {
        var battlers=Battlers().Select(b=>b with{Visible=false}).ToArray();var tiles=Enumerable.Repeat((byte)0x11,32).ToArray();var map=new byte[2048];
        for(int i=0;i<1024;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(i*2),0x8000);
        var palette=new ushort[16];palette[1]=31;var overlay=new SourceOverlayAsset(tiles,map,palette);
        var words=Enumerable.Range(0,161).Select(y=>(ushort)((y%3) switch{0=>0x1000,1=>0x0010,_=>0x0808})).ToArray();
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],0,0,[],[]){Battlers=battlers,Doubles=true,Overlay=new("water",0,0),BlendEnabled=true,BlendFirstTargets=2,BlendSecondTargets=63,BattlerBackgroundPriorities=[1,2],Scanline=new(SourceScanlineDestination.BlendAlpha,false,words)};
        var composer=new SourceBattleFrameComposer(Assets(),battlers,overlays:new Dictionary<string,SourceOverlayAsset>{{"water",overlay}});var pixels=composer.ComposeField(frame);
        for(int y=0;y<112;y++)Assert.AreEqual((ushort)((y%3) switch{0=>0x7c00,1=>31,_=>15|(15<<10)}),Pixel(pixels,120,y));
        Assert.Throws<ArgumentException>(()=>composer.ComposeField(frame with{Scanline=new(SourceScanlineDestination.BlendAlpha,true,new ushort[322])}));
    }
    [TestMethod]
    public void SceneRetainsBanksButSnapshotsDetachAndOldCancellationCannotStopNewOwner()
    {
        var scene=new SourceBattleAnimation.SpriteScene();var old=Runtime(scene);var state=State(old);
        state.Write(0,5,123);state.Write(1,5,456);Call(old,"StartScanline",1,false);var first=old.Tick();
        Assert.AreEqual(123,first.Scanline!.Words[5]);Assert.AreEqual(1,state.SourceBuffer);
        var copied=old.Snapshot();copied.Scanline!.Words[5]=999;Assert.AreEqual(123,old.Snapshot().Scanline!.Words[5]);Assert.AreEqual(1,state.SourceBuffer);
        Call(old,"StopScanlineDeferred");old.Tick();
        for(int ticks=0;old.Active&&ticks<40;ticks++)old.Tick();Assert.IsFalse(old.Active);
        Assert.AreEqual(0,state.State);Assert.AreEqual(123,state.Read(0,5));Assert.AreEqual(1,state.SourceBuffer);
        var next=Runtime(scene);Call(next,"StartScanline",2,false);old.Cancel();Assert.AreEqual(1,state.State);
        Assert.AreEqual(456,next.Tick().Scanline!.Words[5]);Assert.AreEqual(0,state.SourceBuffer);
        scene.Cancel();Assert.AreEqual(0,state.State);Assert.AreEqual(0,state.SourceBuffer);Assert.AreEqual(0,state.Read(0,5));Assert.AreEqual(0,state.Read(1,5));
    }
    [TestMethod]
    public void QueuedAlphaOverridesOnlyRowZeroAndDeferredStopRetainsTerminalUntilExplicitWrite()
    {
        var owner=Runtime(new());var state=State(owner);
        for(int bank=0;bank<2;bank++)for(int row=0;row<161;row++)state.Write(bank,row,(ushort)(row==160?0x0808:0x1000));
        Call(owner,"SetAlphaBlend",2,63,16,0);Call(owner,"StartAlphaScanline");var first=owner.Tick();
        Assert.AreEqual((ushort)16,first.Scanline!.Words[0]);Assert.AreEqual((ushort)0x1000,first.Scanline.Words[1]);
        Assert.AreEqual((ushort)0x1000,owner.Tick().Scanline!.Words[0]);Call(owner,"StopScanlineDeferred");var stopped=owner.Tick();
        Assert.IsNull(stopped.Scanline);Assert.AreEqual(8,stopped.AlphaA);Assert.AreEqual(8,stopped.AlphaB);
        Call(owner,"ClearAlphaBlend");var restored=owner.Tick();Assert.AreEqual(0,restored.AlphaA);Assert.AreEqual(0,restored.AlphaB);owner.Cancel();
    }
}
