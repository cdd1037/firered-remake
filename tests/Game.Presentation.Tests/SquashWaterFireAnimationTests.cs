using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SquashWaterFireAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame){}}
    private sealed class Occupied(int slot,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};public override void Step(SourceBattleAnimation owner){}}
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Flags)!.GetValue(value)!;
    private static object? Invoke(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags)!.Invoke(value,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1,byte tier=3)=>new("squash",1,move,new(actor),new(target)){ActorHpQuarterTier=tier,MovePower=150};
    private static SourceAnimationScript Primitive(SourceAnimationScript source)=>source with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,0,0,64,64){SourceVisibility=new(true,false,false)}).ToArray();
    private static ushort[] Terrain(SourceAnimationResources resources){var s=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>s.PaletteColor(2,i)).ToArray();}
    private static SourceBattleAnimation.VisualSprite[] Live(SourceBattleAnimation animation)=>Field<IEnumerable>(animation,"_sprites").Cast<SourceBattleAnimation.VisualSprite>().ToArray();

    [TestMethod]
    [DataRow(284,3)][DataRow(323,0)][DataRow(323,1)][DataRow(323,2)][DataRow(323,3)]
    public void CompleteRootsComposeSourceSquashRawChildrenAndCleanupAtAllSixPositions(int move,int tier)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            string why=$"move {move}, tier {tier}, doubles {doubles}, actor {actor}";var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());
            foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >5 and <58&&i/64 is >5 and <58?(i%7<3?0x83e0:0x801f):0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();
            var animation=new SourceBattleAnimation(resolved.Script,Cue(move,actor,actor^1,(byte)tier),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);
            var initial=animation.Snapshot();var idle=composer.ComposeField(initial);int ticks=0,maxLaunchRocks=0;bool changed=false,effectPixels=false;var born=new HashSet<SourceBattleAnimation.VisualSprite>();
            while(animation.Active&&ticks++<1800)
            {
                var frame=animation.Tick();var pixels=composer.ComposeField(frame);Assert.AreEqual(240*112*4,pixels.Length,why);Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(animation.Snapshot()),why);
                changed|=!pixels.SequenceEqual(idle);if(frame.Sprites.Length!=0)effectPixels|=!pixels.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
                var live=Live(animation);foreach(var s in live)born.Add(s);maxLaunchRocks=Math.Max(maxLaunchRocks,live.Count(s=>s.Frame.Template=="gEruptionLaunchRockSpriteTemplate"));Assert.IsTrue(scene.Count<=64,why);
            }
            Assert.IsFalse(animation.Active,why);Assert.IsTrue(ticks>60&&changed&&effectPixels,why);Assert.AreEqual(0,scene.Count,why);Assert.AreEqual(0,animation.ActiveVisualCount,why);Assert.IsFalse(animation.GraphicsLoaded,why);Assert.IsNull(Field<object?>(animation,"_visualRandom"),why);
            var end=animation.Snapshot();Assert.IsNull(end.Scanline,why);Assert.IsNull(end.MoveBackground!.Id,why);Assert.AreEqual(0,end.BackgroundSlots.Length,why);Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(end.BattlerVisibility),why);CollectionAssert.AreEqual(idle,composer.ComposeField(end),why);
            if(move==284)
            {
                Assert.AreEqual(14,born.Count(s=>s.Frame.Template=="gEruptionLaunchRockSpriteTemplate"),why);Assert.AreEqual(7,maxLaunchRocks,why);Assert.AreEqual(6,born.Count(s=>s.Frame.Template=="gEruptionFallingRockSpriteTemplate"),why);
                Assert.AreEqual(2,audio.Cues.Count(c=>c.Id=="SE_M_EXPLOSION"),why);Assert.AreEqual(12,audio.Cues.Count(c=>c.Id=="SE_M_ROCK_THROW"),why);
            }
            else
            {
                Assert.AreEqual(new[]{5,7,10,20}[tier]+5*(tier+1),born.Count(s=>s.Frame.Template=="gSmallWaterOrbSpriteTemplate"),why);Assert.AreEqual(5*(tier+1),born.Count(s=>s.Frame.Template=="gWaterHitSplatSpriteTemplate"),why);
                CollectionAssert.AreEqual(new[]{"SE_M_HEADBUTT","SE_M_DIVE","SE_M_SURF"},audio.Cues.Select(c=>c.Id).ToArray(),why);
            }
            animation.Cancel();
        }
    }

    [TestMethod]
    [DataRow(284)][DataRow(323)]
    public void CancellationAcrossSourcePhaseBoundariesPreservesForeignResiduals(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(int stop in new[]{0,1,8,16,32,37,43,46,60,70,90,120,150,190,230,300})
        {
            var bodies=Bodies(true);var scene=new SourceBattleAnimation.SpriteScene();var prior=new SourceBattleAnimation(Primitive(resolved.Script),Cue(move),bodies,true,new Audio(),spriteScene:scene);Invoke(Field<object>(prior,"_sprites"),"Add",new Occupied(63));prior.Tick();Assert.IsFalse(prior.Active);Assert.IsTrue(prior.HasResidualSprites);
            var audio=new Audio();var animation=new SourceBattleAnimation(resolved.Script,Cue(move),bodies,true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var initial=animation.Snapshot();
            for(int i=0;i<stop&&animation.Active;i++)animation.Tick();int sounds=audio.Cues.Count;animation.Cancel();animation.Cancel();
            Assert.AreEqual(1,scene.Count);Assert.AreEqual(0,animation.ActiveVisualCount);Assert.IsFalse(animation.GraphicsLoaded);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);
            var end=animation.Snapshot();Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(end.BattlerVisibility));Assert.IsNull(end.Scanline);Assert.IsNull(end.MoveBackground!.Id);Assert.AreEqual(0,end.BackgroundSlots.Length);Assert.IsNull(Field<object?>(animation,"_visualRandom"));prior.Cancel();Assert.AreEqual(0,scene.Count);
        }
    }

    [TestMethod]
    [DataRow(284)][DataRow(323)]
    public void CapacityDependenciesAndMalformedProgramsFailBeforeAnyMutation(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var primitive=Primitive(source);
        void Reject(SourceAnimationScript script,int retained=0,bool affine=false,CommittedMoveAnimation? cue=null)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var registry=Field<object>(scene,"Sprites");for(int i=0;i<retained;i++)Invoke(registry,"Add",new Occupied(63-i,affine));var audio=new Audio();
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue??Cue(move),Bodies(true),true,audio,new ushort[16],spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(retained,scene.Count);
        }
        Reject(source,42);Reject(source,29,true);Reject(source with{SpriteDependencies=[]});Reject(source with{SineTable=source.SineTable.Take(319).ToArray()});
        var tags=source.SpriteTags.ToDictionary(p=>p.Key,p=>p.Value);tags.Remove(move==284?"ANIM_TAG_WARM_ROCK":"ANIM_TAG_GLOWY_BLUE_ORB");Reject(source with{SpriteTags=tags});
        var sounds=source.SoundEffects.ToDictionary(p=>p.Key,p=>p.Value);sounds.Remove(move==284?"SE_M_EXPLOSION":"SE_M_SURF");Reject(source with{SoundEffects=sounds});
        string delayedSound=move==284?"SE_M_EXPLOSION":"SE_M_SURF";var delayedTasks=Enumerable.Range(0,17).SelectMany(_=>new[]{C("waitplaysewithpan",delayedSound,"SOUND_PAN_TARGET","255"),C("waitforvisualfinish")});Reject(source with{Commands=source.Commands[..^1].Concat(delayedTasks).Append(C("end")).ToArray()});
        string launch=move==284?"AnimTask_EruptionLaunchRocks":"AnimTask_WaterSpoutLaunch";
        Reject(primitive with{Commands=[C("createvisualtask",launch,"2"),C("createvisualtask",launch,"2"),C("end")]});
        Reject(primitive with{Commands=[C("createvisualtask",launch,"2"),C("goto","skipDrain"),C("waitforvisualfinish"),C("createvisualtask",launch,"2"),C("end")],Labels=new Dictionary<string,int>{{"skipDrain",3}}});
        var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);string child=move==284?"gEruptionLaunchRockSpriteTemplate":"gSmallWaterOrbSpriteTemplate";recipes[child]=recipes[child] with{Width=32};Reject(source with{SpriteRecipes=recipes});
        if(move==323)
        {
            Reject(source,cue:Cue(move) with{ActorHpQuarterTier=null});Reject(source,cue:Cue(move) with{ActorHpQuarterTier=4});Reject(source,cue:Cue(move) with{MovePower=null});
            Reject(primitive with{Commands=[C("createvisualtask","AnimTask_WaterSpoutRain","5"),C("createvisualtask","AnimTask_WaterSpoutRain","5"),C("end")]});
        }
        else
        {
            foreach((string delay,string tile) in new[]{("-1","0"),("0","4")})Reject(primitive with{Commands=[C("createsprite","gEruptionFallingRockSpriteTemplate","0","40","100","-32",delay,"80",tile),C("end")]});
            foreach((string start,string end) in new[]{("0","32767"),("80","79"),("-129","80")})Reject(primitive with{Commands=[C("createsprite","gEruptionFallingRockSpriteTemplate","0","40","100",start,"0",end,"0"),C("end")]});
        }
    }

    [TestMethod]
    public void EmissionTierIsIndependentOfEqualQuantizedMovePower()
    {
        var source=AnimationCompositionFixture.Script(323);var counts=new List<int>();
        foreach(byte tier in new byte[]{0,1})
        {
            var script=Primitive(source) with{Commands=[C("loadspritegfx","ANIM_TAG_GLOWY_BLUE_ORB"),C("createvisualtask","AnimTask_WaterSpoutLaunch","5"),C("waitforvisualfinish"),C("end")]};
            var animation=new SourceBattleAnimation(script,Cue(323,tier:tier) with{MovePower=37},Bodies(false),false,new Audio());var born=new HashSet<SourceBattleAnimation.VisualSprite>();int frames=0;
            while(animation.Active&&frames++<500){animation.Tick();foreach(var s in Live(animation))born.Add(s);}Assert.IsFalse(animation.Active);counts.Add(born.Count);animation.Cancel();
        }
        CollectionAssert.AreEqual(new[]{5,7},counts);
    }
}
