using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SelfAttackSourceVectorTests
{
    private const string Blade="gSwordsDanceBladeSpriteTemplate",Sphere="gSharpenSphereSpriteTemplate";
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {
        public readonly List<SourceAnimationSound> Sounds=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);
        public void StopAnimationEffects(){}
    }
    private sealed record Harness(SourceBattleAnimation Owner,SourceBattleAnimation.SpriteScene Scene,
        AnimationBattler[] Battlers,object Sprite,Audio Audio,SourceAnimationScript Script);
    private static object? Read(object value,string name)=>(value.GetType().GetField(name,Instance)??throw new InvalidOperationException(name)).GetValue(value);
    private static T Field<T>(object value,string name)=>(T)(Read(value,name)??throw new InvalidOperationException(name));
    private static void SetField(object value,string name,object data)=>(value.GetType().GetField(name,Instance)??throw new InvalidOperationException(name)).SetValue(value,data);
    private static T Property<T>(object value,string name)=>(T)((value.GetType().GetProperty(name,Instance)??throw new InvalidOperationException(name)).GetValue(value)??throw new InvalidOperationException(name));
    private static void SetProperty(object value,string name,object data)=>(value.GetType().GetProperty(name,Instance)??throw new InvalidOperationException(name)).SetValue(value,data);
    private static object? Invoke(object value,string name,params object[] args)=>(value.GetType().GetMethod(name,Instance)??throw new InvalidOperationException(name)).Invoke(value,args);
    private static object NewNested(string name,params object[] args)
    {
        var type=typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)??throw new InvalidOperationException(name);
        return Activator.CreateInstance(type,Instance,null,args,CultureInfo.InvariantCulture)??throw new InvalidOperationException(name);
    }
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("self-attack-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_self_attack_vectors.py --repo . --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/self-attack-source-vectors.json");
    private static int Number(JsonElement element,string name)=>element.GetProperty(name).GetInt32();
    private static int[] Numbers(JsonElement row)=>row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static int[][] Rows(JsonElement entry)=>entry.GetProperty("states").EnumerateArray().Select(Numbers).ToArray();
    private static Dictionary<string,int> Columns(JsonElement root)=>root.GetProperty("columns").EnumerateArray()
        .Select((n,i)=>(Name:n.GetString()!,Index:i)).ToDictionary(p=>p.Name,p=>p.Index);
    private static int V(int[] row,Dictionary<string,int> columns,string name)=>row[columns[name]];
    private static string Name(JsonElement entry)=>entry.GetProperty("name").GetString()!;

    private static Harness Create(JsonElement root,JsonElement configuration,SourceAnimationScript source)
    {
        int kind=Number(configuration,"kind");var layout=Numbers(root.GetProperty("positionLayouts")[Number(configuration,"layout")]);
        // The C fixture maps battler IDs through gBattlerPositions. The application
        // already uses normalized source positions, so normalize cue IDs once here.
        int actor=layout[Number(configuration,"actor")],target=layout[Number(configuration,"target")];
        var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,(i&1)==0?0:-8,0,64,64){Visible=false}).ToArray();
        for(int i=0;i<4;i++)
        {
            var c=Numbers(root.GetProperty("coordinates")[i]);
            Assert.AreEqual(new PixelPoint(c[0],c[1]),battlers[i].Base(true));
            Assert.AreEqual(new PixelPoint(c[2],c[3]),battlers[i].Center(true,true));
        }
        var script=source with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
        var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
        var owner=new SourceBattleAnimation(script,new("self-attack-source",1,script.MoveId,new(actor),new(target)),battlers,true,audio,spriteScene:scene);
        owner.Tick();Assert.IsFalse(owner.Active);SetField(owner,"_frame",-1);
        object sprite=kind==0?NewNested("SwordBlade",0,32,script.SpriteRecipes[Blade],new[]{Number(configuration,"arg0"),Number(configuration,"arg1")},owner):
            NewNested("SharpenSphere",0,32,script.SpriteRecipes[Sphere],owner);
        Invoke(Field<object>(owner,"_sprites"),"Add",sprite);
        Assert.AreEqual(1,owner.ActiveVisualCount);Assert.AreEqual(1,scene.Count);
        Assert.AreEqual(0,owner.Snapshot().Sprites.Length,"Eager object construction has not run public OAM capture.");
        return new(owner,scene,battlers,sprite,audio,script);
    }

    private static void CheckObject(object sprite,int[] row,int kind,Dictionary<string,int> columns,string context)
    {
        int S(string n)=>V(row,columns,n);var frame=Property<SourceVisualSpriteFrame>(sprite,"Frame");
        Assert.AreEqual(kind==0?Blade:Sphere,frame.Template,context);
        Assert.AreEqual(new PixelPoint(S("x"),S("y")),frame.Position,context+" source position");
        Assert.AreEqual(new PixelPoint(S("x2"),S("y2")),frame.Offset,context+" fixed-point offset");
        Assert.AreEqual(S("tile"),frame.Tile,context);Assert.AreEqual(S("invisible")==0,frame.Visible,context);
        Assert.AreEqual(32,frame.Width,context);Assert.AreEqual(kind==0?64:32,frame.Height,context);
        Assert.AreEqual(kind==0,frame.Blend,context);Assert.AreEqual(kind==0,frame.Affine,context);Assert.IsFalse(frame.DoubleSize,context);
        Assert.IsFalse(frame.HFlip,context);Assert.IsFalse(frame.VFlip,context);
        var player=Field<SourceSpriteFrameProgram>(sprite,"_frames");
        Assert.AreEqual(S("frameEnded")!=0,player.Ended,context+" frame END");
        if(kind==1)Assert.AreEqual(S("frameCommand"),Field<int>(player,"_index"),context+" frame command");
        Assert.AreEqual(S("frameDelay"),player.DelayCounter,context+" frame delay");
        // The source dummy frame END is normalized to a repeating tile0 recipe.
        // It has no source-Beginning counterpart in the shared normalized player.
        if(kind==0)
        {
            var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");
            Assert.AreEqual(S("affineEnded")!=0,affine.Ended,context+" affine END");
            Assert.AreEqual(S("scaleX"),affine.XScale,context);Assert.AreEqual(S("scaleY"),affine.YScale,context);
            Assert.AreEqual(S("affineDelay"),Field<int>(affine,"_delay"),context+" affine delay");
            // sprite.c decrements its command index on END; the shared program
            // retains the END PC. Both denote the same last FRAME when ended.
            Assert.AreEqual(S("affineCommand"),Field<int>(affine,"_pc")-(affine.Ended?1:0),context+" affine command");
            Assert.AreEqual(S("matrixA"),frame.MatrixA,context+" reciprocal X");Assert.AreEqual(S("matrixD"),frame.MatrixD,context+" reciprocal Y");
            Assert.AreEqual(0,frame.MatrixB,context);Assert.AreEqual(0,frame.MatrixC,context);
            int callback=S("callback");int phase=callback switch{2=>0,3=>1,4=>2,5=>3,6=>4,_=>throw new InvalidOperationException("Unexpected source callback "+callback)};
            Assert.AreEqual(phase,Field<int>(sprite,"_phase"),context+" deferred stored callback");
            if(Read(sprite,"_linear") is {} linear)
            {
                int[] actual=[Field<int>(linear,"_remaining"),unchecked((short)Field<int>(linear,"_vx")),unchecked((short)Field<int>(linear,"_vy")),
                    unchecked((short)Field<int>(linear,"_x")),unchecked((short)Field<int>(linear,"_y"))];
                CollectionAssert.AreEqual(Enumerable.Range(0,5).Select(i=>S("data"+i)).ToArray(),actual,context+" translation words");
            }
        }
        else
        {
            CollectionAssert.AreEqual(Enumerable.Range(0,6).Select(i=>S("data"+i)).ToArray(),Field<short[]>(sprite,"_data").Select(v=>(int)v).ToArray(),context+" signed callback words");
            Assert.AreEqual(S("invisible")!=0,Field<bool>(sprite,"_hidden"),context);
        }
    }
    private static void CheckSounds(Harness h,JsonElement configuration,int throughTick,string context)
    {
        var expected=configuration.GetProperty("sounds").EnumerateArray().Select(Numbers).Where(r=>r[1]<=throughTick).ToArray();
        Assert.AreEqual(expected.Length,h.Audio.Sounds.Count,context+" sound count");
        for(int i=0;i<expected.Length;i++)
        {
            var actual=h.Audio.Sounds[i];Assert.AreEqual("SE_M_SWAGGER2",actual.Id,context);
            Assert.AreEqual(expected[i][2],h.Script.SoundEffects[actual.Id],context+" source sound ID");
            Assert.AreEqual(expected[i][3],actual.Pan,context+" signed source pan");Assert.AreEqual(expected[i][1],actual.Frame,context+" callback sound tick");
            Assert.AreEqual(i+1,expected[i][4],context);Assert.AreEqual(8,expected[i][5],context+" callback origin");
        }
    }

    [TestMethod]
    public void ExactCallbackThenFrameThenAffinePhasesAndDeferredHandoffsMatchPinnedC()
    {
        using var document=Vectors();var root=document.RootElement;var columns=Columns(root);
        Assert.AreEqual(66,root.GetProperty("cases").EnumerateObject().Count());Assert.AreEqual(14,root.GetProperty("probes").EnumerateObject().Count());
        foreach(var entry in root.GetProperty("cases").EnumerateObject().Select(p=>p.Value).Where(c=>Name(c) is "blade_actor0_target1" or "sharpen_actor0_target1"))
        {
            int kind=Number(entry,"kind");var h=Create(root,entry,AnimationCompositionFixture.Script(kind==0?14:159));
            try
            {
                var rows=Rows(entry);string name=Name(entry);int last=entry.GetProperty("end")[0].GetInt32()-1;
                var initial=rows.Single(r=>V(r,columns,"tick")==-1&&V(r,columns,"phase")==2);
                CheckObject(h.Sprite,initial,kind,columns,name+" initialized players");
                for(int tick=0;tick<=last;tick++)
                {
                    string context=name+" tick"+tick;SetField(h.Owner,"_frame",tick);
                    var at=rows.Where(r=>V(r,columns,"tick")==tick).ToArray();
                    Invoke(h.Owner,"StepSourceSprite",h.Sprite);
                    if(tick==last)
                    {
                        CheckObject(h.Sprite,at.Single(r=>V(r,columns,"phase")==3),kind,columns,context+" retained before-destroy state");
                        Assert.IsTrue(Property<bool>(h.Sprite,"Finished"),context);Assert.AreEqual(0,h.Owner.ActiveVisualCount,context);Assert.AreEqual(0,h.Scene.Count,context);
                        Assert.IsFalse(at.Any(r=>V(r,columns,"phase") is 1 or 2),context+" no post-destruction player");
                        CheckSounds(h,entry,tick,context);continue;
                    }
                    CheckObject(h.Sprite,at.Last(r=>V(r,columns,"phase")==0),kind,columns,context+" callback phase");
                    CheckSounds(h,entry,tick,context);int sounds=h.Audio.Sounds.Count;
                    Field<SourceSpriteFrameProgram>(h.Sprite,"_frames").Step();
                    CheckObject(h.Sprite,at.Single(r=>V(r,columns,"phase")==1),kind,columns,context+" frame phase");
                    if(kind==0)Field<SourceSpriteAffineProgram>(h.Sprite,"_affine").Step();
                    CheckObject(h.Sprite,at.Single(r=>V(r,columns,"phase")==2),kind,columns,context+" affine phase");
                    Assert.AreEqual(sounds,h.Audio.Sounds.Count,context+" players cannot emit callback sounds");
                }
                Assert.IsNull(Read(h.Owner,"_visualRandom"),name+" this family consumes no visual RNG");
            }
            finally{h.Scene.Cancel();}
        }
    }

    [TestMethod]
    public void AllPositionsMappingsSignedOffsetsAndOamLifetimesMatchPinnedCThroughSharedScene()
    {
        using var document=Vectors();var root=document.RootElement;var columns=Columns(root);
        var scripts=new[]{AnimationCompositionFixture.Script(14),AnimationCompositionFixture.Script(159)};int count=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject().Select(p=>p.Value))
        {
            int kind=Number(entry,"kind"),raw=Number(entry,"raw");var h=Create(root,entry,scripts[kind]);
            try
            {
                string name=Name(entry);var rows=Rows(entry);int initialTick=raw==1?0:-1,last=entry.GetProperty("end")[0].GetInt32()-1;
                // Eager constructors already perform source initializer/Begin players.
                // For raw oracle cases, compare that identical state at source tick0;
                // this is a shifted primitive boundary, not raw production admission.
                var initial=rows.Last(r=>V(r,columns,"tick")==initialTick&&V(r,columns,"active")!=0);
                CheckObject(h.Sprite,initial,kind,columns,name+" initial");
                var frame=h.Scene.CaptureIdle(h.Battlers,true);Assert.AreEqual(1,frame.Sprites.Length,name);
                for(int tick=initialTick+1;tick<=last;tick++)
                {
                    SetField(h.Owner,"_frame",tick);frame=h.Scene.TickIdle(h.Battlers,true);
                    string context=name+" tick"+tick;bool alive=tick<last;
                    Assert.AreEqual(alive?1:0,frame.Sprites.Length,context+" OAM capture");
                    Assert.AreEqual(alive?1:0,h.Owner.ActiveVisualCount,context+" script count");Assert.AreEqual(alive?1:0,h.Scene.Count,context+" registry count");
                    if(alive)
                    {
                        Assert.IsFalse(Property<bool>(h.Sprite,"Finished"),context);
                        var observed=rows.LastOrDefault(r=>V(r,columns,"tick")==tick&&V(r,columns,"active")!=0);
                        if(observed is not null)CheckObject(h.Sprite,observed,kind,columns,context);
                        var actual=frame.Sprites.Single();Assert.AreEqual(0,actual.Slot,context);
                        Assert.AreEqual(kind==0?1:0,frame.Sprites.Count(s=>s.Affine),context+" matrix ownership");
                        Assert.AreEqual(Property<SourceVisualSpriteFrame>(h.Sprite,"Frame") with{OamRank=actual.OamRank},actual,context+" public OAM follows player phase");
                    }
                    else
                    {
                        var death=rows.Single(r=>V(r,columns,"phase")==3);CheckObject(h.Sprite,death,kind,columns,context+" source retirement boundary");
                        Assert.IsTrue(Property<bool>(h.Sprite,"Finished"),context);
                        Assert.AreEqual(0,V(rows.Single(r=>V(r,columns,"phase")==4),columns,"matrixBitmap"),context+" native matrix free");
                    }
                    CheckSounds(h,entry,tick,context);
                }
                Assert.IsNull(Read(h.Owner,"_visualRandom"),name);count++;
            }
            finally{h.Scene.Cancel();Assert.AreEqual(0,h.Scene.Count);}
        }
        Assert.AreEqual(66,count);
    }

    [TestMethod]
    public void CallbackOnlySignedCounterPanAndThreePartDestructionProbesMatchPinnedC()
    {
        using var document=Vectors();var root=document.RootElement;var columns=Columns(root);
        var baseline=root.GetProperty("cases").EnumerateObject().Select(p=>p.Value).Single(c=>Name(c)=="sharpen_actor0_target1");
        var source=AnimationCompositionFixture.Script(159);int count=0;
        foreach(var probe in root.GetProperty("probes").EnumerateObject().Select(p=>p.Value))
        {
            var h=Create(root,baseline,source);
            try
            {
                string name=Name(probe);var data=Field<short[]>(h.Sprite,"_data");var input=Numbers(probe.GetProperty("data"));
                for(int i=0;i<6;i++)data[i]=checked((short)input[i]);
                SetField(h.Sprite,"_hidden",Number(probe,"invisible")!=0);var player=Field<SourceSpriteFrameProgram>(h.Sprite,"_frames");
                SetProperty(player,"Ended",Number(probe,"ended")!=0);
                int frameIndex=Field<int>(player,"_index"),delay=player.DelayCounter;var position=Property<SourceVisualSpriteFrame>(h.Sprite,"Frame").Position;
                var rows=Rows(probe);int steps=probe.GetProperty("end")[0].GetInt32();
                for(int tick=0;tick<steps;tick++)
                {
                    string context=name+" callback"+tick;SetField(h.Owner,"_frame",tick);Invoke(h.Owner,"StepSourceSprite",h.Sprite);
                    var at=rows.Where(r=>V(r,columns,"tick")==tick).ToArray();var death=at.SingleOrDefault(r=>V(r,columns,"phase")==3);
                    var expected=death??at.Last(r=>V(r,columns,"phase")==0);
                    CollectionAssert.AreEqual(Enumerable.Range(0,6).Select(i=>V(expected,columns,"data"+i)).ToArray(),data.Select(v=>(int)v).ToArray(),context);
                    Assert.AreEqual(V(expected,columns,"invisible")!=0,Field<bool>(h.Sprite,"_hidden"),context);
                    Assert.AreEqual(death is not null,Property<bool>(h.Sprite,"Finished"),context);
                    Assert.AreEqual(death is null?1:0,h.Owner.ActiveVisualCount,context);Assert.AreEqual(death is null?1:0,h.Scene.Count,context);
                    Assert.AreEqual(Number(probe,"ended")!=0,player.Ended,context+" callback cannot set frame END");
                    Assert.AreEqual(frameIndex,Field<int>(player,"_index"),context);Assert.AreEqual(delay,player.DelayCounter,context);
                    Assert.AreEqual(position,Property<SourceVisualSpriteFrame>(h.Sprite,"Frame").Position,context);
                    CheckSounds(h,probe,tick,context);
                }
                count++;
            }
            finally{h.Scene.Cancel();}
        }
        Assert.AreEqual(14,count);
    }
}
