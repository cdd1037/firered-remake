using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OrbDefenseSourceVectorTests
{
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gStockpileAbsorptionOrbSpriteTemplate","gSwallowBlueOrbSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static T Field<T>(object value,string name)=>(T)(value.GetType().GetField(name,Instance)?.GetValue(value)??throw new InvalidOperationException("Missing field "+name));
    private static T Property<T>(object value,string name)=>(T)(value.GetType().GetProperty(name,Instance)?.GetValue(value)??throw new InvalidOperationException("Missing property "+name));
    private static object? Invoke(object value,string name,params object[] args)=>(value.GetType().GetMethod(name,Instance)??throw new InvalidOperationException("Missing method "+name)).Invoke(value,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)??throw new InvalidOperationException(name),Instance,null,args,CultureInfo.InvariantCulture)??throw new InvalidOperationException(name);
    private static IList Tasks(SourceBattleAnimation owner)=>Field<IList>(owner,"_tasks");
    private static object Registry(SourceBattleAnimation owner)=>Field<object>(owner,"_sprites");
    private static object[] Sprites(SourceBattleAnimation owner)=>((IEnumerable)Registry(owner)).Cast<object>().ToArray();
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private sealed class Columns(JsonElement root,string name)
    {private readonly Dictionary<string,int> _indices=root.GetProperty(name).EnumerateArray().Select((e,i)=>(e.GetString()!,i)).ToDictionary(x=>x.Item1,x=>x.Item2);public int Read(int[] row,string name)=>row[_indices[name]];public int Read(JsonElement row,string name)=>row[_indices[name]].GetInt32();}
    private sealed record Probe(SourceBattleAnimation Owner,object Callback,int Kind,int Actor,int Slots);
    private static Probe Create(JsonElement root,string name)
    {
        int[] key=name.Split(':').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();int kind=key[0];bool doubles=key[1]!=0;
        var config=root.GetProperty("configs")[kind][key[4]];var geometry=root.GetProperty("conventions").GetProperty("geometry");
        var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,geometry.GetProperty("yOffset")[i].GetInt32(),geometry.GetProperty("elevation")[i].GetInt32(),64,geometry.GetProperty("heights")[i].GetInt32())).ToArray();
        if(kind>=2&&config[3].GetInt32()!=0)battlers[key[2]]=battlers[key[2]] with{Visible=false};
        var source=AnimationCompositionFixture.Script(kind%2==0?254:256);
        var owner=new SourceBattleAnimation(source with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()},new("orb-defense-source",1,source.MoveId,new(key[2]),new(key[3])),battlers,doubles,new Audio());
        var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<3;i++)args[i]=checked((short)config[i].GetInt32());object callback;
        if(kind<2){callback=Nested("OrbDefenseSprite",battlers.Length,battlers[key[2]].Subpriority-2,source.SpriteRecipes[Templates[kind]],owner);Invoke(Registry(owner),"Add",callback);}
        else{string program=kind==2?"sStockpileDeformMonAffineAnimCmds":"sSwallowDeformMonAffineAnimCmds";callback=Nested("TaskAffinePlayback",owner,new AnimationSlot(key[2]),source.TaskAffinePrograms[program]);Tasks(owner).Add(callback);}
        return new(owner,callback,kind,key[2],battlers.Length);
    }
    private static void AssertSprites(Probe p,JsonElement rows,Columns c,string context)
    {
        if(p.Kind>=2)
        {
            var row=Numbers(rows.EnumerateArray().Single());int slot=p.Actor;int S(string n)=>c.Read(row,n);
            Assert.AreEqual(new PixelPoint(S("x2"),S("y2")),Field<PixelPoint[]>(p.Owner,"_offsets")[slot],context+" foot offset");
            Assert.AreEqual(new SourceAffineMatrix(S("a"),S("b"),S("c"),S("d")),new SourceAffineMatrix(Field<int[]>(p.Owner,"_xScale")[slot],Field<int[]>(p.Owner,"_matrixB")[slot],Field<int[]>(p.Owner,"_matrixC")[slot],Field<int[]>(p.Owner,"_yScale")[slot]),context+" matrix");
            Assert.AreEqual(S("affineMode")==3,Field<bool[]>(p.Owner,"_affineMode")[slot],context+" restoration");Assert.AreEqual(S("invisible")==0,p.Owner.Snapshot().Battlers[slot].Visible,context+" hidden-present preservation");return;
        }
        var actual=Sprites(p.Owner);Assert.AreEqual(rows.GetArrayLength(),actual.Length,context+" occupants");if(actual.Length==0)return;
        var expected=Numbers(rows.EnumerateArray().Single());int E(string n)=>c.Read(expected,n);var sprite=actual.Single();var frame=Property<SourceVisualSpriteFrame>(sprite,"Frame");
        Assert.AreEqual(E("slot"),frame.Slot,context);Assert.AreEqual(new PixelPoint(E("x"),E("y")),frame.Position,context+" position");Assert.AreEqual(new PixelPoint(E("x2"),E("y2")),frame.Offset,context+" offset");Assert.AreEqual(E("subpriority"),frame.Subpriority,context);Assert.AreEqual(E("invisible")==0,frame.Visible,context);Assert.AreEqual(E("tile"),frame.Tile,context);Assert.AreEqual(E("hflip")!=0,frame.HFlip,context);Assert.AreEqual(E("vflip")!=0,frame.VFlip,context);Assert.IsTrue(Property<bool>(sprite,"Counted"));Assert.AreEqual((E("affineMode")&1)!=0,frame.Affine,context);Assert.AreEqual(E("affineMode")==3,frame.DoubleSize,context);Assert.AreEqual(E("objMode")==1,frame.Blend,context);Assert.AreEqual(E("priority"),frame.OamPriority,context);
        Assert.AreEqual(new SourceAffineMatrix(E("a"),E("b"),E("c"),E("d")),new SourceAffineMatrix(frame.MatrixA,frame.MatrixB,frame.MatrixC,frame.MatrixD),context+" sprite affine");
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(E("frameDelay"),frames.DelayCounter,context);Assert.AreEqual(E("frameEnded")!=0,frames.Ended,context);Assert.AreEqual(E("frameIndex"),Field<int>(frames,"_index"),context);
        if(frame.Affine){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(E("affineEnded")!=0,affine.Ended,context);Assert.AreEqual(E("scaleX"),affine.XScale,context);Assert.AreEqual(E("scaleY"),affine.YScale,context);Assert.AreEqual(E("rotation"),affine.Rotation,context);}
    }
    [TestMethod]
    public void EveryOriginalAbsorptionSwallowAndDeformationFrameMatchesIncludingHiddenActors()
    {
        using var doc=SourceVectorFixture.Read("orb_defense_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_orb_defense_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;var sc=new Columns(root,"spriteColumns");var st=new Columns(root,"stateColumns");int cases=0,count=0;
        foreach(var entry in root.GetProperty("caseTrajectories").EnumerateObject())
        {
            var p=Create(root,entry.Name);int previous=-2;
            foreach(var row in root.GetProperty("trajectories")[entry.Value.GetInt32()].EnumerateArray())
            {
                var state=row[0];int S(string n)=>st.Read(state,n);int tick=S("tick");string context=entry.Name+" tick "+tick;
                Assert.AreEqual(++previous,tick,context);if(tick>=0)Invoke(p.Owner,"AdvanceSourceSprites");AssertSprites(p,row[1],sc,context+" before task");if(tick>=0)Invoke(p.Owner,"RunSourceTasks");
                if(p.Kind>=2)
                {
                    AssertSprites(p,row[2],sc,context+" after task");var program=Field<SourceTaskAffineProgram>(p.Callback,"_program");
                    CollectionAssert.AreEqual(new[]{S("taskData7"),S("taskData8"),S("taskData9"),S("taskData10"),S("taskData11"),S("taskData12")},new[]{program.CommandIndex,program.FrameTicks,program.LoopRemaining,program.XScale,program.YScale,program.Rotation},context+" task interpreter");
                }
                Assert.AreEqual(S("visualCount"),p.Owner.ActiveVisualCount,context);Assert.AreEqual(S("activeTask"),Tasks(p.Owner).Count,context);Assert.AreEqual(S("liveSprites"),Sprites(p.Owner).Length,context);count++;
            }
            Assert.AreEqual(0,p.Owner.ActiveVisualCount,entry.Name);p.Owner.Cancel();cases++;
        }
        Assert.AreEqual(440,cases);Assert.AreEqual(18210,count);Assert.AreEqual(cases,root.GetProperty("coverage").GetProperty("caseCount").GetInt32());Assert.AreEqual(count,root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
