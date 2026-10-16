using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SourceSoundPlayerPolicyTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    [TestMethod][DataRow(1)][DataRow(2)]
    public void BothSourcePlayerWrappersExecuteImmediatelyWithoutOwningVisualTasks(int player)
    {
        var script=new SourceAnimationScript(322,"named-player",[
            new("setarg",["7","777"],1),
            new("createvisualtask",[$"SoundTask_PlaySE{player}WithPanning","5","SOURCE_SONG","0"],2),
            new("waitforvisualfinish",[],3),new("setarg",["6","666"],4),new("delay",["2"],5),new("end",[],6)])
            {SoundEffects=new Dictionary<string,int>{{"SOURCE_SONG",236}}};
        var audio=new Audio();var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
        var runtime=new SourceBattleAnimation(script,new("named-player",1,322,new(0),new(0)),battlers,false,audio);
        Assert.AreEqual(0,audio.Cues.Count);var frame=runtime.Tick();var cue=audio.Cues.Single();
        Assert.AreEqual(0,cue.Frame);Assert.AreEqual(player,cue.PanningPlayer!.Value);Assert.AreEqual(0,cue.Pan);
        CollectionAssert.AreEqual(new short[]{236,0,0,0,0,0,666,777},frame.AnimationArguments);
        Assert.AreEqual(0,runtime.ActiveVisualCount);
        while(runtime.Active)runtime.Tick();Assert.AreEqual(1,audio.Cues.Count);
    }
    [TestMethod]
    public void NamedSeWrapperRequiresItsActualSourceSongPlayer()
    {
        var ordinary=new SourceAnimationSound("source-song",0,1);
        ordinary.ValidateSourcePlayer(1);ordinary.ValidateSourcePlayer(2);
        foreach(int player in new[]{1,2})
        {
            var explicitPlayer=ordinary with{PanningPlayer=player};explicitPlayer.ValidateSourcePlayer(player);
            Assert.ThrowsExactly<InvalidOperationException>(()=>explicitPlayer.ValidateSourcePlayer(player==1?2:1));
        }
        foreach(int invalid in new[]{0,3,4})Assert.ThrowsExactly<InvalidOperationException>(()=>ordinary.ValidateSourcePlayer(invalid));
    }
}
