using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RandomFistSourceVectorTests
{
    private const string Parent="gFistFootRandomPosSpriteTemplate",Child="gBasicHitSplatSpriteTemplate";
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static object? ReadField(object instance,string name)
        =>(instance.GetType().GetField(name,Instance)??throw new InvalidOperationException(name)).GetValue(instance);
    private static T Field<T>(object instance,string name)=>(T)(ReadField(instance,name)??throw new InvalidOperationException(name));
    private static object? Invoke(object instance,string name,params object?[] args)
        =>(instance.GetType().GetMethod(name,Instance)??throw new InvalidOperationException(name)).Invoke(instance,args);
    private static T Property<T>(object instance,string name)
        =>(T)((instance.GetType().GetProperty(name,Instance)??throw new InvalidOperationException(name)).GetValue(instance)??throw new InvalidOperationException(name));
    private static object NewNested(string name,params object[] args)
    {
        var type=typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)??throw new InvalidOperationException(name);
        return Activator.CreateInstance(type,Instance,null,args,CultureInfo.InvariantCulture)??throw new InvalidOperationException(name);
    }
    private static object Owned(SourceBattleAnimation owner)=>Field<object>(owner,"_sprites");
    private static object[] Objects(SourceBattleAnimation owner)=>((IEnumerable)Owned(owner)).Cast<object>().ToArray();
    private static void Add(SourceBattleAnimation owner,object sprite)=>Invoke(Owned(owner),"Add",sprite);
    private static int FreeSlot(SourceBattleAnimation owner)=>(int)Invoke(owner,"FindFreeSpriteSlot",-1)!;
    private static uint? RandomState(SourceBattleAnimation owner)
        =>ReadField(owner,"_visualRandom") is SourceVisualRandom random?Field<uint>(random,"_state"):null;
    private static long[] Numbers(JsonElement row)=>row.EnumerateArray().Select(v=>v.GetInt64()).ToArray();
    private static int Number(JsonElement value,string name)=>value.GetProperty(name).GetInt32();
    private static SourceVisualSpriteFrame[] Family(SourceAnimationFrame frame)
        =>frame.Sprites.Where(s=>s.Template is Parent or Child).OrderBy(s=>s.Slot).ToArray();
    private static bool IsFamily(object sprite)=>Property<SourceVisualSpriteFrame>(sprite,"Frame").Template is Parent or Child;
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/random-fist-source-vectors.json")));

    [TestMethod]
    public void ExtractedCallbackFrameAffineAndOamBoundariesMatchAllFiftyTwoSourceConfigurations()
    {
        using var document=Vectors();var root=document.RootElement;
        var columns=root.GetProperty("stateColumns").EnumerateArray().Select((p,i)=>(Name:p.GetString()!,Index:i)).ToDictionary(p=>p.Name,p=>p.Index);
        Assert.AreEqual(52,root.GetProperty("cases").EnumerateObject().Count());
        foreach(var entry in root.GetProperty("cases").EnumerateObject())RunCase(root,entry.Value,columns);
    }

    private static void RunCase(JsonElement root,JsonElement configuration,Dictionary<string,int> columns)
    {
        string name=configuration.GetProperty("name").GetString()!;
        int layout=Number(configuration,"layout"),count=Number(configuration,"count"),priority=Number(configuration,"subpriority");
        var profile=root.GetProperty("dimensionProfiles")[Number(configuration,"profile")];
        // The C oracle explicitly starts with an empty local sprite registry.
        // Hiding battler OAM reservations preserves exact slots0..63 while keeping
        // the real four battler picture anchors/dimensions available to Initialize.
        var battlers=Enumerable.Range(0,4).Select(i=>
        {
            var size=profile.GetProperty((i&1)==0?"back":"front");
            return new AnimationBattler(new(i),"b"+i,0,0,size[0].GetInt32(),size[1].GetInt32()){Visible=false};
        }).ToArray();
        for(int i=0;i<4;i++)Assert.AreEqual(new PixelPoint(root.GetProperty("coordinates")[i][0].GetInt32(),root.GetProperty("coordinates")[i][1].GetInt32()),battlers[i].Center(true,true),name);
        var script=AnimationCompositionFixture.Script(24) with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
        var cue=new CommittedMoveAnimation("random-fist-source",1,24,new(Number(configuration,"actor")),new(Number(configuration,"target"))){VisualSeed=configuration.GetProperty("seed").GetUInt32()};
        var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(script,cue,battlers,true,new Audio(),spriteScene:scene);
        owner.Tick();Assert.IsFalse(owner.Active,name);
        var loaded=Field<HashSet<string>>(owner,"_loaded");loaded.Add("ANIM_TAG_HANDS_AND_FEET");loaded.Add("ANIM_TAG_IMPACT");
        var args=Field<short[]>(owner,"_animationArgs");args[0]=checked((short)Number(configuration,"arg0"));args[1]=checked((short)Number(configuration,"duration"));args[2]=checked((short)Number(configuration,"arg2"));
        // These are direct callback probes. Negative duration/nonzero selector and
        // priority255 deliberately exceed the narrower production script admission.
        var command=new SourceAnimationCommand("createsprite",[Parent,"ANIM_ATTACKER","3",args[0].ToString(CultureInfo.InvariantCulture),args[1].ToString(CultureInfo.InvariantCulture),args[2].ToString(CultureInfo.InvariantCulture)],1);
        var allocations=configuration.GetProperty("allocations").EnumerateArray().Select(Numbers).ToArray();
        var parentAllocations=allocations.Where(a=>a[4]==1).ToArray();
        var states=configuration.GetProperty("states").EnumerateArray().Select(Numbers).ToArray();
        int S(long[] row,string column)=>checked((int)row[columns[column]]);
        var observations=states.Where(r=>S(r,"phase")==5).GroupBy(r=>S(r,"tick")).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>S(r,"slot")).ToArray());
        var boundaries=configuration.GetProperty("boundaries").EnumerateArray().Select(Numbers).ToDictionary(r=>checked((int)r[0]));
        var suffixes=configuration.GetProperty("suffixes").EnumerateArray().Select(Numbers).ToArray();
        var actualBySerial=new Dictionary<int,object>();
        var reservations=new List<object>();
        object Reserve(int slot){var sprite=NewNested("BubbleHelper",slot);Add(owner,sprite);reservations.Add(sprite);return sprite;}
        if(layout==2)Reserve(0);
        if(layout==3)for(int i=1;i<64;i++)Reserve(i);
        for(int p=0;p<count;p++)
        {
            var allocation=parentAllocations[p];int slot=FreeSlot(owner);Assert.AreEqual((int)allocation[3],slot,name+" parent allocation");
            object parent;
            if(layout is 1 or 2)
            {
                parent=NewNested("RandomFistSprite",slot,priority,script.SpriteRecipes[Parent]);Add(owner,parent);
                Assert.IsNull(ReadField(parent,"_frames"),name);Assert.IsNull(ReadField(parent,"_child"),name);
                Assert.IsNull(RandomState(owner),name+" raw creation must not run initializer");
                if(layout==2){Assert.IsTrue((bool)Invoke(Owned(owner),"Remove",reservations[0])!);reservations.Clear();}
                // The managed Initialize constructs the real shared frame player,
                // which already performs BeginAnim. Do not advance it a second time.
                Invoke(parent,"Initialize",owner);
            }
            else
            {
                Assert.IsTrue((bool)Invoke(owner,"TryCreateRandomFistSprite",command,slot,priority)!,name);
                parent=Objects(owner).Single(s=>Property<int>(s,"Slot")==slot);
            }
            actualBySerial.Add((int)allocation[2],parent);
            var child=ReadField(parent,"_child");
            var expectedChild=allocations.SingleOrDefault(a=>a[4]==2&&a[5]==allocation[2]);
            if(expectedChild is null)Assert.IsNull(child,name+" MAX_SPRITES must own no child");
            else
            {
                Assert.IsNotNull(child,name);actualBySerial.Add((int)expectedChild[2],child);
                Assert.AreEqual((int)expectedChild[3],Property<int>(child,"Slot"),name+" child allocation");
                Assert.IsFalse(Property<bool>(child,"Counted"),name+" raw child must not hold script count");
                Assert.IsNull(ReadField(child,"_frames"),name+" raw CreateSprite must not BeginAnim");
                Assert.IsNull(ReadField(child,"_affine"),name+" raw CreateSprite must not BeginAffineAnim");
                Assert.AreEqual((int)unchecked((byte)(priority+1)),Property<int>(child,"Subpriority"),name);
            }
            Assert.IsTrue(Property<bool>(parent,"Counted"),name);
            CheckSuffix(owner,suffixes[p],name+" initializer "+p);
            if(layout==1&&child is not null)Invoke(child,"AdvanceAnimation",owner);
        }
        // Raw-layout tick0 is the explicit initialization boundary above. Layout1
        // includes its higher child's first player; layout2 leaves its lower child
        // unadvanced. Every subsequent pass uses the actual shared SpriteScene.
        int tick=layout is 1 or 2?0:-1;
        Assert.AreEqual(0,owner.Snapshot().Sprites.Length,name+" initialization has not built public OAM");
        // The tick−1 comparison below is a synthetic projection of initialized
        // object/frame fields, not an assertion that source BuildOamBuffer ran.
        // Tick0+ below and explicit raw-layout boundaries follow their player phase.
        var frame=scene.CaptureIdle(battlers,true);
        CheckBoundary(tick,frame);
        int lastTick=configuration.GetProperty("end")[0].GetInt32()-1;
        while(tick<lastTick)
        {
            tick++;frame=scene.TickIdle(battlers,true);
            if(boundaries.ContainsKey(tick))CheckBoundary(tick,frame);
        }
        Assert.AreEqual(0,Family(frame).Length,name);Assert.AreEqual(0,owner.ActiveVisualCount,name);
        Assert.AreEqual(layout==3?63:0,scene.Count,name+" only inert reservations may survive");
        Assert.AreEqual(configuration.GetProperty("end")[3].GetUInt32(),RandomState(owner),name);
        foreach(var pair in actualBySerial)
        {
            Assert.IsTrue(Property<bool>(pair.Value,"Finished"),name+" retire serial "+pair.Key);
            Assert.IsFalse(Objects(owner).Any(s=>ReferenceEquals(s,pair.Value)),name+" remove serial "+pair.Key);
            var dead=states.Single(r=>S(r,"phase")==3&&S(r,"serial")==pair.Key);
            CheckObject(pair.Value,dead,name+" before reset serial "+pair.Key);
        }
        scene.Cancel();Assert.AreEqual(0,scene.Count,name);

        void CheckBoundary(int currentTick,SourceAnimationFrame current)
        {
            string context=name+" tick"+currentTick;
            var boundary=boundaries[currentTick];var actual=Family(current);
            var expected=observations.TryGetValue(currentTick,out var rows)?rows:[];
            Assert.AreEqual((int)boundary[1],actual.Length,context+" live count");
            Assert.AreEqual((int)boundary[2],owner.ActiveVisualCount,context+" visual count");
            Assert.AreEqual((int)boundary[1],Objects(owner).Count(IsFamily),context+" registry count");
            // The managed scene models affine ownership by live affine objects;
            // it does not expose the hardware matrix allocator's numeric bit IDs.
            Assert.AreEqual(BitOperations.PopCount((uint)boundary[3]),actual.Count(s=>s.Affine),context+" allocated matrix ownership");
            Assert.AreEqual((uint)boundary[5],RandomState(owner),context+" RNG state");
            Assert.AreEqual((int)boundary[6],(int)args[2],context+" shared arg2");
            Assert.AreEqual(expected.Length,actual.Length,context);
            for(int i=0;i<expected.Length;i++)
            {
                var row=expected[i];var item=actual[i];string message=context+" slot"+S(row,"slot");
                Assert.AreEqual(S(row,"slot"),item.Slot,message);Assert.AreEqual(S(row,"kind")==1?Parent:Child,item.Template,message);
                Assert.AreEqual(new PixelPoint(S(row,"x"),S(row,"y")),item.Position,message);
                Assert.AreEqual(new PixelPoint(S(row,"x2"),S(row,"y2")),item.Offset,message);
                Assert.AreEqual(S(row,"tile"),item.Tile,message);Assert.AreEqual(S(row,"hflip")!=0,item.HFlip,message);Assert.AreEqual(S(row,"vflip")!=0,item.VFlip,message);
                Assert.AreEqual(S(row,"subpriority"),item.Subpriority,message);Assert.AreEqual(32,item.Width,message);Assert.AreEqual(32,item.Height,message);
                bool affine=S(row,"kind")==2;Assert.AreEqual(affine,item.Affine,message);Assert.AreEqual(affine,item.Blend,message);Assert.IsFalse(item.DoubleSize,message);
                Assert.AreEqual(S(row,"matrixA"),item.MatrixA,message);Assert.AreEqual(S(row,"matrixD"),item.MatrixD,message);Assert.AreEqual(0,item.MatrixB,message);Assert.AreEqual(0,item.MatrixC,message);
                CheckObject(actualBySerial[S(row,"serial")],row,message);
            }
            // Both OAM capture and Snapshot are observations, not animation/RNG steps.
            uint? before=RandomState(owner);var repeated=scene.CaptureIdle(battlers,true);owner.Snapshot();
            Assert.AreEqual(JsonSerializer.Serialize(current),JsonSerializer.Serialize(repeated),context+" repeat capture");Assert.AreEqual(before,RandomState(owner),context+" observation must not draw");
            CheckSuffix(owner,suffixes[^1],context+" stable suffix");
        }
        void CheckObject(object sprite,long[] row,string message)
        {
            if(S(row,"kind")==1)
            {
                Assert.AreEqual(S(row,"data0"),(int)Field<short>(sprite,"_remaining"),message+" signed timer");
                var player=Field<SourceSpriteFrameProgram>(sprite,"_frames");
                Assert.AreEqual(S(row,"frameEnded")!=0,player.Ended,message+" parent frame END");
                Assert.AreEqual(S(row,"frameDelay"),player.DelayCounter,message+" parent frame delay");
                Assert.AreEqual(S(row,"tile"),player.Frame.Tile,message+" parent frame tile");
                var child=ReadField(sprite,"_child");
                if(S(row,"data7")==64)Assert.IsNull(child,message+" failed raw allocation");
                else{Assert.IsNotNull(child,message);Assert.AreEqual(S(row,"data7"),Property<int>(child,"Slot"),message+" raw child ID");}
            }
            else
            {
                var affine=ReadField(sprite,"_affine") as SourceSpriteAffineProgram;
                if(S(row,"affineBeginning")!=0)
                {
                    Assert.IsNull(affine,message+" lazy affine Begin");Assert.IsNull(ReadField(sprite,"_frames"),message+" lazy frame Begin");
                    Assert.AreEqual(0,S(row,"matrixWrites"),message+" no eager affine update");
                }
                else
                {
                    Assert.IsNotNull(affine,message);Assert.IsNotNull(ReadField(sprite,"_frames"),message);
                    Assert.AreEqual(S(row,"affineEnded")!=0,affine.Ended,message+" child affine END");
                    Assert.AreEqual(S(row,"affineDelay"),Field<int>(affine,"_delay"),message+" child affine delay");
                    Assert.AreEqual(S(row,"scaleX"),affine.XScale,message);Assert.AreEqual(S(row,"scaleY"),affine.YScale,message);Assert.AreEqual(0,affine.Rotation,message);
                    Assert.AreEqual(S(row,"matrixA"),affine.Matrix.A,message);Assert.AreEqual(S(row,"matrixD"),affine.Matrix.D,message);
                }
                // The imported dummy frame is a normalized constant-tile loop.
                // Its source-equivalent rendering is checked above; its private
                // END/beginning flags are intentionally not claimed equivalent.
                Assert.AreEqual(0,Property<SourceVisualSpriteFrame>(sprite,"Frame").Tile,message);
            }
        }
    }

    private static void CheckSuffix(SourceBattleAnimation owner,long[] suffix,string message)
    {
        Assert.AreEqual((uint)suffix[3],RandomState(owner),message+" prefix state");
        Assert.AreEqual((int)suffix[5],(int)Field<short[]>(owner,"_animationArgs")[2],message+" selected animation");
        var copy=new SourceVisualRandom((uint)suffix[3]);
        for(int i=0;i<8;i++)Assert.AreEqual((ushort)suffix[6+i],copy.Next(),message+" next draw "+i);
        Assert.AreEqual((uint)suffix[3],RandomState(owner),message+" suffix copy must not consume owner RNG");
    }
}
