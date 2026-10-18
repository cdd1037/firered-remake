using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FriendshipContactAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int friendship=0,int actor=0,int target=1)=>new("friendship-contact",1,move,new(actor),new(target)){Friendship=checked((byte)friendship),VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.InvertedPalettes.Length);
    }
    [TestMethod]
    [DataRow(122,0)][DataRow(165,0)]
    [DataRow(216,0)][DataRow(216,60)][DataRow(216,61)][DataRow(216,92)][DataRow(216,201)]
    [DataRow(218,0)][DataRow(218,31)][DataRow(218,101)][DataRow(218,201)]
    public void EverySourceBranchResolvesRendersAndReplaysAcrossSidesAndDoubles(int move,int friendship)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        string[] expected=move switch{122=>["impact","lick"],165=>["impact","movement-waves"],216=>["impact"],_=>["impact","anger"]};CollectionAssert.AreEquivalent(expected,resolved.Assets.Keys.ToArray());
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
            string required=move switch{122=>"gLickSpriteTemplate",165=>"gMovementWavesSpriteTemplate",218 when friendship>200=>"gWeakFrustrationAngerMarkSpriteTemplate",_=>"gBasicHitSplatSpriteTemplate"};Assert.IsTrue(templates.Contains(required),required);
            if(move==216&&friendship>200)Assert.IsTrue(templates.Contains("AnimMonTrace"));
        }
    }
    [TestMethod]
    [DataRow(122,0)][DataRow(165,0)][DataRow(216,0)][DataRow(216,61)][DataRow(216,92)][DataRow(216,201)][DataRow(218,0)][DataRow(218,31)][DataRow(218,101)][DataRow(218,201)]
    public void CancellationAtEveryTickRestoresSharedSceneAndAffineState(int move,int friendship)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));var cue=Cue(move,friendship);var length=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteMaterials:assets);int ticks=0;while(length.Active&&ticks++<700)length.Tick();Assert.IsFalse(length.Active);
        for(int stop=0;stop<=ticks;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
        }
    }
    [TestMethod]
    public void FriendshipIsRequiredOnlyByInvokedPredicateAndDetachedFromLaterCueReplacement()
    {
        foreach(var pair in new[]{(216,"AnimTask_GetReturnPowerLevel"),(218,"AnimTask_GetFrustrationPowerLevel")})
        {
            var source=AnimationCompositionFixture.Script(pair.Item1);var predicate=C("createvisualtask",pair.Item2,"2");var cue=Cue(pair.Item1) with{Friendship=null};
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source,cue,Battlers(false),false,new Audio()));
            var unused=Minimal(source,C("end")) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedFriendship",[predicate,C("return")]}}};var accepted=new SourceBattleAnimation(unused,cue,Battlers(false),false,new Audio());accepted.Tick();Assert.IsFalse(accepted.Active);
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{Commands=[C("call","UnusedFriendship"),C("end")]},cue,Battlers(false),false,new Audio()));
            var script=Minimal(source,predicate,C("delay","1"),C("end")) with{MoveId=354};var original=Cue(354,60);var runtime=new SourceBattleAnimation(script,original,Battlers(false),false,new Audio());original=original with{Friendship=255};runtime.Tick();Assert.AreEqual(pair.Item1==216?0:1,(int)runtime.Snapshot().AnimationArguments[7]);Assert.AreEqual((byte)255,original.Friendship!.Value);runtime.Cancel();
        }
    }
    [TestMethod]
    public void SourceRecipePaletteAndTaskAffineMutationsFailClosed()
    {
        foreach(var pair in new[]{(122,"gLickSpriteTemplate"),(165,"gMovementWavesSpriteTemplate"),(218,"gWeakFrustrationAngerMarkSpriteTemplate")})
        {
            var source=AnimationCompositionFixture.Script(pair.Item1);var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);var original=recipes[pair.Item2];recipes[pair.Item2]=original with{Width=8};Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(pair.Item1),Battlers(false),false,new Audio()));
            recipes[pair.Item2]=original with{Animations=[[new(0,3,false,false)]]};Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(pair.Item1),Battlers(false),false,new Audio()));
            string tag="ANIM_TAG_"+original.Asset.ToUpperInvariant().Replace('-','_');var palettes=source.SpritePalettes.Where(p=>p.Key!=source.SpriteTags[tag]).ToDictionary(p=>p.Key,p=>p.Value);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpritePalettes=palettes},Cue(pair.Item1),Battlers(false),false,new Audio()));
        }
        var frustration=AnimationCompositionFixture.Script(218);var programs=frustration.TaskAffinePrograms.ToDictionary(p=>p.Key,p=>p.Value);programs["sStrongFrustrationAffineAnimCmds"]=[new("frame",0,-15,0,8),new("end")];Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(frustration with{TaskAffinePrograms=programs},Cue(218),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void TraceClosureRejectsInvokedAffineOrFormChangesButIgnoresUnusedHelpers()
    {
        var source=AnimationCompositionFixture.Script(216);var trace=C("createvisualtask","AnimTask_TraceMonBlended","2","0","4","5","1");
        var affine=C("createvisualtask","AnimTask_ScaleMonAndRestore","2","-10","10","8","0","0");
        var commands=new[]{trace,C("waitforvisualfinish"),C("end")};
        var unused=Minimal(source,commands) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedAffine",[affine,C("return")]}}};
        var runtime=new SourceBattleAnimation(unused,Cue(216),Battlers(false),false,new Audio());runtime.Cancel();
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{Commands=[C("call","UnusedAffine"),..commands]},Cue(216),Battlers(false),false,new Audio()));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,affine,trace,C("waitforvisualfinish"),C("end")),Cue(216),Battlers(false),false,new Audio()));
        var form=C("createvisualtask","AnimTask_TransformMon","2","0");
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,trace,form,C("waitforvisualfinish"),C("end")),Cue(216),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void NewCallbackAbiWidthsAndTraceCapacityAreNotMoveIdSpecific()
    {
        var source=AnimationCompositionFixture.Script(165);foreach(string value in new[]{"-1","32768"})
        {var sprite=C("createsprite","gMovementWavesSpriteTemplate","ANIM_ATTACKER","2","0","0",value);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,sprite,C("end")),Cue(165),Battlers(false),false,new Audio()));}
        foreach(var bad in new[]{new[]{"2","0","0","1"},new[]{"0","-1","0","1"},new[]{"0","0","32768","1"},new[]{"0","0","0","-1"}})
        {var command=C("createvisualtask",["AnimTask_TraceMonBlended","2",..bad]);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,command,C("end")),Cue(165),Battlers(false),false,new Audio()));}
        foreach(int selector in new[]{0,1})
        {
            var command=C("createvisualtask","AnimTask_TraceMonBlended","2",selector.ToString(),"0","2","1");var hidden=Battlers(false);hidden[selector]=hidden[selector] with{Visible=false};
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,command,C("waitforvisualfinish"),C("end")),Cue(165),hidden,false,new Audio()));
            var unused=Minimal(source,C("end")) with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedTrace",[command,C("return")]}}};var runtime=new SourceBattleAnimation(unused,Cue(165),hidden,false,new Audio());runtime.Tick();Assert.IsFalse(runtime.Active);
        }
        foreach(int selector in new[]{0,1})
        {
            var command=C("createvisualtask","AnimTask_TraceMonBlended","255",selector.ToString(),"0","100","70");var runtime=new SourceBattleAnimation(Minimal(source,command,C("waitforvisualfinish"),C("end")) with{MoveId=354},Cue(354),Battlers(false),false,new Audio());int ticks=0,peak=0;bool seen=false;
            while(runtime.Active&&ticks++<200){var frame=runtime.Tick();peak=Math.Max(peak,frame.Sprites.Length);Assert.IsTrue(runtime.ActiveVisualCount<=1);foreach(var sprite in frame.Sprites){seen=true;Assert.AreEqual(selector==0?2:1,sprite.OamPriority);Assert.AreEqual(selector,sprite.BattlerCopySlot);}}Clean(runtime);Assert.IsTrue(seen);Assert.AreEqual(62,peak,"Two battlers reserve their source slots; raw traces use the other62.");
        }
    }
}
