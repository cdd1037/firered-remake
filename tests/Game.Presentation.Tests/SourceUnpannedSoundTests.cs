using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SourceUnpannedSoundTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void SourcePlaySePreservesSongPanningAndDoesNotWriteArgumentsOrDelayExecution(int actor)
    {
        var script=new SourceAnimationScript(32,"playse",[new("setarg",["0","99"],1),new("playse",["SOURCE_SONG"],2),new("setarg",["1","88"],3),new("delay",["2"],4),new("end",[],5)]){SoundEffects=new Dictionary<string,int>{{"SOURCE_SONG",1}}};
        var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();var audio=new Audio();
        var runtime=new SourceBattleAnimation(script,new("playse",1,32,new(actor),new(actor^1)),battlers,true,audio);
        var frame=runtime.Tick();var cue=audio.Cues.Single();Assert.AreEqual(0,cue.Pan);Assert.AreEqual(0,cue.Frame);Assert.IsNull(cue.PanningPlayer);Assert.IsFalse(cue.ApplyPanning);
        CollectionAssert.AreEqual(new short[]{99,88,0,0,0,0,0,0}.Concat(new short[8]).ToArray(),frame.AnimationArguments);Assert.AreEqual(0,runtime.ActiveVisualCount);
        while(runtime.Active)runtime.Tick();Assert.AreEqual(1,audio.Cues.Count);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{SoundEffects=new Dictionary<string,int>()},new("missing-song",1,32,new(actor),new(actor^1)),battlers,true,new Audio()));
    }
}
