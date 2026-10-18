using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class TripleKickBranchAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Sounds{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(byte turn,int actor=0,int? target=null)=>new("triple-kick",turn+1,167,new(actor),new(target??(actor^1))){MoveTurn=turn};
    private static SourceAnimationScript Script()=>AnimationCompositionFixture.Script(167);
    private static SourceAnimationFrame FirstHit(SourceBattleAnimation runtime)
    {
        for(int tick=0;tick<20&&runtime.Active;tick++){var frame=runtime.Tick();if(frame.HitSplats.Length!=0)return frame;}
        Assert.Fail("The source branch did not create a hit.");return null!;
    }
    private static void AssertRetired(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
        var frame=runtime.Snapshot();Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);
        Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
    }
    [TestMethod]
    public void ConditionalJumpMatchesOriginalCForEveryByteTurnAndRetainsFalseFallthrough()
    {
        using var fixture=SourceVectorFixture.Read("multihit-branch-original-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_multihit_branch_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/multihit-branch-original-vectors.json");
        var source=fixture.RootElement.GetProperty("jump");var checks=source.GetProperty("checks").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        var branches=source.GetProperty("branches").EnumerateArray().ToArray();Assert.AreEqual(4,checks.Length);Assert.AreEqual(checks.Length,branches.Length);
        for(int check=0;check<checks.Length;check++)
        {
            var script=new SourceAnimationScript(167,"conditional-byte",[
                new("jumpifmoveturn",[checks[check].ToString(),"matched"],1),
                new("setarg",["0","6"],2),new("goto",["done"],3),
                new("setarg",["0","32"],4),new("end",[],5)]){Labels=new Dictionary<string,int>{{"matched",3},{"done",4}}};
            var targets=branches[check].EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(256,targets.Length);
            for(int turn=0;turn<256;turn++)
            {
                var runtime=new SourceBattleAnimation(script,Cue((byte)turn),Battlers(false),false,new Audio());
                var frame=runtime.Tick();Assert.AreEqual((short)targets[turn],frame.AnimationArguments[0],$"Check {checks[check]}, turn {turn}");
                Assert.IsFalse(runtime.Active);
            }
        }
    }
    [TestMethod]
    public void CompleteKickScriptUsesLeftRightThenCenterForAllSourceByteTurns()
    {
        var script=Script();var battlers=Battlers(false);var center=battlers[1].Center(false,true);
        for(int turn=0;turn<256;turn++)
        {
            int branch=turn==0?0:turn==1?1:2;int x=new[]{-16,8,0}[branch],y=new[]{-16,0,-8}[branch];
            var audio=new Audio();var runtime=new SourceBattleAnimation(script,Cue((byte)turn),battlers,false,audio);var frame=FirstHit(runtime);
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y+y),frame.HitSplats.Single().Center);
            Assert.AreEqual(branch==2?216:176,frame.HitSplats.Single().Scale);
            var foot=frame.Sprites.Single(s=>s.Template=="gFistFootSpriteTemplate");
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y+y+8),foot.Position);
            Assert.AreEqual("hands-and-feet",foot.Asset);Assert.AreEqual(16,foot.Tile);Assert.IsFalse(foot.HFlip);Assert.IsFalse(foot.VFlip);
            int shake=Math.Abs(frame.Offsets[1].X),ticks=0;
            while(runtime.Active&&ticks++<100){frame=runtime.Tick();shake=Math.Max(shake,Math.Abs(frame.Offsets[1].X));}
            Assert.AreEqual(branch==2?6:4,shake);AssertRetired(runtime);Assert.AreEqual("SE_M_VITAL_THROW2",audio.Sounds.Single().Id);
        }
    }
    [TestMethod]
    public void EveryCommittedKickComposesGeneratedFootAndImpactForSinglesDoublesAndAllies()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(167);
        foreach(bool doubles in new[]{false,true})
        for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        for(byte turn=0;turn<3;turn++)
        {
            var battlers=Battlers(doubles);var runtime=new SourceBattleAnimation(resolved.Script,Cue(turn,actor,target),battlers,doubles,new Audio());
            var frame=FirstHit(runtime);var center=battlers[target].Center(doubles,true);int direction=battlers[actor].Base(doubles).X<center.X?1:-1;
            int x=new[]{-16,8,0}[turn]*direction,y=new[]{-16,0,-8}[turn];
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y+y),frame.HitSplats.Single().Center);
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y+y+8),frame.Sprites.Single(s=>s.Template=="gFistFootSpriteTemplate").Position);
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);
            foreach(var battler in battlers)assets[battler.Asset]=new(64,64,new ushort[4096]);
            var composer=new SourceBattleFrameComposer(assets,battlers);
            Assert.IsFalse(composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{Sprites=[],HitSplats=[]})));
            int ticks=0;while(runtime.Active&&ticks++<100)composer.ComposeField(runtime.Tick());AssertRetired(runtime);
        }
    }
    [TestMethod]
    public void ConditionalBranchRejectsMissingContextInvalidByteAndUnresolvedTarget()
    {
        var script=Script();var battlers=Battlers(false);var cue=Cue(0);int index=Array.FindIndex(script.Commands,c=>c.Op=="jumpifmoveturn");
        Assert.IsTrue(index>=0);Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue with{MoveTurn=null},battlers,false,new Audio()));
        foreach(var args in new[]{new[]{"-1","TripleKickLeft"},new[]{"256","TripleKickLeft"},new[]{"0"},new[]{"0","missing"}})
        {
            var commands=(SourceAnimationCommand[])script.Commands.Clone();commands[index]=commands[index] with{Args=args};
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=commands},cue,battlers,false,new Audio()));
        }
        foreach(int invalid in new[]{-1,script.Commands.Length})
        {
            var labels=script.Labels.ToDictionary(p=>p.Key,p=>p.Value);labels["TripleKickLeft"]=invalid;
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Labels=labels},cue,battlers,false,new Audio()));
        }
    }
}
