using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CutWallAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int value=1,int actor=0,int target=1)=>new("cut-wall",1,move,new(actor),new(target)){FuryCutterCounter=move==210?checked((byte)value):null,MoveTurn=move==280?checked((byte)value):null,VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Dictionary<string,SourcePixelAsset> Materials(SourceResolvedAnimation r,AnimationBattler[] battlers)
    {var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray());foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));return assets;}
    private static void Clean(SourceBattleAnimation runtime,bool retainsAlpha=false)
    {Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.AreEqual(retainsAlpha,f.BlendEnabled);if(retainsAlpha){Assert.AreEqual(12,f.AlphaA);Assert.AreEqual(8,f.AlphaB);Assert.AreEqual(0,f.BlendFirstTargets);Assert.AreEqual(63,f.BlendSecondTargets);}Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);}
    [TestMethod]
    [DataRow(210,1)] [DataRow(210,2)] [DataRow(210,3)] [DataRow(210,4)] [DataRow(210,5)] [DataRow(239,0)] [DataRow(280,0)] [DataRow(280,1)]
    public void CompleteBranchesResolveRenderAndReplayAcrossSidesAndDoubles(int move,int value)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);string[] expected=move switch{210=>["impact","cut"],239=>["impact","leaf","rocks"],_=>["impact","hands-and-feet","blue-light-wall","torn-metal"]};CollectionAssert.AreEquivalent(expected,resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=Materials(resolved,battlers);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,value,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);int ticks=0;bool pixels=false;var templates=new HashSet<string>();
            while(runtime.Active&&ticks++<700)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));foreach(var s in f.Sprites){templates.Add(s.Template);if(s.Asset is not null)Assert.IsNotNull(s.Material);}
                if(!pixels&&f.Sprites.Any(s=>s.Visible&&s.Asset is not null)){var before=JsonSerializer.Serialize(runtime.Snapshot());pixels=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));}
            }
            // Both Brick Break branches omit blendoff; Cmd_end and player/opponent controller completion retain setalpha 12,8.
            Clean(runtime,move==280);Clean(replay,move==280);runtime.Cancel();replay.Cancel();Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);
            if(move==210)Assert.IsTrue(templates.Contains("gCuttingSliceSpriteTemplate"));
            if(move==239){Assert.IsTrue(templates.Contains("gTwisterLeafSpriteTemplate"));Assert.IsTrue(templates.Contains("gTwisterRockSpriteTemplate"));Assert.IsTrue(templates.Contains("gRandomPosHitSplatSpriteTemplate"));}
            if(move==280){Assert.AreEqual(value==1,templates.Contains("gBrickBreakWallSpriteTemplate"));Assert.AreEqual(value==1,templates.Contains("gBrickBreakWallShardSpriteTemplate"));}
        }
    }
    [TestMethod]
    [DataRow(210,5)] [DataRow(239,0)] [DataRow(280,1)]
    public void CancellationAtEveryTickReleasesOnlyItsSharedSceneAndPaletteOwnership(int move,int value)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var assets=Materials(resolved,battlers);var cue=Cue(move,value);var length=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteMaterials:assets);int ticks=0;while(length.Active&&ticks++<700)length.Tick();Clean(length,move==280);length.Cancel();Clean(length);
        for(int stop=0;stop<=ticks;stop++)
        {var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));}
    }
    [TestMethod]
    public void FuryInputIsDetachedInvokedOnlyAndUsesCallbackSemantics()
    {
        var source=AnimationCompositionFixture.Script(210);foreach(byte? value in new byte?[]{null,0,6,255})Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(210) with{FuryCutterCounter=value},Battlers(false),false,new Audio()));
        var task=C("createvisualtask","AnimTask_GetFuryCutterHitCount","255");var unused=Minimal(source,C("end")) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedFury",[task,C("return")]}}};var idle=new SourceBattleAnimation(unused,Cue(210) with{FuryCutterCounter=null},Battlers(false),false,new Audio());idle.Tick();Clean(idle);
        var renamed=Minimal(source,task,C("delay","1"),C("end")) with{MoveId=354};var cue=Cue(354) with{FuryCutterCounter=5};var runtime=new SourceBattleAnimation(renamed,cue,Battlers(false),false,new Audio());cue=cue with{FuryCutterCounter=1};runtime.Tick();Assert.AreEqual((short)5,((short[])typeof(SourceBattleAnimation).GetField("_animationArgs",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!)[7]);while(runtime.Active)runtime.Tick();Clean(runtime);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(AnimationCompositionFixture.Script(280),Cue(280) with{MoveTurn=null},Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void RecipesAndSourceAbiFailClosedWithoutChangingMaterialIdentity()
    {
        foreach(var (move,name) in new[]{(239,"gTwisterLeafSpriteTemplate"),(239,"gTwisterRockSpriteTemplate"),(280,"gBrickBreakWallSpriteTemplate"),(280,"gBrickBreakWallShardSpriteTemplate")})
        {
            var source=AnimationCompositionFixture.Script(move);var original=source.SpriteRecipes[name];foreach(var bad in new[]{original with{Width=8},original with{Callback="UnknownCutWall"},original with{Blend=!original.Blend},original with{Animations=[[new(1,1,false,false)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[name]=bad;if(bad==original)continue;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(move),Battlers(false),false,new Audio()));}
        }
        var s=AnimationCompositionFixture.Script(239);foreach(var task in new[]{C("createvisualtask","AnimTask_ShakeMonInPlace","2","4","3","0","12","1"),C("createvisualtask","AnimTask_ShakeMonInPlace","2","1","3","0","0","1"),C("createvisualtask","AnimTask_ShakeMonInPlace","2","1","3","0","12","-1")})Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(s,task,C("end")),Cue(239),Battlers(false),false,new Audio()));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(239) with{VisualSeed=null},Battlers(false),false,new Audio()));
        using var resources=new SourceAnimationResources(Folder);var wall=resources.Asset("blue-light-wall");Assert.IsTrue(wall.PaletteIndices.Length>0);Assert.AreEqual(resources.Scripts[280].SpriteTags["ANIM_TAG_BLUE_LIGHT_WALL"],wall.PaletteTag);
    }
    [TestMethod]
    public void InPlaceShakeKeepsHiddenActorsButSkipsHiddenOrAbsentPartners()
    {
        var source=AnimationCompositionFixture.Script(239);
        foreach(bool doubles in new[]{false,true})foreach(int selector in new[]{0,1,2,3})foreach(bool hidden in new[]{false,true})
        {
            var battlers=Battlers(doubles);int slot=selector switch{0=>0,1=>1,2=>2,_=>3};if(hidden&&slot<battlers.Length)battlers[slot]=battlers[slot] with{Visible=false};
            var script=Minimal(source,C("createvisualtask","AnimTask_ShakeMonInPlace","2",selector.ToString(),"3","-2","4","0"),C("waitforvisualfinish"),C("end"));var runtime=new SourceBattleAnimation(script,Cue(239) with{VisualSeed=null},battlers,doubles,new Audio());bool moved=false;int ticks=0;
            while(runtime.Active&&ticks++<30){var f=runtime.Tick();if(slot<battlers.Length){moved|=f.Offsets[slot]!=default;Assert.AreEqual(!hidden,f.Battlers[slot].Visible);}}
            Assert.AreEqual(slot<battlers.Length&&(selector<2||!hidden),moved);Clean(runtime);
        }
    }
    [TestMethod]
    public void SharedRegistryStillHasExactlySixtyEffectSlotsInVisibleDoubles()
    {
        using var resources=new SourceAnimationResources(Folder);var original=resources.Resolve(280);var battlers=Battlers(true);
        foreach(int count in new[]{60,61})
        {
            var commands=new List<SourceAnimationCommand>{C("loadspritegfx","ANIM_TAG_BLUE_LIGHT_WALL")};for(int i=0;i<count;i++)commands.Add(C("createsprite","gBrickBreakWallSpriteTemplate","ANIM_TARGET","2","1","0","0","90","0"));commands.Add(C("waitforvisualfinish"));commands.Add(C("end"));var script=Minimal(original.Script,commands.ToArray());var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(script,Cue(280),battlers,true,new Audio(),spriteScene:scene,spriteMaterials:Materials(original,battlers));
            if(count==61)Assert.Throws<InvalidDataException>(()=>{for(int i=0;i<8&&runtime.Active;i++)runtime.Tick();});else{for(int i=0;i<8&&scene.Count==0;i++)runtime.Tick();}
            Assert.AreEqual(60,scene.Count);Assert.AreEqual(60,runtime.ActiveVisualCount);runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);
        }
    }
}
