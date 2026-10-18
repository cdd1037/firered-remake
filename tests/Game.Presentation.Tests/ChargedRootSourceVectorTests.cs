using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ChargedRootSourceVectorTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static T Field<T>(object value,string name)=>(T)(value.GetType().GetField(name,Flags)?.GetValue(value)??throw new InvalidOperationException(name));
    private static T Property<T>(object value,string name)=>(T)(value.GetType().GetProperty(name,Flags)?.GetValue(value)??throw new InvalidOperationException(name));
    private static object? Invoke(object value,string name,params object[] args)=>(value.GetType().GetMethod(name,Flags)??throw new InvalidOperationException(name)).Invoke(value,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)??throw new InvalidOperationException(name),Flags,null,args,CultureInfo.InvariantCulture)!;
    private static int[] Numbers(JsonElement row)=>row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    [TestMethod]
    public void EverySelectedOriginalBirdOrbAndDeformFrameMatchesBothNativeOptimizationLevels()
    {
        using var doc=SourceVectorFixture.Read("charged-root-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_charged_root_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;
        CollectionAssert.AreEqual(new[]{"O0","O2"},root.GetProperty("optimizationParity").EnumerateArray().Select(x=>x.GetString()).ToArray());
        string[] required=["bird-player","bird-opponent","bird-player-partner","bird-opponent-partner-reverse","orb-authored-0","orb-authored-240","orb-phase255","orb-duration0","orb-duration1","orb-duration255","orb-hidden-rear","deform-player","deform-opponent","deform-rear-player","deform-rear-opponent","deform-hidden-player","deform-hidden-opponent"];
        foreach(string name in required)Assert.IsTrue(root.GetProperty("cases").TryGetProperty(name,out _),name);
        Assert.AreEqual(41,root.GetProperty("representativeCases").GetArrayLength());int cases=0,frames=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            int[] c=Numbers(entry.Value.GetProperty("config"));int kind=c[0],actor=c[2],target=c[3];bool doubles=c[1]!=0;
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();
            if(c[6]!=0)battlers[actor]=battlers[actor] with{SourceVisibility=new(true,true,true)};
            var source=AnimationCompositionFixture.Script(kind==0?143:255);var owner=new SourceBattleAnimation(source with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()},new("charged-root-source",1,source.MoveId,new(actor),new(target)),battlers,doubles,new Audio());
            var args=Field<short[]>(owner,"_animationArgs");args[0]=(short)c[4];args[1]=(short)c[5];object callback;var registry=Field<object>(owner,"_sprites");var tasks=Field<IList>(owner,"_tasks");
            if(kind<2){callback=Nested("ChargedRootSprite",battlers.Length,battlers[kind==0?target:actor].Subpriority-2,source.SpriteRecipes[kind==0?"gSkyAttackBirdSpriteTemplate":"gSpitUpOrbSpriteTemplate"],owner);Invoke(registry,"Add",callback);}
            else{callback=Nested("TaskAffinePlayback",owner,new AnimationSlot(actor),source.TaskAffinePrograms["sSpitUpDeformMonAffineAnimCmds"]);tasks.Add(callback);}
            int tick=-2;
            foreach(var row in entry.Value.GetProperty("rows").EnumerateArray())
            {
                int t=row[0].GetInt32();Assert.AreEqual(++tick,t);string why=entry.Name+" frame "+t;
                if(t>=0){Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"RunSourceTasks");}
                Assert.AreEqual(row[1].GetInt32(),owner.ActiveVisualCount,why+" visual count");Assert.AreEqual(row[2].GetInt32(),tasks.Count,why+" task lifetime");
                var sprites=((IEnumerable)registry).Cast<object>().ToArray();int[] e=Numbers(row[4]);Assert.AreEqual(e.Length==0?0:1,sprites.Length,why+" exact sprite destruction");
                Assert.AreEqual(((1<<battlers.Length)-1)|(sprites.Length==1?1<<battlers.Length:0),row[3].GetInt32(),why+" native matrix ownership");
                Assert.AreEqual((short)c[4],args[0],why+" phase register");Assert.AreEqual((short)c[5],args[1],why+" duration register");
                if(e.Length!=0)
                {
                    var sprite=sprites.Single();var f=Property<SourceVisualSpriteFrame>(sprite,"Frame");
                    Assert.AreEqual(e[0],f.Slot,why);Assert.AreEqual(new PixelPoint(e[1],e[2]),f.Position,why+" x/y");Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Offset,why+" x2/y2");Assert.AreEqual(e[5]==0,f.Visible,why);Assert.AreEqual(e[6],f.Tile,why);Assert.AreEqual(e[7]!=0,f.HFlip,why);Assert.AreEqual(e[8]!=0,f.VFlip,why);Assert.AreEqual(e[9],f.Subpriority,why);
                    CollectionAssert.AreEqual(e[10..14],new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why+" matrix");Assert.IsTrue(f.Affine);Assert.AreEqual(e[14]==3,f.DoubleSize);Assert.AreEqual(e[15]==1,f.Blend);Assert.AreEqual(e[16],f.OamPriority);
                    var program=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(e[17],Field<int>(program,"_index"),why+" frame PC");Assert.AreEqual(e[18],program.DelayCounter,why);Assert.AreEqual(e[19]!=0,program.Ended,why);CollectionAssert.AreEqual(e[26..34],Field<short[]>(sprite,"_data").Select(v=>(int)v).ToArray(),why+" signed callback words");
                    if(kind==1){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(e[20]!=0,affine.Ended,why);Assert.AreEqual(e[21],Field<int>(affine,"_pc"),why+" affine PC");Assert.AreEqual(e[22],Field<int>(affine,"_delay"),why+" affine delay");CollectionAssert.AreEqual(e[23..26],new[]{affine.XScale,affine.YScale,affine.Rotation},why+" affine values");}
                }
                int[] b=Numbers(row[5]);Assert.AreEqual(b[5]!=0,owner.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible,why+" hidden-present actor");
                if(kind==2)
                {
                    Assert.AreEqual(new PixelPoint(b[3],b[4]),Field<PixelPoint[]>(owner,"_offsets")[actor],why+" task foot offset");
                    CollectionAssert.AreEqual(b[10..14],new[]{Field<int[]>(owner,"_xScale")[actor],Field<int[]>(owner,"_matrixB")[actor],Field<int[]>(owner,"_matrixC")[actor],Field<int[]>(owner,"_yScale")[actor]},why+" direct task matrix");
                    Assert.AreEqual(b[14]==3,Field<bool[]>(owner,"_affineMode")[actor],why+" affine restoration");
                    int[] data=Numbers(row[6]);var affine=Field<SourceTaskAffineProgram>(callback,"_program");CollectionAssert.AreEqual(data[7..13],new[]{affine.CommandIndex,affine.FrameTicks,affine.LoopRemaining,affine.XScale,affine.YScale,affine.Rotation},why+" original task interpreter words");
                }
                frames++;
            }
            Assert.AreEqual(0,owner.ActiveVisualCount);owner.Cancel();cases++;
        }
        Assert.AreEqual(cases,root.GetProperty("coverage").GetProperty("caseCount").GetInt32());Assert.AreEqual(frames,root.GetProperty("coverage").GetProperty("rowCount").GetInt32());Assert.IsTrue(cases>=41&&frames>=1413);
    }
}
