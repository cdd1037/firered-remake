using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SubstituteSwapAnimationTests
{
    private const string Callback="AnimTask_SwapMonSpriteToFromSubstitute";
    private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Label(bool mon)=>mon?"Special_SubstituteToMon":"Special_MonToSubstitute";
    private static SourceAnimationScript Script(bool mon,bool task=true)=>new(0,Label(mon),task
        ?[new("createvisualtask",[Callback,"2",mon?"TRUE":"FALSE"],1),new("end",[],2)]
        :[new("end",[],1)]);
    private static CommittedMoveAnimation Cue(int slot,bool mon)=>new("substitute-swap",1,0,new(slot),new(slot)){GeneralAnimationLabel=Label(mon)};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("substitute-swap-vectors.json",
        "python tests/Game.Presentation.Tests/Reference/generate_substitute_swap_vectors.py --root . --output <external-vector-directory>/substitute-swap-vectors.json");
    private static int Read(JsonElement row,string name)=>row.GetProperty(name).GetInt32();
    private static (SourceBattleAnimation Runtime,AnimationBattler[] Initial,AnimationBattler Replacement,Audio Audio) Runtime(JsonElement row,bool task)
    {
        bool doubles=Read(row,"doubles")!=0,mon=Read(row,"loadMon")!=0;int slot=Read(row,"slot");
        var bodies=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,7,0,64,64)).ToArray();
        var body=bodies[slot];var center=body.Base(doubles);
        var doll=body with{Asset="doll-"+slot,DisplayCenter=new(center.X,center.Y+(body.Slot.IsPlayer?17:16))};
        var visibility=new SourceBattlerVisibility(true,Read(row,"hidden")!=0,Read(row,"hidden")!=0);
        bodies[slot]=(mon?doll:body) with{SourceVisibility=visibility};
        var replacement=(mon?body:doll) with{SourceVisibility=visibility};var audio=new Audio();
        var runtime=new SourceBattleAnimation(Script(mon,task),Cue(slot,mon),bodies,doubles,audio,
            battlerReplacements:new Dictionary<int,AnimationBattler>{{slot,replacement}});
        typeof(SourceBattleAnimation).GetMethod("SetBattlerPosition",Private)!.Invoke(runtime,[slot,new PixelPoint(Read(row,"x"),Read(row,"y"))]);
        ((PixelPoint[])typeof(SourceBattleAnimation).GetField("_offsets",Private)!.GetValue(runtime)!)[slot]=new(Read(row,"x2"),Read(row,"y2"));
        ((short[])typeof(SourceBattleAnimation).GetField("_animationArgs",Private)!.GetValue(runtime)!)[0]=(short)(mon?1:0);
        return(runtime,bodies,replacement,audio);
    }
    private static void AssertState(SourceBattleAnimation runtime,JsonElement sample,int slot,AnimationBattler before,AnimationBattler after,Audio audio,bool frame)
    {
        int[] row=sample.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var snapshot=runtime.Snapshot();
        var position=(PixelPoint)typeof(SourceBattleAnimation).GetMethod("BattlerPosition",Private)!.Invoke(runtime,[slot])!;
        var offset=((PixelPoint[])typeof(SourceBattleAnimation).GetField("_offsets",Private)!.GetValue(runtime)!)[slot];
        Assert.AreEqual(new PixelPoint(row[4],row[5]),position);Assert.AreEqual(new PixelPoint(row[6],row[7]),offset);
        Assert.AreEqual(row[8]!=0,snapshot.BattlerVisibility[slot].SpriteInvisible);
        Assert.AreEqual(row[8]!=0,snapshot.BattlerVisibility[slot].LogicalInvisible);
        Assert.IsTrue(snapshot.BattlerVisibility[slot].Present);
        Assert.AreEqual(row[9]==0?before:after,snapshot.Battlers[slot]);Assert.AreEqual(0,audio.Cues.Count);
        if(frame){Assert.AreEqual(row[0],snapshot.Frame);Assert.AreEqual(row[10],runtime.ActiveVisualCount);Assert.AreEqual(row[11]!=0,snapshot.BattlerVisibility[slot].OamInvisible);}
    }
    [TestMethod]
    public void EveryCallbackRegisterAndBothMaterialDirectionsMatchPinnedC()
    {
        using var vectors=Vectors();var cases=vectors.RootElement.GetProperty("cases").EnumerateObject().ToArray();
        Assert.AreEqual(20,cases.Length);
        foreach(var entry in cases)
        {
            var row=entry.Value;int slot=Read(row,"slot");var built=Runtime(row,false);
            var type=typeof(SourceBattleAnimation).GetNestedType("SwapMonSpriteToFromSubstituteTask",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,Private|BindingFlags.Public,null,[slot],null)!;
            foreach(var sample in row.GetProperty("tasks").EnumerateArray())
            {
                type.GetMethod("Step")!.Invoke(task,[built.Runtime]);
                int[] values=sample.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                var data=(short[])type.GetField("_data",Private)!.GetValue(task)!;
                CollectionAssert.AreEqual(values[1..4].Select(v=>(short)v).ToArray(),new[]{data[10],data[11],data[0]});
                Assert.AreEqual(values[10]==0,type.GetProperty("Finished")!.GetValue(task));
                AssertState(built.Runtime,sample,slot,built.Initial[slot],built.Replacement,built.Audio,false);
            }
        }
    }
    [TestMethod]
    public void TableSchedulerUsesOneCreationStepAfterOamAndPreservesHiddenSprites()
    {
        using var vectors=Vectors();
        foreach(var entry in vectors.RootElement.GetProperty("cases").EnumerateObject())
        {
            var row=entry.Value;int slot=Read(row,"slot");var built=Runtime(row,true);
            foreach(var sample in row.GetProperty("frames").EnumerateArray())
            {
                built.Runtime.Tick();AssertState(built.Runtime,sample,slot,built.Initial[slot],built.Replacement,built.Audio,true);
            }
            Assert.IsFalse(built.Runtime.Active);
            Assert.AreEqual(0,built.Runtime.ActiveVisualCount);
            built.Runtime.Cancel();Assert.AreEqual(built.Initial[slot],built.Runtime.Snapshot().Battlers[slot]);
            Assert.AreEqual(default(PixelPoint),built.Runtime.Snapshot().Offsets[slot]);
            Assert.AreEqual(Read(row,"hidden")!=0,built.Runtime.Snapshot().BattlerVisibility[slot].SpriteInvisible);
        }
    }
    [TestMethod]
    public void CancellationInEitherSlideOrTheSwapFrameRestoresOriginalMaterialAndVisibility()
    {
        using var vectors=Vectors();
        foreach(var entry in vectors.RootElement.GetProperty("cases").EnumerateObject())
        foreach(int phase in new[]{0,1,2})
        {
            var row=entry.Value;int slot=Read(row,"slot");var built=Runtime(row,true);
            var phaseFrames=row.GetProperty("frames").EnumerateArray().Where(sample=>sample[1].GetInt32()==phase).Select(sample=>sample[0].GetInt32()).ToArray();
            int stop=phaseFrames.Length==0?-1:phaseFrames[0];
            Assert.IsTrue(phase==0||stop>=0,"An already-offscreen input may leave phase zero on its creation callback.");
            for(int frame=0;frame<=stop;frame++)built.Runtime.Tick();
            built.Runtime.Cancel();var snapshot=built.Runtime.Snapshot();
            Assert.IsFalse(snapshot.Active);Assert.AreEqual(0,built.Runtime.ActiveVisualCount);
            Assert.AreEqual(built.Initial[slot],snapshot.Battlers[slot]);
            Assert.AreEqual(default(PixelPoint),snapshot.Offsets[slot]);
            Assert.AreEqual(Read(row,"hidden")!=0,snapshot.BattlerVisibility[slot].SpriteInvisible);
            Assert.AreEqual(Read(row,"hidden")!=0,snapshot.BattlerVisibility[slot].LogicalInvisible);
            Assert.AreEqual(0,built.Audio.Cues.Count);
        }
    }
    [TestMethod]
    public void BothNamedSpecialProgramsUseSharedResourcesWithoutMoveIdentities()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        foreach(bool mon in new[]{false,true})
        {
            string label=Label(mon);var resolved=resources.ResolveGeneral(label);
            var again=resources.ResolveGeneral(label);Assert.AreNotSame(resolved,again);
            Assert.AreSame(resolved.Assets,again.Assets);Assert.AreSame(resolved.Script.Commands,again.Script.Commands);Assert.AreEqual(0,resolved.Script.MoveId);
            Assert.AreEqual(label,resolved.Script.Label);Assert.AreEqual(2,resolved.Script.Commands.Length);
            CollectionAssert.AreEqual(new[]{Callback,"2",mon?"TRUE":"FALSE"},resolved.Script.Commands[0].Args);
            Assert.AreEqual("end",resolved.Script.Commands[1].Op);
            Assert.IsFalse(resources.Scripts.Values.Any(s=>s.Label==label));
        }
    }
    [TestMethod]
    public void MissingOrForeignMaterialAndWrongSourceOperandsFailClosed()
    {
        var bodies=new[]{new AnimationBattler(new(0),"a",7,0,64,64),new AnimationBattler(new(1),"b",7,0,64,64)};
        var script=Script(true);var cue=Cue(0,true);var audio=new Audio();
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue,bodies,false,audio));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue,bodies,false,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{1,bodies[1]}}));
        foreach(var args in new[]{new[]{Callback},new[]{Callback,"2"},new[]{Callback,"3","TRUE"},new[]{Callback,"2","2"},new[]{Callback,"2","FALSE","0"}})
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[new("createvisualtask",args,1),new("end",[],2)]},cue,bodies,false,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{0,bodies[0]}}));
    }
}
