using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class HardwareFadeCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceBattleAnimation Runtime(SourceAnimationScript script)=>new(script,new("fade",1,script.MoveId,new(0),new(1)),[new(new(0),"a",0,0,64,64),new(new(1),"b",0,0,64,64)],false,new Audio());
    [TestMethod,DataRow(0,10,3,false),DataRow(10,0,3,true),DataRow(4,4,0,false),DataRow(16,0,0,true),DataRow(0,1,64,false),DataRow(0,1,255,false)]
    public void HardwareFadeSharesSourceDelayAndVBlankFinishingOwnership(int from,int target,int delay,bool reset)
    {
        var runtime=Runtime(new(227,"fade",[C("createvisualtask","AnimTask_HardwarePaletteFade","2","248",delay.ToString(),from.ToString(),target.ToString(),reset?"1":"0"),C("waitforvisualfinish"),C("end")]));
        delay&=63;int current=from,counter=delay;bool finishing=false;int ticks=0;
        while(!finishing)
        {
            if(counter<delay)counter++;else{counter=0;int next=current+(from<target?1:-1);if(from<target?next>target:next<target){finishing=true;if(reset)current=0;}else current=next;}
            var f=runtime.Tick();ticks++;
            Assert.AreEqual(!finishing,runtime.PaletteFadeActive);
            if(finishing&&reset){Assert.IsNull(f.Brightness);Assert.AreEqual(0,f.BlendFirstTargets);}
            else {Assert.AreEqual(current,f.Brightness!.Coefficient);Assert.AreEqual(SourceBrightnessMode.Darken,f.Brightness.Mode);Assert.AreEqual(56,f.BlendFirstTargets);}
            Assert.IsTrue(ticks<100);
        }
        // RunTasks saw active before VBlank ended the global fade; its wait task
        // retires on the next pass, followed by the script's authored wait release.
        Assert.IsTrue(runtime.Active);for(int i=0;runtime.Active&&i<8;i++)runtime.Tick();Assert.IsFalse(runtime.Active);
        runtime.Cancel();Assert.IsNull(runtime.Snapshot().Brightness);
    }
    [TestMethod]
    public void ExpandedFiniteHelpersRunInOneScriptPassWithoutSyntheticYield()
    {
        var root=new SourceAnimationScript(1,"finite",[..Enumerable.Repeat(C("call","helper"),40),C("end")]){Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"helper",[C("setarg","7","5"),C("setarg","7","9"),C("return")]}}};
        var runtime=Runtime(root);var f=runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual((short)9,f.AnimationArguments[7]);
        Assert.AreEqual(161,SourceAnimationProgramDependencies.MaximumCost(root,_=>1));
    }
    [TestMethod]
    public void OriginalHardwarePaletteEngineMatchesEachTransferredFrame()
    {
        using var vectors=SourceVectorFixture.Read("spotlight-source-vectors.json","python tests/Reference/Spotlight/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/spotlight-source-vectors.json");
        int cases=0,frames=0;
        foreach(var item in vectors.RootElement.GetProperty("hardware").EnumerateArray())
        {
            string[] args=item.GetProperty("arguments").EnumerateArray().Select(v=>v.GetInt32().ToString()).ToArray();
            var script=new SourceAnimationScript(0,"General_HardwareFadeProbe",[C("createvisualtask",["AnimTask_HardwarePaletteFade","2",..args]),C("waitforvisualfinish"),C("end")]);
            var runtime=new SourceBattleAnimation(script,new("native-fade",1,0,new(0),new(1)){GeneralAnimationLabel=script.Label},[new(new(0),"a",0,0,64,64),new(new(1),"b",0,0,64,64)],false,new Audio());
            foreach(var row in item.GetProperty("rows").EnumerateArray())
            {
                int[] r=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(r[1]!=2)continue;
                var f=runtime.Tick();int control=f.Brightness is {} b?((int)b.Mode<<6)|f.BlendFirstTargets:0;
                Assert.AreEqual(r[0],f.Frame);Assert.AreEqual(r[3]!=0,runtime.PaletteFadeActive);
                Assert.AreEqual(r[11],control);Assert.AreEqual(r[12],f.Brightness?.Coefficient??0);frames++;
            }
            for(int i=0;runtime.Active&&i<8;i++)runtime.Tick();Assert.IsFalse(runtime.Active);cases++;
        }
        Assert.AreEqual(7,cases);Assert.AreEqual(175,frames);
    }

    [TestMethod]
    public void MalformedHardwareOperandsFailBeforeAnyRegisterSideEffect()
    {
        string[] valid=["AnimTask_HardwarePaletteFade","2","248","3","0","10","0"];
        foreach(var change in new[]{(1,"256"),(2,"64"),(2,"256"),(3,"256"),(3,"-1"),(4,"17"),(5,"-1"),(6,"2")})
        {
            var a=(string[])valid.Clone();a[change.Item1]=change.Item2;
            Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime(new(227,"invalid",[C("createvisualtask",a),C("end")])));
        }
        Assert.ThrowsExactly<SourceAnimationException>(()=>Runtime(new(227,"invalid",[C("createvisualtask",valid[..6]),C("end")])));
    }

    [TestMethod]
    public void HardwareResetDoesNotWriteTheIndependentAlphaRegister()
    {
        var runtime=Runtime(new(227,"registers",[C("setalpha","8","7"),C("createvisualtask","AnimTask_HardwarePaletteFade","2","248","0","0","0","1"),C("waitforvisualfinish"),C("end")]));
        var frame=runtime.Tick();Assert.IsFalse(frame.BlendEnabled);Assert.IsNull(frame.Brightness);
        Assert.AreEqual(0,frame.BlendFirstTargets);Assert.AreEqual(0,frame.BlendSecondTargets);
        Assert.AreEqual(8,frame.AlphaA);Assert.AreEqual(7,frame.AlphaB);
    }

}
