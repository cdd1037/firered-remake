using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SubstituteTransitionAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,7,0,32,32)).ToArray();
    private static AnimationBattler Doll(AnimationBattler body,bool doubles)
    {var p=body.Base(doubles);return body with{Asset="substitute",DisplayCenter=new(p.X,p.Y+(body.Slot.IsPlayer?17:16))};}
    private static SourceAnimationScript Script(bool task)=>new(164,"Move_SUBSTITUTE",task
        ?[new("createvisualtask",["AnimTask_MonToSubstitute","2"],1),new("end",[],2)]
        :[new("end",[],1)]);
    private static CommittedMoveAnimation Cue(int slot)=>new("substitute",1,164,new(slot),new(slot));
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("substitute-transition-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_substitute_transition_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/substitute-transition-vectors.json");
    [TestMethod]
    public void EveryOriginalCallbackRegisterAndMaterialSwapMatchesIndependentC()
    {
        using var vectors=Vectors();
        foreach(bool doubles in new[]{false,true})for(int slot=0;slot<(doubles?4:2);slot++)
        {
            var bodies=Bodies(doubles);var doll=Doll(bodies[slot],doubles);var audio=new Audio();
            var runtime=new SourceBattleAnimation(Script(false),Cue(slot),bodies,doubles,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{slot,doll}});
            var type=typeof(SourceBattleAnimation).GetNestedType("MonToSubstituteTask",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,Private|BindingFlags.Public,null,[slot],null)!;var step=type.GetMethod("Step")!;
            foreach(var item in vectors.RootElement.GetProperty("tasks").GetProperty($"{(doubles?1:0)}:{slot}").EnumerateArray())
            {
                int[] row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();step.Invoke(task,[runtime]);var frame=runtime.Snapshot();
                var d=(short[])type.GetField("_data",Private)!.GetValue(task)!;
                Assert.AreEqual(row[1]!=0,type.GetField("_doll",Private)!.GetValue(task));
                CollectionAssert.AreEqual(row[2..7].Select(v=>(short)v).ToArray(),new[]{d[0],d[1],d[2],d[3],d[10]});
                Assert.AreEqual(new PixelPoint(row[8],row[9]),frame.Offsets[slot]);
                Assert.AreEqual(row[10]!=0,frame.BattlerVisibility[slot].SpriteInvisible);
                Assert.AreEqual(row[11]!=0,((bool[])typeof(SourceBattleAnimation).GetField("_affineMode",Private)!.GetValue(runtime)!)[slot]);
                Assert.AreEqual(row[12],frame.BattlerAffine[slot].XScale);Assert.AreEqual(row[13],frame.BattlerAffine[slot].YScale);
                Assert.AreEqual(row[14]==0?bodies[slot]:doll,frame.Battlers[slot]);Assert.AreEqual(row[15],audio.Cues.Count);
                Assert.AreEqual(row[17]==0,type.GetProperty("Finished")!.GetValue(task));
                if(audio.Cues.Count!=0)Assert.AreEqual(row[16],audio.Cues[^1].Pan);
            }
            Assert.AreEqual(2,audio.Cues.Count);Assert.IsTrue(audio.Cues.All(c=>c.Id=="SE_M_BUBBLE2"));
        }
    }
    [TestMethod]
    public void ActualFrameSchedulerPreservesHiddenLoadBuiltOamDelayAndTwoLandingSounds()
    {
        using var vectors=Vectors();
        foreach(bool doubles in new[]{false,true})for(int slot=0;slot<(doubles?4:2);slot++)
        {
            var bodies=Bodies(doubles);var doll=Doll(bodies[slot],doubles);var audio=new Audio();
            var runtime=new SourceBattleAnimation(Script(true),Cue(slot),bodies,doubles,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{slot,doll}});
            foreach(var item in vectors.RootElement.GetProperty("frames").GetProperty($"{(doubles?1:0)}:{slot}").EnumerateArray())
            {
                int[] row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var frame=runtime.Tick();var visible=frame.BattlerVisibility[slot];
                Assert.AreEqual(row[0],frame.Frame);Assert.AreEqual(new PixelPoint(row[8],row[9]),frame.Offsets[slot]);
                Assert.AreEqual(row[10]!=0,visible.SpriteInvisible);Assert.AreEqual(row[20]!=0,visible.OamInvisible);
                Assert.AreEqual(row[21]!=0,frame.BattlerAffine[slot].DoubleSize);
                Assert.AreEqual(row[12],frame.BattlerAffine[slot].XScale);Assert.AreEqual(row[13],frame.BattlerAffine[slot].YScale);
                Assert.AreEqual(row[14]==0?bodies[slot]:doll,frame.Battlers[slot]);Assert.AreEqual(row[15],audio.Cues.Count);
                Assert.AreEqual(row[17],runtime.ActiveVisualCount);
            }
            CollectionAssert.AreEqual(new[]{41,66},audio.Cues.Select(c=>c.Frame).ToArray());
            Assert.IsFalse(runtime.Tick().Active);Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[slot]);
            Assert.IsFalse(runtime.Snapshot().BattlerVisibility[slot].OamInvisible);
            Assert.AreEqual(doll,runtime.Snapshot().Battlers[slot]);
            runtime.Cancel();Assert.AreEqual(bodies[slot],runtime.Snapshot().Battlers[slot]);
        }
    }
    [TestMethod]
    public void MissingOrForeignReplacementAndBadTaskOperandsFailClosed()
    {
        var bodies=Bodies(false);var script=Script(true);var cue=Cue(0);var audio=new Audio();
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue,bodies,false,audio));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue,bodies,false,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{1,Doll(bodies[1],false)}}));
        foreach(var args in new[]{new[]{"AnimTask_MonToSubstitute"},new[]{"AnimTask_MonToSubstitute","3"},new[]{"AnimTask_MonToSubstitute","2","0"}})
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[new("createvisualtask",args,1),new("end",[],2)]},cue,bodies,false,audio,battlerReplacements:new Dictionary<int,AnimationBattler>{{0,Doll(bodies[0],false)}}));
    }
}
