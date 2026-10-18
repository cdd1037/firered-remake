using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class NamedGeneralSourceOracleTests
{
    private const string Label="General_DoomDesireHit";
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static CommittedMoveAnimation Cue(int target)=>new("doom-general-source",17,0,new(target),new(target)){GeneralAnimationLabel=Label};
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){Visible=false}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript script)=>script with{Commands=[C("createvisualtask","AnimTask_SetAnimTargetToBattlerTarget","2"),C("createvisualtask","AnimTask_DoomDesireLightBeam","5"),C("waitforvisualfinish"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>(),Labels=new Dictionary<string,int>()};

    [TestMethod]
    public void EveryDefinedNativeBeamRegisterAndIndependentPixelSampleMatchesAllSixLayouts()
    {
        using var data=SourceVectorFixture.Read("named-general-source-vectors.json","python tests/Reference/NamedGeneral/generate.py --repo . --output tests/Game.Presentation.Tests/Fixtures/named-general-source-vectors.json");
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.ResolveGeneral(Label);int cases=0,rows=0,pixels=0;
        CollectionAssert.AreEquivalent(new[]{"morning-sun"},resolved.Overlays.Keys.ToArray());
        foreach(var test in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles=test.GetProperty("doubles").GetBoolean();int target=test.GetProperty("target").GetInt32();var bodies=Bodies(doubles);
            var runtime=new SourceBattleAnimation(Primitive(resolved.Script),Cue(target),bodies,doubles,new Audio());
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)(0x8000|7|(11<<5)|(17<<10)),240*112).ToArray()));
            foreach(var b in bodies)assets.Add(b.Asset,new(64,64,new ushort[4096]));
            var composer=new SourceBattleFrameComposer(assets,bodies,overlays:resolved.Overlays);SourceAnimationFrame? previous=null;
            foreach(var row in test.GetProperty("rows").EnumerateArray())
            {
                bool cleanup=row.GetProperty("cleanupOnly").GetBoolean();int nativeFrame=row.GetProperty("frame").GetInt32();string at=$"{doubles}/{target}/{nativeFrame}";
                if(cleanup)
                {
                    // This one intervening callback is an explicitly approved
                    // safe rule, not an invented native result beyond the table.
                    var hold=runtime.Tick();Assert.IsNotNull(previous);Assert.AreEqual(previous.Overlay,hold.Overlay,at);Assert.AreEqual(previous.AlphaA,hold.AlphaA,at);Assert.AreEqual(previous.AlphaB,hold.AlphaB,at);
                    Assert.AreEqual(1,runtime.ActiveVisualCount,at);CollectionAssert.AreEqual(composer.ComposeField(previous),composer.ComposeField(hold),at);
                }
                var frame=runtime.Tick();Assert.AreEqual(row.GetProperty("active").GetInt32(),runtime.ActiveVisualCount,at);
                Assert.AreEqual(row.GetProperty("alpha")[0].GetInt32(),frame.AlphaA,at);Assert.AreEqual(row.GetProperty("alpha")[1].GetInt32(),frame.AlphaB,at);
                Assert.AreEqual(row.GetProperty("overlay").GetInt32()!=0,frame.Overlay is not null,at);Assert.AreEqual(row.GetProperty("blend").GetInt32()!=0,frame.BlendEnabled,at);
                Assert.AreEqual(row.GetProperty("x").GetInt32(),frame.Overlay?.ScrollX??0,at);Assert.AreEqual(row.GetProperty("y").GetInt32(),frame.Overlay?.ScrollY??0,at);Assert.AreEqual(row.GetProperty("priority").GetInt32(),frame.BattlerBackgroundPriorities[0],at);
                if(!cleanup)
                {
                    var task=Field<IEnumerable>(runtime,"_tasks").Cast<object>().Single();Assert.AreEqual(row.GetProperty("phase").GetInt32(),Field<int>(task,"_phase"),at);Assert.AreEqual(row.GetProperty("index").GetInt32(),Field<int>(task,"_index"),at);Assert.AreEqual(row.GetProperty("timer").GetInt32(),Field<int>(task,"_timer"),at);
                    Assert.AreEqual(2,frame.BlendFirstTargets,at);Assert.AreEqual(63,frame.BlendSecondTargets,at);
                }
                if(row.TryGetProperty("pixelSamples",out var expected))
                {
                    string snapshot=JsonSerializer.Serialize(frame);var rgba=composer.ComposeField(frame);
                    foreach(var sample in expected.EnumerateArray())
                    {
                        int x=sample.GetProperty("x").GetInt32(),y=sample.GetProperty("y").GetInt32();var colors=sample.GetProperty("rgba").EnumerateArray().Select(v=>(byte)v.GetInt32()).ToArray();
                        CollectionAssert.AreEqual(colors,rgba.Skip((y*240+x)*4).Take(4).ToArray(),at+$" pixel({x},{y}) sourceIndex={sample.GetProperty("index").GetInt32()}");
                    }
                    Assert.IsTrue(expected.GetArrayLength()>=96,at);Assert.AreEqual(snapshot,JsonSerializer.Serialize(frame),at);pixels++;
                }
                previous=frame;rows++;
            }
            Assert.IsFalse(runtime.Active||runtime.GraphicsLoaded||runtime.HasResidualSprites);Assert.AreEqual(117,runtime.Snapshot().Frame);Assert.AreEqual(new PixelPoint(0,0),runtime.Snapshot().BackgroundScrollOffsets[0]);cases++;
        }
        Assert.AreEqual(6,cases);Assert.AreEqual(702,rows);Assert.AreEqual(54,pixels);
    }

    [TestMethod]
    public void BeamRejectsMissingMaterialUnknownAbiAndCompetingBackgroundOwners()
    {
        using var resources=new SourceAnimationResources(Folder);var root=resources.ResolveGeneral(Label).Script;var script=Primitive(root);var bodies=Bodies(false);
        void Reject(SourceAnimationScript candidate)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(candidate,Cue(1),bodies,false,new Audio()));
        Reject(script with{OverlayDependencies=[]});Reject(script with{OverlayPalettes=new Dictionary<string,ushort[]>()});
        Reject(script with{Commands=[script.Commands[0],C("createvisualtask","AnimTask_DoomDesireLightBeam","5","0"),C("end")]});
        Reject(script with{Commands=[script.Commands[0],script.Commands[1],script.Commands[1],C("end")]});
        Reject(script with{Commands=[script.Commands[0],C("monbg","ANIM_ATTACKER"),..script.Commands.Skip(1)]});
        Reject(script with{Commands=[script.Commands[0],C("setalpha","8","8"),..script.Commands.Skip(1)]});
    }
}
