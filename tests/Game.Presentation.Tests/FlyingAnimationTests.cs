using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class FlyingAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Root=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    public void EllipticalGustImmediatelyStepsAndRetiresOnItsSeventyFirstUpdate()
    {
        using var resources=new SourceAnimationResources(Root);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);int target=actor^1;var source=resources.Resolve(16).Script;
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_GUST"],1),new("createsprite",["gEllipticalGustSpriteTemplate","ANIM_ATTACKER","2","0","-16"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
            var runtime=new SourceBattleAnimation(script,new("gust-vector",1,16,new(actor),new(target)),b,doubles,new Audio());int observed=0,ticks=0;var center=b[target].Base(doubles);
            while(runtime.Active&&ticks++<128)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var sprite=frame.Sprites.Single();int angle=(196+5*observed++)&255;
                Assert.AreEqual(new PixelPoint(center.X,center.Y+4),sprite.Position);Assert.AreEqual(new PixelPoint(source.SineTable[angle]*32>>8,source.SineTable[angle+64]*8>>8),sprite.Offset);
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(69,observed);
        }
    }
    [TestMethod]
    public void GustPaletteRotatesOnlyLoadedFadedEntriesOneThroughEightEverySecondTaskTick()
    {
        using var resources=new SourceAnimationResources(Root);var source=resources.Resolve(16).Script;int tag=source.SpriteTags["ANIM_TAG_GUST"];
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_GUST"],1),new("createvisualtask",["AnimTask_AnimateGustTornadoPalette","5","1","6"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
        var runtime=new SourceBattleAnimation(script,new("gust-palette",1,16,new(0),new(1)),Battlers(false),false,new Audio());var original=source.SpritePalettes[tag];
        for(int i=0;i<3;i++)runtime.Tick();
        for(int tick=1;tick<=6;tick++)
        {
            var frame=runtime.Tick();var palette=frame.SpritePalettes[tag];int rotations=tick/2;
            for(int index=0;index<16;index++)Assert.AreEqual(original[index is >=1 and <=8?1+(index-1-rotations+8)%8:index],palette[index]);
        }
        while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.Snapshot().SpritePalettes.Count);
    }
    [TestMethod]
    public void GustProjectileWaitsForAffineEndAndSeparateCallbackHandoffBeforeTranslation()
    {
        using var resources=new SourceAnimationResources(Root);
        for(int actor=0;actor<2;actor++)
        {
            var source=resources.Resolve(17).Script;var b=Battlers(false);int target=actor^1;
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_GUST"],1),new("createsprite",["gGustToTargetSpriteTemplate","ANIM_ATTACKER","2","0","0","13","0","20"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
            var runtime=new SourceBattleAnimation(script,new("gust-flight",1,17,new(actor),new(target)),b,false,new Audio());int observed=0,ticks=0;
            var origin=b[actor].Center(false,true);var destination=b[target].Center(false,true);int dx=destination.X+(actor==0?13:-13)-origin.X,dy=destination.Y-origin.Y;
            int vx=((Math.Abs(dx)<<8)/20&~1)|(dx<0?1:0),vy=((Math.Abs(dy)<<8)/20&~1)|(dy<0?1:0);
            while(runtime.Active&&ticks++<128)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var sprite=frame.Sprites.Single();int index=observed++;
                Assert.AreEqual(origin,sprite.Position);Assert.IsTrue(sprite.Affine);Assert.AreEqual((short)(actor==0?13:-13),frame.AnimationArguments[2]);
                if(index<26)Assert.AreEqual(default(PixelPoint),sprite.Offset);
                else{int count=index-25;Assert.AreEqual(new PixelPoint((((vx*count)&65535)>>8)*(dx<0?-1:1),(((vy*count)&65535)>>8)*(dy<0?-1:1)),sprite.Offset);}
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(46,observed);
        }
    }
    [TestMethod][DataRow(16)][DataRow(17)][DataRow(211)]
    public void CompleteGustRootsComposeForAllSixActorPositions(int move)
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x03e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("gust-root",1,move,new(actor),new(actor^1)),b,doubles,new Audio());var composer=new SourceBattleFrameComposer(assets,b,resolved.StatMask,resolved.Metallic);int ticks=0,visible=0;
            while(runtime.Active&&ticks++<1024){var frame=runtime.Tick();visible+=frame.Sprites.Count(s=>s.Visible);Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsGreaterThan(0,visible);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        }
    }
}
