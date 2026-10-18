using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class VoltTackleAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const string Orb="gVoltTackleOrbSlideSpriteTemplate",Bolt="gVoltTackleBoltSpriteTemplate",Puff="gElectricPuffSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot,int fragments=1,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine,Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),fragments).ToArray()};public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,a);
    private static object New(string n,params object[] a)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,a,CultureInfo.InvariantCulture)!;
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static CommittedMoveAnimation Cue(int a,int t)=>new("volt-tackle",1,344,new(a),new(t));
    private static AnimationBattler[] Bodies(bool doubles,int hidden=-1)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){SourceVisibility=new(true,i==hidden,i==hidden)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("volt-tackle-source-vectors.json","python tests/Reference/VoltTackle/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/volt-tackle-source-vectors.json");
    private static ushort[] Terrain(SourceAnimationResources r){var b=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>b.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void All3172NativeRowsMatchOrbRawOwnershipAllocationFailureAndReappearance()
    {
        using var doc=Vectors();using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(344).Script;int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int I(string n)=>c.GetProperty(n).GetInt32();int kind=I("kind"),actor=I("actor"),target=I("target"),occupied=I("occupied"),release=I("release");bool doubles=c.GetProperty("doubles").GetBoolean(),hidden=c.GetProperty("hidden").GetBoolean();
            var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(Primitive(script),Cue(actor,target),Bodies(doubles,hidden?actor:-1),doubles,new Audio(),spriteScene:scene);
            Field<PixelPoint[]>(owner,"_offsets")[actor]=new(I("offset"),4);var args=Field<short[]>(owner,"_animationArgs");int j=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[j++]=(short)a.GetInt32();var sprites=Field<object>(owner,"_sprites");var blockers=new List<Occupied>();for(int slot=64-occupied;slot<64;slot++){var b=new Occupied(slot);blockers.Add(b);Invoke(sprites,"Add",b);}
            SourceBattleAnimation.VisualSprite? single=kind==0?(SourceBattleAnimation.VisualSprite)New("VoltTackleOrbSprite",doubles?4:2,1,script.SpriteRecipes[Orb],owner):null;object? task=kind==0?null:New("VoltTackleTask",kind==1,owner);
            var instances=new Dictionary<SourceBattleAnimation.VisualSprite,int>();int generation=0;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=row.GetProperty("frame").GetInt32();string why=$"case {cases} kind {kind} actor {actor} frame {tick}";
                if(tick>0){if(tick==release)foreach(var blocker in blockers)Invoke(sprites,"Remove",blocker);if(single is not null){single.Step(owner);if(!single.Finished)single.AdvanceAnimation(owner);}else{Invoke(owner,"AdvanceSourceSprites");Invoke(task!,"Step",owner);}}
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,single is not null?!single.Finished:!(bool)task!.GetType().BaseType!.GetProperty("Finished",Flags)!.GetValue(task)!,why);
                CollectionAssert.AreEqual(row.GetProperty("args").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),args.Take(8).ToArray(),why);Assert.IsTrue(args.Skip(8).All(v=>v==0),why);
                var b=row.GetProperty("battler");Assert.AreEqual(new PixelPoint(b[0].GetInt32(),b[1].GetInt32()),Field<PixelPoint[]>(owner,"_offsets")[actor],why);Assert.AreEqual(b[2].GetInt32()!=0,Field<bool[]>(owner,"_sourceSpriteInvisible")[actor],why);Assert.AreEqual(hidden,Field<bool[]>(owner,"_sourceLogicalInvisible")[actor],why+" logical visibility is untouched");
                if(task is not null)CollectionAssert.AreEqual(row.GetProperty("task").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),Field<short[]>(task,"_d"),why);
                SourceBattleAnimation.VisualSprite[] actual=single is not null?(single.Finished?[]:new[]{single}):((IEnumerable)sprites).Cast<SourceBattleAnimation.VisualSprite>().Where(s=>s is not Occupied).OrderBy(s=>s.Slot).ToArray();var expected=row.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(expected.Length,actual.Length,why);
                for(int i=0;i<actual.Length;i++)
                {
                    var sprite=actual[i];if(!instances.TryGetValue(sprite,out int instance)){instance=++generation;instances.Add(sprite,instance);}int[] e=expected[i].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=sprite.Frame;
                    Assert.AreEqual(e[0],f.Slot,why);Assert.AreEqual(e[1],instance,why);Assert.AreEqual(new PixelPoint(e[2],e[3]),f.Position,why);Assert.AreEqual(new PixelPoint(e[4],e[5]),f.Offset,why);Assert.AreEqual(e[6],f.Tile,why);Assert.AreEqual(e[7]!=0,f.Visible,why);Assert.AreEqual(e[8],f.OamPriority,why);Assert.AreEqual(e[9],f.Subpriority,why);Assert.IsTrue(f.Affine,why);Assert.AreEqual(kind==2,f.DoubleSize,why);Assert.AreEqual(kind==0,f.Blend,why);Assert.AreEqual(kind==0,sprite.Counted,why);
                    var frames=Field<SourceSpriteFrameProgram?>(sprite,"_frames");Assert.AreEqual(e[10]!=0,frames?.Ended??false,why);Assert.AreEqual(e[11],frames?.DelayCounter??0,why);
                    var affine=Field<SourceSpriteAffineProgram?>(sprite,"_affine");CollectionAssert.AreEqual(e.Skip(12).Take(3).ToArray(),new[]{affine?.XScale??256,affine?.YScale??256,affine?.Rotation??0},why);Assert.AreEqual(e[15]!=0,affine?.Ended??false,why);
                    if(kind==2)Assert.AreEqual(0,f.Tile,why+" source does not select its cycling task index");
                }
                rows++;
            }
            owner.Cancel();Assert.AreEqual(0,scene.Count);cases++;
        }
        Assert.AreEqual(102,cases);Assert.AreEqual(3172,rows);
    }
    [TestMethod]
    public void CompleteRootComposesSharedMaterialsAndDrainsEachRawSweepBeforeRestoringIdle()
    {
        using var doc=Vectors();using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(344);
        foreach(var (doubles,actor,target) in new[]{(false,0,1),(false,1,0),(true,0,3),(true,1,2),(true,2,1),(true,3,0)})
        {
            var bodies=Bodies(doubles);var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >10 and <54&&i/64 is >6 and <58?(i%7<3?0x83e0:0x801f):0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var idle=scene.CaptureIdle(bodies,doubles);var audio=new Audio();var owner=new SourceBattleAnimation(r.Script,Cue(actor,target),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var replay=new SourceBattleAnimation(r.Script,Cue(actor,target),bodies,doubles,new Audio(),Terrain(resources),spriteMaterials:r.Assets);
            var seen=new HashSet<SourceBattleAnimation.VisualSprite>();var pixels=new HashSet<string>();int raw=0,ticks=0,maxLive=0;bool moved=false,blink=false;
            while(owner.Active&&ticks++<1000)
            {
                var f=owner.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));foreach(var sprite in scene.Sprites.Where(s=>seen.Add(s)))if(sprite.Frame.Template==Bolt){Assert.IsFalse(sprite.Counted);raw++;}
                maxLive=Math.Max(maxLive,scene.Sprites.Count(s=>s.Frame.Template==Bolt));var rgba=composer.ComposeField(f);foreach(string name in new[]{Orb,Bolt,Puff})if(f.Sprites.Any(s=>s.Template==name)&&!rgba.SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!=name).ToArray()})))pixels.Add(name);
                moved|=Math.Abs(f.Offsets[actor].X)>32;blink|=f.BackgroundSlots.Length==0&&f.BattlerVisibility[actor].SpriteInvisible;
            }
            int expected=doc.RootElement.GetProperty("cases").EnumerateArray().Where(c=>c.GetProperty("kind").GetInt32()==2&&c.GetProperty("doubles").GetBoolean()==doubles&&c.GetProperty("actor").GetInt32()==actor&&c.GetProperty("target").GetInt32()==target&&c.GetProperty("label").GetString()=="authored").Sum(c=>c.GetProperty("rows").EnumerateArray().SelectMany(row=>row.GetProperty("sprites").EnumerateArray().Select(s=>s[1].GetInt32())).DefaultIfEmpty(0).Max());
            Assert.AreEqual(expected,raw);Assert.IsTrue(maxLive is >0 and <=17);Assert.AreEqual(3,pixels.Count);Assert.IsTrue(moved&&blink);Assert.IsFalse(owner.Active||replay.Active||owner.GraphicsLoaded||owner.HasResidualSprites);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.Snapshot().BlendEnabled);Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));Assert.AreEqual(0,owner.Snapshot().BackgroundSlots.Length);CollectionAssert.AreEqual(composer.ComposeField(idle),composer.ComposeField(owner.Snapshot()));
            string[] sounds=["SE_M_CHARGE",..Enumerable.Repeat("SE_M_THUNDERBOLT",5),"SE_M_SELF_DESTRUCT","SE_M_THUNDERBOLT2"];CollectionAssert.AreEqual(sounds,audio.Cues.Select(s=>s.Id).ToArray());
        }
    }
    [TestMethod]
    public void CancellationRestoresHiddenPartnersAndAllOwnedStateDuringEveryPhase()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(344);
        foreach(int stop in new[]{0,1,10,30,60,85,110,145,180,215,245,275,310})
        {var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(r.Script,Cue(0,1),Bodies(true,3),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var visibility=owner.Snapshot().BattlerVisibility;for(int i=0;i<stop&&owner.Active;i++)owner.Tick();int sounds=audio.Cues.Count;owner.Cancel();owner.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.Active||owner.GraphicsLoaded||owner.Snapshot().BlendEnabled);Assert.AreEqual(0,owner.Snapshot().BackgroundSlots.Length);Assert.AreEqual(0,owner.Snapshot().PaletteBlends.Length);Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(JsonSerializer.Serialize(visibility),JsonSerializer.Serialize(owner.Snapshot().BattlerVisibility));}
    }
    [TestMethod]
    public void InvalidRecipesRawDependenciesOperandsAndCapacityFailBeforeSideEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(344).Script;
        void Reject(SourceAnimationScript bad,SourceBattleAnimation.SpriteScene? scene=null){scene??=new();int before=scene.Count;var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(0,1),Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(before,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
        foreach(string name in new[]{Orb,Bolt,Puff}){var r=s.SpriteRecipes[name];foreach(var bad in new[]{r with{Callback="Other"},r with{Width=128},r with{Blend=!r.Blend},r with{Animations=[]}})Reject(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)});}
        Reject(s with{SpriteDependencies=[]});foreach(var c in new[]{C("createvisualtask","AnimTask_VoltTackleBolt","5","5"),C("createvisualtask","AnimTask_VoltTackleAttackerReappear","5","0"),C("createsprite",Orb,"0","2","0"),C("createvisualtask","AnimTask_TraceMonBlended","2","0","1","3","4")})Reject(s with{Commands=[c,..s.Commands]});
        var doubled=s.Commands.SelectMany(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_VoltTackleBolt"?new[]{c,c}:new[]{c}).ToArray();Reject(s with{Commands=doubled});
        foreach(int fragments in new[]{23,24}){var scene=new SourceBattleAnimation.SpriteScene();scene.Sprites.Add(new Occupied(63,fragments));if(fragments==23){var o=new SourceBattleAnimation(s,Cue(0,1),Bodies(true),true,new Audio(),spriteScene:scene);o.Cancel();}else Reject(s,scene);Assert.AreEqual(1,scene.Count);}
        var matrices=new SourceBattleAnimation.SpriteScene();for(int n=4;n<16;n++)matrices.Sprites.Add(new Occupied(n,1,true));Reject(s,matrices);
    }
}
