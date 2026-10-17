using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RotationTaskAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourceAnimationScript Source()=>AnimationCompositionFixture.Script(64);
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationCommand Task(bool restore,int duration,int speed,int selector,int mode)=>new("createvisualtask",[restore?"AnimTask_RotateMonToSideAndRestore":"AnimTask_RotateMonSpriteToSide","2",duration.ToString(),speed.ToString(),selector.ToString(),mode.ToString()],1);
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/rotation-task-original-vectors.json")));
    private static void AssertFrame(SourceBattleAnimation runtime,SourceAnimationFrame frame,int slot,int[] v,bool oamDouble,string context)
    {
        Assert.AreEqual(v[1],(int)frame.AnimationArguments[1],context+" args1");var m=frame.BattlerAffine[slot];
        Assert.AreEqual(v[6],m.XScale,context+" A");Assert.AreEqual(v[7],m.MatrixB,context+" B");Assert.AreEqual(v[8],m.MatrixC,context+" C");Assert.AreEqual(v[9],m.YScale,context+" D");
        Assert.AreEqual(new PixelPoint(0,v[10]),frame.Offsets[slot],context+" offset");Assert.AreEqual(oamDouble,m.DoubleSize,context+" OAM double");Assert.AreEqual(v[12],runtime.ActiveVisualCount,context+" lifetime");
        for(int i=0;i<4;i++)if(i!=slot){Assert.AreEqual(default,frame.Offsets[i]);Assert.AreEqual(256,frame.BattlerAffine[i].XScale);Assert.IsFalse(frame.BattlerAffine[i].DoubleSize);}
    }
    [TestMethod]
    [DataRow(false,0)][DataRow(false,1)][DataRow(false,2)][DataRow(false,3)]
    [DataRow(true,0)][DataRow(true,1)][DataRow(true,2)][DataRow(true,3)]
    public void SharedRotationTasksMatchExtractedSignedStateMatricesOffsetsAndArgumentMutation(bool restore,int actor)
    {
        using var vectors=Vectors();var source=Source();var configs=vectors.RootElement.GetProperty("configs").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
        foreach(int target in new[]{actor^1,actor^2})for(int selector=0;selector<2;selector++)for(int config=0;config<configs.Length;config++)
        {
            var c=configs[config];int slot=selector==0?actor:target;string key=$"{(restore?1:0)}:{slot}:{selector}:{config}";
            var script=source with{Commands=[Task(restore,c[0],c[1],selector,c[2]),new("waitforvisualfinish",[],2),new("end",[],3)]};
            var runtime=new SourceBattleAnimation(script,new("rotation",1,64,new(actor),new(target)),Battlers(),true,new Audio());
            // BattleMainCB2 builds OAM before RunTasks. Matrix writes occur in the
            // task, but the displayed size bit is the constructor/previous-task value.
            bool oamDouble=false;
            foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                if(v[0]>=0)AssertFrame(runtime,runtime.Tick(),slot,v,oamDouble,$"{key} tick{v[0]}");
                oamDouble=v[11]!=0;
            }
            Assert.AreEqual(oamDouble,runtime.Tick().BattlerAffine[slot].DoubleSize);Assert.IsFalse(runtime.Active);runtime.Cancel();Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
        }
    }
    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void HeldItemThreeRocksRewriteArgumentsAndRestoreIndependently(int actor)
    {
        using var vectors=Vectors();var script=Source() with{Commands=Enumerable.Range(0,3).SelectMany(_=>new[]{Task(true,16,128,0,2),new SourceAnimationCommand("waitforvisualfinish",[],2)}).Append(new("end",[],3)).ToArray()};
        var runtime=new SourceBattleAnimation(script,new("three-rocks",1,64,new(actor),new(actor^1)),Battlers(),true,new Audio());
        for(int rock=0;rock<3;rock++)
        {
            bool oamDouble=false;
            foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty($"1:{actor}:0:0").EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                if(v[0]>=0)AssertFrame(runtime,runtime.Tick(),actor,v,oamDouble,$"actor{actor} rock{rock} tick{v[0]}");
                oamDouble=v[11]!=0;
            }
        }
        Assert.IsFalse(runtime.Tick().BattlerAffine[actor].DoubleSize);Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
    }
    [TestMethod]
    [DataRow(0,0)][DataRow(1,16)][DataRow(2,31)][DataRow(3,45)]
    public void CancelRestoresRotationAtEitherHalfAndBetweenRocks(int actor,int ticks)
    {
        var script=Source() with{Commands=Enumerable.Range(0,3).SelectMany(_=>new[]{Task(true,16,128,0,2),new SourceAnimationCommand("waitforvisualfinish",[],2)}).Append(new("end",[],3)).ToArray()};
        var runtime=new SourceBattleAnimation(script,new("cancel",1,64,new(actor),new(actor^1)),Battlers(),true,new Audio());
        for(int i=0;i<ticks;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsTrue(frame.BattlerAffine.All(m=>!m.DoubleSize&&m.XScale==256&&m.YScale==256&&m.MatrixB==0&&m.MatrixC==0));
    }
}
