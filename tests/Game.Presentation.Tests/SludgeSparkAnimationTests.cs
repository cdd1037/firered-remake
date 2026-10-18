using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SludgeSparkAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gSludgeProjectileSpriteTemplate","gSludgeBombHitParticleSpriteTemplate","gSparkElectricityFlashingSpriteTemplate","gSparkElectricitySpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object Nested(string n,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,args,CultureInfo.InvariantCulture)!;
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Bodies(bool doubles,int mask=15)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){SourceVisibility=new(true,(mask&(1<<i))==0,(mask&(1<<i))==0)}).ToArray();
    private static CommittedMoveAnimation Cue(int id,int actor,int target)=>new("sludge-spark",1,id,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}

    [TestMethod,DataRow("legal-source",656),DataRow("approved-policy",238)]
    public void NativeTrajectoriesAndApprovedPolicyBranchesHaveSeparateEvidence(string evidence,int expectedCases)
    {
        using var doc=SourceVectorFixture.Read("sludge-spark-source-vectors.json","python tests/Reference/SludgeSpark/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/sludge-spark-source-vectors.json");int cases=0,rows=0;
        Assert.AreEqual("firered-sludge-spark-v1",doc.RootElement.GetProperty("schema").GetString());
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray().Where(c=>c.GetProperty("evidence").GetString()==evidence))
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),id=kind<2?188:209;bool doubles=c.GetProperty("doubles").GetBoolean();int mask=c.GetProperty("visibleMask").GetInt32();
            var script=AnimationCompositionFixture.Script(id);var owner=new SourceBattleAnimation(Primitive(script),Cue(id,actor,target),Bodies(doubles,mask),doubles,new Audio());
            var priorities=Field<int[]>(owner,"_battlerBackgroundPriorities");priorities[0]=1;priorities[1]=2;
            var args=c.GetProperty("arguments").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var registers=Field<short[]>(owner,"_animationArgs");for(int i=0;i<8;i++)registers[i]=(short)args[i];
            SourceBattleAnimation.VisualSprite sprite=kind<2?(SourceBattleAnimation.VisualSprite)Nested("SludgeSprite",doubles?4:2,2,script.SpriteRecipes[Templates[kind]],args,owner):kind==3?(SourceBattleAnimation.VisualSprite)Nested("FixedSparkSprite",doubles?4:2,2,script.SpriteRecipes[Templates[kind]],args,owner):(SourceBattleAnimation.VisualSprite)Nested("ElementalSprite",doubles?4:2,2,script.SpriteRecipes[Templates[kind]],new AnimationSlot(target),args,owner);
            if(evidence=="legal-source"){if(kind==0)Assert.AreNotEqual(0,args[3]);if(kind==2)Assert.AreNotEqual(0,args[7]&32767);}
            else{Assert.IsTrue(kind is 0 or 2);if(kind==0)Assert.AreEqual(0,args[3]);else Assert.AreEqual(0,args[7]&32767);}
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=row.GetProperty("frame").GetInt32();string why=$"{evidence} case{cases} kind{kind} actor{actor} target{target} frame{tick}";
                if(tick>0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,!sprite.Finished,why);
                CollectionAssert.AreEqual(row.GetProperty("args").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),registers.Take(8).ToArray(),why);
                var f=sprite.Frame;var p=row.GetProperty("position");Assert.AreEqual(new PixelPoint(p[0].GetInt32(),p[1].GetInt32()),f.Position,why);Assert.AreEqual(new PixelPoint(p[2].GetInt32(),p[3].GetInt32()),f.Offset,why);Assert.AreEqual(row.GetProperty("tile").GetInt32(),f.Tile,why);Assert.AreEqual(row.GetProperty("visible").GetInt32()!=0,f.Visible,why);
                Assert.AreEqual(row.GetProperty("priority").GetInt32(),f.OamPriority,why);
                if(kind==3)
                {
                    var m=row.GetProperty("matrix").EnumerateArray().Select(v=>v.GetInt32()).ToArray();CollectionAssert.AreEqual(m,new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why);
                }
                else
                {
                    var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(row.GetProperty("xScale").GetInt32(),affine.XScale,why);Assert.AreEqual(row.GetProperty("yScale").GetInt32(),affine.YScale,why);Assert.AreEqual(row.GetProperty("rotation").GetInt32(),affine.Rotation,why);Assert.AreEqual(row.GetProperty("affineEnded").GetInt32()!=0,affine.Ended,why);
                    if(kind<2)Assert.AreEqual(row.GetProperty("animEnded").GetInt32()!=0,Field<SourceSpriteFrameProgram>(sprite,"_frames").Ended,why);
                }
                if(kind==1)CollectionAssert.AreEqual(row.GetProperty("data").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),Field<short[]>(sprite,"_d"),why);
                if(evidence=="approved-policy")Assert.IsTrue(f.Visible,why);
                rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(expectedCases,cases,"Missing policy/source evidence must fail.");Assert.IsTrue(rows>1000);
    }

    [TestMethod,DataRow(124),DataRow(188),DataRow(209)]
    public void CompleteRootsUseSharedMaterialsSceneAndCanonicalComposition(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2,actor^3}:new[]{actor^1})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(resolved.Script,Cue(id,actor,target),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);int ticks=0;bool pixels=false;var templates=new HashSet<string>();
            while(owner.Active&&ticks++<600)
            {
                var frame=owner.Tick();var before=JsonSerializer.Serialize(owner.Snapshot());var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(owner.Snapshot()));
                if(frame.Sprites.Any(s=>s.Visible&&Templates.Contains(s.Template)))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>!Templates.Contains(s.Template)).ToArray()}));
                foreach(var s in frame.Sprites){templates.Add(s.Template);if(s.Template==Templates[2]&&id==209)Assert.IsTrue(s.Visible);}
            }
            AssertClean(owner);Assert.AreEqual(0,scene.Count);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);Assert.IsNull(Field<SourceVisualRandom?>(owner,"_visualRandom"));
            foreach(string expected in id==124?new[]{Templates[0]}:id==188?Templates.Take(2):Templates.Skip(2))Assert.IsTrue(templates.Contains(expected),expected);
        }
    }
    private static void AssertClean(SourceBattleAnimation owner)
    {
        Assert.IsFalse(owner.Active);Assert.IsFalse(owner.GraphicsLoaded);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.HasResidualSprites);var f=owner.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.Offsets.All(p=>p==default));
    }
    [TestMethod,DataRow(124),DataRow(188),DataRow(209)]
    public void CancellationAtEachTickRestoresSourceVisibilityAndReleasesOwners(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(id);var bodies=Bodies(true,11);int duration=0;var baseline=new SourceBattleAnimation(r.Script,Cue(id,0,1),bodies,true,new Audio(),Terrain(resources));while(baseline.Active&&duration++<600)baseline.Tick();AssertClean(baseline);Assert.IsTrue(duration>20);
        for(int stop=0;stop<=duration;stop++)
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,Cue(id,0,1),bodies,true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var visibility=JsonSerializer.Serialize(o.Snapshot().BattlerVisibility);for(int i=0;i<stop&&o.Active;i++)o.Tick();int cues=audio.Cues.Count;o.Cancel();o.Cancel();AssertClean(o);Assert.AreEqual(0,scene.Count);Assert.AreEqual(visibility,JsonSerializer.Serialize(o.Snapshot().BattlerVisibility));Assert.AreEqual(cues,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);
        }
    }
    [TestMethod]
    public void MalformedNewRecipesAndArgumentsFailBeforeAudio()
    {
        int[][] operands=[[20,0,40,0],[42,27,20],[],[32,24,190,12,0,1,0]];
        foreach(int kind in new[]{0,1,3})
        {
            int id=kind<2?188:209;var source=AnimationCompositionFixture.Script(id);var command=C("createsprite",new[]{Templates[kind],"1","2"}.Concat(operands[kind].Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray());var primitive=Primitive(source) with{Commands=[command,C("end")]};var r=source.SpriteRecipes[Templates[kind]];
            void Reject(SourceAnimationScript s){var a=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(id,0,1),Bodies(false),false,a));Assert.AreEqual(0,a.Cues.Count);}
            foreach(var bad in new[]{r with{Callback="wrong"},r with{Asset="impact"},r with{Width=32},r with{Blend=true},r with{AffineDoubleSize=!r.AffineDoubleSize},r with{Animations=[]},r with{LoopStarts=[9]},r with{Animations=[[new(4,1,false,false)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[r.Template]=bad;Reject(primitive with{SpriteRecipes=recipes});}
            foreach(var args in new[]{command.Args[..^1],command.Args.Concat(new[]{"0"}).ToArray(),command.Args.Select((v,i)=>i==3?"32768":v).ToArray()})Reject(primitive with{Commands=[command with{Args=args},C("end")]});
            if(kind<2){var args=command.Args.ToArray();args[5]="0";Reject(primitive with{Commands=[command with{Args=args},C("end")]});}
            else{var args=command.Args.ToArray();args[7]="4";Reject(primitive with{Commands=[command with{Args=args},C("end")]});}
        }
    }
    [TestMethod]
    public void ExcessiveAffineBirthsAndUnreviewedEmittersFailBeforePlayback()
    {
        var source=AnimationCompositionFixture.Script(188);var sprite=C("createsprite",Templates[0],"1","2","20","0","40","0");
        var crowded=Primitive(source) with{Commands=[..Enumerable.Repeat(sprite,29),C("end")]};
        var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(crowded,Cue(188,0,1),Bodies(false),false,audio));Assert.AreEqual(0,audio.Cues.Count);
        var emitter=Primitive(source) with{Commands=[sprite,C("createvisualtask","AnimTask_ElectricBolt","5","0","-44","0"),C("end")]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(emitter,Cue(188,0,1),Bodies(false),false,audio));Assert.AreEqual(0,audio.Cues.Count);
    }
    [TestMethod]
    public void ApprovedSludgeFrameSelectionDoesNotChangeTheLegalModeMotionOrLifetime()
    {
        var s=AnimationCompositionFixture.Script(124);var c=s.Commands.Single(c=>c.Op=="createsprite"&&c.Args[0]==Templates[0]);var args=c.Args.ToArray();args[6]="1";var legal=s with{Commands=s.Commands.Select(x=>ReferenceEquals(x,c)?c with{Args=args}:x).ToArray()};
        var safe=new SourceBattleAnimation(s,Cue(124,0,1),Bodies(false),false,new Audio());var original=new SourceBattleAnimation(legal,Cue(124,0,1),Bodies(false),false,new Audio());int ticks=0;
        while(safe.Active&&ticks++<600){var a=safe.Tick();var b=original.Tick();CollectionAssert.AreEqual(a.Sprites,b.Sprites);Assert.AreEqual(safe.ActiveVisualCount,original.ActiveVisualCount);}
        AssertClean(safe);AssertClean(original);
    }
}
