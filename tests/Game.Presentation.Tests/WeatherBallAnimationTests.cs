using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WeatherBallAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gWeatherBallUpSpriteTemplate","gWeatherBallNormalDownSpriteTemplate","gWeatherBallFireDownSpriteTemplate","gWeatherBallWaterDownSpriteTemplate","gWeatherBallRockDownSpriteTemplate","gWeatherBallIceDownSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot,bool affine):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags)!.GetValue(o)!;
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags)!.Invoke(o,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static CommittedMoveAnimation Cue(int actor,int target,ushort? weather)=>new("weather-ball",1,311,new(actor),new(target)){AnimationWeather=weather};
    private static AnimationBattler[] Bodies(bool doubles,int y=7,int elevation=0,int flags=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,y,elevation,64,64){SourceVisibility=new(true,(flags&1)!=0,(flags&2)!=0)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static int[] V(JsonElement row,string key)=>row.GetProperty(key).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("weather-ball-source-vectors.json","python tests/Reference/WeatherBall/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/weather-ball-source-vectors.json");

    [TestMethod]
    public void NativeMotionWordWrappingFrameAffinePlayersAndDeferredDestroyMatchEveryRow()
    {
        using var doc=Vectors();Assert.AreEqual("firered-weather-ball-v1",doc.RootElement.GetProperty("schema").GetString());var script=AnimationCompositionFixture.Script(311);int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32();bool doubles=c.GetProperty("doubles").GetBoolean();
            var bodies=Bodies(doubles,c.GetProperty("yOffset").GetInt32(),c.GetProperty("elevation").GetInt32());var owner=new SourceBattleAnimation(Primitive(script),Cue(actor,target,null),bodies,doubles,new Audio());
            var args=Field<short[]>(owner,"_animationArgs");int n=0;foreach(int value in V(c,"arguments"))args[n++]=(short)value;
            // Source picture coordinates ignore shifted raw battler positions/x2/y2.
            foreach(var b in bodies){var p=b.Base(doubles);Call(owner,"SetBattlerPosition",b.Slot.Value,new PixelPoint(p.X+17,p.Y+11));Field<PixelPoint[]>(owner,"_offsets")[b.Slot.Value]=new(7,9);}
            var sprite=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("WeatherBallProjectile",BindingFlags.NonPublic)!,Flags,null,[8,2,script.SpriteRecipes[Templates[kind]],owner],CultureInfo.InvariantCulture)!;
            if(kind==0)Field<short[]>(sprite,"_d")[2]=(short)c.GetProperty("seedX").GetInt32();
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=row.GetProperty("frame").GetInt32();if(tick>=0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}string why=$"case {cases} kind {kind} frame {tick}";var expected=V(row,"sprite");var frame=sprite.Frame;
                Assert.AreEqual(new PixelPoint(expected[0],expected[1]),frame.Position,why);Assert.AreEqual(new PixelPoint(expected[2],expected[3]),frame.Offset,why);Assert.AreEqual(expected[4],frame.Tile,why);Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,!sprite.Finished,why);Assert.IsTrue(frame.Visible,why);
                var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");if(kind!=5)Assert.AreEqual(expected[5]!=0,frames.Ended,why);Assert.AreEqual(expected[6],frames.DelayCounter,why);
                if(kind==0)CollectionAssert.AreEqual(V(row,"data").Take(4).Select(v=>(short)v).ToArray(),Field<short[]>(sprite,"_d"),why);
                else if(!sprite.Finished)Assert.AreEqual(row.GetProperty("callback").GetInt32()==3,Field<bool>(sprite,"_destroyNext"),why);
                if(kind>=3){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");CollectionAssert.AreEqual(V(row,"affine"),new[]{affine.XScale,affine.YScale,affine.Rotation,affine.Ended?1:0},why);Assert.IsTrue(frame.Affine,why);}else Assert.IsFalse(frame.Affine,why);
                CollectionAssert.AreEqual(V(c,"arguments").Select(v=>(short)v).ToArray(),args.Take(7).ToArray(),why);rows++;
            }
            Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;
        }
        Assert.AreEqual(48,cases);Assert.IsTrue(rows>1300);
    }

    [TestMethod]
    public void AllControllerWeatherBitsUseNativeSunFirstPriorityAndPreserveOtherRegisters()
    {
        using var doc=Vectors();var script=AnimationCompositionFixture.Script(311);int cases=0;
        foreach(var row in doc.RootElement.GetProperty("predicates").EnumerateArray())
        {
            ushort weather=(ushort)row.GetProperty("weather").GetInt32();var owner=new SourceBattleAnimation(Primitive(script),Cue(0,1,weather),Bodies(false),false,new Audio());var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<7;i++)args[i]=(short)((i+1)*11);args[7]=-30000;
            Assert.IsTrue((bool)Call(owner,"TryRunWeatherPredicate",C("createvisualtask","AnimTask_GetWeather","2"))!);CollectionAssert.AreEqual(V(row,"args").Select(v=>(short)v).ToArray(),args.Take(8).ToArray());Assert.AreEqual(0,row.GetProperty("alive").GetInt32());Assert.AreEqual(0,owner.ActiveVisualCount);owner.Cancel();cases++;
        }
        Assert.AreEqual(256,cases);
    }

    [TestMethod,DataRow(0,1),DataRow(32,2),DataRow(1,3),DataRow(8,4),DataRow(128,5)]
    public void CompleteSourceBranchesRenderBothSidesAllSlotsAndIndependentVisibility(int weather,int branch)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(311);int cases=0;
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int flags in new[]{0,1,2,3})
        {
            var bodies=Bodies(doubles,7,0,flags);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)(0x83e0+b.Slot.Value*7),4096).ToArray());
            Assert.AreEqual(1024,assets["weather-ball"].PaletteIndices.Length);Assert.AreEqual(16,assets["weather-ball"].Palette.Length);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(actor,actor^1,(ushort)weather),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);var initial=owner.Snapshot();var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);int ticks=0;bool pixels=false;var seen=new HashSet<string>();
            while(owner.Active&&ticks++<500)
            {
                var frame=owner.Tick();foreach(var sprite in frame.Sprites)seen.Add(sprite.Template);
                if(flags==0||frame.Sprites.Any(s=>s.Template==Templates[branch])&&!pixels)
                {string before=JsonSerializer.Serialize(frame);var rendered=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rendered.Length);if(frame.Sprites.Any(s=>s.Template==Templates[branch]))pixels|=!rendered.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Templates[branch]).ToArray()}));Assert.AreEqual(before,JsonSerializer.Serialize(owner.Snapshot()));}
            }
            string why=$"weather {weather}, actor {actor}, doubles {doubles}, flags {flags}";Assert.IsTrue(ticks<500,why);Assert.IsTrue(pixels,why);Assert.IsTrue(seen.Contains(Templates[0]),why);Assert.IsTrue(seen.Contains(Templates[branch]),why);Assert.IsFalse(Templates.Skip(1).Where((_,i)=>i+1!=branch).Any(seen.Contains),why);
            Assert.AreEqual(branch==4,seen.Contains("gRockScatterSpriteTemplate"),why);Assert.AreEqual(branch==5,seen.Contains("gIceCrystalHitLargeSpriteTemplate"),why);Assert.AreEqual(branch==5,seen.Contains("gIceCrystalHitSmallSpriteTemplate"),why);AssertClean(owner);Assert.AreEqual(0,scene.Count,why);CollectionAssert.AreEqual(initial.BattlerVisibility,owner.Snapshot().BattlerVisibility,why);Assert.IsTrue(audio.Cues.Count>=3,why);Assert.AreEqual("SE_M_SWAGGER",audio.Cues[0].Id,why);Assert.AreEqual(actor%2==0?-64:63,audio.Cues[0].Pan,why);cases++;
        }
        Assert.AreEqual(24,cases);
    }
    private static void AssertClean(SourceBattleAnimation owner)
    {Assert.IsFalse(owner.Active);Assert.IsFalse(owner.GraphicsLoaded);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.HasResidualSprites);var f=owner.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.Offsets.All(p=>p==default));}

    [TestMethod]
    public void CancellationAtEveryTickReleasesEveryWeatherBranchWithoutChangingCapturedVisibility()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(311);
        foreach(ushort weather in new ushort[]{0,32,1,8,128})
        {
            var bodies=Bodies(true,7,0,3);var baseline=new SourceBattleAnimation(r.Script,Cue(2,3,weather),bodies,true,new Audio(),Terrain(resources));int duration=0;while(baseline.Active&&duration++<500)baseline.Tick();AssertClean(baseline);Assert.IsTrue(duration<500);
            for(int stop=0;stop<=duration;stop++)
            {var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(r.Script,Cue(2,3,weather),bodies,true,audio,Terrain(resources),spriteScene:scene);var initial=owner.Snapshot();for(int i=0;i<stop&&owner.Active;i++)owner.Tick();int sounds=audio.Cues.Count;owner.Cancel();owner.Cancel();AssertClean(owner);Assert.AreEqual(0,scene.Count);CollectionAssert.AreEqual(initial.BattlerVisibility,owner.Snapshot().BattlerVisibility);CollectionAssert.AreEqual(initial.BattlerAffine,owner.Snapshot().BattlerAffine);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);}
        }
    }

    [TestMethod]
    public void MissingContextMalformedRecipesWidthsAndMixedClosureCapacityFailBeforePlayback()
    {
        var script=AnimationCompositionFixture.Script(311);
        void Reject(SourceAnimationScript s,ushort? weather=0,int occupied=0,bool affine=false)
        {var scene=new SourceBattleAnimation.SpriteScene();for(int i=0;i<occupied;i++)Call(Field<object>(scene,"Sprites"),"Add",new Occupied(63-i,affine));var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(0,1,weather),Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(occupied,scene.Count);}
        Reject(script,null);Reject(script,256);Reject(script,65535);Reject(script,occupied:40);Reject(script,occupied:25,affine:true);
        foreach(string name in Templates)
        {
            var recipe=script.SpriteRecipes[name];foreach(var bad in new[]{recipe with{Width=64},recipe with{Callback="UnknownWeather"},recipe with{Asset="unknown"},recipe with{AffineDoubleSize=true},recipe with{Animations=[[new(255,1,false,false)]]}})
                Reject(script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)});
        }
        var down=script.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]==Templates[1]);
        foreach(var args in new[]{down.Args[..^1],new[]{Templates[1],"ANIM_TARGET","128","-30","-100","25","1","0","0"},new[]{Templates[1],"ANIM_TARGET","2","-30","-100","0","1","0","0"},new[]{Templates[1],"ANIM_TARGET","2","-30","-100","25","1","32768","0"}})
            Reject(script with{Commands=script.Commands.Select(c=>ReferenceEquals(c,down)?c with{Args=args}:c).ToArray()});
        var predicate=C("createvisualtask","AnimTask_GetWeather","2","0");Reject(Primitive(script) with{Commands=[predicate,C("end")]});
        // The shared rock closure remains bounded for mixed affine companions,
        // independent of the move identity and without admitting raw-child tasks.
        var rock=script.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]=="gRockScatterSpriteTemplate");Reject(Primitive(script) with{Commands=[rock,C("createvisualtask","AnimTask_CreateSurfWave","2","0"),C("end")]});
        // A structurally present but uninvoked helper needs recipe/ABI validation,
        // while its unused weather predicate must not invent invocation context.
        var noWeather=Primitive(script) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"unused",[C("createvisualtask","AnimTask_GetWeather","2"),C("return")]}}};var inert=new SourceBattleAnimation(noWeather,Cue(0,1,null),Bodies(false),false,new Audio());inert.Tick();AssertClean(inert);
    }
}
