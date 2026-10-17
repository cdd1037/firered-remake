using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BoneSpriteAnimationTests
{
    private const string Template="gSpinningBoneSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/bone-sprite-original-vectors.json")));
    private static SourceAnimationScript Projectile(SourceAnimationScript source,int[] args)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_BONE"],1),new("createsprite",new[]{Template,"ANIM_ATTACKER","2"}.Concat(args.Select(v=>v.ToString())).ToArray(),2),
        new("waitforvisualfinish",[],3),new("end",[],4)]};

    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void BoneProjectileMatchesExtractedPositionsArgumentMutationAffineAndCallbackRetirement(int actor)
    {
        using var vectors=Vectors();var source=Script(125);var configs=vectors.RootElement.GetProperty("configs").EnumerateArray().ToArray();
        foreach(int target in new[]{actor^1,actor^2})for(int config=0;config<configs.Length;config++)
        {
            var args=configs[config].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var runtime=new SourceBattleAnimation(Projectile(source,args),new("bone-vectors",1,125,new(actor),new(target)),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            string key=$"{actor}:{target}:{config}";
            foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                var frame=runtime.Tick();Assert.AreEqual(v[7],(int)frame.AnimationArguments[2],key+" tick "+v[0]);
                if(v[13]==0){Assert.AreEqual(0,frame.Sprites.Length);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset);
                CollectionAssert.AreEqual(v[9..13],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD});
                Assert.IsTrue(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.AreEqual(32,sprite.Width);Assert.AreEqual(32,sprite.Height);Assert.AreEqual(0,sprite.Tile);
                Assert.IsFalse(sprite.HFlip);Assert.IsFalse(sprite.VFlip);Assert.AreEqual("bone",sprite.Asset);
            }
            for(int i=0;i<10&&runtime.Active;i++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }

    [TestMethod]
    public void SharedSpriteAffineLoopMatchesOriginalBeginPhaseRotationAndSelectionTick()
    {
        var recipe=Script(125).SpriteRecipes[Template];using var vectors=Vectors();var player=new SourceSpriteAffineProgram(recipe.AffineAnimations.Single());
        foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty("0:1:0").EnumerateArray())
        {
            var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]>=0&&v[13]!=0)player.Step();
            Assert.AreEqual(v[8],player.Rotation);Assert.AreEqual(v[20],player.XScale);Assert.AreEqual(v[21],player.YScale);
            CollectionAssert.AreEqual(v[9..13],new[]{player.Matrix.A,player.Matrix.B,player.Matrix.C,player.Matrix.D});Assert.IsFalse(player.Ended);
        }
    }

    [TestMethod][DataRow(125)][DataRow(198)]
    public void CompleteBoneRootsComposeGeneratedPixelsForEverySlotAllyAndCommittedTurn(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x03e0,4096).ToArray()));
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,new("complete-bone",1,move,new(actor),new(target)){MoveTurn=move==198?(byte)4:null},battlers,doubles,audio);
            var composer=new SourceBattleFrameComposer(assets,battlers,resolved.StatMask,resolved.Metallic);int ticks=0,boneFrames=0;bool sawImpact=false,sawPalette=false,composedBone=false;
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                if(frame.Sprites.Any(s=>s.Asset=="bone"))
                {
                    boneFrames++;
                    var withoutBone=composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Asset!="bone").ToArray()});
                    composedBone|=!rgba.SequenceEqual(withoutBone);
                }
                sawImpact|=frame.Sprites.Any(s=>s.Asset=="impact");sawPalette|=frame.PaletteBlends.Length>0;
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(16,boneFrames);Assert.IsTrue(sawImpact);Assert.IsTrue(composedBone);Assert.AreEqual(move==125,sawPalette);
            CollectionAssert.AreEqual(new[]{"SE_M_BONEMERANG",move==125?"SE_M_VITAL_THROW2":"SE_M_HORN_ATTACK"},audio.Cues.Select(c=>c.Id).ToArray());
            int sourcePan=actor%2==0?(target%2==0?-64:63):(target%2==0?-63:63);Assert.IsTrue(audio.Cues.All(c=>c.Pan==sourcePan));Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.AreEqual(0,final.PaletteBlends.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
        }
    }

    [TestMethod][DataRow(125)][DataRow(198)]
    public void BoneCancellationClearsOwnedSpritesPaletteBackgroundAndLoadedTags(int move)
    {
        foreach(int ticks in new[]{1,5,15,25})
        {
            var runtime=new SourceBattleAnimation(Script(move),new("cancel-bone",1,move,new(3),new(1)){MoveTurn=move==198?(byte)0:null},Battlers(true),true,new Audio());
            for(int i=0;i<ticks;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
            Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,frame.Sprites.Length);
            Assert.AreEqual(0,frame.PaletteBlends.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
        }
    }

    [TestMethod]
    public void BoneRecipeOperandsAndGeneratedDependenciesFailClosed()
    {
        var source=Script(125);var args=new[]{-42,-25,0,0,15};
        void Reject(SourceAnimationScript script)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("bad-bone",1,125,new(0),new(1)),Battlers(false),false,new Audio()));
        foreach(int duration in new[]{0,-1,32768}){args[4]=duration;Reject(Projectile(source,args));}
        args[4]=15;var projectile=Projectile(source,args);
        foreach(var bad in new[]{source.SpriteRecipes[Template] with{AffineAnimations=[]},source.SpriteRecipes[Template] with{AffineDoubleSize=true},source.SpriteRecipes[Template] with{Callback="UnknownBoneCallback"}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=bad;Reject(projectile with{SpriteRecipes=recipes});}
        Reject(source with{SpriteTags=new Dictionary<string,int>()});Reject(source with{SoundEffects=new Dictionary<string,int>()});
    }

    [TestMethod]
    public void PhysicalLoaderPreservesGenericSourceSoundAndTagMetadataWithoutSymbolFallbacks()
    {
        var source=Script(125);Assert.AreEqual(180,source.SoundEffects["SE_M_BONEMERANG"]);Assert.AreEqual(10000,source.SpriteTags["ANIM_TAG_BONE"]);
        var audio=new Audio();var script=source with{Commands=[new("playsewithpan",["SE_M_GENERATED_PROBE","12"],1),new("end",[],2)],SoundEffects=new Dictionary<string,int>{{"SE_M_GENERATED_PROBE",180}}};
        var runtime=new SourceBattleAnimation(script,new("generated-sound",1,125,new(0),new(1)),Battlers(false),false,audio);
        for(int i=0;i<16&&runtime.Active;i++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(new SourceAnimationSound("SE_M_GENERATED_PROBE",12,audio.Cues.Single().Frame),audio.Cues.Single());
        using var cues=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"content/generated/audio/cues.json")));
        var actual=cues.RootElement.GetProperty("cues").EnumerateArray().Single(c=>c.GetProperty("id").GetString()=="SE_M_BONEMERANG");Assert.IsTrue(actual.GetProperty("sourcePlayer").GetInt32() is 1 or 2);
    }
}
