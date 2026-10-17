using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SourceSpriteTransformTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceBattleFrameComposer Composer(SourcePixelAsset sprite)
    {
        var battlers=Battlers().Select(b=>b with{Visible=false}).ToArray();
        return new(new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,new ushort[1024])},{"sprite",sprite},{"b0",new(64,64,new ushort[4096])},{"b1",new(64,64,new ushort[4096])}},battlers);
    }
    private static SourceAnimationFrame Frame(SourceVisualSpriteFrame sprite)=>new(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[sprite],BattlerOamRanks=[0,1]};
    [TestMethod]
    public void AffineSpriteSamplesSignedQuarterTurnAroundOriginalFrameCenter()
    {
        var sheet=new SourcePixelAsset(16,16,Enumerable.Range(0,256).Select(i=>(ushort)(0x8000|i)).ToArray());
        var sprite=new SourceVisualSpriteFrame(4,"affine",new(32,32),default,0,true,false,false,256,"sprite",1){Width=16,Height=16,Affine=true,MatrixA=0,MatrixB=-256,MatrixC=256,MatrixD=0};
        var rgba=Composer(sheet).ComposeField(Frame(sprite));int at=(30*240+35)*4,pixel=sheet.Pixels[11*16+10];
        Assert.AreEqual((byte)((pixel&31)*255/31),rgba[at]);Assert.AreEqual((byte)(((pixel>>5)&31)*255/31),rgba[at+1]);
    }
    [TestMethod]
    public void AffineDoubleCanvasPreventsNormalOamClippingWithoutChangingSourceFrame()
    {
        var composer=Composer(new(16,16,Enumerable.Repeat((ushort)0x801f,256).ToArray()));
        var sprite=new SourceVisualSpriteFrame(4,"affine",new(32,32),default,0,true,false,false,256,"sprite",1){Width=16,Height=16,Affine=true,MatrixA=128,MatrixD=128};
        int Red(byte[] pixels)=>Enumerable.Range(0,pixels.Length/4).Count(i=>pixels[i*4]==255);
        Assert.AreEqual(256,Red(composer.ComposeField(Frame(sprite))));Assert.AreEqual(1024,Red(composer.ComposeField(Frame(sprite with{DoubleSize=true}))));
    }
    [TestMethod]
    public void PaletteInversionTogglesFadedColorsAndLaterPaletteWriteReplacesIt()
    {
        var script=new SourceAnimationScript(9,"invert",[
            new("createvisualtask",["AnimTask_BlendBattleAnimPal","2","F_PAL_BG | F_PAL_ATTACKER","0","8","8","RGB_BLACK"],1),
            new("createvisualtask",["AnimTask_InvertScreenColor","2","257","257","0"],2),new("delay",["2"],3),
            new("createvisualtask",["AnimTask_BlendBattleAnimPal","2","F_PAL_ATTACKER","0","0","0","RGB_BLACK"],4),new("end",[],5)]);
        var runtime=new SourceBattleAnimation(script,new("invert",1,9,new(0),new(1)),Battlers(),false,new Audio());
        var first=runtime.Tick();CollectionAssert.AreEqual(new[]{1,2,3,16},first.InvertedPalettes);
        ushort red=0x801f;Assert.AreEqual((ushort)(0x8000|16|31<<5|31<<10),SourcePaletteMath.Apply(red,first.PaletteBlends.Single(p=>p.Palette==16),true));
        int count=0;while(runtime.Active&&count++<10)runtime.Tick();Assert.IsFalse(runtime.Active);CollectionAssert.AreEqual(new[]{1,2,3},runtime.Snapshot().InvertedPalettes);
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().InvertedPalettes.Length);
    }
    [TestMethod]
    public void ScreenInversionUsesFlagBitRatherThanAnyNonzeroOperand()
    {
        var script=new SourceAnimationScript(9,"invert",[new("createvisualtask",["AnimTask_InvertScreenColor","2","1","1","1"],1),new("end",[],2)]);
        var runtime=new SourceBattleAnimation(script,new("invert",1,9,new(0),new(1)),Battlers(),false,new Audio());Assert.AreEqual(0,runtime.Tick().InvertedPalettes.Length);
    }
    [TestMethod]
    public void AffineDoubleHeightChangesSourceOamTieOrdering()
    {
        var source=AnimationCompositionFixture.Script(55);
        const string template="gWaterGunDropletSpriteTemplate";
        foreach(bool doubled in new[]{false,true})
        {
            var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=recipes[template] with{AffineDoubleSize=doubled};
            var script=source with{SpriteRecipes=recipes,Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_BUBBLES"],1),new("createsprite",[template,"ANIM_TARGET","0","0","-20","0","0","55"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
            var runtime=new SourceBattleAnimation(script,new("oam",1,55,new(0),new(1)),Battlers(),false,new Audio());
            SourceAnimationFrame frame;int ticks=0;do{frame=runtime.Tick();}while(frame.Sprites.Length==0&&++ticks<8);
            var sprite=frame.Sprites.Single();Assert.AreEqual(16,sprite.Height);Assert.IsTrue(sprite.Affine);
            Assert.AreEqual(doubled,sprite.OamRank>frame.BattlerOamRanks[1]);runtime.Cancel();
        }
    }

}
