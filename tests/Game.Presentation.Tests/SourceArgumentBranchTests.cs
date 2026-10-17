using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SourceArgumentBranchTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceBattleAnimation Runtime(SourceAnimationScript script,int actor=0,int target=1)=>new(script,new("arg-branch",1,script.MoveId,new(actor),new(target)),Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)),true,new Audio());
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void PredicatesWriteOnlyReturnArgumentImmediatelyWithoutVisualCount(int actor)
    {
        var script=new SourceAnimationScript(1,"predicates",[C("setarg","0","123"),C("setarg","7","99"),C("createvisualtask","AnimTask_IsContest","2"),C("jumpargeq","7","0","side"),C("setarg","0","999"),C("createvisualtask","AnimTask_GetAttackerSide","2"),C("delay","2"),C("createvisualtask","AnimTask_IsTargetPlayerSide","2"),C("delay","2"),C("end")]){Labels=new Dictionary<string,int>{{"side",5}}};
        var runtime=Runtime(script,actor,actor);var first=runtime.Tick();Assert.AreEqual(0,first.Frame);Assert.AreEqual(123,(int)first.AnimationArguments[0]);Assert.AreEqual(actor&1,(int)first.AnimationArguments[7]);Assert.AreEqual(0,runtime.ActiveVisualCount);
        for(int i=0;i<8&&runtime.Active&&runtime.Snapshot().AnimationArguments[7]==(actor&1);i++)runtime.Tick();
        // Target is the same battler; player-side predicate is the inverse side value.
        Assert.AreEqual(1-(actor&1),(int)runtime.Snapshot().AnimationArguments[7]);while(runtime.Active)runtime.Tick();
    }
    [TestMethod][DataRow(0)][DataRow(7)]
    public void EqualityUsesSigned16ValueAndFalsePathFallsThrough(int argument)
    {
        var script=new SourceAnimationScript(1,"signed-branch",[C("setarg",argument.ToString(),"0xFFFF"),C("jumpargeq",argument.ToString(),"-1","matched"),C("setarg","2","999"),C("setarg","3","11"),C("jumpargeq",argument.ToString(),"1","wrong"),C("setarg","4","22"),C("goto","done"),C("setarg","4","999"),C("delay","2"),C("end")]){Labels=new Dictionary<string,int>{{"matched",3},{"wrong",7},{"done",8}}};
        var frame=Runtime(script).Tick();Assert.AreEqual(-1,(int)frame.AnimationArguments[argument]);Assert.AreEqual(0,(int)frame.AnimationArguments[2]);Assert.AreEqual(11,(int)frame.AnimationArguments[3]);Assert.AreEqual(22,(int)frame.AnimationArguments[4]);
    }
    [TestMethod]
    public void SafeExtendedRegisterPolicyPreservesLastSlotAcrossCallbackOperandsAndClearsOnCancel()
    {
        // Magnitude authors index15 although the original declaration has eight
        // arguments. This checks the bounded remake policy, not native aliasing.
        var script=new SourceAnimationScript(1,"safe-register",[C("setarg","15","0xFFFF"),
            C("createvisualtask","AnimTask_BlendMonInAndOut","3","0","RGB_WHITE","3","2","2"),
            C("jumpargeq","15","-1","matched"),C("setarg","14","999"),C("delay","2"),C("end")])
            {Labels=new Dictionary<string,int>{{"matched",4}}};
        var runtime=Runtime(script);var frame=runtime.Tick();
        Assert.AreEqual(16,frame.AnimationArguments.Length);Assert.AreEqual((short)-1,frame.AnimationArguments[15]);
        Assert.AreEqual((short)0,frame.AnimationArguments[14]);Assert.AreEqual((short)32767,frame.AnimationArguments[1]);
        runtime.Cancel();CollectionAssert.AreEqual(new short[16],runtime.Snapshot().AnimationArguments);
        Assert.AreEqual((short)-1,frame.AnimationArguments[15]);
        Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime(new(1,"outside-register",[C("setarg","16","1"),C("end")])));
    }
    [TestMethod]
    public void LocalHelperLabelsAllowBackwardReturnAndCannotEscapeIntoRootScope()
    {
        var helper=new[]{C("jumpargeq","7","1","selected"),C("return"),C("setarg","1","41"),C("goto","finish")};
        var script=new SourceAnimationScript(1,"scoped",[C("setarg","7","1"),C("call","helper"),C("setarg","2","42"),C("delay","2"),C("end")]){Labels=new Dictionary<string,int>{{"selected",4}},Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"helper",helper}},SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>{{"helper",new Dictionary<string,int>{{"selected",2},{"finish",1}}}}};
        var runtime=Runtime(script);var frame=runtime.Tick();Assert.AreEqual(41,(int)frame.AnimationArguments[1]);Assert.AreEqual(42,(int)frame.AnimationArguments[2]);Assert.AreEqual(0,runtime.ActiveVisualCount);while(runtime.Active)runtime.Tick();
        Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime(script with{SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>() }));
    }
    [TestMethod]
    public void CyclesAndScopeErrorsAreRejectedBeforePlayback()
    {
        var loop=new SourceAnimationScript(1,"loop",[C("goto","loop"),C("end")]){Labels=new Dictionary<string,int>{{"loop",0}}};
        Assert.ThrowsExactly<InvalidDataException>(()=>Runtime(loop));
        var fall=new SourceAnimationScript(1,"fall",[C("call","bad"),C("end")]){Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"bad",[C("delay","1")]}}};
        Assert.ThrowsExactly<InvalidDataException>(()=>Runtime(fall));
        Assert.ThrowsExactly<InvalidDataException>(()=>Runtime(new(1,"return",[C("return"),C("end")])));
        var foreign=fall with{SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>{{"absent",new Dictionary<string,int>{{"x",0}}}}};Assert.ThrowsExactly<InvalidDataException>(()=>Runtime(foreign));
    }
    [TestMethod][DataRow("-1","0","done")][DataRow("16","0","done")][DataRow("0","65536","done")][DataRow("0","-32769","done")][DataRow("0","0","missing")]
    public void InvalidArgumentWidthsAndForeignLabelsFailClosed(string index,string value,string label)
    {
        var script=new SourceAnimationScript(1,"invalid-branch",[C("jumpargeq",index,value,label),C("end")]){Labels=new Dictionary<string,int>{{"done",1}}};
        Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime(script));
    }
    [TestMethod]
    public void ChangeBackgroundLoadsPaletteImmediatelyWithoutStartingFadeTask()
    {
        ushort[] colors=Enumerable.Range(0,16).Select(i=>(ushort)(i*3)).ToArray();var script=new SourceAnimationScript(1,"change-bg",[C("changebg","BG_TEST"),C("setarg","0","77"),C("waitforvisualfinish"),C("delay","2"),C("restorebg"),C("waitbgfadeout"),C("waitbgfadein"),C("end")]){BackgroundSymbols=new Dictionary<string,int>{{"BG_TEST",7}},BackgroundPalettes=new Dictionary<int,ushort[]>{{7,colors}}};
        var runtime=Runtime(script);var frame=runtime.Tick();Assert.AreEqual(7,frame.MoveBackground!.Id);CollectionAssert.AreEqual(colors,frame.MoveBackground.FadedPalette);Assert.AreEqual(0,frame.MoveBackground.Darkening);Assert.IsFalse(runtime.PaletteFadeActive);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(77,(int)frame.AnimationArguments[0]);
        while(runtime.Active)runtime.Tick();Assert.IsNull(runtime.Snapshot().MoveBackground!.Id);
        var cancelled=Runtime(script);cancelled.Tick();cancelled.Cancel();Assert.IsNull(cancelled.Snapshot().MoveBackground!.Id);Assert.IsFalse(cancelled.PaletteFadeActive);
    }
}
