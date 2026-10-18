using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class LockedPairAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int value=1,int actor=0,int target=1)=>new("locked-pair",1,move,new(actor),new(target)){MoveTurn=move==117?checked((byte)value):null,VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Dictionary<string,SourcePixelAsset> Materials(SourceResolvedAnimation r,AnimationBattler[] battlers)
    {var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray());foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));return assets;}
    private static void Clean(SourceBattleAnimation runtime)
    {Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(((PixelPoint[])typeof(SourceBattleAnimation).GetField("_battlerPositionOffsets",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!).All(p=>p==default));Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);}
    [TestMethod]
    [DataRow(37,0)] [DataRow(117,0)] [DataRow(117,1)]
    public void CompleteBranchesResolveRenderAndReplayAcrossSidesAndDoubles(int move,int value)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);string[] expected=move==37?["impact","hands-and-feet"]:["impact"];CollectionAssert.AreEquivalent(expected,resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in move==117&&value==0?new[]{actor}:doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=Materials(resolved,battlers);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,value,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);var neutral=composer.ComposeField(runtime.Snapshot());int ticks=0;bool pixels=false;var templates=new HashSet<string>();
            while(runtime.Active&&ticks++<700)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));foreach(var s in f.Sprites){templates.Add(s.Template);if(s.Asset is not null)Assert.IsNotNull(s.Material);}
                if(!pixels){var before=JsonSerializer.Serialize(runtime.Snapshot());pixels=!composer.ComposeField(f).SequenceEqual(neutral);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));}
            }
            Clean(runtime);Clean(replay);runtime.Cancel();replay.Cancel();Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);
            if(move==37)Assert.IsTrue(templates.Contains("gFistFootRandomPosSpriteTemplate"));
            else Assert.AreEqual(value==1,templates.Contains("gBasicHitSplatSpriteTemplate"));
        }
    }
    [TestMethod]
    [DataRow(37,0)] [DataRow(117,0)] [DataRow(117,1)]
    public void CancellationAtEveryTickReleasesOnlyItsSharedSceneAndPaletteOwnership(int move,int value)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var assets=Materials(resolved,battlers);var cue=Cue(move,value,target:move==117&&value==0?0:1);var length=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteMaterials:assets);int ticks=0;while(length.Active&&ticks++<700)length.Tick();Clean(length);length.Cancel();Clean(length);
        for(int stop=0;stop<=ticks;stop++)
        {var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));}
    }
    [TestMethod]
    public void TaskAbiAffineProgramAndBideTurnInputFailClosedBySemantics()
    {
        var source=AnimationCompositionFixture.Script(37);foreach(var command in new[]{C("createvisualtask","AnimTask_ThrashMoveMonHorizontal"),C("createvisualtask","AnimTask_ThrashMoveMonVertical","256"),C("createvisualtask","AnimTask_ThrashMoveMonVertical","2","0")})Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,command,C("end")),Cue(37),Battlers(false),false,new Audio()));
        var programs=source.TaskAffinePrograms.ToDictionary(p=>p.Key,p=>p.Value);programs["sThrashMoveMonAffineAnimCmds"]=[new("frame",1,1,0,1),new("end")];Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{TaskAffinePrograms=programs},Cue(37),Battlers(false),false,new Audio()));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(AnimationCompositionFixture.Script(117),Cue(117) with{MoveTurn=null},Battlers(false),false,new Audio()));
        var renamed=source with{MoveId=354};var runtime=new SourceBattleAnimation(renamed,Cue(354),Battlers(false),false,new Audio());int ticks=0;while(runtime.Active&&ticks++<700)runtime.Tick();Clean(runtime);
    }
    [TestMethod]
    public void InvokedThrashNeedsAPresentSpriteButPreservesKnownHiddenParticipantSemantics()
    {
        var source=AnimationCompositionFixture.Script(37);
        foreach(string name in new[]{"AnimTask_ThrashMoveMonHorizontal","AnimTask_ThrashMoveMonVertical"})
        {
            var script=Minimal(source,C("createvisualtask",name,"2"),C("waitforvisualfinish"),C("end"));var battlers=Battlers(false);
            battlers[0]=battlers[0] with{SourceVisibility=new(false,true,true)};
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(37),battlers,false,new Audio()));
            battlers[0]=battlers[0] with{SourceVisibility=new(true,true,true)};
            var runtime=new SourceBattleAnimation(script,Cue(37),battlers,false,new Audio());int ticks=0;
            while(runtime.Active&&ticks++<150){var f=runtime.Tick();var v=f.BattlerVisibility.Single(v=>v.Slot==0);Assert.IsTrue(v.Present&&v.LogicalInvisible&&v.SpriteInvisible&&v.OamInvisible);}
            Clean(runtime);runtime.Cancel();Clean(runtime);
        }
    }
    [TestMethod]
    public void RawBaseAndPos2AreIndependentAndMonBgCapturesTheirSourceSum()
    {
        var source=AnimationCompositionFixture.Script(37);var runtime=new SourceBattleAnimation(Minimal(source,C("createvisualtask","AnimTask_ThrashMoveMonVertical","2"),C("waitforvisualfinish"),C("end")),Cue(37),Battlers(false),false,new Audio());
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var offsets=(PixelPoint[])typeof(SourceBattleAnimation).GetField("_offsets",flags)!.GetValue(runtime)!;offsets[0]=new(3,-5);var frame=runtime.Tick();Assert.AreEqual(new PixelPoint(3,-5),offsets[0]);Assert.AreEqual(new PixelPoint(7,-5),frame.Offsets[0]);Assert.AreEqual(new PixelPoint(3,-5),frame.ObjectOffsets[0]);
        typeof(SourceBattleAnimation).GetMethod("MoveBattlerToBackground",flags)!.Invoke(runtime,[0,false]);var copy=runtime.Snapshot().StaticBackgroundCopies.Single();Assert.AreEqual(new PixelPoint(7,-5),copy.Offset);runtime.Tick();Assert.AreEqual(copy,runtime.Snapshot().StaticBackgroundCopies.Single());Assert.AreEqual(new PixelPoint(11,-5),runtime.Snapshot().Offsets[0]);runtime.Cancel();Clean(runtime);
    }
}
