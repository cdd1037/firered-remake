using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class TransformMosaicAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void MoveModeZeroUsesTheSameOriginalMosaicCadenceAndSingleMaterialSwap()
    {
        using var doc=SourceVectorFixture.Read("castform-transition-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_castform_transition_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/castform-transition-vectors.json");
        for(int slot=0;slot<4;slot++)
        {
            var before=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"before-"+i,7,0,32,32)).ToArray();
            var after=before[slot] with{Asset="after",YOffset=12,Elevation=8};
            var script=new SourceAnimationScript(144,"Move_TRANSFORM",[new("end",[],1)]);var cue=new CommittedMoveAnimation("transform",1,144,new(slot),new(slot^1));
            var runtime=new SourceBattleAnimation(script,cue,before,true,new Audio(),battlerReplacements:new Dictionary<int,AnimationBattler>{{slot,after}});
            var type=typeof(SourceBattleAnimation).GetNestedType("FormChangeMosaicTask",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[slot],null)!;var step=type.GetMethod("Step")!;
            foreach(var item in doc.RootElement.GetProperty("tasks").GetProperty(slot+":0").EnumerateArray())
            {
                var row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();step.Invoke(task,[runtime]);var frame=runtime.Snapshot();
                Assert.AreEqual(row[4]&15,frame.BackgroundMosaicHorizontal);Assert.AreEqual(row[4]>>4,frame.BackgroundMosaicVertical);
                CollectionAssert.AreEqual(new[]{row[5]!=0,row[6]!=0},frame.BattlerBackgroundMosaic);
                Assert.AreEqual(row[7]==0?before[slot]:after,frame.Battlers[slot]);
                Assert.AreEqual(row[9]==0,type.GetProperty("Finished")!.GetValue(task));
            }
        }
    }
}
