using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class BackgroundAnimationCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Sounds{get;}=[];
        public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);
        public bool IsPlaying=>false;
        public void StopAnimationEffects(){}
    }
    private static SourceBattleAnimation Runtime(SourceAnimationCommand[] commands,Audio audio)
    {
        var script=new SourceAnimationScript(168,"background-timing",commands){BackgroundSymbols=new Dictionary<string,int>{{"BG_DARK",1}}};
        return new(script,new("background",1,168,new(0),new(1)),Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)),false,audio);
    }
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    [TestMethod]
    public void HardwareFadeUsesVBlankCompletionAndSeparateLoadTick()
    {
        var audio=new Audio();var runtime=Runtime([C("fadetobg","BG_DARK"),C("waitbgfadeout"),C("playsewithpan","SE_M_VITAL_THROW","SOUND_PAN_ATTACKER"),C("waitbgfadein"),C("restorebg"),C("waitbgfadein"),C("end")],audio);
        var frames=new List<SourceAnimationFrame>();while(runtime.Active&&frames.Count<100)frames.Add(runtime.Tick());
        Assert.IsFalse(runtime.Active);Assert.AreEqual(76,frames[^1].Frame);
        Assert.AreEqual(19,audio.Sounds.Single().Frame);
        Assert.AreEqual(0,frames[0].MoveBackground!.Darkening);
        for(int i=1;i<=16;i++)Assert.AreEqual(i,frames[i].MoveBackground!.Darkening);
        Assert.IsNull(frames[18].MoveBackground!.Id);
        Assert.AreEqual(1,frames[19].MoveBackground!.Id);Assert.AreEqual(1,frames[19].MoveBackground!.Revision);
        for(int i=20;i<=35;i++)Assert.AreEqual(35-i,frames[i].MoveBackground!.Darkening);
        Assert.AreEqual(1,frames[56].MoveBackground!.Id);
        Assert.IsNull(frames[57].MoveBackground!.Id);Assert.AreEqual(2,frames[57].MoveBackground!.Revision);
        Assert.AreEqual(0,frames[^1].MoveBackground!.Darkening);
    }
    [TestMethod]
    public void BackgroundTaskIsUncountedAndCancelReleasesHardwareState()
    {
        var audio=new Audio();var runtime=Runtime([C("fadetobg","BG_DARK"),C("waitforvisualfinish"),C("playsewithpan","SE_M_VITAL_THROW","SOUND_PAN_ATTACKER"),C("waitbgfadein"),C("end")],audio);
        runtime.Tick();Assert.AreEqual(0,audio.Sounds.Single().Frame);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsTrue(runtime.PaletteFadeActive);
        runtime.Tick();Assert.AreEqual(1,runtime.Snapshot().MoveBackground!.Darkening);
        runtime.Cancel();Assert.IsFalse(runtime.PaletteFadeActive);Assert.AreEqual(0,runtime.Snapshot().MoveBackground!.Darkening);Assert.IsNull(runtime.Snapshot().MoveBackground!.Id);
    }
    [TestMethod]
    public void BackgroundDefinitionIsRequiredBeforeRuntimeStarts()
    {
        Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime([C("fadetobg","BG_UNKNOWN"),C("waitbgfadein"),C("end")],new Audio()));
    }
}
