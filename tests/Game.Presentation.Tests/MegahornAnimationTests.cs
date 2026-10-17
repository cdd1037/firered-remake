using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class MegahornAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Root=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    [TestMethod]
    public void MegahornSelectsTargetSideMutatesArgumentsAndRetainsLinearCompletionHandoff()
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(224).Script;
        for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)
        {
            var b=Battlers(true);var script=source with{Labels=new Dictionary<string,int>(),Commands=[C("loadspritegfx","ANIM_TAG_HORN_HIT_2"),C("createsprite","gMegahornHornSpriteTemplate","ANIM_ATTACKER","3","-42","25","0","0","6"),C("waitforvisualfinish"),C("end")]};
            var runtime=new SourceBattleAnimation(script,new("megahorn-vector",1,224,new(actor),new(target)),b,true,new Audio());bool player=(target&1)==0;var center=b[target].Center(true,true);var origin=new PixelPoint(center.X+(player?42:-42),center.Y+(player?-25:25));int seen=0,ticks=0;int[] ys=player?[4,8,12,16,20,24,24]:[-4,-8,-12,-16,-20,-25,-25];
            while(runtime.Active&&ticks++<32)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var sprite=frame.Sprites.Single();Assert.AreEqual(origin,sprite.Position);Assert.AreEqual(new PixelPoint((player?-1:1)*7*Math.Min(seen+1,6),ys[seen]),sprite.Offset);Assert.AreEqual((short)(player?42:-42),frame.AnimationArguments[0]);Assert.AreEqual((short)(player?-25:25),frame.AnimationArguments[1]);Assert.IsTrue(sprite.Affine&&sprite.DoubleSize);Assert.AreEqual(player?new SourceAffineMatrix(-194,168,-168,-194):new SourceAffineMatrix(189,-171,171,189),new SourceAffineMatrix(sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD));seen++;
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(7,seen,"Six source linear steps, then one callback-handoff frame before destruction.");
        }
    }
    [TestMethod]
    public void MegahornArgumentNegationWrapsAsSignedSixteenBits()
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(224).Script;var b=Battlers(false);
        var script=source with{Labels=new Dictionary<string,int>(),Commands=[C("loadspritegfx","ANIM_TAG_HORN_HIT_2"),C("createsprite","gMegahornHornSpriteTemplate","ANIM_ATTACKER","3","-32768","32767","-1","1","6"),C("waitforvisualfinish"),C("end")]};
        var runtime=new SourceBattleAnimation(script,new("megahorn-signed",1,224,new(1),new(0)),b,false,new Audio());SourceAnimationFrame frame=runtime.Tick();for(int tick=0;frame.Sprites.Length==0&&tick<8;tick++)frame=runtime.Tick();
        Assert.AreEqual(1,frame.Sprites.Length);CollectionAssert.AreEqual(new short[]{-32768,-32767,1,-1,6},frame.AnimationArguments.Take(5).ToArray());Assert.AreEqual(new PixelPoint(-32696,-32671),frame.Sprites.Single().Position);
        while(runtime.Active)runtime.Tick();
    }
    [TestMethod]
    public void MegahornRootComposesAllActorPositionsAndRestoresDrillBackground()
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(224);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("megahorn-root",1,224,new(actor),new(actor^1)),b,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=new SourceBattleFrameComposer(assets,b,resolved.StatMask,resolved.Metallic,surface,resolved.Backgrounds);int ticks=0;bool drilled=false,horn=false;
            while(runtime.Active&&ticks++<2048){var frame=runtime.Tick();horn|=frame.Sprites.Any(s=>s.Template=="gMegahornHornSpriteTemplate");drilled|=frame.MoveBackground?.Id==7;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(drilled&&horn);var final=runtime.Snapshot();Assert.IsNull(final.MoveBackground?.Id);Assert.AreEqual(0,final.MoveBackground!.ScrollX);Assert.AreEqual(0,final.MoveBackground.ScrollY);Assert.AreEqual(0,final.Sprites.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));
        }
    }
}
