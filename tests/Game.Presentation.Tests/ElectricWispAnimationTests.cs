using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ElectricWispAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,args);
    private static object Nested(string n,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,args,CultureInfo.InvariantCulture)!;
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static readonly string[] Templates=["gGrowingChargeOrbSpriteTemplate","gElectricPuffSpriteTemplate","gGrowingShockWaveOrbSpriteTemplate","gWillOWispOrbSpriteTemplate","gWillOWispFireSpriteTemplate"];
    private static readonly string[] Raw=["gElectricChargingParticlesSpriteTemplate","sShockWaveProgressingBoltSpriteTemplate","gLightningSpriteTemplate"];
    private static AnimationBattler[] Bodies(bool doubles,bool hidden=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){SourceVisibility=new(true,false,hidden&&i>=2)}).ToArray();
    private static CommittedMoveAnimation Cue(int id,int actor,int target)=>new("electric-wisp",1,id,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void EveryNativeFrameMatchesAffineLoopsRawChildrenCapacityRetriesWispAudioAndPriority()
    {
        using var doc=SourceVectorFixture.Read("electric-wisp-source-vectors.json","python tests/Reference/ElectricWisp/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/electric-wisp-source-vectors.json");int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),occupied=c.GetProperty("occupied").GetInt32(),release=c.GetProperty("release").GetInt32(),id=kind is 3 or 4?261:kind is 2 or 6 or 7?351:268;bool doubles=c.GetProperty("doubles").GetBoolean();
            var script=AnimationCompositionFixture.Script(id);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(Primitive(script),Cue(id,actor,target),Bodies(doubles),doubles,audio,spriteScene:scene);var backgroundPriority=Field<int[]>(owner,"_battlerBackgroundPriorities");backgroundPriority[0]=1;backgroundPriority[1]=2;var args=Field<short[]>(owner,"_animationArgs");int j=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[j++]=(short)a.GetInt32();owner.GetType().GetField("_customPan",Flags)!.SetValue(owner,c.GetProperty("customPan").GetInt32());var sprites=Field<object>(owner,"_sprites");var blockers=new List<Occupied>();for(int slot=64-occupied;slot<64;slot++){var blocker=new Occupied(slot);blockers.Add(blocker);Invoke(sprites,"Add",blocker);}
            SourceBattleAnimation.VisualSprite? single=kind<5?(SourceBattleAnimation.VisualSprite)Nested("ElectricWispSprite",doubles?4:2,2,script.SpriteRecipes[Templates[kind]],owner):null;
            object? task=kind>=5?Nested("ElectricWispTask",kind-5,owner):null;
            var generations=new Dictionary<SourceBattleAnimation.VisualSprite,int>();int generation=single is null?0:1;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=row.GetProperty("frame").GetInt32();string why=$"case{cases} kind{kind} actor{actor} target{target} frame{tick}";
                if(tick>0)
                {
                    if(tick==release)foreach(var blocker in blockers)Invoke(sprites,"Remove",blocker);
                    if(single is not null){single.Step(owner);if(!single.Finished)single.AdvanceAnimation(owner);}else{Invoke(owner,"AdvanceSourceSprites");Invoke(task!,"Step",owner);}
                }
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,single is not null?!single.Finished:!(bool)task!.GetType().BaseType!.GetProperty("Finished",Flags)!.GetValue(task)!,why);
                CollectionAssert.AreEqual(row.GetProperty("args").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),args.Take(8).ToArray(),why);Assert.IsTrue(args.Skip(8).All(v=>v==0),why+" untouched extended registers");
                if(task is not null)CollectionAssert.AreEqual(row.GetProperty("task").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),Field<short[]>(task,"_d"),why);
                var actual=single is not null?new[]{single}:((IEnumerable)sprites).Cast<SourceBattleAnimation.VisualSprite>().Where(s=>s is not Occupied).OrderBy(s=>s.Slot).ToArray();var expected=row.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(expected.Length,actual.Length,why);
                for(int i=0;i<actual.Length;i++)
                {
                    var sprite=actual[i];if(!generations.TryGetValue(sprite,out int instance)){instance=single is not null?1:++generation;generations.Add(sprite,instance);}int[] e=expected[i].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=sprite.Frame;
                    Assert.AreEqual(e[0],f.Slot,why);Assert.AreEqual(e[1],instance,why);if(task is not null)Assert.AreEqual(Raw[e[2]],f.Template,why);Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Position,why);Assert.AreEqual(new PixelPoint(e[5],e[6]),f.Offset,why);Assert.AreEqual(e[7],f.Tile,why);Assert.AreEqual(e[8]!=0,f.Visible,why);Assert.AreEqual(e[9],f.OamPriority,why);Assert.AreEqual(e[10],f.Subpriority,why);
                    var frames=Field<SourceSpriteFrameProgram?>(sprite,"_frames");Assert.AreEqual(e[11]!=0,frames?.Ended??false,why);Assert.AreEqual(e[12],frames?.DelayCounter??0,why);
                    if(kind is 0 or 2){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(e[13],affine.XScale,why);Assert.AreEqual(e[14],affine.YScale,why);Assert.AreEqual(e[15]!=0,affine.Ended,why);}
                }
                var sounds=row.GetProperty("sounds").EnumerateArray().Select(v=>(Id:v[0].GetInt32(),Pan:v[1].GetInt32())).ToArray();Assert.AreEqual(sounds.Length,audio.Cues.Count,why);for(int i=0;i<sounds.Length;i++){Assert.AreEqual(sounds[i].Id,script.SoundEffects[audio.Cues[i].Id],why);Assert.AreEqual(sounds[i].Pan,audio.Cues[i].Pan,why);Assert.IsTrue(audio.Cues[i].ApplyPanning,why);}audio.Cues.Clear();rows++;
            }
            owner.Cancel();Assert.AreEqual(0,scene.Count);cases++;
        }
        Assert.IsTrue(cases>=78);Assert.IsTrue(rows>=6412);
    }
    [TestMethod,DataRow(261),DataRow(268),DataRow(351)]
    public void CompleteAuthoredRootsComposeTheCanonicalMaterialsAndDrainSharedOwners(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        foreach(var (doubles,actor,target) in new[]{(false,0,id==268?0:1),(false,1,id==268?1:0),(true,2,id==268?2:3),(true,3,id==268?3:2)})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(resolved.Script,Cue(id,actor,target),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);int ticks=0;bool pixels=false,affine=false;var templates=new HashSet<string>();
            while(owner.Active&&ticks++<1200){var frame=owner.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(owner.Snapshot()));var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);if(frame.Sprites.Any(s=>s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));affine|=frame.Sprites.Any(s=>s.Affine);foreach(var s in frame.Sprites)templates.Add(s.Template);}
            Assert.IsFalse(owner.Active);Assert.IsTrue(pixels);Assert.AreEqual(id!=261,affine);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);var final=owner.Snapshot();Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsTrue(audio.Cues.Count>0);Assert.IsNull(Field<SourceVisualRandom?>(owner,"_visualRandom"));
            foreach(string expected in id==261?new[]{Templates[3],Templates[4]}:id==268?new[]{Templates[0],Templates[1],Raw[0]}:new[]{Templates[2],Raw[0],Raw[1],Raw[2]})Assert.IsTrue(templates.Contains(expected),expected);
        }
    }
    [TestMethod,DataRow(261),DataRow(268),DataRow(351)]
    public void CancellationRestoresHiddenPartnersAndSharedPaletteOwnership(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(id);
        foreach(int stop in new[]{0,1,20,45,80,140,220})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,Cue(id,0,id==268?0:1),Bodies(true,true),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var visibility=o.Snapshot().BattlerVisibility;for(int n=0;n<stop&&o.Active;n++)o.Tick();int sounds=audio.Cues.Count;o.Cancel();o.Cancel();var f=o.Snapshot();Assert.AreEqual(0,scene.Count);Assert.IsFalse(o.Active);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(f.BlendEnabled);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(JsonSerializer.Serialize(visibility),JsonSerializer.Serialize(f.BattlerVisibility));
        }
    }
    [TestMethod]
    public void DuplicateRawTasksAndOversizedObjectMatrixOrTaskClosuresFailBeforeAudio()
    {
        using var resources=new SourceAnimationResources(Folder);
        void Reject(SourceAnimationScript s,int id,int occupied=0,bool affine=false)
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var registry=Field<object>(scene,"Sprites");
            for(int i=0;i<occupied;i++)Invoke(registry,"Add",new Occupied(63-i,affine));
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(id,0,id==268?0:1),Bodies(true),true,audio,Terrain(resources),spriteScene:scene));
            Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(occupied,scene.Count,"Preflight must not clear foreign source owners.");
        }
        var charge=AnimationCompositionFixture.Script(268);var shock=AnimationCompositionFixture.Script(351);var wisp=AnimationCompositionFixture.Script(261);
        foreach(string name in new[]{"AnimTask_ElectricChargingParticles","AnimTask_ShockWaveProgressingBolt","AnimTask_ShockWaveLightning"})
            Reject(shock with{Commands=shock.Commands.SelectMany(c=>c.Op=="createvisualtask"&&c.Args[0]==name?new[]{c,c}:new[]{c}).ToArray()},351);
        Reject(charge with{Commands=charge.Commands.SelectMany(c=>c.Op=="createsprite"&&c.Args[0]==Templates[0]?new[]{c,c}:new[]{c}).ToArray()},268);
        Reject(shock with{Commands=shock.Commands.Select(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_ElectricChargingParticles"?C("createvisualtask","AnimTask_ElectricChargingParticles","2","0","60","0","60"):c).ToArray()},351);
        var orb=C("createsprite",Templates[0],"0","2","0");
        Reject(Primitive(charge) with{Commands=[orb,C("end")]},268,29,true);
        Reject(shock,351,9);
        var fires=Enumerable.Repeat(C("createsprite",Templates[4],"0","2","0"),43);
        Reject(Primitive(wisp) with{Commands=[..fires,C("end")]},261);
        var palette=Enumerable.Repeat(C("createvisualtask","AnimTask_BlendBattleAnimPal","5","F_PAL_BG","0","0","0","RGB_BLACK"),16);
        Reject(Primitive(charge) with{Commands=[orb,..palette,C("end")]},268);
    }
    [TestMethod]
    public void MalformedRecipesArgumentsAndRawDependenciesFailBeforeAudio()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int id in new[]{261,268,351})
        {
            var source=AnimationCompositionFixture.Script(id);void Reject(SourceAnimationScript s){var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(id,0,id==268?0:1),Bodies(false),false,audio,Terrain(resources)));Assert.AreEqual(0,audio.Cues.Count);}
            foreach(string name in id==261?Templates.Skip(3):id==268?new[]{Templates[0],Templates[1],Raw[0]}:new[]{Templates[2],Raw[0],Raw[1],Raw[2]})
            {var r=source.SpriteRecipes[name];foreach(var bad in new[]{r with{Width=128},r with{Animations=[]},r with{Callback="wrong"},r with{Blend=!r.Blend}}){var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[name]=bad;Reject(source with{SpriteRecipes=recipes});}}
            if(id!=261){Reject(source with{SpriteDependencies=[]});foreach(string[] args in new[]{new[]{"AnimTask_ElectricChargingParticles","5","0","0","2","12"},new[]{"AnimTask_ElectricChargingParticles","5","0","20","32767","2"},new[]{"AnimTask_ElectricChargingParticles","5","0","20","0","0"}})Reject(Primitive(source) with{Commands=[C("createvisualtask",args),C("end")]});}
            else foreach(string variant in new[]{"-1","4"})Reject(Primitive(source) with{Commands=[C("createsprite",Templates[3],"1","2","0","0",variant),C("end")]});
        }
    }
}
