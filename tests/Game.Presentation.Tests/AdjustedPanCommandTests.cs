using System.Globalization;
using System.Text.Json;
using System.Reflection;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class AdjustedPanCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Sounds{get;}=[];public List<(int Pan,int Frame)> Pans{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound sound)=>Sounds.Add(sound);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}}
    private static string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(int start,int target,int step,int delay,string operation="panse_adjustall")=>new(273,"adjusted-pan",[new(operation,["SE_M_REFLECT",N(start),N(target),N(step),N(delay)],1),new("waitsound",[],2),new("end",[],3)]){SoundEffects=new Dictionary<string,int>{{"SE_M_REFLECT",200}}};

    [TestMethod]
    public void ExtractedSignedByteAdjustmentAndProgressingTaskVectorsMatchTheSharedPlayer()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/panse-adjustall-source-vectors.json")));
        var adjust=typeof(SourceBattleAnimation).GetMethod("AdjustPanning2",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(adjust);
        foreach(var row in doc.RootElement.GetProperty("adjustments").EnumerateArray())
        {
            if(row[1].GetInt32()!=0)continue;int actor=row[0].GetInt32();var runtime=new SourceBattleAnimation(new(273,"empty",[new("end",[],1)]),new("pan",1,273,new(actor),new(actor)),Battlers(),true,new Audio());
            int pan=-128;foreach(var expected in row.EnumerateArray().Skip(2))Assert.AreEqual(expected.GetInt32(),(int)adjust.Invoke(runtime,[pan++])!);
        }
        var inputs=doc.RootElement.GetProperty("inputCases");int checkedCases=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            var config=entry.Value.GetProperty("config");int actor=config[0].GetInt32();var input=inputs[config[1].GetInt32()];if(input[5].GetInt32()!=0)continue;
            int start=input[1].GetInt32(),target=input[2].GetInt32(),step=input[3].GetInt32(),delay=input[4].GetInt32();
            int A(int value)=>actor%2==0?value:unchecked((sbyte)-value);int x=A(start),y=A(target),d=A(step);if(d!=0&&(x==y||(x<y)!=(d>0)))continue;
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(start,target,step,delay),new("pan",1,273,new(actor),new(actor)),Battlers(),true,audio);int tick=0,limit=entry.Value.GetProperty("end")[0].GetInt32()+100;
            while(runtime.Active&&tick++<limit)runtime.Tick();Assert.IsFalse(runtime.Active);checkedCases++;
            var plays=entry.Value.GetProperty("plays");Assert.AreEqual(plays.GetArrayLength(),audio.Sounds.Count);for(int i=0;i<audio.Sounds.Count;i++){Assert.AreEqual(plays[i][1].GetInt32(),audio.Sounds[i].Frame);Assert.AreEqual(plays[i][3].GetInt32(),audio.Sounds[i].Pan);}
            var updates=entry.Value.GetProperty("updates");Assert.AreEqual(updates.GetArrayLength(),audio.Pans.Count);for(int i=0;i<audio.Pans.Count;i++){Assert.AreEqual(updates[i][1].GetInt32(),audio.Pans[i].Frame);Assert.AreEqual(updates[i][2].GetInt32(),audio.Pans[i].Pan);}
        }
        Assert.AreEqual(18,checkedCases);
    }

    [TestMethod]
    public void UnadjustedSweepReusesNativeTaskVectorsWithoutActorOrTargetTransformation()
    {
        // Cmd_panse_adjustnone installs the same source task and raw signed-byte
        // arguments. Player-side noncontest adjusted vectors are its identity case.
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/panse-adjustall-source-vectors.json")));
        var inputs=doc.RootElement.GetProperty("inputCases");int configurations=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            var config=entry.Value.GetProperty("config");if(config[0].GetInt32()!=0)continue;
            var input=inputs[config[1].GetInt32()];if(input[5].GetInt32()!=0)continue;
            int start=input[1].GetInt32(),end=input[2].GetInt32(),step=input[3].GetInt32(),delay=input[4].GetInt32();
            if(step!=0&&(start==end||(start<end)!=(step>0)))continue;
            for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)
            {
                var audio=new Audio();var runtime=new SourceBattleAnimation(Script(start,end,step,delay,"panse_adjustnone"),new("raw-pan",1,273,new(actor),new(target)),Battlers(),true,audio);
                int tick=0,limit=entry.Value.GetProperty("end")[0].GetInt32()+100;
                while(runtime.Active&&tick++<limit)runtime.Tick();Assert.IsFalse(runtime.Active);
                Assert.AreEqual(start,audio.Sounds.Single().Pan);Assert.AreEqual(0,audio.Sounds.Single().Frame);
                var updates=entry.Value.GetProperty("updates");Assert.AreEqual(updates.GetArrayLength(),audio.Pans.Count);
                for(int i=0;i<audio.Pans.Count;i++){Assert.AreEqual(updates[i][1].GetInt32(),audio.Pans[i].Frame);Assert.AreEqual(updates[i][2].GetInt32(),audio.Pans[i].Pan);}
            }
            configurations++;
        }
        Assert.IsTrue(configurations>=8);
    }

    [TestMethod]
    public void AuthoredWishSweepDependsOnlyOnActorSideAndPreservesPositiveSixtyFour()
    {
        for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(63,-64,-3,0),new("pan",1,273,new(actor),new(target)),Battlers(),true,audio);int tick=0;
            while(runtime.Active&&tick++<70)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(1,audio.Sounds.Count);
            Assert.AreEqual(actor%2==0?63:-63,audio.Sounds[0].Pan);Assert.AreEqual(0,audio.Sounds[0].Frame);
            var expected=Enumerable.Range(1,43).Select(n=>actor%2==0?Math.Max(-64,63-3*n):Math.Min(64,-63+3*n)).ToArray();
            CollectionAssert.AreEqual(expected,audio.Pans.Select(p=>p.Pan).ToArray());CollectionAssert.AreEqual(Enumerable.Range(0,43).ToArray(),audio.Pans.Select(p=>p.Frame).ToArray());
        }
    }

    [TestMethod]
    public void SignedByteEndpointsZeroStepDelayAndCancellationRetainSourceTaskTiming()
    {
        foreach(var (start,target,step,delay) in new[]{(-128,-120,2,0),(120,127,2,2),(-12,12,0,3),(5,5,0,0)})
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(start,target,step,delay),new("pan",1,273,new(0),new(0)),Battlers(),true,audio);int tick=0;
            while(runtime.Active&&tick++<70)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(start,audio.Sounds[0].Pan);Assert.AreEqual(target,audio.Pans[^1].Pan);
            Assert.AreEqual(delay,audio.Pans[0].Frame);for(int i=1;i<audio.Pans.Count;i++)Assert.AreEqual(delay+1,audio.Pans[i].Frame-audio.Pans[i-1].Frame);
            if(step==0)Assert.AreEqual(1,audio.Pans.Count);
        }
        var pendingAudio=new Audio();var pending=new SourceBattleAnimation(Script(63,-64,-3,5),new("pan",1,273,new(0),new(0)),Battlers(),true,pendingAudio);
        pending.Tick();Assert.AreEqual(0,pendingAudio.Pans.Count);pending.Cancel();Assert.IsFalse(pending.Active);Assert.AreEqual(0,pendingAudio.Pans.Count);
    }

    [TestMethod]
    public void MissingGeneratedSongsSourceWidthAndNonprogressingSweepsFailClosed()
    {
        void Reject(SourceAnimationScript script,int actor=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("pan",1,273,new(actor),new(actor)),Battlers(),true,new Audio()));
        foreach(var values in new[]{(-129,63,3,0),(63,128,3,0),(63,-64,-129,0),(63,-64,-3,256),(63,-64,3,0),(0,1,-1,0),(1,1,1,0)})Reject(Script(values.Item1,values.Item2,values.Item3,values.Item4));
        Reject(Script(63,-64,-3,0) with{SoundEffects=new Dictionary<string,int>()});
        // Negating the signed byte -128 remains -128; do not normalize it to+128.
        Reject(Script(0,-128,-1,0),1);
    }
}
