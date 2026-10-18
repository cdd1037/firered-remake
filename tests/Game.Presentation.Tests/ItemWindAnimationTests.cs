using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ItemWindAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public readonly List<(int Pan,int Frame)>Pans=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));}
    private sealed class Occupied(int slot,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,args);
    private static object Nested(string n,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,args,CultureInfo.InvariantCulture)!;
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static readonly string[] Templates=["gTrickBagSpriteTemplate","gRecycleSpriteTemplate","gSilverWindBigSparkSpriteTemplate","gSilverWindMediumSparkSpriteTemplate","gSilverWindSmallSparkSpriteTemplate"];
    private static AnimationBattler[] Bodies(bool doubles,bool hidden=false,int height=64,int yoff=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,yoff,0,64,height){SourceVisibility=new(true,false,hidden&&i>=2)}).ToArray();
    private static CommittedMoveAnimation Cue(int id,int actor,int target)=>new("item-wind",1,id,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void EverySelectedNativeFrameMatchesBagRestartDepthBlinkRecycleAlphaAndSharedFlyingParticles()
    {
        using var doc=SourceVectorFixture.Read("item-wind-source-vectors.json","python tests/Reference/ItemWind/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/item-wind-source-vectors.json");int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),id=kind==0?271:kind==1?278:318;bool doubles=c.GetProperty("doubles").GetBoolean();
            var script=AnimationCompositionFixture.Script(id);var owner=new SourceBattleAnimation(Primitive(script),Cue(id,actor,target),Bodies(doubles,height:c.GetProperty("height").GetInt32(),yoff:c.GetProperty("yOffset").GetInt32()),doubles,new Audio());var priorities=Field<int[]>(owner,"_battlerBackgroundPriorities");priorities[0]=1;priorities[1]=2;
            var args=Field<short[]>(owner,"_animationArgs");int j=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[j++]=(short)a.GetInt32();
            SourceBattleAnimation.VisualSprite? sprite=kind==5?null:(SourceBattleAnimation.VisualSprite)(kind<2?Nested("ItemWindSprite",0,2,script.SpriteRecipes[Templates[kind]],owner):Nested("CureFlowerSprite",0,2,script.SpriteRecipes[Templates[kind]],owner,args.Select(a=>(int)a).ToArray()));
            if(kind==5)Assert.AreEqual(true,Invoke(owner,"TryRunArgumentPredicate",C("createvisualtask","AnimTask_GetTargetSide","2")));
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=row.GetProperty("frame").GetInt32();string why=$"case{cases} kind{kind} actor{actor} target{target} frame{frame}";
                if(frame>0){sprite!.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,sprite is not null&&!sprite.Finished,why);
                CollectionAssert.AreEqual(row.GetProperty("args").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),args.Take(8).ToArray(),why);Assert.IsTrue(args.Skip(8).All(v=>v==0),why);
                if(sprite is not null)
                {
                    var f=sprite.Frame;int[] e=row.GetProperty("sprite").EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(new PixelPoint(e[0],e[1]),f.Position,why);Assert.AreEqual(new PixelPoint(e[2],e[3]),f.Offset,why);Assert.AreEqual(e[4],f.Tile,why);Assert.AreEqual(e[5]!=0,f.Visible,why);Assert.AreEqual(e[6],f.OamPriority,why);Assert.AreEqual(e[7],f.Subpriority,why);
                    var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(e[8]!=0,frames.Ended,why);Assert.AreEqual(e[9],frames.DelayCounter,why);var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(e[10],affine.XScale,why);Assert.AreEqual(e[11],affine.YScale,why);Assert.AreEqual(e[12],affine.Rotation,why);Assert.AreEqual(e[13]!=0,affine.Ended,why);
                    var matrix=SourceAffineMatrix.FromScaleRotation(65536/e[10],65536/e[11],e[12]);Assert.AreEqual((matrix.A,matrix.B,matrix.C,matrix.D),(f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD),why);
                    var d=row.GetProperty("data").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray();
                    if(kind<2)CollectionAssert.AreEqual(d,Field<short[]>(sprite,"_d"),why);
                    else{Assert.AreEqual(d[0],Field<int>(sprite,"_angle"),why);Assert.AreEqual(d[1],Field<int>(sprite,"_amplitude"),why);Assert.AreEqual(d[2],Field<int>(sprite,"_speed"),why);Assert.AreEqual(d[3],Field<int>(sprite,"_increment"),why);Assert.AreEqual(d[7],Field<short>(sprite,"_timer"),why);Assert.AreEqual(0,d[5]);Assert.AreEqual(0,d[6]);}
                    if(kind==1){var alpha=row.GetProperty("alpha");Assert.AreEqual(alpha[0].GetInt32(),Field<int>(owner,"_alphaA"),why);Assert.AreEqual(alpha[1].GetInt32(),Field<int>(owner,"_alphaB"),why);}
                }
                rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.IsTrue(cases>=115);Assert.IsTrue(rows>=11671);
    }
    [TestMethod,DataRow(271),DataRow(278),DataRow(318)]
    public void CompleteAuthoredRootsComposeCanonicalMaterialsBothBackgroundBranchesAndDrainOwners(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        foreach(var (doubles,actor,target) in new[]{(false,0,id==278?0:1),(false,1,id==278?1:0),(true,2,id==278?2:3),(true,3,id==278?3:2)})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(resolved.Script,Cue(id,actor,target),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);int ticks=0;bool pixels=false,scroll=false;var templates=new HashSet<string>();var backgrounds=new HashSet<int>();
            while(owner.Active&&ticks++<1200){var frame=owner.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(owner.Snapshot()));var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);if(frame.Sprites.Any(s=>s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));foreach(var s in frame.Sprites)templates.Add(s.Template);if(frame.MoveBackground?.Id is {} bg)backgrounds.Add(bg);scroll|=frame.MoveBackground?.ScrollX!=0;}
            Assert.IsFalse(owner.Active);Assert.IsTrue(pixels);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);var final=owner.Snapshot();Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsTrue(audio.Cues.Count>0);Assert.IsNull(Field<SourceVisualRandom?>(owner,"_visualRandom"));
            foreach(string name in id==271?Templates.Take(1):id==278?Templates.Skip(1).Take(1):Templates.Skip(2))Assert.IsTrue(templates.Contains(name),name);
            if(id==318){
                // Cmd_panse adjusts -64/+63 by actor side, then the priority-1
                // task adds two each frame (including creation), clamping only
                // the terminal value. Opponent reversal starts at clamped +63.
                int[] pans=actor%2==0?Enumerable.Range(1,64).Select(i=>Math.Min(63,-64+2*i)).ToArray():Enumerable.Range(1,63).Select(i=>63-2*i).ToArray();
                CollectionAssert.AreEqual(pans,audio.Pans.Select(p=>p.Pan).ToArray());int start=audio.Cues.Single(c=>c.Id=="SE_M_GUST").Frame;CollectionAssert.AreEqual(Enumerable.Range(start,pans.Length).ToArray(),audio.Pans.Select(p=>p.Frame).ToArray());
                Assert.IsTrue(scroll);Assert.IsTrue(backgrounds.Contains(resolved.Script.BackgroundSymbols[target%2==0?"BG_BUG_OPPONENT":"BG_BUG_PLAYER"]));Assert.IsFalse(backgrounds.Contains(resolved.Script.BackgroundSymbols[target%2==0?"BG_BUG_PLAYER":"BG_BUG_OPPONENT"]));Assert.AreEqual(0,final.MoveBackground!.ScrollX);}
        }
    }
    [TestMethod,DataRow(271),DataRow(278),DataRow(318)]
    public void CancellationRestoresHiddenPartnersAndSharedPaletteOwnership(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(id);
        foreach(int stop in new[]{0,1,16,35,65,90,140,220})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,Cue(id,0,id==278?0:1),Bodies(true,true),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var visibility=o.Snapshot().BattlerVisibility;for(int n=0;n<stop&&o.Active;n++)o.Tick();int sounds=audio.Cues.Count,pans=audio.Pans.Count;o.Cancel();o.Cancel();var f=o.Snapshot();Assert.AreEqual(0,scene.Count);Assert.IsFalse(o.Active);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(f.BlendEnabled);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(pans,audio.Pans.Count);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(JsonSerializer.Serialize(visibility),JsonSerializer.Serialize(f.BattlerVisibility));
        }
    }
    [TestMethod]
    public void MalformedRecipesArgumentsAndObjectMatrixTaskOwnersFailBeforeAudio()
    {
        using var resources=new SourceAnimationResources(Folder);
        void Reject(SourceAnimationScript s,int id,int occupied=0,bool affine=false)
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var registry=Field<object>(scene,"Sprites");for(int i=0;i<occupied;i++)Invoke(registry,"Add",new Occupied(63-i,affine));
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(id,0,id==278?0:1),Bodies(true),true,audio,Terrain(resources),spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(occupied,scene.Count);
        }
        foreach(int id in new[]{271,278,318})
        {
            var s=AnimationCompositionFixture.Script(id);foreach(string n in id==271?Templates.Take(1):id==278?Templates.Skip(1).Take(1):Templates.Skip(2))
            {var r=s.SpriteRecipes[n];foreach(var bad in new[]{r with{Width=128},r with{Animations=[]},r with{AffineAnimations=[]},r with{Callback="unreviewed"},r with{Blend=!r.Blend}}){var recipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[n]=bad;Reject(s with{SpriteRecipes=recipes},id);}}
            Reject(s,id,29,true);
        }
        var trick=AnimationCompositionFixture.Script(271);var recycle=AnimationCompositionFixture.Script(278);var wind=AnimationCompositionFixture.Script(318);var arrow=C("createsprite",Templates[1],"0","2");
        Reject(Primitive(recycle) with{Commands=[arrow,arrow,C("end")]},278);
        Reject(Primitive(trick) with{Commands=[C("createsprite",Templates[0],"0","2","-40","256"),C("end")]},271);
        Reject(Primitive(trick) with{Commands=[..Enumerable.Repeat(C("createsprite",Templates[0],"0","2","-40","80"),43),C("end")]},271);
        Reject(Primitive(trick) with{Commands=[C("createsprite",Templates[0],"0","2","-40","80"),..Enumerable.Repeat(C("createvisualtask","AnimTask_StretchAttackerUp","3"),16),C("end")]},271);
        Reject(wind,318,25);
        foreach(var args in new[]{new[]{"0","0","0","0","0","0","1"},new[]{"0","0","256","1","0","0","1"},new[]{"0","0","0","1","0","4","1"}})Reject(Primitive(wind) with{Commands=[C("createsprite",[Templates[2],"0","2",..args]),C("end")]},318);
    }
    [TestMethod]
    public void AttackerStretchUsesTheSameTaskProgramAndRestoresBothParticipants()
    {
        var s=AnimationCompositionFixture.Script(271);var o=new SourceBattleAnimation(Primitive(s),Cue(271,0,1),Bodies(false),false,new Audio());var commands=s.TaskAffinePrograms["sAffineAnims_StretchBattlerUp"];
        Assert.AreEqual(true,Invoke(o,"TryCreateTaskAffineTask",C("createvisualtask","AnimTask_StretchAttackerUp","3")));Assert.AreEqual(true,Invoke(o,"TryCreateTaskAffineTask",C("createvisualtask","AnimTask_StretchTargetUp","3")));
        Assert.AreEqual(new PixelPoint(4,0),o.Snapshot().Offsets[0]);Assert.AreEqual(new PixelPoint(4,0),o.Snapshot().Offsets[1]);
        var tasks=((IEnumerable)Field<object>(o,"_tasks")).Cast<object>().ToArray();Assert.AreEqual(2,tasks.Length);var program=new SourceTaskAffineProgram(commands);program.Prepare();
        for(int tick=0;tick<22;tick++){var f=o.Snapshot();Assert.AreEqual(f.Offsets[0],f.Offsets[1]);Assert.AreEqual(f.BattlerAffine[0],f.BattlerAffine[1]);Assert.AreEqual(program.Matrix.A,f.BattlerAffine[0].XScale);Assert.AreEqual(program.Matrix.D,f.BattlerAffine[0].YScale);if(tick<21){foreach(var task in tasks)Invoke(task,"Step",o);program.Run();}}
        Assert.IsTrue(o.Snapshot().Offsets.All(p=>p==default));o.Cancel();
    }
}
