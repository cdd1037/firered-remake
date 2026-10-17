using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SpriteOamPhaseTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void TaskCreatedCloneFirstEntersOamOnTheFollowingSpritePass()
    {
        // BattleMainCB2: AnimateSprites, BuildOamBuffer, palette fade, RunTasks.
        var script=new SourceAnimationScript(97,"trace-phase",[
            new("createvisualtask",["AnimTask_TraceMonBlended","2","0","4","7","10"],1),
            new("waitforvisualfinish",[],2),new("end",[],3)]);
        var runtime=new SourceBattleAnimation(script,new("phase",1,97,new(0),new(1)),
            [new(new(0),"actor",0,0,32,32),new(new(1),"target",0,0,32,32)],false,new Audio());
        var allocated=runtime.Tick();
        Assert.AreEqual(2,runtime.ActiveVisualCount,"The task owns its first live clone after RunTasks.");
        Assert.AreEqual(0,allocated.Sprites.Length,"Allocation after OAM construction is not visible yet.");
        Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        var shown=runtime.Tick();
        Assert.AreEqual(1,shown.Sprites.Length);Assert.IsTrue(shown.Sprites[0].Visible);
        CollectionAssert.AreEqual(shown.Sprites,runtime.Snapshot().Sprites);
        runtime.Cancel();runtime.Cancel();
        Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        Assert.AreEqual(1,shown.Sprites.Length,"Previously returned OAM frames stay immutable.");
        Assert.AreEqual(0,allocated.Sprites.Length);
    }
}
