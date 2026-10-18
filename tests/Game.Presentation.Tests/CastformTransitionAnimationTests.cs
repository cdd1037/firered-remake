using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CastformTransitionAnimationTests
{
    private const string Label="General_CastformChange";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static CommittedMoveAnimation Cue(int slot)=>new("form",1,0,new(slot),new(slot)){GeneralAnimationLabel=Label};
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"before"+i,0,0,32,32)).ToArray();
    [TestMethod]
    public void TaskRegisterWritesAndSwapMatchExtractedOriginalCallbackOrdinals()
    {
        using var doc=SourceVectorFixture.Read("castform-transition-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_castform_transition_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/castform-transition-vectors.json");
        for(int slot=0;slot<4;slot++)
        {
            var before=Battlers();var after=before[slot] with{Asset="after",YOffset=9,Elevation=14,SourceWidth=48,SourceHeight=48};
            var script=new SourceAnimationScript(0,Label,[new("end",[],1)]);
            var runtime=new SourceBattleAnimation(script,Cue(slot),before,true,new Audio(),battlerReplacements:new Dictionary<int,AnimationBattler>{{slot,after}});
            var type=typeof(SourceBattleAnimation).GetNestedType("FormChangeMosaicTask",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[slot],null)!;
            var step=type.GetMethod("Step")!;SourceAnimationFrame? old=null;
            foreach(var row in doc.RootElement.GetProperty("tasks").GetProperty(slot+":1").EnumerateArray())
            {
                step.Invoke(task,[runtime]);var frame=runtime.Snapshot();int ordinal=row[0].GetInt32();
                Assert.AreEqual((short)row[1].GetInt32(),type.GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(task));
                Assert.AreEqual((short)row[2].GetInt32(),type.GetField("_stretch",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(task));
                Assert.AreEqual((short)row[3].GetInt32(),type.GetField("_delay",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(task));
                Assert.AreEqual(row[4].GetInt32()&15,frame.BackgroundMosaicHorizontal,$"slot{slot} ordinal{ordinal}");
                Assert.AreEqual(row[4].GetInt32()>>4,frame.BackgroundMosaicVertical);
                CollectionAssert.AreEqual(new[]{row[5].GetInt32()!=0,row[6].GetInt32()!=0},frame.BattlerBackgroundMosaic);
                Assert.AreEqual(row[7].GetInt32()==0?before[slot]:after,frame.Battlers.Single(b=>b.Slot.Value==slot));
                Assert.AreEqual(row[9].GetInt32()==0,type.GetProperty("Finished")!.GetValue(task));
                if(ordinal==45)old=frame;
                if(ordinal==46)Assert.AreEqual(before[slot],old!.Battlers.Single(b=>b.Slot.Value==slot),"Prior material snapshots are immutable.");
            }
            runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().BackgroundMosaicHorizontal);Assert.IsTrue(runtime.Snapshot().BattlerBackgroundMosaic.All(x=>!x));
        }
    }
    [TestMethod,DataRow(false),DataRow(true)]
    public void NamedGeneralUsesVisibleMosaicOrImmediateHiddenSwap(bool hidden)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.ResolveGeneral(Label);var script=resolved.Script;
        var forms=new SourceBattleFormCatalog(Path.Combine(TestAssets.Root,"content/generated/battle/art/campaign-hit-art"));
        for(int slot=0;slot<4;slot++)for(int form=1;form<4;form++)
        {
            var battlers=Battlers();var old=forms.Resolve(385,new(slot),0,slot%2==0,true);var next=forms.Resolve(385,new(slot),form,slot%2==0,true);
            battlers[slot]=old.Geometry with{Asset="old-form",Visible=!hidden};var replacement=next.Geometry with{Asset="new-form",Visible=!hidden};
            var assets=resolved.Assets.ToDictionary(x=>x.Key,x=>x.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8421,240*112).ToArray());
            foreach(var battler in battlers)assets[battler.Asset]=forms.Resolve(385,battler.Slot,0,false).Pixels;
            assets["old-form"]=old.Pixels;assets["new-form"]=next.Pixels;
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
            var audio=new Audio();var runtime=new SourceBattleAnimation(script,Cue(slot),battlers,true,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{slot,replacement}});
            int count=0,swaps=0;bool changed=false,sawMaximum=false;
            while(runtime.Active&&count++<140)
            {
                var frame=runtime.Tick();Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);var material=frame.Battlers.Single(b=>b.Slot.Value==slot);
                if(material.Asset=="new-form"&&!changed){changed=true;swaps++;Assert.AreEqual(hidden?0:15,frame.BackgroundMosaicHorizontal);}
                sawMaximum|=frame.BackgroundMosaicHorizontal==15;
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(1,swaps);Assert.AreEqual(!hidden,sawMaximum);
            Assert.AreEqual(hidden?0:2,audio.Cues.Count);if(!hidden)CollectionAssert.AreEqual(new[]{"SE_M_TELEPORT","SE_M_MINIMIZE"},audio.Cues.Select(c=>c.Id).ToArray());
            Assert.IsTrue(runtime.Snapshot().BattlerBackgroundMosaic.All(x=>!x));Assert.AreEqual(0,runtime.Snapshot().BackgroundMosaicHorizontal);Assert.AreEqual(0,runtime.Snapshot().BackgroundSlots.Length);
        }
    }
    [TestMethod]
    public void MissingReplacementAndUnsupportedTransformModeFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.ResolveGeneral(Label).Script;var battlers=Battlers();
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(0),battlers,true,new Audio()));
        var rows=source.Commands.ToArray();int index=Array.FindIndex(rows,c=>c.Args.FirstOrDefault()=="AnimTask_TransformMon");rows[index]=rows[index] with{Args=["AnimTask_TransformMon","2","255"]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=rows},Cue(0),battlers,true,new Audio(),battlerReplacements:new Dictionary<int,AnimationBattler>{{0,battlers[0]}}));
        var unused=new SourceAnimationScript(0,Label,[new("end",[],1)]){Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"Unused",[new("createvisualtask",["AnimTask_TransformMon","2","1"],2),new("return",[],3)]}}};
        var valid=new SourceBattleAnimation(unused,Cue(0),battlers,true,new Audio());Assert.IsNotNull(valid);
        var malformed=unused with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"Unused",[new("createvisualtask",["AnimTask_TransformMon","2","255"],2),new("return",[],3)]}}};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(malformed,Cue(0),battlers,true,new Audio()));
    }
}
