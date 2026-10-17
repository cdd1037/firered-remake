using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class PsychicBackgroundStateTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static ushort[] Colors(int offset=0)=>Enumerable.Range(0,16).Select(i=>(ushort)(i+offset)).ToArray();
    private static SourceBattleAnimation Runtime(params SourceAnimationCommand[] tail)
    {
        var script=new SourceAnimationScript(95,"psychic-state",new[]{C("fadetobg","BG_PSYCHIC"),C("waitbgfadeout"),C("createvisualtask","AnimTask_SetPsychicBackground","5")}.Concat(tail).ToArray())
        {BackgroundSymbols=new Dictionary<string,int>{{"BG_PSYCHIC",3}},BackgroundPalettes=new Dictionary<int,ushort[]>{{3,Colors()}}};
        return new(script,new("state",1,95,new(0),new(1)),Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)),false,new Audio(),Colors(16));
    }
    [TestMethod]
    public void PaletteTaskStartsOnLoadFrameRotatesOnlyElevenEntriesAndStopsAfterSharedArgument()
    {
        var runtime=Runtime(C("delay","3"),C("setarg","7","0xFFFF"),C("delay","8"),C("waitbgfadein"),C("end"));
        CollectionAssert.AreEqual(new short[16],runtime.Snapshot().AnimationArguments);
        var frames=new List<SourceAnimationFrame>();while(runtime.Active&&frames.Count<100)frames.Add(runtime.Tick());
        CollectionAssert.AreEqual(Colors(),frames[19].MoveBackground!.FadedPalette);
        CollectionAssert.AreEqual(Colors(),frames[21].MoveBackground!.FadedPalette);
        var rotated=Colors();rotated[1]=11;for(int i=2;i<=11;i++)rotated[i]=(ushort)(i-1);
        CollectionAssert.AreEqual(rotated,frames[22].MoveBackground!.FadedPalette);
        // WaitAnimFrameCount decrements three times, switches callback on tick23, resumes on24.
        Assert.AreEqual((short)-1,frames[24].AnimationArguments[7]);
        CollectionAssert.AreEqual(rotated,frames[30].MoveBackground!.FadedPalette);
        Assert.AreEqual(0,frames[19].HitSplats.Length);Assert.AreEqual(0,runtime.ActiveVisualCount);
    }
    [TestMethod]
    public void PaletteBlendRebuildsFromUnfadedColorsBeforeLaterRotationAndArgumentsKeepTheirTail()
    {
        var runtime=Runtime(C("setarg","7","1234"),C("delay","3"),C("createvisualtask","AnimTask_BlendBattleAnimPal","0","F_PAL_BG","0","8","8","RGB_WHITE"),C("delay","3"),C("setarg","7","0xFFFF"),C("waitbgfadein"),C("end"));
        var frames=new List<SourceAnimationFrame>();while(runtime.Active&&frames.Count<100)frames.Add(runtime.Tick());
        var blended=Colors().Select(c=>SourcePaletteMath.Blend(c,8,32767)).ToArray();
        CollectionAssert.AreEqual(blended,frames[24].MoveBackground!.FadedPalette);
        CollectionAssert.AreEqual(new short[]{1,0,8,8,32767,0,0,1234}.Concat(new short[8]).ToArray(),frames[24].AnimationArguments);
        ushort last=blended[11];for(int i=10;i>0;i--)blended[i+1]=blended[i];blended[1]=last;
        CollectionAssert.AreEqual(blended,frames[26].MoveBackground!.FadedPalette);
    }
    [TestMethod]
    public void RestoreLoadsOriginalTerrainPaletteAndCancelClearsArgumentState()
    {
        var runtime=Runtime(C("waitbgfadein"),C("restorebg"),C("waitbgfadeout"),C("setarg","7","0xFFFF"),C("waitbgfadein"),C("end"));
        var frames=new List<SourceAnimationFrame>();while(runtime.Active&&frames.Count<100)frames.Add(runtime.Tick());
        var restored=frames.First(f=>f.MoveBackground!.Revision==2);
        CollectionAssert.AreEqual(Colors(16),restored.MoveBackground!.FadedPalette);
        Assert.IsNull(frames[^1].MoveBackground!.Id);Assert.AreEqual((short)-1,frames[^1].AnimationArguments[7]);
        runtime.Cancel();CollectionAssert.AreEqual(new short[16],runtime.Snapshot().AnimationArguments);Assert.AreEqual(0,runtime.Snapshot().MoveBackground!.FadedPalette.Length);
    }
}
