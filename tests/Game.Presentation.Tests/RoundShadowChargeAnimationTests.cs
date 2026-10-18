using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RoundShadowChargeAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int turn,int actor,int target)=>new("round-shadow",turn+1,move,new(actor),new(target)){MoveTurn=(byte)turn,VisualSeed=0x12345678};
    private static Dictionary<string,SourcePixelAsset> Materials(SourceResolvedAnimation r,AnimationBattler[] b)
    {var a=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);a["background"]=new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray());foreach(var x in b)a.Add(x.Asset,new(64,64,Enumerable.Repeat((ushort)(0x83e0+x.Slot.Value*64),4096).ToArray()));return a;}
    private static void Clean(SourceBattleAnimation r)
    {
        Assert.IsFalse(r.Active);Assert.AreEqual(0,r.ActiveVisualCount);var f=r.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.SpritePalettes.Count);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256&&a.MatrixB==0&&a.MatrixC==0));
    }
    [TestMethod,DataRow(19,0),DataRow(19,1),DataRow(340,0),DataRow(340,1),DataRow(291,0),DataRow(291,1)]
    public void CompleteSourceBranchesComposeReplayAndOwnTheirActualVisibilityInEveryLayout(int move,int turn)
    {
        // Resolve includes the compositor's shared impact primitive even when Dive's authored helper uses its water-impact sheet.
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(move);CollectionAssert.AreEquivalent(move==291?new[]{"impact","splash","sweat-bead","round-shadow","water-impact","small-bubbles"}:new[]{"round-shadow","impact"},r.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2,actor^3}:new[]{actor^1})
        {
            var b=Battlers(doubles);b[actor]=b[actor] with{SourceVisibility=new(true,turn==1,turn==1)};var assets=Materials(r,b);var composer=AnimationCompositionFixture.Create(resources,r,assets,b);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,turn,actor,target);
            var runtime=new SourceBattleAnimation(r.Script,cue,b,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(r.Script,cue,b,doubles,new Audio(),spriteMaterials:assets);var initial=runtime.Snapshot();var neutral=composer.ComposeField(initial);int ticks=0;bool changed=false,sawHidden=false,sawRestored=false;
            while(runtime.Active&&ticks++<800)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));var v=f.BattlerVisibility.Single(x=>x.Slot==actor);sawHidden|=v.SpriteInvisible;sawRestored|=sawHidden&&!v.SpriteInvisible;
                foreach(var s in f.Sprites)if(s.Asset is not null)Assert.IsNotNull(s.Material);
                string before=JsonSerializer.Serialize(runtime.Snapshot());changed|=!composer.ComposeField(f).SequenceEqual(neutral);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
            }
            Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(changed);Assert.IsTrue(audio.Cues.Count>0);Assert.IsTrue(sawHidden);Assert.AreEqual(turn==1,sawRestored);
            Assert.AreEqual(turn==0,runtime.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible);
            var completed=new SourceCompletedBattlerState();Assert.IsTrue(completed.Capture(initial,runtime.Snapshot(),assets));var idle=scene.CaptureIdle(b,doubles,completed);CollectionAssert.AreEqual(composer.ComposeField(runtime.Snapshot()),composer.ComposeField(idle),"Terminal battler pose is detached into the shared idle composer.");
            runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(turn==1,runtime.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible,"Cancel restores the invocation entry flag.");
            completed.Clear();scene.Cancel();Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod,DataRow(19),DataRow(340),DataRow(291)]
    public void SetupHandoffRemainsHiddenUntilTheNextReleaseActuallyRestoresItsSprite(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var assets=Materials(r,b);var scene=new SourceBattleAnimation.SpriteScene();var setup=new SourceBattleAnimation(r.Script,Cue(move,0,actor,actor^1),b,doubles,new Audio(),spriteScene:scene,spriteMaterials:assets);var start=setup.Snapshot();int ticks=0;while(setup.Active&&ticks++<800)setup.Tick();Clean(setup);
            var completed=new SourceCompletedBattlerState();Assert.IsTrue(completed.Capture(start,setup.Snapshot(),assets));for(int i=0;i<12;i++)Assert.IsTrue(scene.TickIdle(b,doubles,completed).BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible);
            var held=scene.CaptureIdle(b,doubles,completed).BattlerVisibility.Single(v=>v.Slot==actor);b[actor]=b[actor] with{SourceVisibility=new(held.Present,held.LogicalInvisible,held.SpriteInvisible)};
            var release=new SourceBattleAnimation(r.Script,Cue(move,1,actor,actor^1),b,doubles,new Audio(),spriteScene:scene,spriteMaterials:assets);completed.Clear();Assert.IsTrue(release.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible);
            ticks=0;while(release.Active&&ticks++<800)release.Tick();Clean(release);Assert.IsFalse(release.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible);Assert.AreEqual(0,scene.Count);scene.Cancel();
        }
    }
    [TestMethod,DataRow(19,0),DataRow(19,1),DataRow(340,0),DataRow(340,1),DataRow(291,0),DataRow(291,1)]
    public void CancellationAtEveryFrameReleasesSceneOwnersAndRestoresAllEntryFlags(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(move);
        for(int actor=0;actor<4;actor++)
        {
            var b=Battlers(true);b[actor]=b[actor] with{SourceVisibility=new(true,turn==1,turn==1)};var assets=Materials(r,b);var cue=Cue(move,turn,actor,actor^1);var probe=new SourceBattleAnimation(r.Script,cue,b,true,new Audio(),spriteMaterials:assets);int length=0;while(probe.Active&&length++<800)probe.Tick();Clean(probe);probe.Cancel();
            for(int stop=0;stop<=length;stop++)
            {
                var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(r.Script,cue,b,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int n=0;n<stop&&runtime.Active;n++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);
                foreach(var v in runtime.Snapshot().BattlerVisibility){Assert.IsTrue(v.Present);Assert.AreEqual(v.Slot==actor&&turn==1,v.SpriteInvisible);Assert.AreEqual(v.Slot==actor&&turn==1,v.LogicalInvisible);}
            }
        }
    }
    [TestMethod]
    public void MissingTurnMalformedRecipeNonterminatingRiseAndAbsentActorFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);var b=Battlers(false);
        foreach(int move in new[]{19,340,291})
        {
            var s=resources.Resolve(move).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("no-turn",1,move,new(0),new(1)),b,false,new Audio()));
            var recipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);string name=move==19?"gFlyBallUpSpriteTemplate":move==340?"gBounceBallShrinkSpriteTemplate":"gDiveBallSpriteTemplate";recipes[name]=recipes[name] with{Width=32};
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteRecipes=recipes},Cue(move,0,0,1),b,false,new Audio()));
            var absent=(AnimationBattler[])b.Clone();absent[0]=absent[0] with{SourceVisibility=new(false,false,false)};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(move,0,0,1),absent,false,new Audio()));
        }
        foreach(var command in new[]{new SourceAnimationCommand("createsprite",["gFlyBallAttackSpriteTemplate","ANIM_ATTACKER","2","0"],1),new("createsprite",["gDiveBallSpriteTemplate","ANIM_ATTACKER","2","0","-127","0","336"],1),new("createsprite",["gDiveWaterSplashSpriteTemplate","ANIM_TARGET","3","2"],1)})
        {
            int move=command.Args[0].StartsWith("gFly",StringComparison.Ordinal)?19:291;var s=resources.Resolve(move).Script with{Commands=[command,new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(move,1,0,1),b,false,new Audio()));
        }
    }
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void TwoCompletedChargeVisibilitiesSurviveAnotherActorsAnimationWithoutKeepingItsGeometry(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var charge=resources.Resolve(move);var b=Battlers(true);var assets=Materials(charge,b);var scene=new SourceBattleAnimation.SpriteScene();var completed=new SourceCompletedBattlerState();
        foreach(int actor in new[]{0,2})
        {
            completed.ClearTransient();var setup=new SourceBattleAnimation(charge.Script,Cue(move,0,actor,actor^1),b,true,new Audio(),spriteScene:scene,spriteMaterials:assets);var initial=setup.Snapshot();int ticks=0;while(setup.Active&&ticks++<800)setup.Tick();Clean(setup);
            Assert.IsTrue(completed.Capture(initial,setup.Snapshot(),assets));Assert.IsTrue(completed.RetainVisibility(setup.Snapshot(),actor));
            var flags=setup.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor);b[actor]=b[actor] with{SourceVisibility=new(flags.Present,flags.LogicalInvisible,flags.SpriteInvisible)};
        }
        var other=resources.Resolve(33);foreach(var asset in other.Assets)assets[asset.Key]=asset.Value;completed.ClearTransient();
        var attack=new SourceBattleAnimation(other.Script,new("other-actor",20,33,new(1),new(3)),b,true,new Audio(),spriteScene:scene,spriteMaterials:assets);var before=attack.Snapshot();int count=0;while(attack.Active&&count++<800)attack.Tick();Clean(attack);Assert.IsTrue(completed.Capture(before,attack.Snapshot(),assets));
        var idle=scene.CaptureIdle(Battlers(true),true,completed);foreach(int actor in new[]{0,2})Assert.IsTrue(idle.BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible,"Another actor must not release a charged battler.");Assert.IsTrue(idle.Offsets.All(p=>p==default));Assert.IsTrue(idle.BattlerAffine.All(a=>a==new SourceBattlerAffine(false,256,256)));
        completed.DropVisibility(0);idle=scene.CaptureIdle(Battlers(true),true,completed);Assert.IsFalse(idle.BattlerVisibility.Single(v=>v.Slot==0).SpriteInvisible);Assert.IsTrue(idle.BattlerVisibility.Single(v=>v.Slot==2).SpriteInvisible);
        completed.DropSlot(2);Assert.IsFalse(completed.HasState,"Charge release cannot uncover a duplicate stale hidden pose.");Assert.IsFalse(completed.RetainVisibility(attack.Snapshot() with{Active=true},2));completed.Clear();Assert.AreEqual(0,scene.Count);
    }

}
