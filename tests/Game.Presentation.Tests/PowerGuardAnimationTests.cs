using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PowerGuardAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int power=80,int actor=0,int target=1)=>new("power-guard",1,move,new(actor),new(target)){MovePower=checked((ushort)power),VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.InvertedPalettes.Length);Assert.AreEqual(0,f.GreyscaleSlots.Length);
    }
    [TestMethod]
    [DataRow(175,80)]
    [DataRow(179,20)]
    [DataRow(179,200)]
    [DataRow(182,80)]
    [DataRow(247,80)]
    public void EverySourceBranchResolvesRendersAndReplaysAcrossSidesAndDoubles(int move,int power)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        string[] expected=move switch{175=>["impact"],179=>["blue-orb","hands-and-feet","impact"],182=>["impact","protect"],_=>["impact","shadow-ball"]};CollectionAssert.AreEquivalent(expected,resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,power,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
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
            string required=move switch{175=>"gRandomPosHitSplatSpriteTemplate",179=>"gReversalOrbSpriteTemplate",182=>"gProtectSpriteTemplate",_=>"gShadowBallSpriteTemplate"};Assert.IsTrue(templates.Contains(required),required);

        }
    }
    [TestMethod]
    [DataRow(175,80)]
    [DataRow(179,20)]
    [DataRow(179,200)]
    [DataRow(182,80)]
    [DataRow(247,80)]
    public void CancellationAtEveryTickRestoresSharedSceneAndAffineState(int move,int power)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));var cue=Cue(move,power);var length=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteMaterials:assets);int ticks=0;while(length.Active&&ticks++<700)length.Tick();Assert.IsFalse(length.Active);
        for(int stop=0;stop<=ticks;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
        }
    }
    [TestMethod]
    public void RandomImpactRecipeAndInvokedInputValidationFailClosed()
    {
        var source=AnimationCompositionFixture.Script(175);const string template="gRandomPosHitSplatSpriteTemplate";var original=source.SpriteRecipes[template];
        foreach(var bad in new[]{original with{Width=8},original with{Animations=[[new(0,3,false,false)]]},original with{Callback="UnknownRandomImpact"},original with{Blend=false},original with{AffineAnimations=[]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(175),Battlers(false),false,new Audio()));}
        foreach(var args in new[]{new[]{"0","4"},new[]{"2","3"},new[]{"0","-2"}})
        {var sprite=C("createsprite",[template,"ANIM_TARGET","2",..args]);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,sprite,C("end")),Cue(175),Battlers(false),false,new Audio()));}
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(175) with{MovePower=null},Battlers(false),false,new Audio()));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(175) with{VisualSeed=null},Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void EveryPreparedSourcePowerReplaysWithoutDisplayHpInference()
    {
        foreach(int power in new[]{20,40,80,100,150,200})
        {var runtime=new SourceBattleAnimation(AnimationCompositionFixture.Script(175),Cue(175,power),Battlers(false),false,new Audio());for(int t=0;runtime.Active&&t<700;t++)runtime.Tick();Clean(runtime);}
    }
    [TestMethod]
    public void FlailMovementKeepsKnownHiddenParticipantsAndUsesCallbackSemantics()
    {
        foreach(int participant in new[]{0,1})
        {
            var task=C("createvisualtask","AnimTask_FlailMovement","255",participant.ToString());var source=Minimal(AnimationCompositionFixture.Script(175),task,C("waitforvisualfinish"),C("end")) with{MoveId=354};
            var battlers=Battlers(false);battlers[participant]=battlers[participant] with{Visible=false};var runtime=new SourceBattleAnimation(source,Cue(354) with{MovePower=null,VisualSeed=null},battlers,false,new Audio());bool changed=false;
            for(int t=0;runtime.Active&&t<100;t++){var f=runtime.Tick();Assert.IsFalse(f.Battlers[participant].Visible);changed|=f.BattlerAffine[participant].MatrixB!=0;}
            Assert.IsTrue(changed);Clean(runtime);
        }
        var original=AnimationCompositionFixture.Script(175);var random=C("createsprite","gRandomPosHitSplatSpriteTemplate","ANIM_TARGET","2","ANIM_TARGET","3");var unused=Minimal(original,C("end")) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedRandom",[random,C("return")]}}};
        var idle=new SourceBattleAnimation(unused,Cue(175) with{MovePower=null,VisualSeed=null},Battlers(false),false,new Audio());idle.Tick();Clean(idle);
    }
}
