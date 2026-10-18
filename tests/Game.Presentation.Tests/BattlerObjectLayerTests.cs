using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class BattlerObjectLayerTests
{
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourcePixelAsset Solid(int size,ushort color)=>new(size,size,Enumerable.Repeat(color,size*size).ToArray());
    private static (SourceBattleFrameComposer Composer,SourceAnimationFrame Frame) Scene()
    {
        var battlers=new[]{new AnimationBattler(new(0),"b0",8,0,64,64){Visible=false},new AnimationBattler(new(1),"b1",8,0,64,64){DisplayCenter=new(80,50)}};
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray())},{"impact",Solid(32,0x83e0)},{"b0",Solid(64,0x83e0)},{"b1",Solid(64,0x801f)}};
        return(new(assets,battlers),new(0,true,new PixelPoint[2],[1],8,8,[],[]){BlendEnabled=true,BattlerBackgroundPriorities=[2,2],BattlerOamRanks=[1,0],ObjectOffsets=[default,new(32,0)],BattlerAffine=[new(false,256,256),new(true,512,512)],RestoredObjectSlots=[1],BlendedObjectSlots=[1]});
    }
    private static void Pixel(byte[] rgba,int x,int y,int r,int g,int b)
    {int at=(y*240+x)*4;Assert.AreEqual((byte)r,rgba[at]);Assert.AreEqual((byte)g,rgba[at+1]);Assert.AreEqual((byte)b,rgba[at+2]);}
    [TestMethod,DataRow(12,18),DataRow(31,31),DataRow(17,4)]
    public void AlphaRegisterRetainsFiveBitOperandsWhileHardwarePixelsSaturateAtSixteen(int a,int b)
    {
        // Cmd_setalpha copies both script bytes into BLDALPHA. Its five-bit
        // coefficients retain 18 in Overheat; pixel blending saturates at 16.
        var script=new SourceAnimationScript(315,"alpha-register",[new("setalpha",[a.ToString(),b.ToString()],1),new("delay",["2"],2),new("end",[],3)]);
        var bodies=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64));
        var runtime=new SourceBattleAnimation(script,new("alpha",1,315,new(0),new(1)),bodies,false,new Audio());
        var sampled=runtime.Tick();Assert.AreEqual(a,sampled.AlphaA);Assert.AreEqual(b,sampled.AlphaB);
        var(composer,frame)=Scene();
        CollectionAssert.AreEqual(composer.ComposeField(frame with{AlphaA=Math.Min(a,16),AlphaB=Math.Min(b,16)}),composer.ComposeField(frame with{AlphaA=sampled.AlphaA,AlphaB=sampled.AlphaB}));
        runtime.Cancel();Assert.IsFalse(runtime.Snapshot().BlendEnabled);
    }
    [TestMethod]
    public void AlphaOperandsOutsideHardwareCoefficientFieldsFailBeforePlayback()
    {
        foreach(int value in new[]{-1,32,255})foreach(int index in new[]{0,1})
        {
            string[] args=["12","18"];args[index]=value.ToString();
            var script=new SourceAnimationScript(315,"alpha-register",[new("setalpha",args,1),new("end",[],2)]);
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("alpha",1,315,new(0),new(1)),Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)),false,new Audio()));
        }
    }
    [TestMethod]
    public void MonBackgroundRemainsUnscaledWhenDistinctBlendObjectMovesAcrossTerrain()
    {
        var (composer,frame)=Scene();var image=composer.ComposeField(frame);
        Pixel(image,60,50,255,0,0); // Unscaled BG copy survives outside the affine OBJ.
        Pixel(image,120,50,123,0,123); // OBJ blends with actual blue BG3.
        Pixel(image,100,50,255,0,0); // OBJ and its independent red BG copy overlap.
    }
    [TestMethod]
    public void FrontmostObjectBlendsWithBackgroundRatherThanLowerObject()
    {
        var (composer,frame)=Scene();frame=frame with{Sprites=[new(4,"test",new(120,50),default,0,true,false,false,256,"impact",1){OamRank=1}]};
        Pixel(composer.ComposeField(frame),120,50,123,0,123);
    }
    [TestMethod]
    public void HardwarePriorityPrecedesOamOrderAndPromotedBackground()
    {
        var (composer,frame)=Scene();frame=frame with{BattlerBackgroundPriorities=[1,2],Sprites=[new(4,"test",new(80,50),default,0,true,false,false,256,"impact",1){OamRank=0,OamPriority=2}]};
        Pixel(composer.ComposeField(frame),80,50,255,0,0);
        Pixel(composer.ComposeField(frame with{Sprites=[frame.Sprites[0] with{OamPriority=0}]}),80,50,0,255,0);
    }
    [TestMethod]
    public void ScalePreparationRestoresObjectAndModeResetDoesNotHideItAgain()
    {
        var script=new SourceAnimationScript(93,"scale-mode",[new("monbg",["ANIM_TARGET"],1),new("setalpha",["8","8"],2),new("createvisualtask",["AnimTask_ScaleMonAndRestore","5","3","3","1","ANIM_TARGET","1"],3),new("waitforvisualfinish",[],4),new("delay",["3"],5),new("clearmonbg",["ANIM_TARGET"],6),new("end",[],7)]);
        var runtime=new SourceBattleAnimation(script,new("scale",1,93,new(0),new(1)),Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)),false,new Audio());
        var first=runtime.Tick();CollectionAssert.AreEqual(new[]{1},first.RestoredObjectSlots);CollectionAssert.AreEqual(new[]{1},first.BlendedObjectSlots);Assert.AreEqual(259,first.BattlerAffine[1].XScale);
        runtime.Tick();var reset=runtime.Tick();CollectionAssert.AreEqual(new[]{1},reset.RestoredObjectSlots);Assert.AreEqual(0,reset.BlendedObjectSlots.Length);Assert.IsTrue(reset.BackgroundSlots.Contains(1));
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().RestoredObjectSlots.Length);
    }
    [TestMethod]
    public void StaticBackgroundCopyRetainsPositionAndPaletteWhileObjectStateChanges()
    {
        var (composer,frame)=Scene();
        frame=frame with{RestoredObjectSlots=[],BlendedObjectSlots=[],Offsets=[default,new(100,0)],PaletteBlends=[new(17,16,0x03e0)],StaticBackgroundCopies=[new(1,default,null,false,false)]};
        var pixels=composer.ComposeField(frame);Pixel(pixels,60,50,255,0,0);Pixel(pixels,180,50,0,0,255);
        frame=frame with{StaticBackgroundCopies=[]};pixels=composer.ComposeField(frame);Pixel(pixels,60,50,0,0,255);Pixel(pixels,180,50,0,255,0);
    }

}
