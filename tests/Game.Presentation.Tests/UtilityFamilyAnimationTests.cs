using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class UtilityFamilyAnimationTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation owner){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static ushort[] TerrainPalette(SourceAnimationResources resources){var surface=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();}
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{56,40,32,64}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("utility",1,move,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static object? Call(SourceBattleAnimation r,string method,params object[] args)=>typeof(SourceBattleAnimation).GetMethod(method,Hidden)!.Invoke(r,args);
    private static object Field(object obj,string field)=>obj.GetType().GetField(field,Hidden)!.GetValue(obj)!;
    private static object[] Sprites(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_sprites")).Cast<object>().ToArray();
    private static SourceVisualSpriteFrame Sprite(object s)=>(SourceVisualSpriteFrame)s.GetType().GetProperty("Frame")!.GetValue(s)!;
    private static bool Finished(object s)=>(bool)s.GetType().GetProperty("Finished")!.GetValue(s)!;
    private static int Int(object o,string name)=>Convert.ToInt32(Field(o,name));
    private static int[] Row(SourceVisualSpriteFrame f)=>[f.Slot,f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y,f.Visible?1:0,f.Tile,f.OamPriority,f.Subpriority,f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD,f.Affine?1:0];
    private static object[] Tasks(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_tasks")).Cast<object>().ToArray();
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("utility-family-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_utility_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
    [TestMethod]
    public void EverySelectedSourceCallbackTaskFrameAffineAndRegisterRowMatches()
    {
        using var resources=new SourceAnimationResources(Folder);using var vectors=Vectors();var root=vectors.RootElement;int cases=0,frames=0,rows=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[2],target=key[3],capacity=key[5],move=new[]{281,191,285,288}[kind];bool doubles=key[1]!=0;int slots=doubles?4:2;
            int[] args=root.GetProperty("configs")[kind][key[4]].EnumerateArray().Select(x=>x.GetInt32()).ToArray();string template=root.GetProperty("templates")[kind].GetString()!;
            var resolved=resources.Resolve(move);var r=new SourceBattleAnimation(Minimal(resolved.Script,C("end")),Cue(move,actor,target),Bodies(doubles),doubles,new Audio(),TerrainPalette(resources));
            string tag="ANIM_TAG_"+resolved.Script.SpriteRecipes[template].Asset.ToUpperInvariant().Replace('-','_');Call(r,"LoadSpriteGraphics",tag);
            if(capacity>=0)
            {
                var owned=Field(r,"_sprites");
                for(int slot=slots;slot<64-capacity;slot++)owned.GetType().GetMethod("Add")!.Invoke(owned,[new Occupied(slot)]);
                var occupied=((IEnumerable)owned).Cast<SourceBattleAnimation.VisualSprite>().OfType<Occupied>().ToArray();
                Assert.AreEqual(64-capacity-slots,occupied.Length);
                CollectionAssert.AreEqual(Enumerable.Range(slots,64-capacity-slots).ToArray(),occupied.Select(s=>s.Slot).ToArray());
                Assert.IsTrue(occupied.All(s=>!s.Counted));
            }
            short[] registers=(short[])Field(r,"_animationArgs");for(int i=0;i<args.Length;i++)registers[i]=unchecked((short)args[i]);
            if(kind<2)Assert.AreEqual(true,Call(r,"TryCreateUtilitySprite",C("createsprite",[template,"ANIM_TARGET","2",..args.Select(x=>x.ToString())]),slots,28));
            else
            {
                if(kind==2&&args[0]==2)registers[0]=1;
                Assert.AreEqual(true,Call(r,"TryCreateUtilityTask",kind==2?C("createvisualtask","AnimTask_SkillSwap","3",registers[0].ToString()):C("createvisualtask","AnimTask_GrudgeFlames","3")));
            }
            foreach(var expected in entry.Value.EnumerateArray())
            {
                int[] state=expected.GetProperty("state").EnumerateArray().Select(x=>x.GetInt32()).ToArray();int tick=state[0];
                if(tick>=0)Call(r,"AdvanceSourceSprites");
                var actual=Sprites(r).Select(Sprite).Where(s=>s.Template==template).OrderBy(s=>s.Slot).Select(Row).ToArray();
                var wanted=expected.GetProperty("sprites").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToArray();
                Assert.AreEqual(wanted.Length,actual.Length,entry.Name+" tick "+tick);for(int i=0;i<wanted.Length;i++){CollectionAssert.AreEqual(wanted[i],actual[i],entry.Name+" tick "+tick+" sprite "+i);rows++;}
                if(tick>=0)
                {
                    if(kind==2&&args[0]==2&&tick==16){registers[0]=0;Call(r,"TryCreateUtilityTask",C("createvisualtask","AnimTask_SkillSwap","3","0"));}
                    foreach(var task in Tasks(r).Where(t=>!Finished(t)))task.GetType().GetMethod("Step")!.Invoke(task,[r]);
                }
                var tasks=Tasks(r);for(int i=0;i<2;i++)Assert.AreEqual(state[i+1]!=0,i<tasks.Length&&!Finished(tasks[i]),entry.Name+" tick "+tick+" task "+i);
                int visual=tasks.Count(t=>!Finished(t))+Sprites(r).Count(s=>(bool)s.GetType().GetProperty("Counted")!.GetValue(s)!);Assert.AreEqual(state[3],visual);
                Assert.AreEqual(state[6],(int)registers[2]);Assert.AreEqual(state[7],slots+Sprites(r).Select(Sprite).Count(s=>s.Template==template&&s.Affine));
                if(kind==2)for(int i=0;i<tasks.Length;i++)
                {
                    int at=8+i*16;var t=tasks[i];Assert.AreEqual(state[at],Int(t,"_phase"));Assert.AreEqual(state[at+1],Int(t,"_delay"));Assert.AreEqual(state[at+2],Int(t,"_births"));Assert.AreEqual(state[at+10],Int(t,"_height"));Assert.AreEqual(new PixelPoint(state[at+11],state[at+12]),Field(t,"_start"));Assert.AreEqual(new PixelPoint(state[at+13],state[at+14]),Field(t,"_end"));
                }
                if(kind==3)
                {
                    var f=r.Snapshot();Assert.AreEqual(state[4]&63,f.BlendFirstTargets);Assert.AreEqual((state[4]>>8)&63,f.BlendSecondTargets);Assert.AreEqual((state[4]&0xc0)==0x40,f.BlendEnabled);Assert.AreEqual(state[5]&31,f.AlphaA);Assert.AreEqual((state[5]>>8)&31,f.AlphaB);
                    var t=tasks[0];string[] fields=["_phase","_delay","_a","_b","_priority","_subpriority","_radius"];int[] offsets=[0,1,3,4,5,6,11];for(int i=0;i<fields.Length;i++)Assert.AreEqual(state[8+offsets[i]],Int(t,fields[i]));Assert.AreEqual(state[15],t.GetType().GetProperty("Children")!.GetValue(t));Assert.AreEqual(state[16]!=0,t.GetType().GetProperty("KillChildren")!.GetValue(t));Assert.AreEqual(new PixelPoint(state[17],state[18]),Field(t,"_position"));
                }
                frames++;
            }
            r.Cancel();cases++;
        }
        var coverage=root.GetProperty("coverage");Assert.AreEqual(coverage.GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(coverage.GetProperty("frames").GetInt32(),frames);Assert.AreEqual(coverage.GetProperty("spriteRows").GetInt32(),rows);Assert.IsTrue(cases>0&&frames>cases&&rows>0);
    }
    [TestMethod]
    [DataRow(281)] [DataRow(285)] [DataRow(288)] [DataRow(191)]
    public void CompleteRootsComposeEveryFrameAcrossAllSixLayoutsAndCleanOwners(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>i%64 is >4 and <59&&i/64 is >4 and <59?(ushort)0x83e0:(ushort)0).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,move==288?actor:actor^1);
            var r=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),TerrainPalette(resources),spriteMaterials:assets);byte[] baseline=composer.ComposeField(r.Snapshot());int ticks=0;bool changed=false,particles=false;var slots=new HashSet<int>();
            while(r.Active&&ticks++<900)
            {
                var f=r.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);changed|=!rgba.SequenceEqual(baseline);particles|=f.Sprites.Any(s=>s.Asset is "pink-cloud" or "bluegreen-orb" or "purple-flame" or "spikes");foreach(var s in f.Sprites)slots.Add(s.Slot);
            }
            Assert.IsFalse(r.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(r.GraphicsLoaded);Assert.IsTrue(changed&&particles&&slots.Count>0);var last=r.Snapshot();Assert.IsFalse(last.BlendEnabled);Assert.IsNotNull(last.MoveBackground);Assert.IsNull(last.MoveBackground!.Id);Assert.AreEqual(0,last.BackgroundSlots.Length);Assert.IsTrue(last.Offsets.All(p=>p==default));Assert.AreEqual(0,last.PaletteBlends.Length);CollectionAssert.AreEqual(baseline,composer.ComposeField(last));Assert.IsTrue(audio.Cues.Count>0);
        }
    }
    [TestMethod]
    public void EveryRegisterPhaseCanCancelAndRestoreTheScene()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{281,285,288,191})foreach(int stop in new[]{0,1,16,17,28,59,60,66,77,95,112,160,240})
        {var scene=new SourceBattleAnimation.SpriteScene();var r=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move),Bodies(true),true,new Audio(),TerrainPalette(resources),spriteScene:scene);for(int i=0;i<stop&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();var f=r.Snapshot();Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,f.Sprites.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsNotNull(f.MoveBackground);Assert.IsNull(f.MoveBackground!.Id);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));}
    }
    [TestMethod]
    public void HiddenDoublesPartnerStillContributesToSpikesAverage()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(191).Script;
        foreach(bool present in new[]{true,false})
        {
            var bodies=Bodies(true);bodies[3]=bodies[3] with{SourceVisibility=new(present,true,true)};var a=new SourceBattleAnimation(s,Cue(191),bodies,true,new Audio(),TerrainPalette(resources));var b=new SourceBattleAnimation(s,Cue(191),Bodies(true),true,new Audio(),TerrainPalette(resources));bool observed=false;
            while(a.Active)
            {
                var fa=a.Tick();var fb=b.Tick();Assert.AreEqual(fa.Sprites.Length,fb.Sprites.Length);
                // Presence changes native allocation: absent slot 3 is reusable.
                // Every position, offset, frame, priority and matrix stays equal.
                CollectionAssert.AreEqual(fa.Sprites.SelectMany(f=>Row(f).Skip(1)).ToArray(),fb.Sprites.SelectMany(f=>Row(f).Skip(1)).ToArray());
                if(!observed&&fa.Sprites.Length!=0){Assert.AreEqual(present?4:3,fa.Sprites.Min(f=>f.Slot));Assert.AreEqual(4,fb.Sprites.Min(f=>f.Slot));observed=true;}
            }
            Assert.IsTrue(observed);Assert.IsFalse(b.Active);
        }
    }
    [TestMethod]
    public void MissingChildMaterialsAndCompetingRegisterOwnersFailBeforePlayback()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{281,285,288,191})
        {
            var s=resources.Resolve(move).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpritePalettes=new Dictionary<int,ushort[]>()},Cue(move),Bodies(false),false,new Audio(),TerrainPalette(resources)));
            if(move is 285 or 288)Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteDependencies=[]},Cue(move),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        }
        var grudge=resources.Resolve(288).Script;foreach(var extra in new[]{C("setalpha","8","8"),C("createvisualtask","AnimTask_GrudgeFlames","3")})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(grudge with{Commands=[..grudge.Commands.Take(grudge.Commands.Length-1),extra,C("end")]},Cue(288),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        var yawn=resources.Resolve(281).Script;foreach(string value in new[]{"-1","3","32768"})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(yawn with{Commands=yawn.Commands.Select(c=>c.Op=="createsprite"&&c.Args[0]=="gYawnCloudSpriteTemplate"?c with{Args=[c.Args[0],c.Args[1],c.Args[2],value]}:c).ToArray()},Cue(281),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        var swap=resources.Resolve(285).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(swap with{Commands=[..swap.Commands.Take(swap.Commands.Length-1),C("createvisualtask","AnimTask_SkillSwap","3","0"),C("end")]},Cue(285),Bodies(false),false,new Audio(),TerrainPalette(resources)));
    }
}
