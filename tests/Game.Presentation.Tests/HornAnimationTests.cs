using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class HornAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Root=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript source,int x,int y,int duration)=>source with{Commands=[C("loadspritegfx","ANIM_TAG_HORN_HIT"),C("createsprite","gHornHitSpriteTemplate","ANIM_ATTACKER","4",x.ToString(),y.ToString(),duration.ToString()),C("waitforvisualfinish"),C("end")]};
    [TestMethod][DataRow(0)][DataRow(1)]
    public void HornUsesTargetPictureCenterUnmirroredOffsetsAndPenultimateSnap(int actor)
    {
        using var resources=new SourceAnimationResources(Root);var script=Primitive(resources.Resolve(30).Script,8,-8,3);var runtime=new SourceBattleAnimation(script,new("horn-vector",1,30,new(actor),new(actor^1)),Battlers(false),false,new Audio());
        PixelPoint[] expected=actor==0?[new(157,53),new(184,40)]:[new(106,74),new(80,88)];int seen=0,ticks=0;
        while(runtime.Active&&ticks++<32){var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var sprite=frame.Sprites.Single();Assert.AreEqual(expected[seen++],sprite.Position);Assert.AreEqual(actor==1,sprite.HFlip);Assert.AreEqual(actor==1,sprite.VFlip);Assert.AreEqual((short)3,frame.AnimationArguments[2]);}
        Assert.IsFalse(runtime.Active);Assert.AreEqual(2,seen);
    }
    [TestMethod]
    public void HornKeepsSignedSixteenBitSevenFractionalBitAccumulators()
    {
        using var resources=new SourceAnimationResources(Root);var runtime=new SourceBattleAnimation(Primitive(resources.Resolve(30).Script,120,0,10),new("horn-wrap",1,30,new(0),new(1)),Battlers(false),false,new Audio());
        int[] xs=[-252,-248,-244,-240,-236,-232,-228,-224,296],ys=[66,64,62,60,58,56,54,52,48];int seen=0,ticks=0;
        while(runtime.Active&&ticks++<32){var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;Assert.AreEqual(new PixelPoint(xs[seen],ys[seen]),frame.Sprites.Single().Position);seen++;}
        Assert.IsFalse(runtime.Active);Assert.AreEqual(9,seen);
    }
    [TestMethod][DataRow(-32768,2)][DataRow(0,2)][DataRow(32767,127)]
    public void HornClampsAndMutatesSharedDurationBeforeSourceStepping(int requested,int bounded)
    {
        using var resources=new SourceAnimationResources(Root);var runtime=new SourceBattleAnimation(Primitive(resources.Resolve(30).Script,0,0,requested),new("horn-duration",1,30,new(0),new(1)),Battlers(false),false,new Audio());int seen=0,ticks=0;
        while(runtime.Active&&ticks++<160){var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;seen++;Assert.AreEqual((short)bounded,frame.AnimationArguments[2]);}
        Assert.IsFalse(runtime.Active);Assert.AreEqual(bounded-1,seen);
    }
    [TestMethod][DataRow(30,0)][DataRow(31,0)][DataRow(31,1)][DataRow(32,0)]
    public void CompleteHornRootsComposeAllActorPositionsAndSourceTurnBranches(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("horn-root",1,move,new(actor),new(actor^1)){MoveTurn=(byte)turn},b,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=new SourceBattleFrameComposer(assets,b,resolved.StatMask,resolved.Metallic,surface,resolved.Backgrounds);int ticks=0,visible=0;bool drilled=false;
            while(runtime.Active&&ticks++<2048){var frame=runtime.Tick();visible+=frame.Sprites.Count(s=>s.Visible);drilled|=frame.MoveBackground?.Id==7;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsGreaterThan(0,visible);Assert.AreEqual(move==32,drilled);Assert.IsNull(runtime.Snapshot().MoveBackground?.Id);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        }
    }
}
