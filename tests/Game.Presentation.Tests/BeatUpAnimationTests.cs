using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BeatUpAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Sounds{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(byte turn,int actor=0,int target=1,long sequence=1)=>new("beat-up",sequence,251,new(actor),new(target)){MoveTurn=turn};
    private static void Clean(SourceBattleAnimation runtime)
    {
        var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
        Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);
        Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsFalse(runtime.HasResidualSprites);
    }
    [TestMethod]
    public void EverySourceByteSelectsTwoAuthoredImpactsUsingExistingTwoTurnParity()
    {
        using var vectors=SourceVectorFixture.Read("multihit-branch-original-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_multihit_branch_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/multihit-branch-original-vectors.json");
        var targets=vectors.RootElement.GetProperty("targets").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        var branches=vectors.RootElement.GetProperty("branches").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        Assert.AreEqual(2,targets.Length);Assert.AreEqual(256,branches.Length);
        var script=AnimationCompositionFixture.Script(251);var battlers=Battlers(false);var center=battlers[1].Center(false,true);
        for(int turn=0;turn<256;turn++)
        {
            int branch=Array.IndexOf(targets,branches[turn]);Assert.IsTrue(branch is 0 or 1);
            var audio=new Audio();var runtime=new SourceBattleAnimation(script,Cue((byte)turn),battlers,false,audio);
            var seen=new HashSet<PixelPoint>();int ticks=0;
            while(runtime.Active&&ticks++<100)
            {
                var frame=runtime.Tick();foreach(var hit in frame.HitSplats){seen.Add(hit.Center);Assert.AreEqual(176,hit.Scale);}
                foreach(var fist in frame.Sprites.Where(s=>s.Template=="gFistFootSpriteTemplate"))
                {Assert.AreEqual("hands-and-feet",fist.Asset);Assert.AreEqual(0,fist.Tile);Assert.IsFalse(fist.HFlip);Assert.IsFalse(fist.VFlip);}
            }
            CollectionAssert.AreEquivalent(new[]{new PixelPoint(center.X+(branch==0?-20:12),center.Y-20),
                new PixelPoint(center.X+(branch==0?8:-12),center.Y)},seen.ToArray());
            Assert.AreEqual(2,audio.Sounds.Count);Assert.IsTrue(audio.Sounds.All(c=>c.Id=="SE_M_VITAL_THROW2"));Clean(runtime);
        }
    }
    [TestMethod]
    public void FullRootComposesBothFistsAndImpactsOnBothSidesAllSlotsAndSelectedAllies()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(251);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})for(byte turn=0;turn<6;turn++)
        {
            var battlers=Battlers(doubles);var center=battlers[target].Center(doubles,true);
            int direction=battlers[actor].Base(doubles).X<center.X?1:-1;
            PixelPoint[] impacts=[new(center.X+(turn%2==0?-20:12)*direction,center.Y-20),new(center.X+(turn%2==0?8:-12)*direction,center.Y)];
            PixelPoint[] fists=impacts.Select(p=>new PixelPoint(p.X,p.Y+8)).ToArray();
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var battler in battlers)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(turn,actor,target);var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());
            var seenHits=new HashSet<PixelPoint>();var seenFists=new HashSet<PixelPoint>();bool pixels=false,shake=false;int ticks=0;
            while(runtime.Active&&ticks++<100)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                foreach(var hit in frame.HitSplats)seenHits.Add(hit.Center);
                foreach(var fist in frame.Sprites.Where(s=>s.Template=="gFistFootSpriteTemplate"))seenFists.Add(fist.Position);
                shake|=frame.Offsets[target]!=default;
                if(frame.Sprites.Length>0)pixels|=!composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{Sprites=[],HitSplats=[]}));
            }
            CollectionAssert.AreEquivalent(impacts,seenHits.ToArray());CollectionAssert.AreEquivalent(fists,seenFists.ToArray());
            Assert.IsTrue(pixels);Assert.IsTrue(shake);Assert.AreEqual(2,audio.Sounds.Count);Clean(runtime);Clean(replay);
        }
    }
    [TestMethod]
    public void EveryLifetimeCancellationAndSubsequentInvocationReleaseSharedState()
    {
        var script=AnimationCompositionFixture.Script(251);var battlers=Battlers(true);
        for(byte turn=0;turn<2;turn++)for(int stop=0;stop<50;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var cue=Cue(turn,3,1);var audio=new Audio();
            var runtime=new SourceBattleAnimation(script,cue,battlers,true,audio,spriteScene:scene);
            for(int ticks=0;runtime.Active&&ticks<stop;ticks++)runtime.Tick();
            int sounds=audio.Sounds.Count;runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.AreEqual(sounds,audio.Sounds.Count);
            var next=new SourceBattleAnimation(script,cue with{Sequence=2},battlers,true,new Audio(),spriteScene:scene);
            for(int ticks=0;next.Active&&ticks<100;ticks++)next.Tick();Clean(next);Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod]
    public void ContributorHitReceiptOwnsOneGatePlaybackEvenThoughRootContainsTwoStrikes()
    {
        var script=AnimationCompositionFixture.Script(251);var battlers=Battlers(false);var gate=new SourceBattleAnimationGate();
        for(byte ordinal=0;ordinal<6;ordinal++)
        {
            var cue=Cue(ordinal,sequence:100+ordinal);var audio=new Audio();SourceBattleAnimation Create()=>new(script,cue,battlers,false,audio);
            Assert.IsTrue(gate.TryStart(cue,Create));Assert.IsFalse(gate.TryStart(cue,Create));
            for(int tick=0;gate.Current!.Active&&tick<100;tick++)gate.Current.Tick();
            Clean(gate.Current!);Assert.AreEqual(2,audio.Sounds.Count);Assert.IsFalse(gate.TryStart(cue,Create));
        }
        gate.Reset();Assert.IsFalse(gate.IsBlocking);
    }
    [TestMethod]
    public void MissingTurnAndMalformedBranchDependenciesFailClosed()
    {
        var script=AnimationCompositionFixture.Script(251);var battlers=Battlers(false);var cue=Cue(0);
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue with{MoveTurn=null},battlers,false,new Audio()));
        int index=Array.FindIndex(script.Commands,c=>c.Op=="choosetwoturnanim");Assert.IsTrue(index>=0);
        foreach(var args in new[]{new[]{"BeatUpLeft"},new[]{"BeatUpLeft","MissingBeatUp"}})
        {
            var commands=(SourceAnimationCommand[])script.Commands.Clone();commands[index]=commands[index] with{Args=args};
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=commands},cue,battlers,false,new Audio()));
        }
        var recipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove("gFistFootSpriteTemplate");
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{SpriteRecipes=recipes},cue,battlers,false,new Audio()));
    }
}
