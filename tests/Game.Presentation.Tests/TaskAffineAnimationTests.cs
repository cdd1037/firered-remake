using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class TaskAffineAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Cues{get;}=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Cues.Add(cue);
        public void StopAnimationEffects(){}
    }
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("task-affine-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_task_affine_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/task-affine-vectors.json");
    private static SourceAnimationScript Script(int move)
    =>AnimationCompositionFixture.Script(move);
    private static AnimationBattler[] Battlers(bool doubles,int yOffset=12)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,yOffset,0,64,64)).ToArray();
    private static SourceTaskAffineCommand[] Program(string name)=>name switch
    {
        "absolute"=>[new("frame",512,128,255,0),new("frame",-1,2,255,2),new("end")],
        "consecutive"=>[new("frame",400,120,1,0),new("frame",200,100,2,0),new("frame",1,-1,3,1),new("end")],
        "marked"=>[new("frame",2,3,0,2),new("loop"),new("frame",-3,4,0,2),new("loop",Count:3),new("end")],
        "unmarked"=>[new("frame",1,-1,0,1),new("loop",Count:2),new("end")],
        "jumps"=>[new("jump",Target:2),new("frame",100,100,0,2),new("frame",-4,2,0,1),new("jump",Target:4),new("end")],
        "overflow"=>[new("frame",32760,-32760,250,0),new("frame",20,-20,255,2),new("end")],
        _=>throw new ArgumentOutOfRangeException(nameof(name))
    };
    [TestMethod]
    [DataRow("absolute")][DataRow("consecutive")][DataRow("marked")][DataRow("unmarked")][DataRow("jumps")][DataRow("overflow")]
    public void TaskAffineControlFlowMatchesExtractedSourceVectors(string name)
    {
        using var vectors=Vectors();var program=new SourceTaskAffineProgram(Program(name));
        Assert.AreEqual(256,program.XScale);Assert.AreEqual(256,program.YScale);Assert.AreEqual(0,program.CommandIndex);
        foreach(var row in vectors.RootElement.GetProperty("programs").GetProperty(name).EnumerateArray())
        {
            var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();bool running=program.Run();
            CollectionAssert.AreEqual(v.Skip(1).ToArray(),new[]{running?1:0,program.CommandIndex,program.FrameTicks,program.LoopRemaining,program.XScale,program.YScale,program.Rotation,program.AppliedFrame?1:0},$"{name} tick {v[0]}");
        }
        Assert.IsTrue(program.Ended);Assert.IsFalse(program.Run());Assert.AreEqual(new SourceAffineMatrix(256,0,0,256),program.Matrix);
        program.Prepare();Assert.AreEqual(0,program.CommandIndex);Assert.AreEqual(0,program.LoopRemaining);Assert.IsFalse(program.Ended);Assert.AreEqual(256,program.XScale);
    }
    [TestMethod]
    public void TaskAffineUsesDirectHardwareScaleAndByteRotationWithoutSpriteReciprocals()
    {
        var program=new SourceTaskAffineProgram([new("frame",0,0,255,2),new("end")]);
        Assert.IsTrue(program.Run());Assert.AreEqual(new SourceAffineMatrix(256,0,0,256),program.Matrix);
        Assert.IsTrue(program.Run());Assert.AreEqual(new SourceAffineMatrix(255,-6,6,255),program.Matrix);
        var scale=new SourceTaskAffineProgram([new("frame",-128,256,0,1),new("end")]);scale.Run();
        Assert.AreEqual(new SourceAffineMatrix(128,0,0,512),scale.Matrix);
        Assert.AreEqual(12,SourceTaskAffineProgram.FootOffset(512,8));
        Assert.AreEqual(-40,SourceTaskAffineProgram.FootOffset(1,8)); // Upper clipping to 128 pixels.
        Assert.AreEqual(24,SourceTaskAffineProgram.FootOffset(0,8)); // Source SAFE_DIV/ARM zero divisor.
        Assert.AreEqual(48,SourceTaskAffineProgram.FootOffset(-256,8));
        Assert.AreEqual(0,SourceTaskAffineProgram.FootOffset(250,12)); // Signed integer division truncates toward zero.
    }
    [TestMethod]
    [DataRow(0)][DataRow(12)]
    public void SplashEveryHopMatchesSourceScaleFootAnchorPauseAndEndFrame(int yOffset)
    {
        using var vectors=Vectors();var source=Script(150);
        foreach(int actor in new[]{0,1,2,3})
        {
            var runtime=new SourceBattleAnimation(source,new("splash",1,150,new(actor),new(actor)),Battlers(true,yOffset),true,new Audio());
            foreach(var row in vectors.RootElement.GetProperty("splashes").GetProperty(yOffset.ToString()).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();var frame=runtime.Tick();var affine=frame.BattlerAffine[actor];
                Assert.AreEqual(v[1],affine.XScale,$"actor {actor}, tick {v[0]}");Assert.AreEqual(v[2],affine.YScale);
                Assert.AreEqual(v[3],frame.Offsets[actor].Y);Assert.AreEqual(v[4],runtime.ActiveVisualCount);
                Assert.IsTrue(frame.Offsets.Where((_,i)=>i!=actor).All(p=>p==default));
            }
            Assert.IsTrue(runtime.Active);var ended=runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(ended.Offsets.All(p=>p==default));
            Assert.IsTrue(ended.BattlerAffine.All(a=>!a.DoubleSize));Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod]
    [DataRow(0)][DataRow(1)]
    public void MudParticleBothCallbacksMatchSourcePositionTileBlinkAndDestruction(int mode)
    {
        using var vectors=Vectors();var source=Script(300);var battlers=Battlers(false,8);
        int x=mode==0?-4:120,y=mode==0?-16:60;
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_MUD_SAND"],1),new("createsprite",["gMudsportMudSpriteTemplate","ANIM_TARGET","2",mode.ToString(),x.ToString(),y.ToString()],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
        var runtime=new SourceBattleAnimation(script,new("mud",1,300,new(0),new(1)),battlers,false,new Audio());
        // The graphic loader yields the same three frames as all source packs.
        for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        foreach(var row in vectors.RootElement.GetProperty("mud").GetProperty(mode.ToString()).EnumerateArray())
        {
            var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();var frame=runtime.Tick();
            if(v[5]==0){Assert.AreEqual(0,frame.Sprites.Length);continue;}
            var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,$"mode {mode}, tick {v[0]}");
            Assert.AreEqual(v[3],sprite.Offset.Y);Assert.AreEqual(v[4]==0,sprite.Visible);Assert.AreEqual(1,sprite.Tile);
            Assert.AreEqual(16,sprite.Width);Assert.AreEqual(16,sprite.Height);Assert.AreEqual("mud-sand",sprite.Asset);
        }
        runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);
    }
    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void MudRisesFromActorCoordinatesWithoutMirroringOrFollowingBattlerHop(int actor)
    {
        var source=Script(300);var battlers=Battlers(true);
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_MUD_SAND"],1),new("createvisualtask",["AnimTask_Splash","2","0","1"],2),new("createsprite",["gMudsportMudSpriteTemplate","ANIM_TARGET","2","0","-4","-16"],3),new("waitforvisualfinish",[],4),new("end",[],5)]};
        var runtime=new SourceBattleAnimation(script,new("side",1,300,new(actor),new(actor^1)),battlers,true,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();
        var center=battlers[actor].Center(true,true);var first=runtime.Tick();
        Assert.AreEqual(new PixelPoint(center.X-4,center.Y-20),first.Sprites.Single().Position);
        Assert.IsTrue(first.Offsets[actor].Y>0);Assert.AreEqual(default,first.Sprites.Single().Offset);
        var second=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(center.X-5,center.Y-24),second.Position);
        runtime.Cancel();
    }
    [TestMethod]
    public void SplashPreservesHorizontalOffsetAndZeroHopsDoesNotStartAffine()
    {
        var source=Script(150);
        foreach(int hops in new[]{0,1})
        {
            var script=source with{Commands=[new("createsprite",["gSlideMonToOffsetSpriteTemplate","ANIM_ATTACKER","2","0","12","0","0","1"],1),new("waitforvisualfinish",[],2),new("createvisualtask",["AnimTask_Splash","2","0",hops.ToString()],3),new("waitforvisualfinish",[],4),new("end",[],5)]};
            var runtime=new SourceBattleAnimation(script,new("anchor",1,150,new(0),new(0)),Battlers(false),false,new Audio());
            int ticks=0;bool affine=false;
            while(runtime.Active&&ticks++<100){var frame=runtime.Tick();if(ticks>2)Assert.AreEqual(12,frame.Offsets[0].X);affine|=frame.BattlerAffine[0].DoubleSize;}
            Assert.IsFalse(runtime.Active);Assert.AreEqual(hops!=0,affine);Assert.AreEqual(new PixelPoint(12,0),runtime.Snapshot().Offsets[0]);
            runtime.Cancel();Assert.AreEqual(default,runtime.Snapshot().Offsets[0]);
        }
    }
    [TestMethod]
    [DataRow(111)][DataRow(150)][DataRow(300)][DataRow(303)][DataRow(339)]
    public void CompleteRootsResolveGeneratedAssetsAndFinishForEverySideAndTargetGroup(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        Assert.IsTrue(resolved.Script.TaskAffinePrograms.ContainsKey("sSplashEffectAffineAnimCmds"));
        foreach(bool doubles in new[]{false,true})
        for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in new[]{actor,actor^1})
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,new("complete",1,move,new(actor),new(target)),Battlers(doubles),doubles,audio);
            int ticks=0;bool deformation=false,mud=false;
            while(runtime.Active&&ticks++<500)
            {
                var frame=runtime.Tick();deformation|=frame.BattlerAffine[actor].XScale!=256;
                foreach(var sprite in frame.Sprites){Assert.IsTrue(resolved.Assets.ContainsKey(sprite.Asset!));mud=true;}
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(deformation);Assert.AreEqual(move!=150,mud);
            Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsTrue(runtime.Snapshot().BattlerAffine.All(a=>!a.DoubleSize));
            if(move==150){Assert.AreEqual(3,audio.Cues.Count);Assert.IsTrue(audio.Cues.All(c=>c.Id=="SE_M_TAIL_WHIP"));}
            else if(move==300){Assert.AreEqual(3,audio.Cues.Count(c=>c.Id=="SE_M_DIG"));Assert.AreEqual(10,audio.Cues.Count(c=>c.Id=="SE_M_BUBBLE2"));Assert.IsTrue(audio.Cues.Where(c=>c.Id=="SE_M_BUBBLE2").All(c=>c.Pan==0));}
            else Assert.AreEqual(move==303?2:3,audio.Cues.Count);
        }
    }
    [TestMethod]
    [DataRow(111,30)][DataRow(111,80)][DataRow(300,0)][DataRow(300,8)][DataRow(300,24)][DataRow(300,105)][DataRow(303,25)][DataRow(339,60)]
    public void CancellingTaskAffineFamiliesRetiresTasksSpritesAudioAndAllTransforms(int move,int frame)
    {
        var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),new("cancel",1,move,new(0),new(1)),Battlers(false),false,audio);
        for(int i=0;i<frame;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();
        var result=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,result.Sprites.Length);
        Assert.IsTrue(result.Offsets.All(p=>p==default));Assert.IsTrue(result.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.IsFalse(runtime.GraphicsLoaded);
    }
    [TestMethod]
    public void MissingAffineDataAndUnknownParticleCallbackFailClosed()
    {
        var source=Script(150) with{TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>()};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,new("bad",1,150,new(0),new(0)),Battlers(false),false,new Audio()));
        var mud=Script(300);var recipes=mud.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes["gMudsportMudSpriteTemplate"]=recipes["gMudsportMudSpriteTemplate"] with{Callback="UnimplementedMud"};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(mud with{SpriteRecipes=recipes},new("bad",1,300,new(0),new(0)),Battlers(false),false,new Audio()));
        Assert.Throws<InvalidDataException>(()=>new SourceTaskAffineProgram([new("jump",Target:2),new("end")]));
        Assert.Throws<InvalidDataException>(()=>new SourceTaskAffineProgram([new("frame",1,1)]));
    }
    [TestMethod]
    [DataRow(339,"0")][DataRow(303,"1")][DataRow(111,"2")]
    public void RecoveryAndGrowthTasksMatchExtractedScaleAndHorizontalShakeVectors(int move,string vector)
    {
        using var vectors=Vectors();var source=Script(move);
        var command=source.Commands.Single(c=>c.Op=="createvisualtask"&&c.Args[0] is "AnimTask_GrowAndShrink" or "AnimTask_SlackOffSquish" or "AnimTask_DefenseCurlDeformMon");
        var script=source with{Commands=[command,new("waitforvisualfinish",[],2),new("end",[],3)]};
        for(int actor=0;actor<4;actor++)
        {
            var runtime=new SourceBattleAnimation(script,new("deform",1,move,new(actor),new(actor)),Battlers(true),true,new Audio());
            foreach(var row in vectors.RootElement.GetProperty("deform").GetProperty(vector).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();var frame=runtime.Tick();var affine=frame.BattlerAffine[actor];
                Assert.AreEqual(v[1],affine.XScale,$"move {move} tick {v[0]}");Assert.AreEqual(v[2],affine.YScale);
                Assert.AreEqual(new PixelPoint(v[3],v[4]),frame.Offsets[actor]);Assert.AreEqual(v[5],runtime.ActiveVisualCount);
            }
            runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
        }
    }
    [TestMethod]
    [DataRow(0)][DataRow(1)]
    public void BreathPuffSharesFrameEngineAndMatchesSourceSignedFixedPointAndCallbackLifetime(int actor)
    {
        using var vectors=Vectors();var source=Script(339);var battlers=Battlers(true,8);
        var sprite=source.Commands.Single(c=>c.Op=="createsprite");
        var script=source with{Commands=[source.Commands[0],sprite,new("waitforvisualfinish",[],3),new("end",[],4)]};
        var runtime=new SourceBattleAnimation(script,new("breath",1,339,new(actor),new(actor)),battlers,true,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();var center=battlers[actor].Center(true,true);
        foreach(var row in vectors.RootElement.GetProperty("breath").GetProperty(actor.ToString()).EnumerateArray())
        {
            var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();var frame=runtime.Tick();
            if(v[5]==0){Assert.AreEqual(0,frame.Sprites.Length);continue;}
            var puff=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(center.X+v[1]-72,center.Y+v[2]-96),puff.Position);
            Assert.AreEqual(new PixelPoint(v[3],0),puff.Offset);Assert.AreEqual(actor==0,puff.HFlip);
            Assert.AreEqual(v[0]<3?0:v[0]<43?4:v[0]<47?8:12,puff.Tile,$"actor {actor}, tick {v[0]}");
            Assert.AreEqual(16,puff.Width);Assert.AreEqual(16,puff.Height);
        }
        runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);
    }

}
