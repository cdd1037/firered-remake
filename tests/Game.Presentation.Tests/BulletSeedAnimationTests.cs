using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BulletSeedAnimationTests
{
    private const string Template="gBulletSeedSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Cues{get;}=[];public Action<SourceAnimationSound>? OnPlay{get;set;}
        public bool IsPlaying=>false;public void Play(SourceAnimationSound sound){OnPlay?.Invoke(sound);Cues.Add(sound);}public void StopAnimationEffects(){}
    }
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script()=>AnimationCompositionFixture.Script(331);
    private static CommittedMoveAnimation Cue(int actor,int target,uint? seed)=>new("bullet-seed",1,331,new(actor),new(target)){VisualSeed=seed,MoveTurn=0};
    private static SourceAnimationScript Particle(SourceAnimationScript source,int x=20,int y=0,int unused2=0,int unused3=0)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_SEED"],1),new("setarg",["2",unused2.ToString(CultureInfo.InvariantCulture)],2),new("setarg",["3",unused3.ToString(CultureInfo.InvariantCulture)],3),new("setarg",["7","777"],4),
        new("createsprite",[Template,"ANIM_TARGET","2",x.ToString(CultureInfo.InvariantCulture),y.ToString(CultureInfo.InvariantCulture)],5),new("waitforvisualfinish",[],6),new("end",[],7)]};
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/bullet-seed-source-vectors.json")));
    // Observe the private stream without adding a production telemetry/rollback API.
    // Null proves cancellation before impact did not even initialize the lazy RNG.
    private static uint? RandomState(SourceBattleAnimation runtime)
    {
        var field=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(field);
        if(field.GetValue(runtime) is not SourceVisualRandom random)return null;
        var state=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(state);
        return (uint)state.GetValue(random)!;
    }
    private static SourceAnimationFrame FirstParticle(SourceBattleAnimation runtime)
    {
        for(int i=0;i<10;i++){var frame=runtime.Tick();if(frame.Sprites.Length!=0)return frame;}
        throw new InvalidOperationException("Source particle never appeared.");
    }

    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void LinearHandoffImpactDrawsAndPausedAffineMatchExtractedSource(int actor)
    {
        using var doc=Vectors();var source=Script();var configs=doc.RootElement.GetProperty("configs").EnumerateArray().ToArray();Assert.IsTrue(configs.Length>=17);
        for(int target=0;target<4;target++)for(int config=0;config<configs.Length;config++)
        {
            var input=configs[config].EnumerateArray().ToArray();uint seed=input[0].GetUInt32();int x=input[1].GetInt32(),y=input[2].GetInt32();
            if(x is < -128 or >127||y is < -128 or >127)continue; // Source-only wide-word probes are outside the reviewed operand admission.
            int unused2=input[3].GetInt32(),unused3=input[4].GetInt32();string key=$"{actor}:{target}:{config}";
            var observation=doc.RootElement.GetProperty("observations").GetProperty(key).EnumerateArray().Select(v=>v.GetInt64()).ToArray();
            var audio=new Audio();var runtime=new SourceBattleAnimation(Particle(source,x,y,unused2,unused3),Cue(actor,target,seed),Battlers(true),true,audio);
            audio.OnPlay=sound=>{Assert.IsNull(RandomState(runtime),"Impact sound must precede both source RNG draws.");Assert.AreEqual((int)observation[1],source.SoundEffects[sound.Id]);};
            var frame=FirstParticle(runtime);int tick=0,firstFrame=frame.Frame,visible=1;
            foreach(var row in doc.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();if(v[0]<0)continue;
                while(tick<v[0]){frame=runtime.Tick();tick++;visible+=frame.Sprites.Length;}
                string message=$"{key} tick{tick}";
                CollectionAssert.AreEqual(new[]{x,y,unused2,unused3},frame.AnimationArguments.Take(4).Select(n=>(int)n).ToArray(),message);Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);
                Assert.AreEqual(v[19],audio.Cues.Count,message);
                if(v[18]==0)Assert.IsNull(RandomState(runtime),message);else Assert.AreEqual((uint)observation[8],RandomState(runtime),message);
                if(v[5]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                var matrix=SourceAffineMatrix.FromScaleRotation(256,256,v[16]);
                CollectionAssert.AreEqual(new[]{matrix.A,matrix.B,matrix.C,matrix.D},new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual("seed",sprite.Asset);Assert.AreEqual(16,sprite.Width);Assert.AreEqual(16,sprite.Height);Assert.AreEqual(0,sprite.Tile);
                Assert.IsTrue(sprite.Visible);Assert.IsTrue(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);
            }
            Assert.AreEqual(37,visible);Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual(firstFrame+21,audio.Cues[0].Frame);Assert.AreEqual((int)observation[2],audio.Cues[0].Pan);
            while(runtime.Active)runtime.Tick();Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
        }
    }

    [TestMethod]
    public void TenSourceParticlesConsumeTwentyImpactDrawsAndRetainTheEleventhContinuation()
    {
        using var doc=Vectors();var source=Script();
        foreach(var seedCase in doc.RootElement.GetProperty("primaryRng").EnumerateObject())
        {
            uint seed=uint.Parse(seedCase.Name,CultureInfo.InvariantCulture);var rows=seedCase.Value.EnumerateArray().ToArray();Assert.AreEqual(11,rows.Length);
            var commands=new List<SourceAnimationCommand>{new("loadspritegfx",["ANIM_TAG_SEED"],1)};
            SourceAnimationCommand Create()=>new("createsprite",[Template,"ANIM_TARGET","2","20","0"],2);
            for(int i=0;i<10;i++)commands.Add(Create());commands.Add(new("waitforvisualfinish",[],3));commands.Add(Create());commands.Add(new("waitforvisualfinish",[],4));commands.Add(new("end",[],5));
            var audio=new Audio();var runtime=new SourceBattleAnimation(source with{Commands=commands.ToArray()},Cue(0,1,seed),Battlers(true),true,audio);
            audio.OnPlay=_=>
            {
                int particle=audio.Cues.Count;
                if(particle==0)Assert.IsNull(RandomState(runtime));else Assert.AreEqual(rows[particle-1][3].GetUInt32(),RandomState(runtime),"Each impact consumes exactly two draws after its sound.");
            };
            int ticks=0,particleFrames=0;
            while(runtime.Active&&ticks++<150){var frame=runtime.Tick();runtime.Snapshot();runtime.Snapshot();particleFrames+=frame.Sprites.Length;}
            Assert.IsFalse(runtime.Active);Assert.AreEqual(11,audio.Cues.Count);Assert.AreEqual(11*37,particleFrames);Assert.AreEqual(rows[10][3].GetUInt32(),RandomState(runtime));
            Assert.IsTrue(audio.Cues.Take(10).All(c=>c.Frame==audio.Cues[0].Frame));Assert.IsTrue(audio.Cues[10].Frame>audio.Cues[9].Frame);
        }
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void AuthoredRootUsesTenParticlesPerCommittedHitAndStableIdentityReplay(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(331);
        Assert.AreEqual(10,resolved.Script.Commands.Count(c=>c.Op=="createsprite"&&c.Args[0]==Template));Assert.AreEqual(159,resolved.Script.SoundEffects["SE_M_HORN_ATTACK"]);
        for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
            var cue=Cue(actor,target,null);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var other=cue with{Sequence=2,MoveTurn=1};other=other with{VisualSeed=SourceAnimationVisualSeed.For(other)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());var changed=new SourceBattleAnimation(resolved.Script,other,battlers,doubles,new Audio());
            int ticks=0,particleFrames=0;bool pixels=false,different=false;
            while(runtime.Active&&ticks++<200)
            {
                var frame=runtime.Tick();var repeat=replay.Tick();var otherFrame=changed.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(repeat));
                runtime.Snapshot();runtime.Snapshot();var rgba=composer.ComposeField(frame);composer.ComposeField(frame);
                particleFrames+=frame.Sprites.Count(s=>s.Template==Template);different|=!frame.Sprites.Select(s=>s.Offset).SequenceEqual(otherFrame.Sprites.Select(s=>s.Offset));
                if(frame.Sprites.Length>0)pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsFalse(changed.Active);Assert.AreEqual(10*37,particleFrames);Assert.IsTrue(pixels);Assert.IsTrue(different);Assert.AreEqual(10,audio.Cues.Count);
            Assert.IsTrue(audio.Cues.All(c=>c.Id=="SE_M_HORN_ATTACK"));Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
        }
    }

    [TestMethod]
    public void CancellationBeforeImpactConsumesNoRandomnessOrSoundAndLaterStagesCleanUp()
    {
        var source=Particle(Script());var cue=Cue(3,1,42);var battlers=Battlers(true);var baseline=new SourceBattleAnimation(source,cue,battlers,true,new Audio());var frames=new List<string>();
        while(baseline.Active&&frames.Count<100)frames.Add(JsonSerializer.Serialize(baseline.Tick()));Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,15,23,24,25,26,30,frames.Count-1,frames.Count,frames.Count+5}.Distinct())
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(source,cue,battlers,true,audio);int reached=0;
            while(runtime.Active&&reached<requested){Assert.AreEqual(frames[reached],JsonSerializer.Serialize(runtime.Tick()));reached++;}
            Assert.AreEqual(Math.Min(frames.Count,requested),reached);Assert.AreEqual(requested<frames.Count,runtime.Active);uint? before=RandomState(runtime);
            if(requested<=24){Assert.IsNull(before);Assert.AreEqual(0,audio.Cues.Count);}
            int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();Assert.IsNull(RandomState(runtime),"Cancellation releases the animation-local stream.");Assert.AreEqual(sounds,audio.Cues.Count,"Cancellation must not emit another impact sound.");Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        }
        var fresh=new SourceBattleAnimation(source,cue,battlers,true,new Audio());foreach(string frame in frames)Assert.AreEqual(frame,JsonSerializer.Serialize(fresh.Tick()));Assert.IsFalse(fresh.Active);
    }

    [TestMethod]
    public void MissingSeedCallbackSoundOrIncorrectRecipeFailsClosed()
    {
        var source=Particle(Script());void Reject(SourceAnimationScript script,uint? seed=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(0,1,seed),Battlers(false),false,new Audio()));
        Reject(source,null);Reject(source with{SoundEffects=new Dictionary<string,int>()});Reject(source with{SineTable=[]});
        foreach(var point in new[]{(128,0),(-129,0),(0,128),(0,-129),(32767,-32768)})Reject(Particle(Script(),point.Item1,point.Item2));
        foreach(var bad in new[]{source.SpriteRecipes[Template] with{Width=8},source.SpriteRecipes[Template] with{Blend=true},source.SpriteRecipes[Template] with{AffineDoubleSize=true},source.SpriteRecipes[Template] with{AffineAnimations=[]},source.SpriteRecipes[Template] with{Callback="UnknownSeed"}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=bad;Reject(source with{SpriteRecipes=recipes});}
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(Template);Reject(source with{SpriteRecipes=missing});
    }
}
