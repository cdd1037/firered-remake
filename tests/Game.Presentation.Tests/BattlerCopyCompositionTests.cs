using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BattlerCopyCompositionTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)
        {DisplayCenter=new(35+i*55,50)}).ToArray();
    private static Dictionary<string,SourcePixelAsset> Assets()
    {
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0x8421,240*112).ToArray())},
            {"impact",new(32,32,new ushort[1024])}};
        foreach(var b in Battlers())assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%7==0?0:0x8000|(i%32)|(((i/64)%32)<<5)|0x4000)).ToArray());
        return assets;
    }
    private static ushort Pixel(byte[] rgba,int x,int y)
    {int i=(y*240+x)*4;return (ushort)(0x8000|((rgba[i]*31+127)/255)|(((rgba[i+1]*31+127)/255)<<5)|(((rgba[i+2]*31+127)/255)<<10));}
    [TestMethod]
    public void CopiedObjOwnsItsPaletteIndependentlyOfTheSourceAndSnapshots()
    {
        var assets=Assets();var battlers=Battlers().Select(b=>b with{Visible=false}).ToArray();
        var composer=new SourceBattleFrameComposer(assets,battlers);
        var copy=new SourceVisualSpriteFrame(4,"copy",new(100,50),default,0,true,false,false,256,"b0",2)
            {Width=64,Height=64,BattlerCopySlot=0,BattlerCopyPalette=new(0,11,0),PaletteSlot=4};
        var frame=new SourceAnimationFrame(1,true,new PixelPoint[4],[],0,0,[],[])
            {Sprites=[copy],PaletteBlends=[new(16,16,0x7fff)],GreyscaleSlots=[0],InvertedPalettes=[16]};
        var saved=composer.ComposeField(frame);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            ushort source=assets["b0"].Pixels[y*64+x];
            ushort expected=(source&0x8000)==0?(ushort)0x8421:SourcePaletteMath.Blend(source,11,0);
            Assert.AreEqual(expected,Pixel(saved,68+x,18+y));
        }
        var changed=composer.ComposeField(frame with{Sprites=[copy with{BattlerCopyPalette=new(0,0,0)}]});
        Assert.IsFalse(saved.SequenceEqual(changed));CollectionAssert.AreEqual(saved,composer.ComposeField(frame));
    }
    [TestMethod]
    [DataRow(1)] [DataRow(2)]
    public void BackgroundEnableControlsTheWholeRankAndLeavesTheOtherRank(int rank)
    {
        var battlers=Battlers();var assets=Assets();var composer=new SourceBattleFrameComposer(assets,battlers);
        var frame=new SourceAnimationFrame(1,true,new PixelPoint[4],[0,1,2,3],0,0,[],[]){Doubles=true};
        var full=composer.ComposeField(frame);bool[] enabled=[true,true];enabled[rank-1]=false;
        var hidden=composer.ComposeField(frame with{BattlerBackgroundEnabled=enabled});
        foreach(var b in battlers)
        {
            int x=b.DisplayCenter!.Value.X,y=50;
            Assert.AreEqual(b.BackgroundRank==rank?(ushort)0x8421:Pixel(full,x,y),Pixel(hidden,x,y));
        }
        CollectionAssert.AreEqual(full,composer.ComposeField(frame));
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(frame with{BattlerBackgroundEnabled=[]}));
    }
    [TestMethod]
    public void StaticMonbgRetainsObjectsAndClearsAtTheSecondTaskPass()
    {
        var script=new SourceAnimationScript(102,"static-monbg",[
            new("monbg_static",["ANIM_TARGET"],1),new("delay",["3"],2),
            new("clearmonbg_static",["ANIM_TARGET"],3),new("end",[],4)]);
        var runtime=new SourceBattleAnimation(script,new("static",1,102,new(0),new(1)),Battlers(),true,new Audio());
        var first=runtime.Tick();CollectionAssert.AreEquivalent(new[]{1,3},first.BackgroundSlots);
        Assert.IsTrue(first.BattlerVisibility.All(v=>!v.SpriteInvisible&&!v.OamInvisible));
        Assert.AreEqual(2,first.StaticBackgroundCopies.Length);
        SourceAnimationFrame? clearing=null;
        for(int i=0;i<12&&runtime.Active;i++)
        {
            var frame=runtime.Tick();
            if(clearing is null&&frame.Trace.Any(t=>t.EndsWith(":clearmonbg_static",StringComparison.Ordinal)))
            {clearing=frame;Assert.AreEqual(2,frame.BackgroundSlots.Length);}
            else if(clearing is not null){Assert.AreEqual(0,frame.BackgroundSlots.Length);break;}
        }
        Assert.IsNotNull(clearing);
        runtime.Cancel();Assert.IsTrue(runtime.Snapshot().BattlerBackgroundEnabled.All(v=>v));
    }
}
