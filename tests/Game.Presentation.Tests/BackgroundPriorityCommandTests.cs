using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class BackgroundPriorityCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void UnconditionalSplitUsesBothRegistersForEveryActorAndRejectsOperands()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var source=resources.Resolve(54).Script;
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
            var script=source with{Commands=[new("splitbgprio_all",[],1),new("delay",["1"],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var cue=new CommittedMoveAnimation("priority-all",1,54,new(actor),new(actor));
            var runtime=new SourceBattleAnimation(script,cue,battlers,doubles,new Audio());
            CollectionAssert.AreEqual(new[]{1,2},runtime.Tick().BattlerBackgroundPriorities);
            while(runtime.Active)runtime.Tick();
            CollectionAssert.AreEqual(new[]{1,2},runtime.Snapshot().BattlerBackgroundPriorities);
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[new("splitbgprio_all",["ANIM_ATTACKER"],1),new("end",[],2)]},cue,battlers,doubles,new Audio()));
        }
    }
}
