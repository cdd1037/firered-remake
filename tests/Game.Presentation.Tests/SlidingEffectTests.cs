using Game.Presentation.BattleAnimations;
using System.Text.Json;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SlidingEffectTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    [TestMethod]
    public void PatternShakeReadsLiveGlobalOffsetAndLeavesOamOneTaskPhaseBehind()
    {
        var b=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var script=new SourceAnimationScript(87,"pattern-live",[C("createvisualtask","AnimTask_ShakeTargetInPattern","2","10","3","TRUE","0"),C("delay","0"),C("setarg","1","257"),C("delay","0"),C("setarg","1","2"),C("waitforvisualfinish"),C("end")]);
        var runtime=new SourceBattleAnimation(script,new("pattern",1,87,new(0),new(1)),b,false,new Audio());int prior=3;
        foreach(int expected in new[]{0,3,257,0,0,2,2,2,0})
        {
            var frame=runtime.Tick();Assert.AreEqual(expected,frame.Offsets[1].Y);Assert.AreEqual(prior&255,frame.ObjectOffsets[1].Y);prior=expected;
        }
        while(runtime.Active)runtime.Tick();Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[1]);
    }
    [TestMethod][DataRow("FALSE",1)][DataRow("TRUE",0)]
    public void PatternShakePreservesSignedSixteenBitAssignmentAtMinimumAmplitude(string vertical,int pattern)
    {
        var b=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var script=new SourceAnimationScript(87,"pattern-width",[C("createvisualtask","AnimTask_ShakeTargetInPattern","2","4","-32768",vertical,pattern.ToString()),C("waitforvisualfinish"),C("end")]);
        var runtime=new SourceBattleAnimation(script,new("pattern-width",1,87,new(0),new(1)),b,false,new Audio());
        var first=runtime.Tick();
        if(vertical=="FALSE")Assert.AreEqual(-32768,first.Offsets[1].X);
        else{Assert.AreEqual(0,first.Offsets[1].Y);Assert.AreEqual(-32768,runtime.Tick().Offsets[1].Y);}
        while(runtime.Active)runtime.Tick();Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[1]);
    }
    [TestMethod][DataRow("-1")][DataRow("2")]
    public void PatternShakeRejectsValuesOutsideTheSourceBooleanContract(string vertical)
    {
        var b=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var script=new SourceAnimationScript(87,"pattern-invalid",[C("createvisualtask","AnimTask_ShakeTargetInPattern","2","4","3",vertical,"0"),C("waitforvisualfinish"),C("end")]);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("pattern-invalid",1,87,new(0),new(1)),b,false,new Audio()));
    }
    [TestMethod]
    public void PatternShakePreservesOmittedSignedPatternAndUsesZeroVersusNonzeroSourceBranch()
    {
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/overlay-pattern-shake-source-vectors.json")));
        int count=0;
        foreach(var sample in vectors.RootElement.GetProperty("cases").EnumerateArray())foreach(bool explicitPattern in new[]{false,true})
        {
            int pattern=sample.GetProperty("pattern").GetInt32();string vertical=sample.GetProperty("vertical").GetInt32().ToString();
            var arguments=new List<string>{"AnimTask_ShakeTargetInPattern","3","20","1",vertical};if(explicitPattern)arguments.Add(pattern.ToString());
            var script=new SourceAnimationScript(184,"pattern-retained",[C("setarg","3",pattern.ToString()),new("createvisualtask",arguments.ToArray(),1),C("waitforvisualfinish"),C("end")]);
            var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
            var runtime=new SourceBattleAnimation(script,new("pattern-retained",1,184,new(0),new(1)),battlers,false,new Audio());
            // Synchronous initializer is callback1; the same Tick runs callback2.
            int previousX=sample.GetProperty("rows")[0][1].GetInt32(),previousY=sample.GetProperty("rows")[0][2].GetInt32();
            foreach(var row in sample.GetProperty("rows").EnumerateArray().Skip(1))
            {
                var frame=runtime.Tick();Assert.AreEqual((short)pattern,frame.AnimationArguments[3]);
                Assert.AreEqual(new PixelPoint(row[1].GetInt32(),row[2].GetInt32()),frame.Offsets[1]);
                int wrappedX=previousX<0?512+previousX:previousX;
                Assert.AreEqual(new PixelPoint(wrappedX>255?wrappedX-512:wrappedX,previousY&255),frame.ObjectOffsets[1]);
                Assert.AreEqual(row[3].GetInt32(),runtime.ActiveVisualCount);previousX=row[1].GetInt32();previousY=row[2].GetInt32();
            }
            while(runtime.Active)runtime.Tick();Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[1]);count++;
        }
        Assert.AreEqual(16,count);
    }
    [TestMethod]
    public void NonAttackerBlendUsesAllFourBanksAndShiftsSharedArgumentsBeforeImmediateStep()
    {
        for(int actor=0;actor<4;actor++)
        {
            var b=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64){Visible=i!=(actor^2)}).ToArray();
            var script=new SourceAnimationScript(322,"nonattacker",[C("setarg","4","77"),C("setarg","5","88"),C("createvisualtask","AnimTask_BlendNonAttackerPalettes","2","2","0","3","RGB_BLACK"),C("waitforvisualfinish"),C("end")]);
            var runtime=new SourceBattleAnimation(script,new("blend",1,322,new(actor),new(actor)),b,true,new Audio());
            for(int tick=1;tick<=11;tick++)
            {
                var frame=runtime.Tick();CollectionAssert.AreEqual(new short[]{2,2,0,3,0,77,0,0}.Concat(new short[8]).ToArray(),frame.AnimationArguments);int coefficient=Math.Max(0,(tick+1)/3-1);
                if(coefficient==0){Assert.AreEqual(0,frame.PaletteBlends.Length);continue;}
                CollectionAssert.AreEquivalent(Enumerable.Range(16,4).Where(p=>p!=16+actor).ToArray(),frame.PaletteBlends.Select(p=>p.Palette).ToArray());Assert.IsTrue(frame.PaletteBlends.All(p=>p.Coefficient==coefficient&&p.Color==0));
            }
            Assert.AreEqual(0,runtime.ActiveVisualCount);while(runtime.Active)runtime.Tick();
        }
    }
    [TestMethod][DataRow(87,11)][DataRow(322,16)]
    public void SlidingRootsComposeAllActorPositionsAndRestoreSourceRegisters(int move,int background)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("sliding-root",1,move,new(actor),new(move==322?actor:actor^1)),b,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=new SourceBattleFrameComposer(assets,b,resolved.StatMask,resolved.Metallic,surface,resolved.Backgrounds);int ticks=0;bool moved=false,loaded=false;
            while(runtime.Active&&ticks++<2048){var frame=runtime.Tick();moved|=frame.MoveBackground!.ScrollX!=0||frame.MoveBackground.ScrollY!=0;loaded|=frame.MoveBackground.Id==background;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(moved&&loaded);var final=runtime.Snapshot().MoveBackground!;Assert.IsNull(final.Id);Assert.AreEqual(0,final.ScrollX);Assert.AreEqual(0,final.ScrollY);Assert.AreEqual(1,final.ScreenSize);Assert.AreEqual(0,final.Darkening);
        }
    }
}
