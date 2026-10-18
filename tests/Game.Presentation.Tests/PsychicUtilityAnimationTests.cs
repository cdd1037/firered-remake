using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PsychicUtilityAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static ushort[] TerrainPalette(SourceAnimationResources resources){var surface=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();}
    private static readonly string[] Templates=["gBentSpoonSpriteTemplate","gKinesisZapEnergySpriteTemplate","gPsychUpSpiralSpriteTemplate","gLusterPurgeCircleSpriteTemplate"];
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,args,null)!;
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Bodies(bool doubles,bool hidden=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,(i&1)==0?0:8,0,64,64){SourceVisibility=new(true,false,hidden&&i>=2)}).ToArray();
    private static CommittedMoveAnimation Cue(int id,int actor,int target)=>new("psychic",1,id,new(actor),new(target)){VisualSeed=0x12345678};
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("psychic-utility-source-vectors.json","python tests/Reference/PsychicUtility/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/psychic-utility-source-vectors.json");

    [TestMethod]
    public void EveryNativeSpriteFrameMatchesFiniteLoopsXorFlipsAffineStateAndDeferredDestruction()
    {
        using var v=Vectors();int cases=0,rows=0;
        foreach(var c in v.RootElement.GetProperty("sprites").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),config=c.GetProperty("config").GetInt32(),id=kind<2?134:kind==2?244:295;
            bool doubles=c.GetProperty("doubles").GetBoolean();var script=AnimationCompositionFixture.Script(id);var o=new SourceBattleAnimation(Primitive(script),Cue(id,actor,actor^1),Bodies(doubles),doubles,new Audio());
            var args=Field<short[]>(o,"_animationArgs");if(kind==1){args[0]=32;args[1]=(short)(config==0?-8:16);args[2]=(short)config;}
            var sprite=(SourceBattleAnimation.VisualSprite)(kind<2?Nested("KinesisFrameSprite",4,2,script.SpriteRecipes[Templates[kind]],o):Nested("ElementalSprite",4,2,script.SpriteRecipes[Templates[kind]],new AnimationSlot(actor),new int[4],o));
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                var r=N(row);if(r[0]>=0){sprite.Step(o);if(!sprite.Finished)sprite.AdvanceAnimation(o);}var f=sprite.Frame;string why=$"kind{kind} doubles{doubles} actor{actor} config{config} frame{r[0]}";
                Assert.AreEqual(r[1]==0,sprite.Finished,why);Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position,why);Assert.AreEqual(r[4],f.Tile,why);Assert.AreEqual(r[5]!=0,f.HFlip,why);Assert.AreEqual(r[6]!=0,f.VFlip,why);
                var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(r[7]!=0,frames.Ended,why);Assert.AreEqual(r[13],frames.DelayCounter,why);Assert.AreEqual(r[14],frames.LoopCounter,why);
                if(kind<2)Assert.AreEqual(r[12]!=0,Field<bool>(sprite,"_destroyReady"),why);
                else {var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(r[8],affine.XScale,why);Assert.AreEqual(r[9],affine.YScale,why);Assert.AreEqual(r[10],affine.Rotation,why);Assert.AreEqual(r[11]!=0,affine.Ended,why);}
                rows++;
            }
            cases++;
        }
        Assert.AreEqual(30,cases);Assert.AreEqual(3156,rows);
    }

    [TestMethod]
    public void ExcludedPaletteCycleMatchesEveryOriginalPaletteFrameAndCountsHiddenPartnerSlots()
    {
        using var v=Vectors();int cases=0,rows=0;var source=AnimationCompositionFixture.Script(244);
        foreach(var c in v.RootElement.GetProperty("palettes").EnumerateArray())
        {
            bool doubles=c.GetProperty("doubles").GetBoolean();int actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32();var args=N(c.GetProperty("arguments"));
            var o=new SourceBattleAnimation(Primitive(source),Cue(244,actor,target),Bodies(doubles,true),doubles,new Audio());
            if(c.GetProperty("prior").GetBoolean())
            {var prior=Nested("PaletteFade",1u<<16,0,16,(ushort)31,0);o.GetType().GetField("_fade",Private)!.SetValue(o,prior);Invoke(prior,"Update",o);}
            Assert.IsTrue((bool)Invoke(o,"TryCreatePsychicUtilityTask",C("createvisualtask",["AnimTask_BlendColorCycleExclude","2",..args.Select(x=>x.ToString())]))!);
            var task=Field<IEnumerable>(o,"_tasks").Cast<object>().Single();
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                var r=N(row);if(r[0]>=0){var fade=Field<object?>(o,"_fade");if(fade is not null)Invoke(fade,"Update",o);Invoke(task,"Step",o);}
                var f=o.Snapshot();string why=$"palette case{cases} frame{r[0]}";Assert.AreEqual(r[1]==0,(bool)task.GetType().GetProperty("Finished")!.GetValue(task)!,why);Assert.AreEqual(r[3],Field<int>(task,"_remaining"),why);Assert.AreEqual(r[4]!=0,Field<bool>(task,"_restore"),why);
                int n=7;foreach(int bank in new[]{1,2,3,16,17,18,19})for(int index=0;index<16;index++)
                {int i=bank*16+index;ushort original=(ushort)(((i+3)&31)|(((i*7+5)&31)<<5)|(((i*11+9)&31)<<10));Assert.AreEqual((ushort)r[n++],SourcePaletteMath.Apply(original,f.PaletteBlends.SingleOrDefault(p=>p.Palette==bank),false),why+$" bank{bank} index{index}");}
                rows++;
            }
            cases++;
        }
        Assert.IsTrue(cases>=9&&rows>=1097);
    }

    [TestMethod,DataRow(134),DataRow(244),DataRow(295)]
    public void AuthoredRootsComposeEveryFrameAcrossSixLayoutsAndRestoreSharedScene(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());
            foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(resolved.Script,Cue(id,actor,actor^1),bodies,doubles,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:resolved.Assets);
            int ticks=0,peak=0;bool pixels=false,affine=false,psychic=false;var templates=new HashSet<string>();
            while(o.Active&&ticks++<1000)
            {
                var frame=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(o.Snapshot()));var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);peak=Math.Max(peak,frame.Sprites.Length);affine|=frame.Sprites.Any(s=>s.Affine);psychic|=frame.MoveBackground?.Id is not null;
                foreach(var s in frame.Sprites)templates.Add(s.Template);
                if(frame.Sprites.Any(s=>s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(pixels);Assert.IsTrue(peak>0);Assert.AreEqual(id!=134,affine);Assert.AreEqual(id!=244,psychic);Assert.IsTrue(templates.Contains(id==134?Templates[0]:id==244?Templates[2]:Templates[3]));if(id==134)Assert.IsTrue(templates.Contains(Templates[1]));if(id==295)Assert.IsTrue(templates.Contains("gRandomPosHitSplatSpriteTemplate"));
            var final=o.Snapshot();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsTrue(final.Offsets.All(x=>x==default));Assert.IsTrue(final.BattlerAffine.All(x=>!x.DoubleSize));Assert.IsNull(final.MoveBackground!.Id);Assert.IsTrue(audio.Cues.Count>0);
            if(id!=295)Assert.IsNull(Field<object?>(o,"_visualRandom"));
        }
    }

    [TestMethod,DataRow(134),DataRow(244),DataRow(295)]
    public void CancellationReleasesPaletteAffineBackgroundAndSpriteOwnership(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        foreach(int tick in new[]{0,1,20,40,80,120,170,220,280})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(resolved.Script,Cue(id,3,0),Bodies(true),true,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:resolved.Assets);for(int i=0;i<tick&&o.Active;i++)o.Tick();int sounds=audio.Cues.Count;o.Cancel();o.Cancel();var f=o.Snapshot();
            Assert.IsFalse(o.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(f.BlendEnabled);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.IsTrue(f.Offsets.All(x=>x==default));Assert.IsTrue(f.BattlerAffine.All(x=>!x.DoubleSize));Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);
        }
    }

    [TestMethod]
    public void MalformedRecipesFiniteLoopsAffineProgramsAndCycleOperandsFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int id in new[]{134,244,295})
        {
            var source=AnimationCompositionFixture.Script(id);string template=id==134?Templates[0]:id==244?Templates[2]:Templates[3];var r=source.SpriteRecipes[template];
            void Reject(SourceAnimationScript script){var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(id,0,1),Bodies(false),false,audio,TerrainPalette(resources)));Assert.AreEqual(0,audio.Cues.Count);}
            foreach(var bad in new[]{r with{Width=8},r with{Callback="SpriteCallbackDummy"},r with{Blend=!r.Blend},r with{AffineDoubleSize=!r.AffineDoubleSize},r with{Animations=[]},id==134?r with{FrameLoops=[]}:r with{AffineAnimations=[]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Reject(source with{SpriteRecipes=recipes});}
            Reject(source with{SpritePalettes=new Dictionary<int,ushort[]>()});
        }
        var psych=Primitive(AnimationCompositionFixture.Script(244));
        foreach(string[] args in new[]{new[]{"1","-14","6","1","11","0"},new[]{"1","64","6","1","11","0"},new[]{"1","128","6","1","11","0"},new[]{"1","2","0","1","11","0"},new[]{"1","2","6","1","17","0"}})
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(psych with{Commands=[C("createvisualtask",["AnimTask_BlendColorCycleExclude","2",..args]),C("end")]},Cue(244,0,1),Bodies(false),false,new Audio()));
    }
}
