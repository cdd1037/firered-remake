using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ProjectileAdapterAnimationTests
{
    private static readonly (int Move,string Template,string Asset)[] Families=[(49,"gSonicBoomSpriteTemplate","air-wave"),(127,"gSmallDriftingBubblesSpriteTemplate","small-bubbles"),(155,"gBonemerangSpriteTemplate","bone")];
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target,uint? seed=1)=>new("projectile-adapters",1,move,new(actor),new(target)){VisualSeed=seed,MoveTurn=move==155?(byte)0:null};
    private static SourceAnimationScript Primitive(int kind,params int[] args)
    {
        var family=Families[kind];var source=AnimationCompositionFixture.Script(family.Move);
        return source with{Commands=[new("loadspritegfx",["ANIM_TAG_"+family.Asset.ToUpperInvariant().Replace('-','_')],1),
            new("createsprite",new[]{family.Template,"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),2),new("waitforvisualfinish",[],3),new("end",[],4)]};
    }
    private static uint? RandomState(SourceBattleAnimation runtime)
    {
        var stream=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(runtime);
        return stream is null?null:(uint)typeof(SourceVisualRandom).GetField("_state",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(stream)!;
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)]
    public void SourceCallbacksMatchEveryTickIncludingSignedArithmeticAndExactHandoffs(int kind)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/projectile-adapter-source-vectors.json")));
        var root=doc.RootElement;var family=Families[kind];int count=0;
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();if(key[0]!=kind)continue;count++;
            uint seed=1;int[] args=[];
            if(kind==0)args=root.GetProperty("sonic")[key[3]].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            else if(kind==1){var config=root.GetProperty("bubbles")[key[3]];seed=config[0].GetUInt32();args=[config[1].GetInt32(),config[2].GetInt32()];}
            var runtime=new SourceBattleAnimation(Primitive(kind,args),Cue(family.Move,key[1],key[2],seed),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            int visible=0;
            foreach(var row in item.Value.EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                var frame=runtime.Tick();string message=item.Name+" tick "+v[0];
                CollectionAssert.AreEqual(v[14..18],frame.AnimationArguments.Take(4).Select(x=>(int)x).ToArray(),message);
                if(v[5]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                visible++;var sprite=frame.Sprites.Single();Assert.AreEqual(family.Template,sprite.Template,message);Assert.AreEqual(family.Asset,sprite.Asset,message);
                Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                Assert.AreEqual(v[7],sprite.Tile,message);CollectionAssert.AreEqual(v[9..13],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual(kind!=1,sprite.Affine);Assert.AreEqual(kind==0,sprite.DoubleSize);Assert.AreEqual(kind==0,sprite.Blend);
                Assert.AreEqual(kind==1?8:32,sprite.Width);Assert.AreEqual(kind==0?16:kind==1?8:32,sprite.Height);
            }
            Assert.AreEqual(kind==0?args[4]+1:kind==1?20:41,visible);
            Assert.AreEqual(kind==1?(uint?)root.GetProperty("finalStates").GetProperty(item.Name).GetUInt32():null,RandomState(runtime));
            int ticks=0;while(runtime.Active&&ticks++<10)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
        Assert.AreEqual(kind==0?64:kind==1?208:16,count,"Empty or partial vector selections must fail.");
    }
    [TestMethod][DataRow(49,48)][DataRow(127,360)][DataRow(155,41)]
    public void CompleteRootsComposeRealMaterialsPreserveAuthoredMultiplicityAndReplay(int move,int expectedFrames)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var family=Families.Single(f=>f.Move==move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(move,actor,target);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());
            int ticks=0,particleFrames=0;bool pixels=false;
            while(runtime.Active&&ticks++<500)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                var before=RandomState(runtime);runtime.Snapshot();runtime.Snapshot();var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,RandomState(runtime),"Snapshots/composition cannot consume visual RNG.");
                particleFrames+=frame.Sprites.Count(s=>s.Template==family.Template);
                if(frame.Sprites.Any(s=>s.Template==family.Template))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=family.Template).ToArray()}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(expectedFrames,particleFrames);Assert.IsTrue(pixels);
            Assert.AreEqual(move==49?3:move==127?8:1,audio.Cues.Count(c=>c.Id==(move==49?"SE_M_RAZOR_WIND2":move==127?"SE_M_CRABHAMMER":"SE_M_BONEMERANG")));
            AssertClean(runtime);
        }
    }
    private static void AssertClean(SourceBattleAnimation runtime)
    {
        var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
    }
    [TestMethod][DataRow(49)][DataRow(127)][DataRow(155)]
    public void RepeatedCancellationAndReplacementControllerReleaseSceneResources(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var battlers=Battlers(true);var cue=Cue(move,3,1);var baseline=new SourceBattleAnimation(source,cue,battlers,true,new Audio());int duration=0;
        while(baseline.Active&&duration<500){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,4,10,20,25,40,60,90,duration-1,duration,duration+5}.Distinct())
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(source,cue,battlers,true,audio,spriteScene:scene);
            int reached=0;while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(requested,duration),reached);
            int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();AssertClean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsNull(RandomState(runtime));Assert.AreEqual(sounds,audio.Cues.Count);
            var next=new SourceBattleAnimation(source,cue,battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(next.Active&&ticks++<500)next.Tick();AssertClean(next);Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod]
    public void MissingSeedRecipeAndInvalidSourceWidthsFailClosed()
    {
        void Reject(SourceAnimationScript source,CommittedMoveAnimation cue)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,cue,Battlers(true),true,new Audio()));
        Reject(Primitive(1,0,20),Cue(127,0,1,null));Reject(Primitive(1,32768,20),Cue(127,0,1));Reject(Primitive(1,0,-32769),Cue(127,0,1));
        foreach(int duration in new[]{0,-1,256})Reject(Primitive(0,16,0,0,0,duration),Cue(49,0,1));Reject(Primitive(0,128,0,0,0,15),Cue(49,0,1));
        foreach(int kind in Enumerable.Range(0,3))
        {
            var source=Primitive(kind,kind==0?[16,0,0,0,15]:kind==1?[0,20]:[]);var family=Families[kind];var recipe=source.SpriteRecipes[family.Template];
            foreach(var bad in new[]{recipe with{Callback="UnknownProjectile"},recipe with{Width=64},recipe with{Blend=!recipe.Blend},recipe with{LoopStarts=[-1]},recipe with{Animations=[[new(1,1,false,false)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[family.Template]=bad;Reject(source with{SpriteRecipes=recipes},Cue(family.Move,0,1));}
            Reject(source with{SineTable=[]},Cue(family.Move,0,1));var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(family.Template);Reject(source with{SpriteRecipes=missing},Cue(family.Move,0,1));
        }
    }
}
