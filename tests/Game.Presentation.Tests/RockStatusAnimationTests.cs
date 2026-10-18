using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RockStatusAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int friendship=0,int actor=0,int target=1)=>new("rock-status",1,move,new(actor),new(target)){Friendship=checked((byte)friendship),VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.InvertedPalettes.Length);Assert.AreEqual(0,f.GreyscaleSlots.Length);
    }
    [TestMethod]
    [DataRow(50,0)][DataRow(197,0)][DataRow(246,0)][DataRow(317,0)]
    public void EverySourceBranchResolvesRendersAndReplaysAcrossSidesAndDoubles(int move,int friendship)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        string[] expected=move switch{246=>["impact","rocks"],317=>["impact","rocks","x-sign"],_=>["impact","sparkle-4"]};CollectionAssert.AreEquivalent(expected,resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,friendship,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0;bool visible=false,pixels=false;var templates=new HashSet<string>();
            while(runtime.Active&&ticks++<700)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                foreach(var sprite in frame.Sprites){templates.Add(sprite.Template);if(sprite.Asset is not null&&sprite.BattlerCopySlot<0)Assert.IsNotNull(sprite.Material);}
                visible|=frame.Sprites.Any(s=>s.Visible);
                if(!pixels&&frame.Sprites.Any(s=>s.Visible&&s.Asset is not null))
                {var before=JsonSerializer.Serialize(runtime.Snapshot());pixels=!composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));}
            }
            Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(visible);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);
            string required=move switch{246=>"gAncientPowerRockSpriteTemplate",317=>"gRedXSpriteTemplate",_=>"gSpinningSparkleSpriteTemplate"};Assert.IsTrue(templates.Contains(required),required);

        }
    }
    [TestMethod]
    [DataRow(50,0)][DataRow(197,0)][DataRow(246,0)][DataRow(317,0)]
    public void CancellationAtEveryTickRestoresSharedSceneAndAffineState(int move,int friendship)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));var cue=Cue(move,friendship);var length=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteMaterials:assets);int ticks=0;while(length.Active&&ticks++<700)length.Tick();Assert.IsFalse(length.Active);
        for(int stop=0;stop<=ticks;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
        }
    }
    [TestMethod]
    public void RecipesAndIndexedPaletteDependenciesFailClosed()
    {
        foreach(var pair in new[]{(246,"gAncientPowerRockSpriteTemplate"),(317,"gRockTombRockSpriteTemplate"),(317,"gRedXSpriteTemplate"),(197,"gSpinningSparkleSpriteTemplate")})
        {
            var source=AnimationCompositionFixture.Script(pair.Item1);var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);var original=recipes[pair.Item2];
            foreach(var bad in new[]{original with{Width=8},original with{Animations=[[new(0,3,false,false)]]},original with{Callback="UnknownRockStatus"},original with{Blend=true}})
            {recipes[pair.Item2]=bad;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(pair.Item1),Battlers(false),false,new Audio()));}
            if(original.Asset!="x-sign")continue;
            string tag="ANIM_TAG_"+original.Asset.ToUpperInvariant().Replace('-','_');var palettes=source.SpritePalettes.Where(p=>p.Key!=source.SpriteTags[tag]).ToDictionary(p=>p.Key,p=>p.Value);
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpritePalettes=palettes},Cue(pair.Item1),Battlers(false),false,new Audio()));
        }
    }
    [TestMethod]
    public void CallbackAbiValidationIsSemanticAndRejectsUnboundedLifetimes()
    {
        var source=AnimationCompositionFixture.Script(317);
        foreach(var args in new[]{new[]{"0","0","64","0","0"},new[]{"0","0","-1","30","0"},new[]{"0","0","64","30","6"},new[]{"32768","0","64","30","0"}})
        {var sprite=C("createsprite",["gRockTombRockSpriteTemplate","ANIM_TARGET","2",..args]);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,sprite,C("end")),Cue(317),Battlers(false),false,new Audio()));}
        foreach(var args in new[]{new[]{"2","30"},new[]{"1","-1"},new[]{"1","32768"}})
        {var sprite=C("createsprite",["gRedXSpriteTemplate","ANIM_TARGET","2",..args]);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,sprite,C("end")),Cue(317),Battlers(false),false,new Audio()));}
        var task=C("createvisualtask","AnimTask_GrowAndGrayscale","255");var minimal=Minimal(AnimationCompositionFixture.Script(50),task,C("waitforvisualfinish"),C("end")) with{MoveId=354};
        var runtime=new SourceBattleAnimation(minimal,Cue(354),Battlers(false),false,new Audio());for(int i=0;runtime.Active&&i<100;i++)runtime.Tick();Clean(runtime);
        var hidden=Battlers(false);hidden[1]=hidden[1] with{Visible=false};Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(minimal,Cue(354),hidden,false,new Audio()));
        var unused=Minimal(minimal,C("end")) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedGrow",[task,C("return")]}}};runtime=new SourceBattleAnimation(unused,Cue(354),hidden,false,new Audio());runtime.Tick();Clean(runtime);
    }
}
