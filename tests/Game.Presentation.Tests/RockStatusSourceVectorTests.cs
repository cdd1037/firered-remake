using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RockStatusSourceVectorTests
{
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly int[] Moves=[246,317,317,197,50];
    private static readonly string[] Templates=["gAncientPowerRockSpriteTemplate","gRockTombRockSpriteTemplate","gRedXSpriteTemplate","gSpinningSparkleSpriteTemplate"];
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
    {
        private readonly Dictionary<string,int> _indices=root.GetProperty(name).EnumerateArray().Select((e,i)=>(e.GetString()!,i)).ToDictionary(x=>x.Item1,x=>x.Item2);
        public int Read(int[] row,string name)=>row[_indices[name]];
    }
    private sealed record Probe(SourceBattleAnimation Owner,object Callback,int Kind,int[] Key);
    private static Probe Create(JsonElement root,string name)
    {
        int[] key=name.Split(':').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();int kind=key[0];bool doubles=key[1]!=0;
        var config=Numbers(root.GetProperty("configs")[kind][key[4]]);var geometry=root.GetProperty("geometry");
        var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,geometry.GetProperty("yOffset")[i].GetInt32(),geometry.GetProperty("elevation")[i].GetInt32(),64,geometry.GetProperty("heights")[i].GetInt32())).ToArray();
        var script=AnimationCompositionFixture.Script(Moves[kind]);var owner=new SourceBattleAnimation(script with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()},new("rock-status-source",1,script.MoveId,new(key[2]),new(key[3])),battlers,doubles,new Audio());
        var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<5;i++)args[i]=checked((short)config[i]);
        object callback;
        if(kind<4){int selected=config[5]==0?key[2]:key[3];callback=Nested("RockStatusSprite",battlers.Length,battlers[selected].Subpriority-2,script.SpriteRecipes[Templates[kind]],config[5],owner);Invoke(Registry(owner),"Add",callback);}
        else{callback=Nested("GrowAndGrayscaleTask",owner);Tasks(owner).Add(callback);}
        return new(owner,callback,kind,key);
    }
    private static void AssertSprites(Probe p,JsonElement rows,Columns c,string context)
    {
        if(p.Kind==4)
        {
            var row=Numbers(rows.EnumerateArray().Single());int slot=p.Key[3];int S(string n)=>c.Read(row,n);
            Assert.AreEqual(new PixelPoint(S("x2"),S("y2")),Field<PixelPoint[]>(p.Owner,"_offsets")[slot],context+" unchanged foot offset");
            Assert.AreEqual(new SourceAffineMatrix(S("a"),S("b"),S("c"),S("d")),new SourceAffineMatrix(Field<int[]>(p.Owner,"_xScale")[slot],Field<int[]>(p.Owner,"_matrixB")[slot],Field<int[]>(p.Owner,"_matrixC")[slot],Field<int[]>(p.Owner,"_yScale")[slot]),context+" matrix");
            Assert.AreEqual(S("affineMode")==3,Field<bool[]>(p.Owner,"_affineMode")[slot],context+" double size/restoration");Assert.AreEqual(S("objMode")==1,Field<bool[]>(p.Owner,"_blendedObject")[slot],context+" object blend");return;
        }
        var actual=Sprites(p.Owner);Assert.AreEqual(rows.GetArrayLength(),actual.Length,context+" occupants");if(actual.Length==0)return;
        var expected=Numbers(rows.EnumerateArray().Single());int E(string n)=>c.Read(expected,n);var sprite=actual.Single();var frame=Property<SourceVisualSpriteFrame>(sprite,"Frame");
        Assert.AreEqual(E("slot"),frame.Slot,context);Assert.AreEqual(new PixelPoint(E("x"),E("y")),frame.Position,context+" position");Assert.AreEqual(new PixelPoint(E("x2"),E("y2")),frame.Offset,context+" offset");Assert.AreEqual(E("subpriority"),frame.Subpriority,context);Assert.AreEqual(E("invisible")==0,frame.Visible,context+" visibility");Assert.AreEqual(E("tile"),frame.Tile,context+" tile");Assert.AreEqual(E("hflip")!=0,frame.HFlip,context);Assert.AreEqual(E("vflip")!=0,frame.VFlip,context);Assert.IsTrue(Property<bool>(sprite,"Counted"));Assert.IsFalse(frame.Affine);Assert.IsFalse(frame.Blend);
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(E("frameDelay"),frames.DelayCounter,context+" frame delay");Assert.AreEqual(E("frameEnded")!=0,frames.Ended,context+" frame end");Assert.AreEqual(E("frameIndex"),Field<int>(frames,"_index"),context+" frame index");
        if(p.Kind==1)foreach(var pair in new[]{("_speed","data0"),("_timer","data1"),("_yOffset","data2"),("_drop","data3")})Assert.AreEqual(E(pair.Item2),(int)Field<short>(sprite,pair.Item1),context+" "+pair.Item1);
        if(p.Kind==2)foreach(var pair in new[]{("_timer","data0"),("_ticks","data1")})Assert.AreEqual(E(pair.Item2),(int)Field<short>(sprite,pair.Item1),context+" "+pair.Item1);
    }
    [TestMethod]
    public void EveryExtractedSpriteFrameAndTaskPhaseMatchesIncludingWrapAndRestoration()
    {
        using var doc=SourceVectorFixture.Read("rock_status_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_rock_status_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;var sc=new Columns(root,"spriteColumns");var st=new Columns(root,"stateColumns");int cases=0,count=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            var p=Create(root,entry.Name);int previous=-2;
            foreach(var row in entry.Value.EnumerateArray())
            {
                var state=Numbers(row.GetProperty("state"));int S(string n)=>st.Read(state,n);int tick=S("tick");string context=entry.Name+" tick "+tick;
                Assert.AreEqual(++previous,tick,context);if(tick>=0)Invoke(p.Owner,"AdvanceSourceSprites");AssertSprites(p,row.GetProperty("sprites"),sc,context+" before task");if(tick>=0)Invoke(p.Owner,"RunSourceTasks");
                if(p.Kind==4)
                {
                    AssertSprites(p,row.GetProperty("afterTaskSprites"),sc,context+" after task");Assert.AreEqual(S("taskTimer"),(int)Field<short>(p.Callback,"_timer"),context+" lifetime");
                    bool grey=Field<HashSet<int>>(p.Owner,"_greyscale").Contains(p.Key[3]);Assert.AreEqual(S("activeTask")!=0,grey,context+" palette restoration");var palette=Numbers(row.GetProperty("palette"));
                    for(int i=0;i<16;i++){int index=(p.Key[3]+16)*16+i;ushort original=(ushort)(0x8000|((index*73+31)&32767));Assert.AreEqual(palette[i],SourcePaletteMath.Apply(original,null,false,grey)&32767,context+" palette color "+i);}
                }
                Assert.AreEqual(S("visualCount"),p.Owner.ActiveVisualCount,context+" visual ownership");Assert.AreEqual(S("activeTask"),Tasks(p.Owner).Count,context+" task lifetime");Assert.AreEqual(S("liveSprites"),Sprites(p.Owner).Length,context+" sprite lifetime");count++;
            }
            Assert.AreEqual(0,p.Owner.ActiveVisualCount,entry.Name);p.Owner.Cancel();cases++;
        }
        AssertCoverage(root, cases, count);
    }

    private static void AssertCoverage(JsonElement root, int cases, int rows)
    {
        string? profile = root.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full", "Explicit source-vector profile is required.");
        string[] required =
        [
            // Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
            "0:0:0:1:0","0:0:0:1:1","0:0:0:1:2","0:0:0:1:3","0:0:0:1:4","0:0:0:1:5","0:0:0:1:6","0:0:0:1:7","0:0:0:1:8",
            // Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
            "1:0:0:1:0","1:0:0:1:1","1:0:0:1:2","1:0:0:1:3","1:0:0:1:4","1:0:0:1:5","1:0:0:1:6","1:0:0:1:7",
            // Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
            "2:0:0:1:0","2:0:0:1:1","2:0:0:1:2","2:0:0:1:3","2:0:0:1:4","2:0:0:1:5",
            // Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
            "3:0:0:1:0","3:0:0:1:1","3:0:0:1:2","3:0:0:1:3","3:0:0:1:4",
            // Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
            "4:0:0:1:0",
            // Reverse side: mirrored coordinates and palette/affine restoration
            "0:0:1:0:0","0:0:1:0:7","0:0:1:0:8",
            // Reverse side: mirrored coordinates and palette/affine restoration
            "1:0:1:0:6","1:0:1:0:7",
            // Reverse side: mirrored coordinates and palette/affine restoration
            "2:0:1:0:1",
            // Reverse side: mirrored coordinates and palette/affine restoration
            "3:0:1:0:3","3:0:1:0:4",
            // Reverse side: mirrored coordinates and palette/affine restoration
            "4:0:1:0:0",
            // Doubles rear coordinates, picture heights and target palette
            "0:1:2:3:0",
            // Doubles rear coordinates, picture heights and target palette
            "1:1:2:3:0",
            // Doubles rear coordinates, picture heights and target palette
            "2:1:2:3:0",
            // Doubles rear coordinates, picture heights and target palette
            "3:1:2:3:0",
            // Doubles rear coordinates, picture heights and target palette
            "4:1:2:3:0",
            // Doubles rear coordinates, picture heights and target palette
            "0:1:3:2:0",
            // Doubles rear coordinates, picture heights and target palette
            "1:1:3:2:0",
            // Doubles rear coordinates, picture heights and target palette
            "2:1:3:2:0",
            // Doubles rear coordinates, picture heights and target palette
            "3:1:3:2:0",
            // Doubles rear coordinates, picture heights and target palette
            "4:1:3:2:0",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        Assert.AreEqual(profile == "full" ? 580 : 48, cases);
        Assert.AreEqual(profile == "full" ? 24880 : 2270, rows);
        Assert.AreEqual(cases, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
        Assert.AreEqual(rows, root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
