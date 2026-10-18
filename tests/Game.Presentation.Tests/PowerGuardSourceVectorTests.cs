using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PowerGuardSourceVectorTests
{
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly int[] Moves=[175,175,175,175,175];
    private static readonly string[] Templates=["gReversalOrbSpriteTemplate","gProtectSpriteTemplate","gShadowBallSpriteTemplate","gRandomPosHitSplatSpriteTemplate"];
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
    {private readonly Dictionary<string,int> _indices=root.GetProperty(name).EnumerateArray().Select((e,i)=>(e.GetString()!,i)).ToDictionary(x=>x.Item1,x=>x.Item2);public int Read(int[] row,string name)=>row[_indices[name]];public long Read(JsonElement row,string name)=>row[_indices[name]].GetInt64();}
    private sealed record Probe(SourceBattleAnimation Owner,object Callback,int Kind,int[] Key,int Selected,uint Seed,int Draws);
    private static Probe Create(JsonElement root,string name)
    {
        int[] key=name.Split(':').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();int kind=key[0];bool doubles=key[1]!=0;
        var config=root.GetProperty("configs")[kind][key[4]];var geometry=root.GetProperty("geometry");uint seed=config[4].GetUInt32();
        var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,geometry.GetProperty("yOffset")[i].GetInt32(),geometry.GetProperty("elevation")[i].GetInt32(),64,geometry.GetProperty("heights")[i].GetInt32())).ToArray();
        int selected=config[kind==4?0:3].GetInt32()==0?key[2]:key[3];if(kind==4&&config[1].GetInt32()!=0)battlers[selected]=battlers[selected] with{Visible=false};
        var script=AnimationCompositionFixture.Script(Moves[kind]);
        // Probe recipes are inputs transcribed from the pinned template/affine arrays,
        // compared against original C output below. They do not publish artwork roots.
        var recipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);
        recipes[Templates[0]]=new(Templates[0],"blue-orb","AnimReversalOrb",false,[[new(0,1,false,false)]]){Width=8,Height=8,LoopStarts=[0]};
        recipes[Templates[1]]=new(Templates[1],"protect","AnimProtect",true,[[new(0,1,false,false)]]){Width=64,Height=64,LoopStarts=[0]};
        recipes[Templates[2]]=new(Templates[2],"shadow-ball","AnimShadowBall",false,[[new(0,1,false,false)]]){Width=32,Height=32,LoopStarts=[0],AffineAnimations=[[new("frame",0,0,10,1,0),new("jump",0,0,0,0,0)]]};
        var tags=script.SpriteTags.ToDictionary(p=>p.Key,p=>p.Value);tags["ANIM_TAG_PROTECT"]=60000; // Opaque tag; the probe observes the source-owned palette slot.
        script=script with{SpriteRecipes=recipes,SpriteTags=tags};if(kind==1)script=script with{SpritePalettes=new Dictionary<int,ushort[]>{{script.SpriteTags["ANIM_TAG_PROTECT"],Enumerable.Range(320,16).Select(i=>(ushort)((i*73+31)&32767)).ToArray()}}};
        var owner=new SourceBattleAnimation(script with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()},new("power-guard-source",1,script.MoveId,new(key[2]),new(key[3])){MovePower=80,VisualSeed=seed},battlers,doubles,new Audio());
        var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<3;i++)args[i]=checked((short)config[i].GetInt32());
        object callback;
        if(kind<4){if(kind==1)Invoke(owner,"LoadSpriteGraphics","ANIM_TAG_PROTECT");callback=Nested("PowerGuardSprite",battlers.Length,battlers[selected].Subpriority-2,script.SpriteRecipes[Templates[kind]],config[3].GetInt32(),owner);Invoke(Registry(owner),"Add",callback);}
        else{callback=Nested("FlailMovementTask",selected,owner);Tasks(owner).Add(callback);}
        return new(owner,callback,kind,key,selected,seed,kind==3?(config[1].GetInt32()==-1?3:2):0);
    }
    private static void AssertSprites(Probe p,JsonElement rows,Columns c,string context)
    {
        if(p.Kind==4)
        {
            var row=Numbers(rows.EnumerateArray().Single());int slot=p.Selected;int S(string n)=>c.Read(row,n);
            Assert.AreEqual(new PixelPoint(S("x2"),S("y2")),Field<PixelPoint[]>(p.Owner,"_offsets")[slot],context+" rotation foot offset");
            Assert.AreEqual(new SourceAffineMatrix(S("a"),S("b"),S("c"),S("d")),new SourceAffineMatrix(Field<int[]>(p.Owner,"_xScale")[slot],Field<int[]>(p.Owner,"_matrixB")[slot],Field<int[]>(p.Owner,"_matrixC")[slot],Field<int[]>(p.Owner,"_yScale")[slot]),context+" matrix");
            Assert.AreEqual(S("affineMode")==3,Field<bool[]>(p.Owner,"_affineMode")[slot],context+" double size/restoration");Assert.AreEqual(S("objMode")==1,Field<bool[]>(p.Owner,"_blendedObject")[slot],context+" blend");Assert.AreEqual(S("invisible")==0,p.Owner.Snapshot().Battlers[slot].Visible,context+" hidden-present preservation");return;
        }
        var actual=Sprites(p.Owner);Assert.AreEqual(rows.GetArrayLength(),actual.Length,context+" occupants");if(actual.Length==0)return;
        var expected=Numbers(rows.EnumerateArray().Single());int E(string n)=>c.Read(expected,n);var sprite=actual.Single();var frame=Property<SourceVisualSpriteFrame>(sprite,"Frame");
        Assert.AreEqual(E("slot"),frame.Slot,context);Assert.AreEqual(new PixelPoint(E("x"),E("y")),frame.Position,context+" position");Assert.AreEqual(new PixelPoint(E("x2"),E("y2")),frame.Offset,context+" offset");Assert.AreEqual(E("subpriority"),frame.Subpriority,context);Assert.AreEqual(E("invisible")==0,frame.Visible,context+" visibility");Assert.AreEqual(E("tile"),frame.Tile,context+" tile");Assert.AreEqual(E("hflip")!=0,frame.HFlip,context);Assert.AreEqual(E("vflip")!=0,frame.VFlip,context);Assert.IsTrue(Property<bool>(sprite,"Counted"));Assert.AreEqual((E("affineMode")&1)!=0,frame.Affine,context);Assert.AreEqual(E("objMode")==1,frame.Blend,context);Assert.AreEqual(E("priority"),frame.OamPriority,context);
        Assert.AreEqual(new SourceAffineMatrix(E("a"),E("b"),E("c"),E("d")),new SourceAffineMatrix(frame.MatrixA,frame.MatrixB,frame.MatrixC,frame.MatrixD),context+" sprite affine");
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(E("frameDelay"),frames.DelayCounter,context+" frame delay");Assert.AreEqual(E("frameEnded")!=0,frames.Ended,context+" frame end");Assert.AreEqual(E("frameIndex"),Field<int>(frames,"_index"),context+" frame index");
        if(frame.Affine){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(E("affineEnded")!=0,affine.Ended,context+" affine end");Assert.AreEqual(E("scaleX"),affine.XScale,context);Assert.AreEqual(E("scaleY"),affine.YScale,context);Assert.AreEqual(E("rotation"),affine.Rotation,context);}
        var data=Field<short[]>(sprite,"_data");for(int i=0;i<(p.Kind==3?6:8);i++)Assert.AreEqual(E("data"+i),(int)data[i],context+" callback register "+i);
    }
    [TestMethod]
    public void EveryOriginalCallbackFrameMatchesIncludingHiddenParticipantsPaletteAndRng()
    {
        using var doc=SourceVectorFixture.Read("power_guard_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_power_guard_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;var sc=new Columns(root,"spriteColumns");var st=new Columns(root,"stateColumns");int cases=0,count=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            var p=Create(root,entry.Name);int previous=-2;var random=new SourceVisualRandom(p.Seed);for(int i=0;i<p.Draws;i++)random.Next();uint expectedSeed=Field<uint>(random,"_state");
            foreach(var row in entry.Value.EnumerateArray())
            {
                var state=row.GetProperty("state");long S(string n)=>st.Read(state,n);int tick=(int)S("tick");string context=entry.Name+" tick "+tick;
                Assert.AreEqual(++previous,tick,context);if(tick>=0)Invoke(p.Owner,"AdvanceSourceSprites");AssertSprites(p,row.GetProperty("sprites"),sc,context+" before task");if(tick>=0)Invoke(p.Owner,"RunSourceTasks");
                if(p.Kind==4)
                {
                    AssertSprites(p,row.GetProperty("afterTaskSprites"),sc,context+" after task");foreach(var pair in new[]{("_state",0),("_ticks",1),("_rotation",2),("_remaining",12),("_limit",14)})Assert.AreEqual(S("taskData"+pair.Item2),(long)Field<short>(p.Callback,pair.Item1),context+" task "+pair.Item1);
                }
                if(p.Kind==1)
                {
                    var f=p.Owner.Snapshot();Assert.AreEqual(S("blendControl")!=0,f.BlendEnabled,context+" blend control");if(f.BlendEnabled){Assert.AreEqual(0,f.BlendFirstTargets,context);Assert.AreEqual(63,f.BlendSecondTargets,context);}Assert.AreEqual((int)(S("blendAlpha")&31),f.AlphaA,context+" EVA");Assert.AreEqual((int)(S("blendAlpha")>>8),f.AlphaB,context+" EVB");CollectionAssert.AreEqual(Numbers(row.GetProperty("palette")).Select(x=>(ushort)x).ToArray(),f.SpritePaletteSlots[4],context+" palette left rotation");
                }
                Assert.AreEqual((long)expectedSeed,S("rngState"),context+" source random state");Assert.AreEqual((long)p.Draws,S("rngDraws"),context+" source random draws");if(p.Draws!=0)Assert.AreEqual(expectedSeed,Field<uint>(Field<SourceVisualRandom>(p.Owner,"_visualRandom"),"_state"),context+" runtime random state");
                for(int i=0;i<3;i++)Assert.AreEqual(S("arg"+i),(long)Field<short[]>(p.Owner,"_animationArgs")[i],context+" retained argument "+i);
                Assert.AreEqual(S("visualCount"),(long)p.Owner.ActiveVisualCount,context+" visual ownership");Assert.AreEqual(S("activeTask"),(long)Tasks(p.Owner).Count,context+" task lifetime");Assert.AreEqual(S("liveSprites"),(long)Sprites(p.Owner).Length,context+" sprite lifetime");count++;
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
            // All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
            "0:0:0:1:0","0:0:0:1:1","0:0:0:1:2","0:0:0:1:3","0:0:0:1:4","0:0:0:1:5","0:0:0:1:6","0:0:0:1:7","0:0:0:1:8","0:0:0:1:9",
            // All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
            "1:0:0:1:0","1:0:0:1:1","1:0:0:1:2","1:0:0:1:3","1:0:0:1:4","1:0:0:1:5","1:0:0:1:6","1:0:0:1:7","1:0:0:1:8",
            // All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
            "2:0:0:1:0","2:0:0:1:1","2:0:0:1:2","2:0:0:1:3","2:0:0:1:4",
            // All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
            "3:0:0:1:0","3:0:0:1:4","3:0:0:1:8","3:0:0:1:12","3:0:0:1:16","3:0:0:1:17","3:0:0:1:18","3:0:0:1:19","3:0:0:1:20","3:0:0:1:21","3:0:0:1:22","3:0:0:1:23",
            // All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
            "4:0:0:1:0","4:0:0:1:1","4:0:0:1:2","4:0:0:1:3",
            // Reverse side: source mirroring, BG priority and rotation foot offset
            "0:0:1:0:0","0:0:1:0:6","0:0:1:0:8",
            // Reverse side: source mirroring, BG priority and rotation foot offset
            "1:0:1:0:0","1:0:1:0:3","1:0:1:0:7","1:0:1:0:8",
            // Reverse side: source mirroring, BG priority and rotation foot offset
            "2:0:1:0:0","2:0:1:0:1",
            // Reverse side: source mirroring, BG priority and rotation foot offset
            "3:0:1:0:0","3:0:1:0:20",
            // Reverse side: source mirroring, BG priority and rotation foot offset
            "4:0:1:0:0","4:0:1:0:1","4:0:1:0:2","4:0:1:0:3",
            // Doubles rear geometry and source priorities
            "0:1:2:3:0",
            // Doubles rear geometry and source priorities
            "1:1:2:3:0",
            // Doubles rear geometry and source priorities
            "2:1:2:3:0",
            // Doubles rear geometry and source priorities
            "3:1:2:3:16",
            // Doubles rear geometry and source priorities
            "4:1:2:3:0",
            // Doubles rear geometry and source priorities
            "0:1:3:2:0",
            // Doubles rear geometry and source priorities
            "1:1:3:2:0",
            // Doubles rear geometry and source priorities
            "2:1:3:2:0",
            // Doubles rear geometry and source priorities
            "3:1:3:2:16",
            // Doubles rear geometry and source priorities
            "4:1:3:2:0",
            // Shadow Ball: equal-position and same-side interpolation endpoints
            "2:0:0:0:0",
            // Shadow Ball: equal-position and same-side interpolation endpoints
            "2:1:0:2:0",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        Assert.AreEqual(profile == "full" ? 1040 : 67, cases);
        Assert.AreEqual(profile == "full" ? 42180 : 2777, rows);
        Assert.AreEqual(cases, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
        Assert.AreEqual(rows, root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
