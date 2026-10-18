using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SpotlightAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly string[] Names=["gSpotlightSpriteTemplate","gFlatterSpotlightSpriteTemplate","gClappingHandSpriteTemplate","gClappingHand2SpriteTemplate","gFlatterConfettiSpriteTemplate"];
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;}
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private)!.Invoke(o,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target,uint seed=0x12345678)=>new("spotlight",1,move,new(actor),new(target)){VisualSeed=seed};
    private static AnimationBattler[] Bodies(bool doubles,bool hiddenPartner=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){SourceVisibility=new(true,false,hiddenPartner&&i>=2)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("spotlight-source-vectors.json","python tests/Reference/Spotlight/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/spotlight-source-vectors.json");
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}
    private static SourceBattleAnimation.VisualSprite Sprite(SourceBattleAnimation owner,SourceSpriteRecipe recipe)=>
        (SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("SpotlightSprite",BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,new object[]{4,2,recipe,owner},null)!;

    [TestMethod]
    public void OriginalCallbacksMatchAll3444FramesIncludingAffineRestartObjMasksClapsAndRandomWidths()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,frames=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),move=kind is 1 or 4?260:227;uint seed=c.GetProperty("seed").GetUInt32();var audio=new Audio();var full=resources.Resolve(move).Script;
            var owner=new SourceBattleAnimation(Primitive(full),Cue(move,actor,target,seed),Bodies(true),true,audio);var args=Field<short[]>(owner,"_animationArgs");int i=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[i++]=(short)a.GetInt32();var untouched=(short[])args.Clone();var sprite=Sprite(owner,full.SpriteRecipes[Names[kind]]);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                var r=row.EnumerateArray().Select(v=>v.GetInt64()).ToArray();int tick=(int)r[0];typeof(SourceBattleAnimation).GetField("_frame",Private)!.SetValue(owner,tick);
                if(tick>0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}string why=$"kind{kind} actor{actor} case{cases} frame{tick}";
                Assert.AreEqual(r[1]==0,sprite.Finished,why+" lifetime");CollectionAssert.AreEqual(untouched,args,why+" shared argument registers");Assert.AreEqual((int)r[14],audio.Cues.Count,why+" clap count");
                if(kind==4)Assert.AreEqual((uint)r[15],Field<uint>(Field<SourceVisualRandom>(owner,"_visualRandom"),"_state"),why+" exact three RNG draws");else Assert.IsNull(Field<object?>(owner,"_visualRandom"),why+" no RNG");
                if(kind<2){Assert.AreEqual((r[16]&32768)!=0,owner.Snapshot().ObjectWindow is {Enabled:true},why+" OBJ window ownership");if(!sprite.Finished){Assert.AreEqual(31,owner.Snapshot().ObjectWindow!.InsideMask);Assert.AreEqual(63,owner.Snapshot().ObjectWindow!.OutsideMask);}}
                if(!sprite.Finished)
                {
                    var f=sprite.Frame;Assert.AreEqual(new PixelPoint((int)r[2],(int)r[3]),f.Position,why+" position");Assert.AreEqual(new PixelPoint((int)r[4],(int)r[5]),f.Offset,why+" offset");Assert.AreEqual((int)r[6],f.Tile,why+" tile");Assert.AreEqual(r[7]!=0,f.HFlip,why+" flip");Assert.AreEqual(r[8]==0,f.Visible,why+" invisible");Assert.AreEqual(r[9]==2,f.ObjectWindow,why+" OAM OBJ mode");Assert.AreEqual(kind<2,f.Affine);Assert.AreEqual(kind<2,f.DoubleSize);Assert.IsFalse(f.Blend);
                    if(kind<2){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual((int)r[10],affine.XScale,why+" native x scale");Assert.AreEqual((int)r[11],affine.YScale,why+" native y scale");Assert.AreEqual((int)r[12],affine.Rotation,why+" native rotation");Assert.AreEqual(r[13]!=0,affine.Ended,why+" affine END handoff");Assert.AreEqual(affine.Matrix.A,f.MatrixA);Assert.AreEqual(affine.Matrix.D,f.MatrixD);}
                }
                frames++;
            }
            Assert.IsTrue(audio.Cues.All(sound=>sound.Id=="SE_M_ENCORE"&&sound.PanningPlayer==1&&sound.Pan==(actor%2==0?-64:63)));if(kind==3)Assert.AreEqual(0,audio.Cues.Count,"Duplicate OBJ-window hands must be silent.");owner.Cancel();cases++;
        }
        Assert.AreEqual(46,cases);Assert.AreEqual(3444,frames);
    }

    [TestMethod]
    public void WindowSetupAndRemovalMatchOriginalImmediateTaskRegisters()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var owner=new SourceBattleAnimation(Primitive(resources.Resolve(227).Script),Cue(227,0,1),Bodies(false),false,new Audio());var rows=vectors.RootElement.GetProperty("windowTasks").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
        Assert.AreEqual(true,Invoke(owner,"TryCreateSpotlightTask",C("createvisualtask","AnimTask_CreateSpotlight","2")));var w=owner.Snapshot().Window1!;Assert.AreEqual((rows[0][1]&16384)!=0,w.Enabled);Assert.AreEqual(rows[0][2]>>8,w.InsideMask);Assert.AreEqual(rows[0][2]&63,w.OutsideMask);Assert.AreEqual(rows[0][3],(int)w.Horizontal);Assert.AreEqual(rows[0][4],(int)w.Vertical);Assert.AreEqual(rows[0][5],(int)w.Horizontal);Assert.AreEqual(rows[0][6],(int)w.Vertical);Assert.AreEqual(0,owner.ActiveVisualCount);
        Assert.AreEqual(true,Invoke(owner,"TryCreateSpotlightTask",C("createvisualtask","AnimTask_RemoveSpotlight","2")));Assert.AreEqual(0,rows[1][1]&16384);Assert.AreEqual(0,rows[1][5]);Assert.AreEqual(0,rows[1][6]);Assert.IsNull(owner.Snapshot().Window1);Assert.AreEqual(0,owner.ActiveVisualCount);owner.Cancel();
    }

    [TestMethod,DataRow(227),DataRow(260)]
    public void CompleteRootsMatchSourceBirthTimingComposeDeterministicallyAndRestoreIdle(int move)
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var schedule=vectors.RootElement.GetProperty("schedules").EnumerateArray().Single(c=>c.GetProperty("move").GetInt32()==move).GetProperty("births").EnumerateArray().Select(b=>(b.GetProperty("frame").GetInt32(),b.GetProperty("template").GetString()!)).ToArray();
        var burst=vectors.RootElement.GetProperty("confettiBurst").EnumerateArray().Select(c=>c.GetProperty("rows")[1].EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
        foreach(var (doubles,actor,target) in new[]{(false,0,1),(false,1,0),(true,0,1),(true,1,0),(true,2,3),(true,3,2)})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,target);var runtime=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),Terrain(resources),spriteMaterials:resolved.Assets);
            var seen=new HashSet<SourceBattleAnimation.VisualSprite>();List<(int,string)> births=[];bool maskPixels=false,visualPixels=false;int ticks=0;
            while(runtime.Active&&ticks++<400)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(runtime.Snapshot()));
                var born=scene.Sprites.Where(s=>seen.Add(s)).ToArray();foreach(var sprite in born)births.Add((f.Frame,sprite.Frame.Template));
                if(move==260&&born.Any(s=>s.Frame.Template==Names[4]))
                {
                    var confetti=born.Where(s=>s.Frame.Template==Names[4]).ToArray();Assert.AreEqual(38,confetti.Length,"19 source calls must run without synthetic yielding");
                    for(int i=0;i<38;i++){var r=burst[i];var sprite=confetti[i].Frame;Assert.AreEqual(new PixelPoint((int)r[2],(int)r[3]),sprite.Position);Assert.AreEqual(new PixelPoint((int)r[4],(int)r[5]),sprite.Offset);Assert.AreEqual((int)r[6],sprite.Tile);}
                    Assert.AreEqual((uint)burst[^1][15],Field<uint>(Field<SourceVisualRandom>(runtime,"_visualRandom"),"_state"),"114 native draws");
                }
                if(f.ObjectWindow is not null&&f.Brightness is {Coefficient:>0}&&f.Sprites.Any(s=>s.ObjectWindow&&s.Visible))
                    maskPixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{ObjectWindow=null,Sprites=f.Sprites.Where(s=>!s.ObjectWindow).ToArray()}));
                if(f.Sprites.Any(s=>!s.ObjectWindow&&s.Visible)&&ticks%7==0)visualPixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.ObjectWindow).ToArray()}));
            }
            CollectionAssert.AreEqual(schedule,births.ToArray());Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(maskPixels,"Indexed affine OBJ mask gates brightness");Assert.IsTrue(visualPixels);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);var final=runtime.Snapshot();Assert.AreEqual(new SourceWindow0Frame(true,0,0,63,63),final.Window0);Assert.IsNull(final.Window1);Assert.IsNull(final.ObjectWindow);Assert.IsNull(final.Brightness);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));CollectionAssert.AreEqual(composer.ComposeField(scene.CaptureIdle(bodies,doubles)),composer.ComposeField(final));
            if(move==227)
            {
                Assert.IsNull(Field<object?>(runtime,"_visualRandom"));var claps=audio.Cues.Where(s=>s.Id=="SE_M_ENCORE").ToArray();CollectionAssert.AreEqual(Enumerable.Range(0,9).Select(i=>53+i*12).ToArray(),claps.Select(s=>s.Frame).ToArray());Assert.IsTrue(claps.All(s=>s.PanningPlayer==1));Assert.AreEqual(1,audio.Cues.Count(s=>s.Id=="SE_M_ENCORE2"&&s.PanningPlayer==2));
            }
            else {Assert.AreEqual(2,audio.Cues.Count(s=>s.Id=="SE_M_FLATTER"&&s.PanningPlayer==1));Assert.AreEqual(1,audio.Cues.Count(s=>s.Id=="SE_M_ENCORE2"&&s.PanningPlayer==2));}
        }
    }

    [TestMethod,DataRow(227),DataRow(260)]
    public void CancellationDuringFadeExpansionClapBurstShrinkAndDrainRestoresHiddenPartners(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(int tick in new[]{0,1,5,25,48,50,64,70,100,149,171,191,220})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(move,1,0),Bodies(true,true),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var initial=owner.Snapshot().BattlerVisibility;
            for(int i=0;i<tick&&owner.Active;i++)owner.Tick();int sounds=audio.Cues.Count;owner.Cancel();owner.Cancel();var f=owner.Snapshot();Assert.IsFalse(owner.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);Assert.IsNull(f.Window0);Assert.IsNull(f.Window1);Assert.IsNull(f.ObjectWindow);Assert.IsNull(f.Brightness);Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.AreEqual(JsonSerializer.Serialize(initial),JsonSerializer.Serialize(f.BattlerVisibility));Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);
        }
    }

    private sealed class Occupied(int slot,int fragments,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"retained",default,default,0,false,false,false,256,null,100){Affine=affine,Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),fragments).ToArray()};public override void Step(SourceBattleAnimation owner){}}
    [TestMethod]
    public void CompletePathCapacityCountsAll19CallsAndRetainedOamFragmentsBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(260).Script;
        Assert.AreEqual(41,SourceAnimationProgramDependencies.MaximumCost(s,c=>c.Op=="createsprite"?1:0));
        foreach(int fragments in new[]{1,2})
        {
            var scene=new SourceBattleAnimation.SpriteScene();scene.Sprites.Add(new Occupied(63,fragments));var audio=new Audio();
            if(fragments==1){var owner=new SourceBattleAnimation(s,Cue(260,0,1),Bodies(true),true,audio,spriteScene:scene);owner.Cancel();}
            else Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(260,0,1),Bodies(true),true,audio,spriteScene:scene));
            Assert.AreEqual(1,scene.Count);Assert.AreEqual(0,audio.Cues.Count);
        }
        var matrices=new SourceBattleAnimation.SpriteScene();for(int i=4;i<32;i++)matrices.Sprites.Add(new Occupied(i,0,true));Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(resources.Resolve(227).Script,Cue(227,0,1),Bodies(true),true,new Audio(),spriteScene:matrices));Assert.AreEqual(28,matrices.Count);
    }

    [TestMethod]
    public void CorruptRecipesMissingSeedSoundUnpairedWindowAndUnreviewedRegisterOwnerRejectBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{227,260})
        {
            var s=resources.Resolve(move).Script;
            void Reject(SourceAnimationScript script,CommittedMoveAnimation? cue=null){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue??Cue(move,0,1),Bodies(false),false,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(0,scene.Count);}
            // Catalog recipes are shared across roots. Corrupt only templates in this
            // invocation; an unrelated root's unused recipe is not an operand.
            var invoked=s.Commands.Concat(SourceAnimationProgramDependencies.RequiredSubroutines(s.Commands,s.Subroutines).Values.SelectMany(commands=>commands)).Where(c=>c.Op=="createsprite").Select(c=>c.Args[0]).ToHashSet();
            foreach(string name in Names.Where(invoked.Contains))
            {
                var r=s.SpriteRecipes[name];foreach(var bad in new[]{r with{Callback="Other"},r with{Width=16},r with{Blend=true},r with{AffineDoubleSize=!r.AffineDoubleSize},r with{Animations=[]},r with{FrameLoops=[[],[]]}})Reject(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)});
            }
            Reject(s with{Commands=s.Commands.Where(c=>c.Args.FirstOrDefault()!="AnimTask_RemoveSpotlight").ToArray()});Reject(s with{Commands=[C("setalpha","12","8"),..s.Commands]});
            var mask=move==227?Names[0]:Names[1];var recipe=s.SpriteRecipes[mask];Reject(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==mask?recipe with{AffineAnimations=[[new("frame",1,384,0,0,0),new("end",0,0,0,0,0)],recipe.AffineAnimations[1]]}:p.Value)});
            if(move==260){Reject(s,Cue(move,0,1) with{VisualSeed=null});var helper=s.Subroutines["CreateFlatterConfetti"];Reject(s with{Subroutines=s.Subroutines.ToDictionary(p=>p.Key,p=>p.Key=="CreateFlatterConfetti"?[helper[0],helper[1],helper[0],helper[2]]:p.Value)});Reject(s with{Commands=s.Commands.Select(c=>c.Op=="createsprite"&&c.Args[0]==Names[1]?c with{Args=[..c.Args.Take(5),"0"]}:c).ToArray()});}
            else Reject(s with{SoundEffects=s.SoundEffects.Where(p=>p.Key!="SE_M_ENCORE").ToDictionary()});
        }
    }
}
