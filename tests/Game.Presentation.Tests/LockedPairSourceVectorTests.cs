using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class LockedPairSourceVectorTests
{
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static T Field<T>(object o,string name)=>(T)(o.GetType().GetField(name,Instance)?.GetValue(o)??throw new InvalidOperationException(name));
    private static object? Invoke(object o,string name,params object[] args)=>(o.GetType().GetMethod(name,Instance)??throw new InvalidOperationException(name)).Invoke(o,args);
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private sealed class Columns(JsonElement root,string name)
    {private readonly Dictionary<string,int> _indices=root.GetProperty(name).EnumerateArray().Select((e,i)=>(e.GetString()!,i)).ToDictionary(x=>x.Item1,x=>x.Item2);public int Read(int[] row,string name)=>row[_indices[name]];}
    private static void Sprite(SourceBattleAnimation owner,int slot,int[] row,Columns c,string why)
    {
        int E(string n)=>c.Read(row,n);Assert.AreEqual(new PixelPoint(E("x"),E("y")),(PixelPoint)Invoke(owner,"BattlerPosition",slot)!,why+" raw base");
        Assert.AreEqual(new PixelPoint(E("x2"),E("y2")),Field<PixelPoint[]>(owner,"_offsets")[slot],why+" independent pos2");
        Assert.AreEqual(new SourceAffineMatrix(E("a"),E("b"),E("c"),E("d")),new SourceAffineMatrix(Field<int[]>(owner,"_xScale")[slot],Field<int[]>(owner,"_matrixB")[slot],Field<int[]>(owner,"_matrixC")[slot],Field<int[]>(owner,"_yScale")[slot]),why+" matrix");
        Assert.AreEqual(E("affineMode")==3,Field<bool[]>(owner,"_affineMode")[slot],why+" double-size affine mode");var visibility=owner.Snapshot().BattlerVisibility.Single(v=>v.Slot==slot);Assert.IsTrue(visibility.Present,why+" present");Assert.IsFalse(visibility.LogicalInvisible,why+" logical flag");Assert.AreEqual(E("invisible")!=0,visibility.SpriteInvisible,why+" native sprite flag");
    }
    [TestMethod]
    public void NativeThrashTasksPreserveIndependentBasePos2AffineStateSignedWidthsAndOamPhase()
    {
        using var doc=SourceVectorFixture.Read("locked_pair_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_locked_pair_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;var sc=new Columns(root,"spriteColumns");var st=new Columns(root,"stateColumns");int cases=0,rows=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();int kind=key[0],actor=key[2];bool doubles=key[1]!=0;var c=Numbers(root.GetProperty("configs")[key[3]]);var g=root.GetProperty("geometry");
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,g.GetProperty("yOffset")[i].GetInt32(),g.GetProperty("elevation")[i].GetInt32(),64,g.GetProperty("heights")[i].GetInt32())).ToArray();
            var original=battlers[actor].Center(doubles);var anchor=new PixelPoint(c[0]!=0?c[0]:original.X,c[1]!=0?c[1]:original.Y);battlers[actor]=battlers[actor] with{DisplayCenter=anchor,SourceVisibility=new(true,false,c[4]!=0)};
            var script=AnimationCompositionFixture.Script(37) with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
            var owner=new SourceBattleAnimation(script,new("locked-source",1,37,new(actor),new(actor^1)),battlers,doubles,new Audio());Field<PixelPoint[]>(owner,"_offsets")[actor]=new(c[2],c[3]);
            object? horizontal=null,vertical=null;var tasks=Field<IList>(owner,"_tasks");
            if(kind!=1){Assert.AreEqual(true,Invoke(owner,"TryCreateLockedPairTask",new SourceAnimationCommand("createvisualtask",["AnimTask_ThrashMoveMonHorizontal","2"],1)));horizontal=tasks[tasks.Count-1];}
            if(kind!=0){Assert.AreEqual(true,Invoke(owner,"TryCreateLockedPairTask",new SourceAnimationCommand("createvisualtask",["AnimTask_ThrashMoveMonVertical","2"],1)));vertical=tasks[tasks.Count-1];}
            int tick=-2;
            foreach(var row in entry.Value.EnumerateArray())
            {
                var state=Numbers(row.GetProperty("state"));int S(string n)=>st.Read(state,n);Assert.AreEqual(++tick,S("tick"));var before=Numbers(row.GetProperty("beforeTask"));string why=entry.Name+" tick "+tick;Sprite(owner,actor,before,sc,why+" pretask");
                if(tick>=0)
                {
                    var f=owner.Tick();int x=((sc.Read(before,"x")+sc.Read(before,"x2")-32)&511)-anchor.X+32;if(x>255)x-=512;int y=((sc.Read(before,"y")+sc.Read(before,"y2")-32)&255)-anchor.Y+32;
                    Assert.AreEqual(sc.Read(before,"invisible")==0?new PixelPoint(x,y):default,f.ObjectOffsets[actor],why+" latched raw-position OAM geometry");
                }
                Sprite(owner,actor,Numbers(row.GetProperty("afterTask")),sc,why+" posttask");Assert.AreEqual(S("visualCount"),owner.ActiveVisualCount,why+" count");Assert.AreEqual(S("horizontalAlive")+S("verticalAlive"),tasks.Count,why+" lifetime");
                if(horizontal is not null)
                {
                    var p=Field<SourceTaskAffineProgram>(horizontal,"_program");foreach(var (n,value) in new[]{("horizontalData7",p.CommandIndex),("horizontalData8",p.FrameTicks),("horizontalData9",p.LoopRemaining),("horizontalData10",p.XScale),("horizontalData11",p.YScale),("horizontalData12",p.Rotation)})Assert.AreEqual(S(n),value,why+" "+n);
                }
                if(vertical is not null){var data=Field<short[]>(vertical,"_data");for(int i=1;i<16;i++)Assert.AreEqual(S("verticalData"+i),(int)data[i],why+" raw task data "+i);}
                rows++;
            }
            owner.Cancel();Assert.IsTrue(Field<PixelPoint[]>(owner,"_battlerPositionOffsets").All(p=>p==default));cases++;
        }
        AssertCoverage(root, cases, rows);
    }

    private static void AssertCoverage(JsonElement root, int cases, int rows)
    {
        string? profile = root.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full", "Explicit source-vector profile is required.");
        string[] required =
        [
            // Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries
            "0:0:0:0","0:0:0:1","0:0:0:2","0:0:0:3","0:0:0:4",
            // Reverse side and affine foot anchoring
            "0:0:1:0",
            // Doubles hidden rear battler and signed initial offsets
            "0:1:2:4",
            // Doubles reverse side signed base positions
            "0:1:3:3",
            // Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries
            "1:0:0:0","1:0:0:1","1:0:0:2","1:0:0:3","1:0:0:4",
            // Reverse side and affine foot anchoring
            "1:0:1:0",
            // Doubles hidden rear battler and signed initial offsets
            "1:1:2:4",
            // Doubles reverse side signed base positions
            "1:1:3:3",
            // Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries
            "2:0:0:0","2:0:0:1","2:0:0:2","2:0:0:3","2:0:0:4",
            // Reverse side and affine foot anchoring
            "2:0:1:0",
            // Doubles hidden rear battler and signed initial offsets
            "2:1:2:4",
            // Doubles reverse side signed base positions
            "2:1:3:3",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        Assert.AreEqual(profile == "full" ? 90 : 24, cases);
        Assert.AreEqual(profile == "full" ? 7890 : 2104, rows);
        Assert.AreEqual(cases, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
        Assert.AreEqual(rows, root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
