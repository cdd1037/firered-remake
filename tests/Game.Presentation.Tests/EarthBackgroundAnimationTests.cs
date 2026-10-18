using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class EarthBackgroundAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame){}}
    private sealed class Occupied(int slot,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static T Property<T>(object o,string n)=>(T)o.GetType().GetProperty(n,Flags)!.GetValue(o)!;
    private static void Set(object o,string n,object value)=>o.GetType().GetProperty(n,Flags)!.SetValue(o,value);
    private static object? Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,a);
    private static object Nested(string n,params object[] a)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,a,CultureInfo.InvariantCulture)!;
    private static readonly string[] Names=["AnimTask_PositionFissureBgOnBattler","AnimTask_GetSeismicTossDamageLevel","AnimTask_MoveSeismicTossBg","AnimTask_SeismicTossBgAccelerateDownAtEnd","gRockScatterSpriteTemplate"];
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Bodies(bool doubles,int y=0,int elevation=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,y,elevation,64,64)).ToArray();
    private static int[] V(JsonElement r,string key)=>r.GetProperty(key).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static ushort[] Terrain(SourceAnimationResources r){var b=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>b.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void EveryNativeFrameMatchesBg3VelocityHeldRegisterStopAndAffineScatter()
    {
        using var doc=SourceVectorFixture.Read("earth-backgrounds-source-vectors.json","python tests/Reference/EarthBackgrounds/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/earth-backgrounds-source-vectors.json");int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),id=kind==0?90:69,actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),damage=c.GetProperty("damage").GetInt32(),stop=c.GetProperty("stop").GetInt32();bool doubles=c.GetProperty("doubles").GetBoolean();var script=AnimationCompositionFixture.Script(id);
            var o=new SourceBattleAnimation(Primitive(script),new("earth-native",1,id,new(actor),new(target)){MoveDamage=damage},Bodies(doubles,c.GetProperty("yOffset").GetInt32(),c.GetProperty("elevation").GetInt32()),doubles,new Audio());var args=Field<short[]>(o,"_animationArgs");int i=0;foreach(int a in V(c,"arguments"))args[i++]=(short)a;
            var bg=V(c,"bg");Set(o,"BackgroundScrollX",(ushort)bg[0]);Set(o,"BackgroundScrollY",(ushort)bg[1]);
            object? callback=kind==1?null:kind==4?Nested("EarthScatterRock",8,2,script.SpriteRecipes[Names[4]],o):Nested("EarthBackgroundTask",Names[kind],o);
            if(kind==1)Assert.IsTrue((bool)Invoke(o,"TryCreateEarthBackgroundTask",C("createvisualtask",Names[1],"3"))!);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=row.GetProperty("frame").GetInt32();string why=$"case {cases}, kind {kind}, actor {actor}, target {target}, frame {frame}";
                if(frame>(kind==4?-1:0)){if(kind!=4&&frame==stop)args[7]=kind==0?args[2]:(short)4095;Invoke(callback!,"Step",o);if(kind==4&&!Property<bool>(callback!,"Finished"))Invoke(callback!,"AdvanceAnimation",o);}
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,callback is not null&&!Property<bool>(callback,"Finished"),why);CollectionAssert.AreEqual(V(row,"args").Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why);Assert.IsTrue(args.Skip(8).All(v=>v==0),why);
                var expected=V(row,"bg");CollectionAssert.AreEqual(expected,new[]{(int)Property<ushort>(o,"BackgroundScrollX"),Property<ushort>(o,"BackgroundScrollY"),Property<int>(o,"BackgroundScreenSize")},why);
                if(callback is not null)CollectionAssert.AreEqual(V(row,"data").Select(v=>(short)v).ToArray(),Field<short[]>(callback,"_d"),why);
                if(kind==0){Assert.IsFalse(Property<bool>(callback!,"Counted"));Assert.AreEqual(row.GetProperty("priority").GetInt32(),Property<int>(callback!,"Priority"));Assert.AreEqual(0,row.GetProperty("parentAlive").GetInt32());}
                if(kind==4)
                {
                    var sprite=(SourceBattleAnimation.VisualSprite)callback!;var f=sprite.Frame;var e=V(row,"sprite");Assert.AreEqual(new PixelPoint(e[0],e[1]),f.Position,why);Assert.AreEqual(new PixelPoint(e[2],e[3]),f.Offset,why);Assert.AreEqual(e[4],f.Tile,why);
                    var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(e[5]!=0,frames.Ended,why);Assert.AreEqual(e[6],frames.DelayCounter,why);
                    var a=Field<SourceSpriteAffineProgram>(sprite,"_affine");CollectionAssert.AreEqual(V(row,"affine"),new[]{a.XScale,a.YScale,a.Rotation,a.Ended?1:0},why);Assert.IsTrue(f.Affine);Assert.IsFalse(f.DoubleSize);
                }
                rows++;
            }
            o.Cancel();cases++;
        }
        Assert.IsTrue(cases>=57);Assert.IsTrue(rows>=1327);
    }
    [TestMethod,DataRow(90,0),DataRow(69,0),DataRow(69,32),DataRow(69,33),DataRow(69,65),DataRow(69,66),DataRow(69,100)]
    public void CompleteRootsComposeEveryLayoutChooseSourceDamageBranchAndDrainBgHolders(int id,int damage)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var r=resources.Resolve(id);
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            var bodies=Bodies(doubles);var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)(0x83e0+b.Slot.Value*7),4096).ToArray());
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,new("earth-full",1,id,new(actor),new(actor^1)){MoveDamage=id==69?damage:null},bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);int ticks=0,backgroundFrames=0,births=0;bool scroll=false,affine=false;
            while(o.Active&&ticks++<1400){var f=o.Tick();births+=f.Trace.Count(t=>t.EndsWith(":createsprite",StringComparison.Ordinal));Assert.AreEqual(240*112*4,composer.ComposeField(f).Length);if(f.MoveBackground is{Id:not null})backgroundFrames++;scroll|=f.MoveBackground is{ScrollY:not 0};affine|=f.Sprites.Any(s=>s.Template==Names[4]&&s.Affine);Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));}
            string why=$"move {id}, damage {damage}, doubles {doubles}, actor {actor}";Assert.IsFalse(o.Active,why);Assert.IsTrue(backgroundFrames>0,why);Assert.IsTrue(scroll,why);Assert.AreEqual(id==69,affine,why);var end=o.Snapshot();Assert.AreEqual(id==90?14:damage<33?10:damage<66?15:20,births,why);
            Assert.AreEqual(0,o.ActiveVisualCount,why);Assert.AreEqual(0,scene.Count,why);Assert.IsFalse(o.GraphicsLoaded,why);Assert.AreEqual(0,end.BackgroundSlots.Length,why);Assert.AreEqual(0,end.MoveBackground!.ScrollX,why);Assert.AreEqual(0,end.MoveBackground.ScrollY,why);Assert.AreEqual(1,end.MoveBackground.ScreenSize,why);Assert.IsNull(end.MoveBackground.Id,why);Assert.IsTrue(audio.Cues.Count>0,why);o.Cancel();
        }
    }
    [TestMethod,DataRow(90),DataRow(69)]
    public void CancelAtTaskAndSignalBoundariesRestoresBgAndRetainedSceneOwnership(int id)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var r=resources.Resolve(id);
        foreach(int stop in new[]{0,1,8,40,100,122,150,180,220,280})
        {
            var bodies=Bodies(true);bodies[2]=bodies[2] with{SourceVisibility=new(true,false,true)};var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,new("earth-cancel",1,id,new(0),new(1)){MoveDamage=id==69?66:null},bodies,true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var initial=o.Snapshot();for(int i=0;i<stop&&o.Active;i++)o.Tick();int cues=audio.Cues.Count;o.Cancel();o.Cancel();var f=o.Snapshot();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,f.MoveBackground!.ScrollX);Assert.AreEqual(0,f.MoveBackground.ScrollY);Assert.AreEqual(1,f.MoveBackground.ScreenSize);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(cues,audio.Cues.Count);Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(f.BattlerVisibility));
        }
    }
    [TestMethod]
    public void MissingDamageSignalsBadRecipesAndCapacityOverflowFailBeforeAudio()
    {
        void Reject(SourceAnimationScript s,int id,int? damage=66,int occupied=0,bool affine=false){var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var sprites=Field<object>(scene,"Sprites");for(int i=0;i<occupied;i++)Invoke(sprites,"Add",new Occupied(63-i,affine));Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("bad",1,id,new(0),new(1)){MoveDamage=damage},Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(occupied,scene.Count);}
        var seismic=AnimationCompositionFixture.Script(69);var fissure=AnimationCompositionFixture.Script(90);Reject(seismic,69,null);Reject(seismic,69,-1);Reject(seismic,69,66,23);Reject(seismic,69,66,9,true);Reject(fissure,90,occupied:29);
        foreach(var s in new[]{seismic,fissure}){var own=s.Commands.First(c=>c.Op=="createvisualtask"&&Names.Take(4).Contains(c.Args[0]));Reject(Primitive(s) with{Commands=[own,own,C("end")]},s.MoveId);Reject(s with{Commands=s.Commands.Select(c=>c.Op=="setarg"?C("delay","0"):c).ToArray()},s.MoveId);}
        var rock=C("createsprite",Names[4],"1","2","0","0","3","0");Reject(Primitive(seismic) with{Commands=[..Enumerable.Repeat(rock,43),C("end")]},69);
        foreach(string animation in new[]{"-1","6"})Reject(Primitive(seismic) with{Commands=[rock with{Args=[Names[4],"1","2","0","0","3",animation]},C("end")]},69);
        var recipes=seismic.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Names[4]]=recipes[Names[4]] with{AffineDoubleSize=true};Reject(seismic with{SpriteRecipes=recipes},69);
    }
}
