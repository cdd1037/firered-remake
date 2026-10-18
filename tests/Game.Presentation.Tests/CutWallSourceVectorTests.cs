using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CutWallSourceVectorTests
{
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gTwisterLeafSpriteTemplate","gTwisterRockSpriteTemplate","gBrickBreakWallSpriteTemplate","gBrickBreakWallShardSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static T Field<T>(object o,string name)=>(T)(o.GetType().GetField(name,Instance)?.GetValue(o)??throw new InvalidOperationException(name));
    private static T Property<T>(object o,string name)=>(T)(o.GetType().GetProperty(name,Instance)?.GetValue(o)??throw new InvalidOperationException(name));
    private static object? Invoke(object o,string name,params object[] args)=>(o.GetType().GetMethod(name,Instance)??throw new InvalidOperationException(name)).Invoke(o,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)??throw new InvalidOperationException(name),Instance,null,args,CultureInfo.InvariantCulture)??throw new InvalidOperationException(name);
    private static IList Tasks(SourceBattleAnimation o)=>Field<IList>(o,"_tasks");
    private static object Registry(SourceBattleAnimation o)=>Field<object>(o,"_sprites");
    private static object[] Sprites(SourceBattleAnimation o)=>((IEnumerable)Registry(o)).Cast<object>().ToArray();
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private sealed class Columns(JsonElement root,string name)
    {private readonly Dictionary<string,int> _indices=root.GetProperty(name).EnumerateArray().Select((e,i)=>(e.GetString()!,i)).ToDictionary(x=>x.Item1,x=>x.Item2);public int Read(int[] row,string name)=>row[_indices[name]];}
    private sealed record Probe(SourceBattleAnimation Owner,object? Callback,int Kind,int[] Key);
    private static SourceAnimationScript Empty(SourceAnimationScript s)=>s with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Probe Create(JsonElement root,string name)
    {
        int[] key=name.Split(':').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();int kind=key[0];bool doubles=key[1]!=0;var c=Numbers(root.GetProperty("configs")[kind][key[4]]);var g=root.GetProperty("geometry");
        var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,g.GetProperty("yOffset")[i].GetInt32(),g.GetProperty("elevation")[i].GetInt32(),64,g.GetProperty("heights")[i].GetInt32())).ToArray();
        int selected=kind==4?c[0] switch{0=>key[2],1=>key[3],2=>key[2]^2,_=>key[3]^2}:key[3]^2;
        if(c[6]!=0&&selected<battlers.Length)battlers[selected]=battlers[selected] with{Visible=false};
        var script=Empty(AnimationCompositionFixture.Script(kind is 2 or 3?280:239));var owner=new SourceBattleAnimation(script,new("cut-wall-source",1,script.MoveId,new(key[2]),new(key[3])),battlers,doubles,new Audio());
        var a=Field<short[]>(owner,"_animationArgs");for(int i=0;i<5;i++)a[i]=checked((short)c[i]);object? callback=null;
        if(kind<4)
        {int creation=c[5]==0?key[2]:key[3];callback=Nested("CutWallSprite",battlers.Length,battlers[creation].Subpriority-2,script.SpriteRecipes[Templates[kind]],c[5],owner);Invoke(Registry(owner),"Add",callback);}
        else
        {
            var offsets=Field<PixelPoint[]>(owner,"_offsets");for(int i=0;i<battlers.Length;i++)offsets[i]=new(3,-5);
            if(Invoke(owner,"ResolveInPlaceShakeParticipant",c[0]) is AnimationSlot slot)
            {var p=offsets[slot.Value];offsets[slot.Value]=new(unchecked((short)(p.X+c[1])),unchecked((short)(p.Y+c[2])));callback=Nested("InPlaceShake",slot,c[1],c[2],c[3],c[4]);Invoke(callback,"Step",owner);if(!Property<bool>(callback,"Finished"))Tasks(owner).Add(callback);}
        }
        return new(owner,callback,kind,key);
    }
    private static void AssertSprites(Probe p,JsonElement expected,Columns columns,string context)
    {
        if(p.Kind==4)
        {
            foreach(var row in expected.EnumerateArray())
            {var values=Numbers(row);int S(string n)=>columns.Read(values,n);int slot=S("slot");Assert.AreEqual(new PixelPoint(S("x2"),S("y2")),Field<PixelPoint[]>(p.Owner,"_offsets")[slot],context+" offset "+slot);Assert.AreEqual(S("invisible")==0,p.Owner.Snapshot().Battlers[slot].Visible,context+" hidden participant "+slot);}
            return;
        }
        var sprites=Sprites(p.Owner);Assert.AreEqual(expected.GetArrayLength(),sprites.Length,context+" sprite count");if(sprites.Length==0)return;
        var numbers=Numbers(expected.EnumerateArray().Single());int E(string n)=>columns.Read(numbers,n);var sprite=sprites.Single();var f=Property<SourceVisualSpriteFrame>(sprite,"Frame");
        Assert.AreEqual(E("slot"),f.Slot,context);Assert.AreEqual(new PixelPoint(E("x"),E("y")),f.Position,context+" position");Assert.AreEqual(new PixelPoint(E("x2"),E("y2")),f.Offset,context+" offset");Assert.AreEqual(E("invisible")==0,f.Visible,context);Assert.AreEqual(E("tile"),f.Tile,context+" tile");Assert.AreEqual(E("hflip")!=0,f.HFlip,context);Assert.AreEqual(E("vflip")!=0,f.VFlip,context);Assert.AreEqual(E("subpriority"),f.Subpriority,context);Assert.AreEqual(E("priority"),f.OamPriority,context+" priority");Assert.AreEqual(E("objMode")==1,f.Blend,context);Assert.IsFalse(f.Affine);Assert.IsTrue(Property<bool>(sprite,"Counted"));
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(E("frameIndex"),Field<int>(frames,"_index"),context+" frame index");Assert.AreEqual(E("frameDelay"),frames.DelayCounter,context+" frame delay");Assert.AreEqual(E("frameEnded")!=0,frames.Ended,context+" frame end");
        var data=Field<short[]>(sprite,"_data");for(int i=0;i<8;i++)Assert.AreEqual(E("data"+i),(int)data[i],context+" callback register "+i);
    }
    [TestMethod]
    public void EveryOriginalCallbackFrameMatchesGeometryPrioritiesSignedWidthsAndPartnerPresence()
    {
        using var doc=SourceVectorFixture.Read("cut_wall_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_cut_wall_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;var sc=new Columns(root,"spriteColumns");var st=new Columns(root,"stateColumns");int cases=0,count=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            var p=Create(root,entry.Name);int previous=-2;
            foreach(var row in entry.Value.EnumerateArray())
            {
                var state=Numbers(row.GetProperty("state"));int S(string n)=>st.Read(state,n);int tick=S("tick");string context=entry.Name+" tick "+tick;Assert.AreEqual(++previous,tick,context);
                if(tick>=0)Invoke(p.Owner,"AdvanceSourceSprites");AssertSprites(p,row.GetProperty("sprites"),sc,context+" pretask");if(tick>=0)Invoke(p.Owner,"RunSourceTasks");
                if(p.Kind==4)
                {AssertSprites(p,row.GetProperty("afterTaskSprites"),sc,context+" posttask");if(p.Callback is {} task){Assert.AreEqual(S("taskData1"),Field<int>(task,"_count"),context+" count");Assert.AreEqual(S("taskData3"),Field<int>(task,"_timer"),context+" delay");}}
                Assert.AreEqual(S("visualCount"),p.Owner.ActiveVisualCount,context+" source ownership");Assert.AreEqual(S("activeTask"),Tasks(p.Owner).Count,context+" task lifetime");Assert.AreEqual(S("liveSprites"),Sprites(p.Owner).Length,context+" sprite lifetime");count++;
            }
            Assert.AreEqual(0,p.Owner.ActiveVisualCount,entry.Name);p.Owner.Cancel();cases++;
        }
        AssertCoverage(root, cases, count);
    }
    [TestMethod]
    public void BothImmediateFuryPredicatesMatchAllSourceBytesWithoutInventingCounterAdmission()
    {
        using var doc=SourceVectorFixture.Read("cut_wall_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_cut_wall_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var script=Empty(AnimationCompositionFixture.Script(210));int count=0;
        foreach(var row in doc.RootElement.GetProperty("furyPredicates").EnumerateArray())
        {
            byte counter=row[0].GetByte();var owner=new SourceBattleAnimation(script,new("fury-source",1,210,new(0),new(1)){FuryCutterCounter=counter},[new(new(0),"b0",0,0,64,64),new(new(1),"b1",0,0,64,64)],false,new Audio());
            foreach(var (callback,index) in new[]{("AnimTask_IsFuryCutterHitRight",1),("AnimTask_GetFuryCutterHitCount",2)})
            {Assert.AreEqual(true,Invoke(owner,"TryCreateCutWallTask",new SourceAnimationCommand("createvisualtask",[callback,"2"],1)));Assert.AreEqual(row[index].GetInt32(),(int)Field<short[]>(owner,"_animationArgs")[7]);Assert.AreEqual(0,owner.ActiveVisualCount);}
            owner.Cancel();count++;
        }
        Assert.AreEqual(256,count);
    }

    private static void AssertCoverage(JsonElement root, int cases, int rows)
    {
        string? profile = root.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full", "Explicit source-vector profile is required.");
        string[] required =
        [
            // Twister: nominal, zero/positive/0xff rise, acceleration threshold and signed wrap
            "0:0:0:1:0","0:0:0:1:9","0:0:0:1:10","0:0:0:1:12","0:0:0:1:13","0:0:0:1:14","0:0:0:1:15",
            // Twister: opposite source side
            "0:0:1:0:0",
            // Twister: partner-average geometry, hidden partner and priority sides
            "0:1:2:3:0","0:1:2:3:14","0:1:2:3:15",
            // Twister: partner-average geometry, hidden partner and priority sides
            "0:1:3:2:0","0:1:3:2:14","0:1:3:2:15",
            // Twister: nominal, zero/positive/0xff rise, acceleration threshold and signed wrap
            "1:0:0:1:0","1:0:0:1:9","1:0:0:1:10","1:0:0:1:12","1:0:0:1:13","1:0:0:1:14","1:0:0:1:15",
            // Twister: opposite source side
            "1:0:1:0:0",
            // Twister: partner-average geometry, hidden partner and priority sides
            "1:1:2:3:0","1:1:2:3:14","1:1:2:3:15",
            // Twister: partner-average geometry, hidden partner and priority sides
            "1:1:3:2:0","1:1:3:2:14","1:1:3:2:15",
            // Brick wall: hold/retirement, shake/no-shake and signed offsets
            "2:0:0:1:0","2:0:0:1:1","2:0:0:1:2","2:0:0:1:3","2:0:0:1:4",
            // Brick wall: reverse side
            "2:0:1:0:0",
            // Brick wall: doubles target offsets
            "2:1:2:3:2",
            // Brick shards: all four tile/direction branches, both origins and signed extremes
            "3:0:0:1:0","3:0:0:1:1","3:0:0:1:2","3:0:0:1:3","3:0:0:1:4","3:0:0:1:5","3:0:0:1:6","3:0:0:1:7","3:0:0:1:8","3:0:0:1:9",
            // Brick shards: reverse side
            "3:0:1:0:0",
            // Brick shards: doubles target origin
            "3:1:3:2:7",
            // Shake: both battlers, odd/even terminal corrections, delay zero/one and signed-width edges
            "4:0:0:1:0","4:0:0:1:1","4:0:0:1:2","4:0:0:1:3","4:0:0:1:4","4:0:0:1:8","4:0:0:1:9","4:0:0:1:10","4:0:0:1:11","4:0:0:1:12","4:0:0:1:16","4:0:0:1:20","4:0:0:1:24","4:0:0:1:28",
            // Shake: both partner selectors present/hidden, odd/even and signed-width edges
            "4:1:2:3:16","4:1:2:3:17","4:1:2:3:18","4:1:2:3:19","4:1:2:3:20","4:1:2:3:24","4:1:2:3:25","4:1:2:3:26","4:1:2:3:27","4:1:2:3:28",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        Assert.AreEqual(profile == "full" ? 1580 : 71, cases);
        Assert.AreEqual(profile == "full" ? 66092 : 2243, rows);
        Assert.AreEqual(cases, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
        Assert.AreEqual(rows, root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
