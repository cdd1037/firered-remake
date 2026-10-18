using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ImpactEmissionAnimationTests
{
    private static readonly (int Move,string Template,string Asset)[] Families=[
        (238,"gCrossChopHandSpriteTemplate","hands-and-feet"),(238,"gCrossImpactSpriteTemplate","cross-impact"),
        (6,"gCoinThrowSpriteTemplate","coin"),(6,"gFallingCoinSpriteTemplate","coin"),
        (65,"gFlashingHitSplatSpriteTemplate","impact"),(140,"gBarrageBallSpriteTemplate","red-ball")];
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("impact-emission",1,move,new(actor),new(target));
    private static SourceAnimationScript Primitive(int kind,params int[] args)
    {
        var family=Families[kind];var source=AnimationCompositionFixture.Script(family.Move);
        SourceAnimationCommand create=kind<4?new SourceAnimationCommand("createsprite",new[]{family.Template,"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),2):
            new("createvisualtask",[kind==4?"AnimTask_DrillPeckHitSplats":"AnimTask_BarrageBall","5"],2);
        return source with{Commands=[new("loadspritegfx",["ANIM_TAG_"+family.Asset.ToUpperInvariant().Replace('-','_')],1),create,new("waitforvisualfinish",[],3),new("end",[],4)]};
    }
    [TestMethod][DataRow(0,80)][DataRow(1,80)][DataRow(2,60)][DataRow(3,20)][DataRow(4,20)][DataRow(5,20)]
    public void ExtractedCallbacksMatchEveryTickIncludingCountedAndRawChildren(int kind,int expectedCases)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/impact-emission-source-vectors.json")));
        var root=doc.RootElement;var family=Families[kind];int cases=0,rows=0,peak=0;bool deferredChild=false,lowerReuse=false;int priorNewSlot=-1;
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();if(key[0]!=kind)continue;cases++;
            int[] args=root.GetProperty("configs")[kind][key[4]].EnumerateArray().Select(v=>v.GetInt32()).ToArray();bool doubles=key[1]!=0;
            var runtime=new SourceBattleAnimation(Primitive(kind,args),Cue(family.Move,key[2],key[3]),Battlers(doubles),doubles,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            var previousSlots=new HashSet<int>();priorNewSlot=-1;
            foreach(var row in item.Value.EnumerateArray())
            {
                var state=row.GetProperty("state").EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(state[0]<0)continue;rows++;
                var frame=runtime.Tick();var actual=frame.Sprites.OrderBy(s=>s.Slot).ToArray();var expected=row.GetProperty("sprites").EnumerateArray().ToArray();string message=item.Name+" tick "+state[0];
                Assert.AreEqual(expected.Length,actual.Length,message);Assert.AreEqual(state[1],runtime.ActiveVisualCount,message);
                CollectionAssert.AreEqual(state[5..10],frame.AnimationArguments.Take(5).Select(v=>(int)v).ToArray(),message);
                for(int i=0;i<expected.Length;i++)
                {
                    var v=expected[i].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var sprite=actual[i];
                    Assert.AreEqual(v[0],sprite.Slot,message);Assert.AreEqual(family.Template,sprite.Template,message);Assert.AreEqual(family.Asset,sprite.Asset,message);
                    Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                    Assert.AreEqual(v[5]==0,sprite.Visible,message);Assert.AreEqual(v[6],sprite.Tile,message);Assert.AreEqual(v[7]!=0,sprite.HFlip,message);Assert.AreEqual(v[8]!=0,sprite.VFlip,message);Assert.AreEqual(v[9],sprite.Subpriority,message);
                    // The shared legacy impact adapter exports its exact scale;
                    // the compositor's AffineImpact turns it into this matrix.
                    var matrix=kind==4?new[]{65536/sprite.Scale,0,0,65536/sprite.Scale}:new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD};
                    CollectionAssert.AreEqual(v[10..14],matrix,message);Assert.AreEqual(v[14]!=0,kind==4||sprite.Affine,message);
                    Assert.AreEqual(kind==1,sprite.Blend,message);Assert.IsFalse(sprite.DoubleSize);Assert.AreEqual(kind is 2 or 3?16:32,sprite.Width);Assert.AreEqual(sprite.Width,sprite.Height);
                    if(kind==4&&!previousSlots.Contains(sprite.Slot)){if(priorNewSlot>=0)lowerReuse|=sprite.Slot<priorNewSlot;priorNewSlot=sprite.Slot;}
                }
                previousSlots=actual.Select(s=>s.Slot).ToHashSet();peak=Math.Max(peak,state[4]);
                if(kind==4)deferredChild|=state[4]>state[3];
                if(kind==5)Assert.AreEqual(state[2],state[1],"Raw ball never contributes a second visual count.");
                Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(runtime.Snapshot()));
            }
            int ticks=0;while(runtime.Active&&ticks++<10)runtime.Tick();AssertClean(runtime);
        }
        Assert.AreEqual(expectedCases,cases);Assert.IsTrue(rows>0);Assert.IsTrue(peak<=4,"Ordinary roots plus four battler matrices stay below source32.");
        if(kind==4){Assert.IsTrue(deferredChild,"Task emissions enter OAM on the next sprite phase.");Assert.IsTrue(lowerReuse,"First-free source slots are reused below a surviving child.");}
        if(kind==5)Assert.AreEqual(1,peak);
    }
    [TestMethod][DataRow(6)][DataRow(65)][DataRow(140)][DataRow(238)]
    public void CompleteRootsUseRealMaterialsAudioAndRestoreAllOwnedResources(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var templates=Families.Where(f=>f.Move==move).Select(f=>f.Template).ToHashSet();
        string[] expectedAssets=move switch{6=>["coin","impact"],65=>["impact","whirlwind-lines"],140=>["impact","red-ball"],_=>["impact","hands-and-feet","cross-impact"]};
        CollectionAssert.AreEquivalent(expectedAssets,resolved.Assets.Keys.ToArray());Assert.IsNull(resolved.Metallic);Assert.IsNull(resolved.StatMask);
        if(move==65){CollectionAssert.AreEqual(new[]{"whirlwind-lines"},resolved.Script.MaterialDependencies);Assert.AreEqual(16,resolved.Assets["whirlwind-lines"].Palette!.Length);}
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(move,actor,target);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0;bool pixels=false,hidden=false,blended=false,affine=false;var seen=new HashSet<string>();
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));var before=JsonSerializer.Serialize(runtime.Snapshot());var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
                var sprites=frame.Sprites.Where(s=>templates.Contains(s.Template)).ToArray();foreach(var sprite in sprites){seen.Add(sprite.Template);Assert.IsNotNull(sprite.Material);}
                hidden|=sprites.Any(s=>!s.Visible);blended|=frame.BlendEnabled;affine|=sprites.Any(s=>s.Affine||s.Scale!=256);
                if(sprites.Any(s=>s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>!templates.Contains(s.Template)).ToArray()}));
            }
            AssertClean(runtime);AssertClean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(pixels);CollectionAssert.AreEquivalent(templates.ToArray(),seen.ToArray());
            Assert.AreEqual(move is 65 or 140,hidden);Assert.AreEqual(move is 6 or 238,blended);Assert.AreEqual(move!=238,affine);
            Dictionary<string,int> expectedSounds=move switch{6=>new Dictionary<string,int>{{"SE_M_RAZOR_WIND2",1},{"SE_M_PAY_DAY",1}},65=>new(){{"SE_M_HEADBUTT",1},{"SE_M_HORN_ATTACK",8}},140=>new(){{"SE_M_SWAGGER",1},{"SE_M_STRENGTH",2}},_=>new(){{"SE_M_MEGA_KICK",1},{"SE_M_RAZOR_WIND",1}}};
            Assert.AreEqual(expectedSounds.Values.Sum(),audio.Cues.Count);foreach(var pair in expectedSounds)Assert.AreEqual(pair.Value,audio.Cues.Count(c=>c.Id==pair.Key));
        }
    }
    [TestMethod][DataRow(6)][DataRow(65)][DataRow(140)][DataRow(238)]
    public void CancellationAtEveryTickAndSceneReplacementDoNotLeakChildren(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var battlers=Battlers(true);var cue=Cue(move,3,1);var baseline=new SourceBattleAnimation(source,cue,battlers,true,new Audio());int duration=0;
        while(baseline.Active&&duration<400){baseline.Tick();duration++;}AssertClean(baseline);Assert.IsTrue(duration>30);
        foreach(int requested in Enumerable.Range(0,duration+1).Append(duration+5))
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(source,cue,battlers,true,new Audio(),spriteScene:scene);int reached=0;
            while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(requested,duration),reached);runtime.Cancel();runtime.Cancel();AssertClean(runtime);Assert.AreEqual(0,scene.Count);
            var next=new SourceBattleAnimation(source,cue,battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(next.Active&&ticks++<400)next.Tick();AssertClean(next);Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod]
    public void MissingRecipesMutationsAndUnboundedEmitterCompositionFailClosed()
    {
        int[][] args=[[0,0,0],[0,0,1,20],[20,0,0,0,1152],[],[],[]];
        for(int kind=0;kind<6;kind++)
        {
            var source=Primitive(kind,args[kind]);var family=Families[kind];var recipe=source.SpriteRecipes[family.Template];
            void Reject(SourceAnimationScript script)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(family.Move),Battlers(false),false,new Audio()));
            var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(family.Template);Reject(source with{SpriteRecipes=missing});
            foreach(var bad in new[]{recipe with{Callback="UnknownImpactEmission"},recipe with{Asset="unknown"},recipe with{Width=8},recipe with{Height=8},recipe with{Blend=!recipe.Blend},recipe with{AffineDoubleSize=true},recipe with{LoopStarts=[99]},recipe with{Animations=[[new(1,1,false,false)]]},recipe with{AffineAnimations=[[new("end",0,0,0,0,0)]]},recipe with{FrameLoops=[[new(1,0,1)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[family.Template]=bad;Reject(source with{SpriteRecipes=recipes});}
            if(kind>=4)
            {
                Reject(source with{SpriteDependencies=[]});var commands=source.Commands.ToList();commands.Insert(2,source.Commands[1]);Reject(source with{Commands=commands.ToArray()});
                commands=source.Commands.ToList();commands.Insert(2,new("createsprite",["gBasicHitSplatSpriteTemplate","ANIM_TARGET","2","0","0","1","0"],9));Reject(source with{Commands=commands.ToArray()});
                Reject(source with{SineTable=[]});
            }
        }
        foreach(int speed in new[]{0,-1,32768})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Primitive(2,20,0,0,0,speed),Cue(6),Battlers(false),false,new Audio()));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Primitive(2,0,0,0,0,256),Cue(6,0,0),Battlers(false),false,new Audio()));
        using var resources=new SourceAnimationResources(Folder);var drill=resources.Resolve(65).Script;
        Assert.Throws<InvalidDataException>(()=>resources.Resolve(drill with{MaterialDependencies=["missing-loaded-art"]}));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(drill with{MaterialDependencies=[]},Cue(65),Battlers(false),false,new Audio()));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(drill with{SpritePalettes=new Dictionary<int,ushort[]>()},Cue(65),Battlers(false),false,new Audio()));
        var materials=resources.Resolve(65).Assets.ToDictionary(p=>p.Key,p=>p.Value);materials["whirlwind-lines"]=materials["whirlwind-lines"] with{Palette=[]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(drill,Cue(65),Battlers(false),false,new Audio(),spriteMaterials:materials));
    }
    [TestMethod][DataRow(4)][DataRow(5)]
    public void RetainedAffineOffChildrenKeepTheirOwnerAcrossEmitterCancellation(int kind)
    {
        var source=AnimationCompositionFixture.Script(84);var battlers=Battlers(true);var scene=new SourceBattleAnimation.SpriteScene();
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SPARK"],1),new("createvisualtask",["AnimTask_ElectricBolt","5","0","-44","0"],2),new("waitforvisualfinish",[],3),new("end",[],4)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var old=new SourceBattleAnimation(script,Cue(84),battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(old.Active&&ticks++<40)old.Tick();
        Assert.IsFalse(old.Active);Assert.IsTrue(old.HasResidualSprites);int retained=scene.Count;Assert.IsTrue(retained>0);
        var current=new SourceBattleAnimation(Primitive(kind),Cue(Families[kind].Move),battlers,true,new Audio(),spriteScene:scene);
        for(int i=0;i<4;i++)current.Tick();Assert.IsTrue(old.HasResidualSprites);Assert.IsTrue(current.ActiveVisualCount>0);
        current.Cancel();Assert.AreEqual(0,current.ActiveVisualCount);Assert.IsFalse(current.HasResidualSprites);Assert.IsTrue(old.HasResidualSprites);Assert.IsTrue(scene.Count<=retained);
        ticks=0;while(old.HasResidualSprites&&ticks++<40)scene.TickIdle(battlers,true);Assert.AreEqual(0,scene.Count);Assert.IsFalse(old.HasResidualSprites);
    }
    private static void AssertClean(SourceBattleAnimation runtime)
    {
        var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsFalse(runtime.HasResidualSprites);
    }
}
