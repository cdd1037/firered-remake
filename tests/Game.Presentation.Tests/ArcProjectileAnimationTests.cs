using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ArcProjectileAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static string Template(int move)=>move==42?"gPinMissileSpriteTemplate":"gIcicleSpearSpriteTemplate";
    private static string Asset(int move)=>move==42?"needle":"icicle-spear";
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("arc-projectile",1,move,new(actor),new(target)){MoveTurn=2};
    private static SourceAnimationScript Projectile(SourceAnimationScript source,int[] args)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_"+Asset(source.MoveId).ToUpperInvariant().Replace('-','_')],1),new("setarg",["7","777"],2),
        new("createsprite",new[]{Template(source.MoveId),"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),3),
        new("waitforvisualfinish",[],4),new("end",[],5)]};

    [TestMethod][DataRow(42,0)][DataRow(42,1)][DataRow(42,2)][DataRow(42,3)]
    [DataRow(333,0)][DataRow(333,1)][DataRow(333,2)][DataRow(333,3)]
    public void ArcLookAheadMatchesOriginalEightWordRestoreAndRetainedPositionVectors(int move,int actor)
    {
        using var doc=SourceVectorFixture.Read("arc-projectile-original-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_arc_projectile_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/arc-projectile-original-vectors.json");
        var configs=doc.RootElement.GetProperty("configs").EnumerateArray().ToArray();Assert.AreEqual(16,configs.Length);
        foreach(int target in new[]{actor^1,actor^2})for(int config=0;config<configs.Length;config++)
        {
            int[] args=configs[config].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var runtime=new SourceBattleAnimation(Projectile(Script(move),args),Cue(move,actor,target),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            string key=$"{actor}:{target}:{config}";int visible=0;var offsets=new List<PixelPoint>();
            foreach(var row in doc.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                var frame=runtime.Tick();string message=$"{move}/{key} tick {v[0]}";
                CollectionAssert.AreEqual(v[5..11],frame.AnimationArguments.Take(6).Select(x=>(int)x).ToArray(),message);
                Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);
                if(v[16]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                visible++;var sprite=frame.Sprites.Single();offsets.Add(sprite.Offset);
                Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                CollectionAssert.AreEqual(v[12..16],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual(v[17]==0,sprite.Visible,message);Assert.AreEqual(Template(move),sprite.Template);Assert.AreEqual(Asset(move),sprite.Asset);
                Assert.IsTrue(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);Assert.AreEqual(move==42?16:32,sprite.Width);Assert.AreEqual(sprite.Width,sprite.Height);
                if(args[4]==1)CollectionAssert.AreEqual(new[]{256,0,0,256},new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},"No look-ahead matrix update when the second call reports completion.");
            }
            Assert.AreEqual(args[4],visible,"The first completed translation destroys immediately, without a callback handoff frame.");
            if(args[4]>1)Assert.AreEqual(offsets[^2],offsets[^1],"Penultimate look-ahead retains the final position while data resumes from the prior step.");
            while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod][DataRow(42)][DataRow(333)]
    public void CompleteRootsPreserveThreeAuthoredProjectilesPerCommittedInvocationAndRealPixels(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),battlers,doubles,audio);
            var composer=new SourceBattleFrameComposer(assets,battlers,resolved.StatMask,resolved.Metallic);int ticks=0,projectileFrames=0;bool pixels=false,impact=false;
            while(runtime.Active&&ticks++<500)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                projectileFrames+=frame.Sprites.Count(s=>s.Template==Template(move));
                if(frame.Sprites.Any(s=>s.Template==Template(move)))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Template(move)).ToArray()}));
                impact|=frame.Sprites.Any(s=>s.Asset=="impact");
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(60,projectileFrames);Assert.IsTrue(pixels);Assert.IsTrue(impact);
            Assert.AreEqual(1,audio.Cues.Count(c=>c.Id==(move==42?"SE_M_JUMP_KICK":"SE_M_ICY_WIND")));Assert.AreEqual(3,audio.Cues.Count(c=>c.Id=="SE_M_HORN_ATTACK"));
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
        }
    }
    [TestMethod][DataRow(42)][DataRow(333)]
    public void CancellationAtActiveAndCompletedStagesClearsAllOwnedState(int move)
    {
        var source=Script(move);var baseline=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int duration=0;
        while(baseline.Active&&duration<500){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,15,25,45,duration-1,duration,duration+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int reached=0;
            while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(duration,requested),reached);Assert.AreEqual(requested<duration,runtime.Active);
            runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
        }
    }
    [TestMethod][DataRow(42)][DataRow(333)]
    public void InvalidDurationShapeAmplitudeAndMissingRecipeFailClosed(int move)
    {
        var source=Script(move);string template=Template(move);
        void Reject(SourceAnimationScript script)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(move,0,1),Battlers(false),false,new Audio()));
        foreach(int duration in new[]{0,-1,256,32768})Reject(Projectile(source,[20,-8,-8,-8,duration,-32]));
        Reject(Projectile(source,[128,0,0,0,20,0]));Reject(Projectile(source,[0,-129,0,0,20,0]));Reject(Projectile(source,[0,0,0,0,20,128]));Reject(Projectile(source,[0,0,0,0,20,-129]));
        Reject(Projectile(source,[20,-8,-8,-8,20,-32]) with{SineTable=[]});
        foreach(var bad in new[]{source.SpriteRecipes[template] with{Width=8},source.SpriteRecipes[template] with{Blend=true},source.SpriteRecipes[template] with{AffineDoubleSize=true},source.SpriteRecipes[template] with{Callback="UnknownArc"},source.SpriteRecipes[template] with{Animations=[[new(4,1,false,false)]]},source.SpriteRecipes[template] with{LoopStarts=[-1]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Reject(Projectile(source,[20,-8,-8,-8,20,-32]) with{SpriteRecipes=recipes});}
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(template);Reject(Projectile(source,[20,-8,-8,-8,20,-32]) with{SpriteRecipes=missing});
        Assert.IsTrue(source.SpriteTags.ContainsKey("ANIM_TAG_"+Asset(move).ToUpperInvariant().Replace('-','_')));
    }
}
