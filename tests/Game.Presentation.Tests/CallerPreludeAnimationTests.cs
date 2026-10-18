using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CallerPreludeAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly string[] Templates=["gThoughtBubbleSpriteTemplate","gMetronomeFingerSpriteTemplate","gLetterZSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int,int)> Pans=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("caller-prelude-source-vectors.json", "python tests/Reference/CallerPrelude/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/caller-prelude-source-vectors.json");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private)!.SetValue(o,value);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("caller-prelude",1,move,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int visible=15,int[]? widths=null,int[]? heights=null,int[]? offsets=null)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,offsets?[i]??0,0,widths?[i]??64,heights?[i]??64){Visible=(visible&(1<<i))!=0}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript full)=>full with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceBattleAnimation Owner(SourceAnimationScript full,JsonElement c,Audio audio,SourceBattleAnimation.SpriteScene? scene=null)
    {bool doubles=c.GetProperty("doubles").GetBoolean();int[]? Get(string key)=>c.TryGetProperty(key,out var x)?N(x):null;var owner=new SourceBattleAnimation(Primitive(full),Cue(I(c,"root"),I(c,"attacker"),I(c,"target")),Battlers(doubles,c.TryGetProperty("visibleMask",out var m)?m.GetInt32():15,Get("sourceWidths"),Get("sourceHeights"),Get("sourceYOffsets")),doubles,audio,spriteScene:scene);Set(owner,"_frame",0);return owner;}
    private static short[] Arguments(SourceBattleAnimation owner,JsonElement array)
    {var args=Field<short[]>(owner,"_animationArgs");var native=N(array);Assert.AreEqual(8,native.Length);for(int i=0;i<8;i++)args[i]=(short)native[i];for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);return args.Skip(8).ToArray();}
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,args,null)!;
    private static SourceBattleAnimation.VisualSprite Sprite(SourceBattleAnimation owner,SourceAnimationScript full,int kind,int priority,int slot=4)=>(SourceBattleAnimation.VisualSprite)Nested(kind==2?"CallerLetterSprite":"SocialSprite",slot,priority,full.SpriteRecipes[Templates[kind]],owner);
    private static void Step(SourceBattleAnimation.VisualSprite sprite,SourceBattleAnimation owner){if(sprite.Finished)return;sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
    private static void ArgsRow(SourceBattleAnimation owner,JsonElement row,short[] extension,string why)
    {CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(v=>(short)v).ToArray(),Field<short[]>(owner,"_animationArgs").Take(8).ToArray(),why+" native arguments");CollectionAssert.AreEqual(extension,Field<short[]>(owner,"_animationArgs").Skip(8).ToArray(),why+" untouched extended registers");Assert.IsNull(Field<object?>(owner,"_visualRandom"),why+" source has no random call");}
    private static void SpriteRow(SourceBattleAnimation.VisualSprite sprite,JsonElement e,string why,bool players=true)
    {
        var f=sprite.Frame;Assert.AreEqual(I(e,"alive")==0,sprite.Finished,why+" life");CollectionAssert.AreEqual(N(e.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" position");Assert.AreEqual(I(e,"tile"),f.Tile,why+" rendered tile");Assert.AreEqual(I(e,"visible")!=0,f.Visible,why+" visibility");Assert.AreEqual(I(e,"priority"),f.OamPriority,why+" priority");Assert.AreEqual(I(e,"subpriority"),f.Subpriority,why+" subpriority");Assert.AreEqual(I(e,"objMode")==1,f.Blend,why+" blend");
        // DestroySpriteAndMatrix clears the native affine mode immediately before slot reset;
        // the retired managed frame is never emitted. Compare its last matrix separately.
        if(!sprite.Finished){bool affine=(I(e,"affineMode")&1)!=0;Assert.AreEqual(affine,f.Affine,why+" affine mode");Assert.AreEqual(I(e,"affineMode")==3,f.DoubleSize,why+" affine canvas");int bits=I(e,"oamMatrixNum");Assert.AreEqual(!affine&&(bits&8)!=0,f.HFlip,why+" rendered H flip");Assert.AreEqual(!affine&&(bits&16)!=0,f.VFlip,why+" rendered V flip");}
        if(!players||sprite.Finished)return;
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");if(f.Template is "gThoughtBubbleSpriteTemplate" or "gLetterZSpriteTemplate"){Assert.AreEqual(I(e,"animEnded")!=0,frames.Ended,why+" frame end");Assert.AreEqual(I(e,"animDelay"),frames.DelayCounter,why+" frame delay");}
        var affinePlayer=Field<SourceSpriteAffineProgram?>(sprite,"_affine");if(affinePlayer is not null&&!sprite.Finished){Assert.AreEqual(I(e,"xScale"),affinePlayer.XScale,why+" affine X");Assert.AreEqual(I(e,"yScale"),affinePlayer.YScale,why+" affine Y");Assert.AreEqual(I(e,"rotation"),affinePlayer.Rotation,why+" affine rotation");Assert.AreEqual(I(e,"affineEnded")!=0,affinePlayer.Ended,why+" affine end");}
    }
    [TestMethod]
    public void NativeSpriteCallbacksMatchGeometryHandoffsArgumentsPlayersAndLifetime()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {var full=resources.Resolve(I(c,"root")).Script;var audio=new Audio();var owner=Owner(full,c,audio);var extension=Arguments(owner,c.GetProperty("initialArgs"));var sprite=Sprite(owner,full,I(c,"kindId"),I(c,"initialSubpriority"));foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0)Step(sprite,owner);string why=c.GetProperty("id").GetString()+" frame "+frame;SpriteRow(sprite,row.GetProperty("sprite"),why);ArgsRow(owner,row,extension,why);if(I(c,"kindId")==2){var data=N(row.GetProperty("sprite").GetProperty("data"));Assert.AreEqual((short)data[0],Field<short>(sprite,"_age"),why+" age");Assert.AreEqual((short)data[3],Field<short>(sprite,"_x"),why+" signed X sum");Assert.AreEqual((short)data[4],Field<short>(sprite,"_y"),why+" signed Y sum");}Assert.AreEqual(sprite.Finished?0:1,I(row,"visualCount"),why);Assert.AreEqual(0,audio.Cues.Count,why);rows++;}Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;}
        Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?56:18,cases);Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?4348:1198,rows);
    }
    [TestMethod][DataRow(118)][DataRow(214)]
    public void CompleteRootsMatchNativeBirthSchedulesComposeBothSidesAndLeaveSceneReadyForLeaf(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);using var schedules=SourceVectorFixture.Read("caller-prelude-schedule-source-vectors.json", "python tests/Reference/CallerPrelude/generate_schedule.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/caller-prelude-schedule-source-vectors.json");var expected=schedules.RootElement.GetProperty("cases").EnumerateArray().Single(c=>I(c,"move")==move).GetProperty("births").EnumerateArray().Select(b=>(I(b,"frame"),b.GetProperty("template").GetString()!)).ToArray();
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor,actor^1,actor^2,actor^3}:new[]{actor,actor^1})
        {
            var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,target);var runtime=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),spriteMaterials:resolved.Assets);var seen=new HashSet<SourceBattleAnimation.VisualSprite>();List<(int,string)> births=[];bool pixels=false,rotated=false,doubleSize=false,sway=false;int ticks=0;
            while(runtime.Active&&ticks++<400)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(runtime.Snapshot()));
                foreach(var sprite in scene.Sprites)if(seen.Add(sprite))births.Add((f.Frame,sprite.Frame.Template));
                if(f.Sprites.Length>0&&ticks%7==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));rotated|=f.Sprites.Any(s=>s.MatrixB!=0);doubleSize|=f.Sprites.Any(s=>s.DoubleSize);sway|=f.Offsets[actor].X!=0;
            }
            CollectionAssert.AreEqual(expected,births.ToArray());Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.IsTrue(rotated);Assert.AreEqual(move==118,doubleSize);Assert.AreEqual(move==214,sway);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsNull(Field<object?>(runtime,"_visualRandom"));Assert.IsFalse(runtime.Snapshot().BlendEnabled);
            CollectionAssert.AreEqual(move==118?new[]{"SE_M_METRONOME","SE_M_TAIL_WHIP","SE_M_TAIL_WHIP","SE_M_TAIL_WHIP"}:new[]{"SE_M_SNORE","SE_M_SNORE","SE_M_SNORE"},audio.Cues.Select(c=>c.Id).ToArray());Assert.IsTrue(audio.Cues.All(c=>c.Pan==(actor%2==0?-64:63)&&c.ApplyPanning));
            // A fresh called-leaf controller shares the same scene only after
            // the root has ended. The old owner retains no counted resources.
            var leaf=resources.Resolve(33);var next=new SourceBattleAnimation(leaf.Script,Cue(33,actor,target),bodies,doubles,new Audio(),spriteScene:scene,spriteMaterials:leaf.Assets);int leafTicks=0;while(next.Active&&leafTicks++<400)next.Tick();Assert.IsFalse(next.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,next.ActiveVisualCount);
        }
    }
    [TestMethod]
    public void SleepTalkSwayWritesAttackerSideSignedRegisterUntilFirstLetterReplacesIt()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(214);
        var sway=resolved.Script.Commands.Single(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_SwayMon");
        CollectionAssert.AreEqual(new[]{"AnimTask_SwayMon","5","0","4","4096","2","ANIM_ATTACKER"},sway.Args);
        // Run the authored root unchanged. Opposite-side targets distinguish the
        // attacker's sign from target side, in both player and opponent slots.
        for(int actor=0;actor<4;actor++)
        {
            var runtime=new SourceBattleAnimation(resolved.Script,Cue(214,actor,actor^1),Battlers(true),true,new Audio(),spriteMaterials:resolved.Assets);
            short amplitude=(short)(actor%2==0?4:-4);int retainedFrames=0;
            for(int tick=0;tick<=25;tick++)
            {
                var frame=runtime.Tick();Assert.AreEqual(tick,frame.Frame);string why=$"actor {actor}, frame {frame.Frame}";
                short[] expected=frame.Frame<3?new short[8]:frame.Frame<25?[0,amplitude,4096,2,0,0,0,0]:[0,20,5,-1,0,0,0,0];
                CollectionAssert.AreEqual(expected,frame.AnimationArguments.Take(8).ToArray(),why+" source shared registers");
                Assert.IsTrue(frame.AnimationArguments.Skip(8).All(x=>x==0),why+" untouched extension");
                if(frame.Frame is >=3 and <25)retainedFrames++;
                if(frame.Frame==3)
                {
                    Assert.AreEqual((resolved.Script.SineTable[16]*amplitude)>>8,frame.Offsets[actor].X,why+" signed amplitude applied once");
                    Assert.AreEqual(default(PixelPoint),frame.Offsets[actor^1],why+" selected attacker only");
                }
                Assert.AreEqual(frame.Frame==25,frame.Sprites.Any(s=>s.Template==Templates[2]),why+" first native letter boundary");
            }
            Assert.AreEqual(22,retainedFrames);runtime.Cancel();
        }
    }
    [TestMethod]
    public void CancellationAtLoadGrowthHoldLoopAndLetterFlightReleasesOnlyOwnedEffects()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{118,214})foreach(int tick in new[]{0,1,3,6,14,23,40,75,103,117,150,190})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move,1,0),Battlers(true),true,audio,spriteScene:scene);for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);
        }
    }
    [TestMethod]
    public void MissingOrCorruptProgramsAndNonterminatingLetterOperandsFailBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{118,214})
        {
            var s=resources.Resolve(move).Script;string name=move==118?Templates[1]:Templates[2];var r=s.SpriteRecipes[name];
            foreach(var bad in new[]{r with{Callback="AnimSleepLetterZ"},r with{Width=64},r with{AffineDoubleSize=!r.AffineDoubleSize},r with{AffineAnimations=[]},r with{LoopStarts=[]},r with{Animations=[[new(0,40,false,false)]]}})
            {
                var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)},Cue(move,0,1),Battlers(true),true,audio,spriteScene:scene));Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,audio.Cues.Count);
            }
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteRecipes=s.SpriteRecipes.Where(p=>p.Key!=name).ToDictionary()},Cue(move,0,1),Battlers(true),true,new Audio()));
        }
        var sleep=resources.Resolve(214).Script;
        foreach(string speed in new[]{"0","256","-1"})
        {var commands=sleep.Commands.Select(c=>c.Op=="createsprite"?c with{Args=[..c.Args.Take(5),speed,c.Args[6]]}:c).ToArray();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(sleep with{Commands=commands},Cue(214,0,1),Battlers(true),true,new Audio()));}
        var rest=resources.Resolve(156).Script;Assert.AreNotEqual(sleep.SpriteRecipes[Templates[2]].Callback,rest.SpriteRecipes["gSleepLetterZSpriteTemplate"].Callback);Assert.AreNotEqual(sleep.SpriteRecipes[Templates[2]].Animations[0][0].Duration,rest.SpriteRecipes["gSleepLetterZSpriteTemplate"].Animations[0][0].Duration);
    }
    private sealed class FragmentOccupant(int slot,int count,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"retained",default,default,0,false,false,false,256,null,100){Affine=affine,Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),count).ToArray()};public override void Step(SourceBattleAnimation owner){}}
    [TestMethod]
    public void ConservativeOamAndAffineBoundsIncludeRetainedSceneObjectsBeforeStarting()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(214).Script;
        foreach(bool affine in new[]{false,true})
        {var scene=new SourceBattleAnimation.SpriteScene();if(affine)for(int slot=4;slot<28;slot++)scene.Sprites.Add(new FragmentOccupant(slot,0,true));else scene.Sprites.Add(new FragmentOccupant(4,34));var audio=new Audio();int retained=scene.Count;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(214,0,1),Battlers(true),true,audio,spriteScene:scene));Assert.AreEqual(retained,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
        var unbounded=s with{Commands=[..Enumerable.Range(0,29).Select(_=>C("createsprite",Templates[2],"1","2","0","20","5","-1")),C("waitforvisualfinish"),C("end")]};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(unbounded,Cue(214,0,1),Battlers(true),true,new Audio()));
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("caller-prelude-source-vectors.json", "python tests/Reference/CallerPrelude/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/caller-prelude-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[]{0,1,2}, cases.Select(c=>I(c,"kindId")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{-32768,-5,-3,-1}, cases.Where(c=>I(c,"kindId")==2).Select(c=>c.GetProperty("initialArgs")[3].GetInt32()).Distinct().ToArray());
        foreach(string variant in new[]{"target-selector-other-geometry","head-order-offset-unused-y","unsigned-horizontal-edge","signed16-y-wrap-unused-y-offset"})
            Assert.IsTrue(cases.Any(c=>c.GetProperty("variant").GetString()==variant),variant);
        Assert.AreEqual(4,cases.Count(c=>c.GetProperty("variant").GetString()=="unsigned-horizontal-edge"));
    }
}
