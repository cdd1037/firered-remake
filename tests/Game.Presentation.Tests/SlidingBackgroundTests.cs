using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SlidingBackgroundTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceBattleAnimation Runtime(int actor,params SourceAnimationCommand[] commands)=>new(new(87,"sliding-vector",commands),new("sliding",1,87,new(actor),new(actor^1)),Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)),false,new Audio());
    [TestMethod]
    public void SlidingUsesSignedFractionalCarryMirroringAndUnsignedRegisterWrap()
    {
        foreach(int actor in new[]{0,1})foreach(int flip in new[]{0,1})
        {
            var runtime=Runtime(actor,C("createvisualtask","AnimTask_StartSlidingBg","2","-384","128",flip.ToString(),"-1"),C("waitforvisualfinish"),C("delay","40"),C("setarg","7","0xFFFF"),C("end"));int sign=actor==1&&flip==1?-1:1;
            for(int tick=1;tick<=16;tick++)
            {
                var frame=runtime.Tick();var bg=frame.MoveBackground!;Assert.AreEqual(0,bg.ScreenSize);Assert.AreEqual(unchecked((ushort)(int)Math.Floor(-384d*sign*tick/256)),bg.ScrollX);Assert.AreEqual(unchecked((ushort)(int)Math.Floor(128d*sign*tick/256)),bg.ScrollY);Assert.AreEqual((short)(-384*sign),frame.AnimationArguments[0]);Assert.AreEqual((short)(128*sign),frame.AnimationArguments[1]);Assert.AreEqual(0,runtime.ActiveVisualCount);
            }
            runtime.Cancel();var cancelled=runtime.Snapshot().MoveBackground!;Assert.AreEqual(0,cancelled.ScrollX);Assert.AreEqual(0,cancelled.ScrollY);Assert.AreEqual(1,cancelled.ScreenSize);
        }
    }
    [TestMethod]
    public void SlidingUncountedWaitAdvancesAndSharedStopRestoresZeroAndLargeMap()
    {
        var runtime=Runtime(0,C("createvisualtask","AnimTask_StartSlidingBg","5","-2304","768","1","-1"),C("waitforvisualfinish"),C("delay","2"),C("setarg","7","0xFFFF"),C("delay","0"),C("end"));
        for(int tick=1;tick<=4;tick++){var frame=runtime.Tick();Assert.AreEqual(unchecked((ushort)(-9*tick)),frame.MoveBackground!.ScrollX);Assert.AreEqual(3*tick,frame.MoveBackground.ScrollY);Assert.AreEqual(0,runtime.ActiveVisualCount);}
        var stopped=runtime.Tick();Assert.AreEqual(0,stopped.MoveBackground!.ScrollX);Assert.AreEqual(0,stopped.MoveBackground.ScrollY);Assert.AreEqual(1,stopped.MoveBackground.ScreenSize);Assert.AreEqual((short)-1,stopped.AnimationArguments[7]);while(runtime.Active)runtime.Tick();
    }
    [TestMethod]
    public void RawUpdaterPriorityFivePreservesCreationOrderInsteadOfInitializerPriority()
    {
        var runtime=Runtime(0,C("createvisualtask","AnimTask_StartSlidingBg","10","-256","0","0","0"),C("createvisualtask","AnimTask_StartSlidingBg","2","256","0","0","-1"),C("delay","10"),C("setarg","7","0xFFFF"),C("end"));
        var frame=runtime.Tick();Assert.AreEqual(1,frame.MoveBackground!.ScrollX);Assert.AreEqual(1,frame.MoveBackground.ScreenSize);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(2,runtime.Tick().MoveBackground!.ScrollX);runtime.Cancel();
    }
    [TestMethod]
    public void SlidingRejectsWrongCommandWidthsUnknownMirrorFlagsAndVelocityOverflow()
    {
        foreach(var args in new[]{new[]{"5","1","2","0"},new[]{"5","1","2","0","-1","0"},new[]{"5","1","2","2","-1"},new[]{"5","32768","2","0","-1"},new[]{"5","1","-32769","0","-1"},new[]{"5","1","2","0","65536"}})
            Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime(0,C("createvisualtask",new[]{"AnimTask_StartSlidingBg"}.Concat(args).ToArray()),C("end")));
    }

}
