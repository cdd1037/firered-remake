using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class MultiHitBranchAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Sounds{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationScript Script(int move)
    {using var hit=SourceHitAnimationCatalog.Read(Path.Combine(Folder,"hit"));return SourcePhysicalAnimationCatalog.Read(Path.Combine(Folder,"physical.json"),hit)[move];}
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,byte turn,int actor=0,long sequence=1,int? target=null)=>new("multi-hit",sequence,move,new(actor),new(target??(actor^1))){MoveTurn=turn};
    private static SourceAnimationFrame FirstHit(SourceBattleAnimation runtime)
    {
        for(int tick=0;tick<20&&runtime.Active;tick++)
        {var frame=runtime.Tick();if(frame.HitSplats.Length!=0)return frame;}
        Assert.Fail("The selected original branch did not create its hit.");return null!;
    }
    private static void Finish(SourceBattleAnimation runtime)
    {
        for(int ticks=0;runtime.Active&&ticks<100;ticks++)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        var frame=runtime.Snapshot();Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);
        Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
    }
    [TestMethod][DataRow(3)][DataRow(4)]
    public void EverySourceByteTurnChoosesTheOriginalCBranchWithoutInventingHitCounts(int move)
    {
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/multihit-branch-original-vectors.json")));
        var targets=vectors.RootElement.GetProperty("targets").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        var branches=vectors.RootElement.GetProperty("branches").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        Assert.AreEqual(2,targets.Length);Assert.AreEqual(256,branches.Length);
        var script=Script(move);var battlers=Battlers(false);var center=battlers[1].Center(false,true);
        for(int turn=0;turn<branches.Length;turn++)
        {
            int branch=Array.IndexOf(targets,branches[turn]);Assert.IsTrue(branch is 0 or 1);
            var audio=new Audio();var runtime=new SourceBattleAnimation(script,Cue(move,(byte)turn),battlers,false,audio);
            var frame=FirstHit(runtime);var hit=frame.HitSplats.Single();
            Assert.AreEqual(new PixelPoint(center.X+(branch==0?-8:8),center.Y-(move==4?8:0)),hit.Center,$"Move {move}, source byte {turn}");
            Assert.AreEqual(176,hit.Scale);
            if(move==4)
            {
                var fist=frame.Sprites.Single(s=>s.Template=="gFistFootSpriteTemplate");
                Assert.AreEqual(new PixelPoint(hit.Center.X,center.Y),fist.Position);Assert.AreEqual("hands-and-feet",fist.Asset);
                Assert.AreEqual(0,fist.Tile);Assert.IsFalse(fist.HFlip);Assert.IsFalse(fist.VFlip);
            }
            Finish(runtime);Assert.AreEqual(move==3?"SE_M_DOUBLE_SLAP":"SE_M_COMET_PUNCH",audio.Sounds.Single().Id);
        }
    }
    [TestMethod][DataRow(3)][DataRow(4)]
    public void CompleteBranchRootsResolveComposeAndRetireAcrossBothSidesAndFourPositions(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})
        for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        for(byte ordinal=0;ordinal<5;ordinal++)
        {
            var battlers=Battlers(doubles);var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,ordinal,actor,target:target),battlers,doubles,audio);
            var frame=FirstHit(runtime);var center=battlers[target].Center(doubles,true);
            int x=(ordinal%2==0?-8:8)*(battlers[actor].Base(doubles).X<center.X?1:-1);
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y-(move==4?8:0)),frame.HitSplats.Single().Center);
            Assert.AreEqual(move==4?2:1,frame.Sprites.Length);
            foreach(var sprite in frame.Sprites)Assert.IsTrue(resolved.Assets.ContainsKey(sprite.Asset!));
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets["background"]=new(240,112,new ushort[240*112]);
            foreach(var battler in battlers)assets[battler.Asset]=new(64,64,new ushort[64*64]);
            var composer=new SourceBattleFrameComposer(assets,battlers);
            var rendered=composer.ComposeField(frame);
            var empty=composer.ComposeField(frame with{Sprites=[],HitSplats=[]});
            Assert.IsFalse(rendered.SequenceEqual(empty),"The selected source hit/fist must contribute generated pixels.");
            Finish(runtime);Assert.AreEqual(1,audio.Sounds.Count);
        }
    }
    [TestMethod][DataRow(3)][DataRow(4)]
    public void BranchingRequiresExplicitMoveTurnAndBothValidSourceLabels(int move)
    {
        var source=Script(move);var battlers=Battlers(false);var cue=Cue(move,0);
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,cue with{MoveTurn=null},battlers,false,new Audio()));
        int index=Array.FindIndex(source.Commands,c=>c.Op=="choosetwoturnanim");Assert.IsTrue(index>=0);
        foreach(var args in new[]{new[]{source.Commands[index].Args[0]},new[]{source.Commands[index].Args[0],"MissingBranch"}})
        {
            var commands=(SourceAnimationCommand[])source.Commands.Clone();commands[index]=commands[index] with{Args=args};
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=commands},cue,battlers,false,new Audio()));
        }
        foreach(int invalid in new[]{-1,source.Commands.Length})
        {
            var labels=source.Labels.ToDictionary(p=>p.Key,p=>p.Value);labels[source.Commands[index].Args[1]]=invalid;
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Labels=labels},cue,battlers,false,new Audio()));
        }
    }
    [TestMethod][DataRow(3)][DataRow(4)]
    public void EveryHitUsesItsOwnGateReceiptAndCancellationRestoresAllSharedState(int move)
    {
        var source=Script(move);var battlers=Battlers(false);var gate=new SourceBattleAnimationGate();
        for(byte ordinal=0;ordinal<5;ordinal++)
        {
            var cue=Cue(move,ordinal,sequence:100+ordinal);
            SourceBattleAnimation Create()=>new(source,cue,battlers,false,new Audio());
            Assert.IsTrue(gate.TryStart(cue,Create));Assert.IsFalse(gate.TryStart(cue,Create));
            var runtime=gate.Current!;FirstHit(runtime);
            Assert.IsFalse(gate.TryStart(cue with{Sequence=cue.Sequence+1},Create),"The next committed receipt waits for this visual.");
            if(ordinal==2)
            {
                runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
                Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);
                Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsFalse(runtime.GraphicsLoaded);
            }
            Finish(runtime);Assert.IsFalse(gate.TryStart(cue,Create),"Acknowledging a receipt cannot replay it.");
        }
        gate.Reset();Assert.IsFalse(gate.IsBlocking);
    }
}
