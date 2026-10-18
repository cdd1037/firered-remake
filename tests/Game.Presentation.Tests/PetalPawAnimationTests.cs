using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PetalPawAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gAssistPawprintSpriteTemplate","gSweetScentPetalSpriteTemplate"];
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int Pan,int Frame)> Pans=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects()=>Stops++;}
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("petal-paw",1,move,new(actor),new(target));
    private static AnimationBattler[] Bodies(bool doubles,bool hiddenPartner=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){SourceVisibility=new(true,false,hiddenPartner&&i>=2)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("petal-paw-source-vectors.json","python tests/Reference/PetalPaw/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/petal-paw-source-vectors.json");
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}

    [TestMethod]
    public void OriginalCallbacksMatchAllFramesSignedWidthsPlayersUnusedRegistersAndDeferredPawDestruction()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),move=kind==0?274:230;
            var full=resources.Resolve(move).Script;var owner=new SourceBattleAnimation(Primitive(full),Cue(move,actor,actor^1),Bodies(true),true,new Audio());var args=Field<short[]>(owner,"_animationArgs");int i=0;
            foreach(var a in c.GetProperty("arguments").EnumerateArray())args[i++]=(short)a.GetInt32();for(;i<args.Length;i++)args[i]=(short)(0x5a00+i);var extension=args.Skip(8).ToArray();
            var sprite=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("PetalPawSprite",BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,new object[]{4,2,full.SpriteRecipes[Templates[kind]],owner},null)!;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                var r=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(r[0]>0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}string why=$"case{cases} kind{kind} actor{actor} frame{r[0]}";
                Assert.AreEqual(r[1]==0,sprite.Finished,why+" lifetime");CollectionAssert.AreEqual(r.Skip(12).Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why+" source registers");CollectionAssert.AreEqual(extension,args.Skip(8).ToArray(),why+" extension");Assert.IsNull(Field<object?>(owner,"_visualRandom"),why+" no RNG");
                if(!sprite.Finished)
                {
                    var f=sprite.Frame;Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position,why+" position");Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Offset,why+" offset");Assert.AreEqual(r[6],f.Tile,why+" tile");Assert.AreEqual(r[7]!=0,f.HFlip,why+" H flip");Assert.AreEqual(r[8]!=0,f.VFlip,why+" V flip");Assert.IsFalse(f.Affine);Assert.IsFalse(f.Blend);
                    if(kind==1){var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(r[9]!=0,frames.Ended,why+" frame end");Assert.AreEqual(r[10],frames.DelayCounter,why+" frame delay");}
                    else Assert.AreEqual(r[11]!=0,Field<bool>(sprite,"_destroyNext"),why+" stored destructor");
                }
                rows++;
            }
            Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;
        }
        Assert.AreEqual(44,cases);Assert.AreEqual(1833,rows);
    }

    [TestMethod]
    public void SetpanMatchesOriginalOpcodeAcrossEverySignedByteAndActorTargetPair()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var s=Primitive(resources.Resolve(230).Script);int rows=0;
        var expected=vectors.RootElement.GetProperty("setpan").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
        foreach(var group in expected.GroupBy(r=>(Actor:r[0],Target:r[1])))
        foreach(var chunk in group.Chunk(63))
        {
            // Each chunk fits the64-command per-frame budget including end;
            // all256 signed-byte cases are still compared for every slot pair.
            var audio=new Audio();var script=s with{Commands=[..chunk.Select(r=>C("setpan",r[2].ToString(System.Globalization.CultureInfo.InvariantCulture))),C("end")]};
            var runtime=new SourceBattleAnimation(script,Cue(230,group.Key.Actor,group.Key.Target),Bodies(true),true,audio);runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,audio.Cues.Count);Assert.IsTrue(audio.Pans.All(p=>p.Frame==0));CollectionAssert.AreEqual(chunk.Select(r=>r[3]).ToArray(),audio.Pans.Select(p=>p.Pan).ToArray());rows+=chunk.Length;
        }
        Assert.AreEqual(4096,rows);Assert.AreEqual(16,expected.GroupBy(r=>(r[0],r[1])).Count());
    }

    [TestMethod,DataRow(230),DataRow(274)]
    public void CompleteRootsMatchNativeBirthSchedulesComposeReplayAndReturnUnchangedIdlePixels(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);using var schedules=SourceVectorFixture.Read("petal-paw-schedule-source-vectors.json","python tests/Reference/PetalPaw/generate_schedule.py --root . --output tests/Game.Presentation.Tests/Fixtures/petal-paw-schedule-source-vectors.json");
        var expected=schedules.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("move").GetInt32()==move).GetProperty("births").EnumerateArray().Select(b=>(b.GetProperty("frame").GetInt32(),b.GetProperty("template").GetString()!)).ToArray();
        foreach(var (doubles,actor,target) in new[]{(false,0,1),(false,1,0),(true,0,1),(true,1,0),(true,2,3),(true,3,2)})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,target);var runtime=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),Terrain(resources),spriteMaterials:resolved.Assets);
            var seen=new HashSet<SourceBattleAnimation.VisualSprite>();List<(int,string)> births=[];var tinted=new HashSet<int>();bool pixels=false;int ticks=0;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(runtime.Snapshot()));
                foreach(var sprite in scene.Sprites)if(seen.Add(sprite))births.Add((frame.Frame,sprite.Frame.Template));foreach(var palette in frame.PaletteBlends.Where(p=>p.Coefficient>0))tinted.Add(palette.Palette);
                if(frame.Sprites.Length>0&&ticks%7==0)pixels|=!composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            CollectionAssert.AreEqual(expected,births.ToArray());Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsNull(Field<object?>(runtime,"_visualRandom"));var final=runtime.Snapshot();Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.PaletteBlends.All(p=>p.Coefficient==0));CollectionAssert.AreEqual(composer.ComposeField(scene.CaptureIdle(bodies,doubles)),composer.ComposeField(final));
            if(move==230)
            {
                CollectionAssert.AreEquivalent(doubles?new[]{16+target,16+(target^2)}:new[]{16+target},tinted.ToArray());CollectionAssert.AreEqual(new[]{"SE_M_SWEET_SCENT"},audio.Cues.Select(c=>c.Id).ToArray());CollectionAssert.AreEqual(new[]{(0,30),(actor%2==0?63:-63,77)},audio.Pans.ToArray());Assert.AreEqual(actor%2==0?-64:63,audio.Cues[0].Pan);
            }
            else
            {
                Assert.AreEqual(0,tinted.Count);Assert.AreEqual(0,audio.Pans.Count);CollectionAssert.AreEqual(new[]{7,11,15,19},audio.Cues.Select(c=>c.Frame).ToArray());Assert.IsTrue(audio.Cues.All(c=>c.Id=="SE_M_SCRATCH"&&c.Pan==0));
                var leaf=resources.Resolve(33);var next=new SourceBattleAnimation(leaf.Script,Cue(33,actor,target),bodies,doubles,new Audio(),spriteScene:scene,spriteMaterials:leaf.Assets);int leafTicks=0;while(next.Active&&leafTicks++<400)next.Tick();Assert.IsFalse(next.Active);Assert.AreEqual(0,scene.Count);
            }
        }
    }

    [TestMethod,DataRow(230),DataRow(274)]
    public void CancellationAtLoadFlightPaletteFadeAndLateDrainRestoresSceneAndHiddenPartners(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(int tick in new[]{0,1,3,15,38,54,77,103,145,190})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,1,0),Bodies(true,true),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var initial=runtime.Snapshot().BattlerVisibility;
            for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int cues=audio.Cues.Count,pans=audio.Pans.Count;runtime.Cancel();runtime.Cancel();var f=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.AreEqual(JsonSerializer.Serialize(initial),JsonSerializer.Serialize(f.BattlerVisibility));Assert.AreEqual(cues,audio.Cues.Count);Assert.AreEqual(pans,audio.Pans.Count);Assert.AreEqual(1,audio.Stops);
        }
    }

    [TestMethod]
    public void CorruptRecipesInvalidPansMissingDependenciesAndInvalidDurationRejectBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{274,230})
        {
            var s=resources.Resolve(move).Script;string name=Templates[move==274?0:1];var r=s.SpriteRecipes[name];
            void Reject(SourceAnimationScript script){var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(move,0,1),Bodies(false),false,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(0,audio.Pans.Count);Assert.AreEqual(0,scene.Count);}
            foreach(var bad in new[]{r with{Callback="SpriteCallbackDummy"},r with{Width=64},r with{Blend=true},r with{AffineDoubleSize=true},r with{Animations=[]},r with{LoopStarts=[]},r with{Animations=[[new(0,8,false,false)]]}})Reject(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)});
            Reject(s with{SpriteRecipes=s.SpriteRecipes.Where(p=>p.Key!=name).ToDictionary()});
            if(move==230)
            {
                Reject(s with{Subroutines=s.Subroutines.Where(p=>p.Key!="SweetScentEffect").ToDictionary()});
                foreach(string pan in new[]{"-129","128","32767"})Reject(Primitive(s) with{Commands=[C("setpan",pan),C("end")]});
                Reject(Primitive(s) with{Commands=[C("setpan"),C("end")]});Reject(Primitive(s) with{Commands=[C("setpan","0","1"),C("end")]});
                foreach(string animation in new[]{"-1","3"})Reject(Primitive(s) with{Commands=[C("createsprite",name,"0","2","100",animation,"64"),C("end")]});
            }
            else foreach(string duration in new[]{"0","-1","32768"})Reject(Primitive(s) with{Commands=[C("createsprite",name,"0","50","0","0","100","100",duration),C("end")]});
        }
    }
}
