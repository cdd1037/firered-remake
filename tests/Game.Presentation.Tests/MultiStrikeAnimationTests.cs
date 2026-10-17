using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class MultiStrikeAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static string Template(int move)=>move==154?"gFurySwipesSpriteTemplate":"gArmThrustHandSpriteTemplate";
    private static string Tag(int move)=>move==154?"ANIM_TAG_SWIPE":"ANIM_TAG_HANDS_AND_FEET";
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target,byte? turn)=>new("multi-strike",1,move,new(actor),new(target)){MoveTurn=turn};
    private static SourceAnimationScript Isolated(SourceAnimationScript source,int[] args)=>source with{Labels=new Dictionary<string,int>(),Commands=[
        new("loadspritegfx",[Tag(source.MoveId)],1),new("setarg",["3","333"],2),new("setarg",["7","777"],3),
        new("createsprite",new[]{Template(source.MoveId),"ANIM_ATTACKER","2"}.Concat(args.Select(a=>a.ToString(CultureInfo.InvariantCulture))).ToArray(),4),new("waitforvisualfinish",[],5),new("end",[],6)]};
    [TestMethod][DataRow(154)][DataRow(292)]
    public void SourceTargetParityFrameSelectionAndDirectLifetimesMatchExtractedCallbacks(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/multi-strike-source-vectors.json")));
        var configs=vectors.RootElement.GetProperty("configs").EnumerateArray().Select(r=>r.EnumerateArray().Select(n=>n.GetInt32()).ToArray()).ToArray();
        for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)for(int config=0;config<configs.Length;config++)
        {
            var c=configs[config];if(c[0]!=(move==154?1:0)||c[4]==32767&&(actor!=0||target!=1))continue;
            int[] args=move==154?[c[2],c[3],c[5]]:[c[2],c[3],c[4],c[5]];
            var runtime=new SourceBattleAnimation(Isolated(source,args),Cue(move,actor,target,(byte)c[1]),Battlers(true),true,new Audio());
            // loadspritegfx yields its source three ticks before creation, including
            // the zero-duration controller that can destroy on its first update.
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            int tick=-1,visible=0;SourceAnimationFrame? frame=null;
            foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty($"{target}:{config}").EnumerateArray())
            {
                int[] v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();if(v[0]<0)continue;
                while(tick<v[0]){frame=runtime.Tick();tick++;visible+=frame.Sprites.Length;}
                Assert.IsNotNull(frame);string message=$"{move}/{actor}/{target}/{config}/{tick}";
                CollectionAssert.AreEqual(args,frame.AnimationArguments.Take(args.Length).Select(n=>(int)n).ToArray(),message);Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);if(move==154)Assert.AreEqual(333,(int)frame.AnimationArguments[3]);
                if(v[1]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[2],v[3]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[4],v[5]),sprite.Offset,message);Assert.AreEqual(v[6],sprite.Tile,message);Assert.AreEqual(v[7]!=0,sprite.HFlip,message);Assert.IsFalse(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);
            }
            Assert.AreEqual(move==154?16:c[4],visible);while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod][DataRow(154)][DataRow(292)]
    public void EveryCommittedOrdinalRunsItsAuthoredRootForAllPositionsAndSelectedAllies(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})for(byte turn=0;turn<5;turn++)
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target,turn),battlers,doubles,audio);
            int ticks=0,objects=0;bool pixels=false,hit=false;var positions=new HashSet<int>();
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);objects+=frame.Sprites.Count(s=>s.Template==Template(move));
                if(frame.Sprites.Any(s=>s.Template==Template(move)))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Template(move)).ToArray()}));
                foreach(var sprite in frame.Sprites.Where(s=>s.Template==Template(move)))
                {
                    if(move==292){int parity=(turn+((target&1)==0?1:0))&1;Assert.AreEqual(new PixelPoint(parity==0?10:-10,-8),sprite.Offset);Assert.AreEqual(parity!=0,sprite.HFlip);}
                    else positions.Add(sprite.Position.X-battlers[target].Center(doubles,true).X);
                }
                foreach(var sprite in frame.Sprites.Where(s=>s.Asset=="impact")){hit=true;Assert.AreEqual(battlers[target].Center(doubles,true).X+Math.Sign(battlers[target].Base(doubles).X-battlers[actor].Base(doubles).X)*((turn&1)==0?8:-8),sprite.Position.X);}
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(pixels);Assert.AreEqual(move==154?32:14,objects);Assert.AreEqual(move==292,hit);if(move==154)CollectionAssert.AreEquivalent(new[]{-16,16},positions.ToArray());
            Assert.AreEqual(2,audio.Cues.Count);Assert.AreEqual(move==154?2:0,audio.Cues.Count(s=>s.Id=="SE_M_SCRATCH"));var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsFalse(final.BlendEnabled);
        }
    }
    [TestMethod][DataRow(154)][DataRow(292)]
    public void CancellationAndCompletedDrainReleaseTheirOwnSpritesAndTransforms(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];var baseline=new SourceBattleAnimation(source,Cue(move,3,1,1),Battlers(true),true,new Audio());int duration=0;while(baseline.Active&&duration<400){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,15,25,duration-1,duration,duration+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,Cue(move,3,1,1),Battlers(true),true,new Audio());int reached=0;while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(duration,requested),reached);Assert.AreEqual(requested<duration,runtime.Active);runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsFalse(frame.BlendEnabled);
        }
    }
    [TestMethod]
    public void UnusedArmHelpersNeedNoHitContextButRetainStaticValidation()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[292];
        var create=Isolated(source,[10,-8,14,3]).Commands[3];
        var helpers=source.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);
        helpers["ArmHit"]=[create,new("return",[],2)];
        var unused=source with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=helpers};
        var runtime=new SourceBattleAnimation(unused,Cue(292,0,1,null),Battlers(false),false,new Audio());
        runtime.Tick();Assert.IsFalse(runtime.Active);
        var called=unused with{Commands=[new("loadspritegfx",[Tag(292)],1),new("call",["ArmHit"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(called,Cue(292,0,1,null),Battlers(false),false,new Audio()));
        var known=new SourceBattleAnimation(called,Cue(292,0,1,0),Battlers(false),false,new Audio());
        int visible=0,ticks=0;while(known.Active&&ticks++<40)visible+=known.Tick().Sprites.Length;
        Assert.IsFalse(known.Active);Assert.AreEqual(14,visible);
        foreach(int operand in new[]{5,6})
        {
            var args=(string[])create.Args.Clone();args[operand]="-1";
            var malformed=helpers.ToDictionary(p=>p.Key,p=>p.Value);
            malformed["ArmHit"]=[create with{Args=args},new("return",[],2)];
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{Subroutines=malformed},Cue(292,0,1,null),Battlers(false),false,new Audio()));
        }
    }
    [TestMethod][DataRow(154)][DataRow(292)]
    public void OperandWidthsFramesAndRequiredHitContextFailClosed(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];int[] valid=move==154?[16,0,1]:[10,-8,14,3];var script=Isolated(source,valid);
        void Reject(SourceAnimationScript bad,byte? turn=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(move,0,1,turn),Battlers(false),false,new Audio()));
        foreach(int bad in new[]{-129,128}){var args=(int[])valid.Clone();args[0]=bad;Reject(Isolated(source,args));}
        foreach(int bad in new[]{-1,move==154?2:4}){var args=(int[])valid.Clone();args[^1]=bad;Reject(Isolated(source,args));}
        if(move==292){Reject(script,null);foreach(int bad in new[]{-1,32768}){var args=(int[])valid.Clone();args[2]=bad;Reject(Isolated(source,args));}}
        foreach(var bad in new[]{source.SpriteRecipes[Template(move)] with{Width=16},source.SpriteRecipes[Template(move)] with{Blend=true},source.SpriteRecipes[Template(move)] with{AffineDoubleSize=true},source.SpriteRecipes[Template(move)] with{Animations=[]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template(move)]=bad;Reject(script with{SpriteRecipes=recipes});}
    }
}
