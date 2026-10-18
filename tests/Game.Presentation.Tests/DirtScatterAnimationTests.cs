using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class DirtScatterAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static string Template(int move)=>move==28?"gSandAttackDirtSpriteTemplate":"gMudSlapMudSpriteTemplate";
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static CommittedMoveAnimation Cue(int move,int actor,int target,uint? seed)=>new("dirt-scatter",1,move,new(actor),new(target)){VisualSeed=seed};
    private static SourceAnimationScript Particle(SourceAnimationScript source,int[] args)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_MUD_SAND"],1),new("setarg",["7","777"],2),
        new("createsprite",new[]{Template(source.MoveId),"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),3),
        new("waitforvisualfinish",[],4),new("end",[],5)]};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("dirt-scatter-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_dirt_scatter_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/dirt-scatter-source-vectors.json");

    [TestMethod][DataRow(28,0)][DataRow(28,1)][DataRow(28,2)][DataRow(28,3)]
    [DataRow(189,0)][DataRow(189,1)][DataRow(189,2)][DataRow(189,3)]
    public void RandomOffsetsLinearWordsAndCallbackHandoffsMatchExtractedSource(int move,int actor)
    {
        using var doc=Vectors();var configs=doc.RootElement.GetProperty("configs").EnumerateArray().ToArray();Assert.IsTrue(configs.Length>=20);
        foreach(int target in new[]{actor^1,actor^2})for(int config=0;config<configs.Length;config++)
        {
            var values=configs[config].EnumerateArray().ToArray();uint seed=values[0].GetUInt32();int[] args=values.Skip(1).Select(v=>v.GetInt32()).ToArray();
            var runtime=new SourceBattleAnimation(Particle(Script(move),args),Cue(move,actor,target,seed),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            int tick=-1,visible=0;SourceAnimationFrame? frame=null;PixelPoint last=default;
            string key=$"{actor}:{target}:{config}";
            foreach(var row in doc.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                while(tick<v[0]){frame=runtime.Tick();tick++;visible+=frame.Sprites.Length;}
                Assert.IsNotNull(frame);string message=$"{move}/{key} tick {tick}";
                CollectionAssert.AreEqual(args,frame.AnimationArguments.Take(5).Select(x=>(int)x).ToArray(),message);Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);
                if(v[5]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                Assert.AreEqual(Template(move),sprite.Template);Assert.AreEqual("mud-sand",sprite.Asset);Assert.AreEqual(move==28?8:16,sprite.Width);Assert.AreEqual(sprite.Width,sprite.Height);
                Assert.AreEqual(move==28?0:1,sprite.Tile);Assert.IsTrue(sprite.Visible);Assert.IsFalse(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);
                if(tick==args[2])Assert.AreEqual(last,sprite.Offset,"Completion installs destruction without changing the retained final offset.");
                last=sprite.Offset;
            }
            Assert.AreEqual(args[2]+1,visible);while(runtime.Active)runtime.Tick();Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
        }
    }

    [TestMethod][DataRow(28)][DataRow(189)]
    public void ThirtyInitializersConsumeExactlySixtyDrawsAndMovementConsumesNone(int move)
    {
        using var vectors=Vectors();var source=Script(move);
        foreach(var seedCase in vectors.RootElement.GetProperty("primaryRng").EnumerateObject())
        {
            uint seed=uint.Parse(seedCase.Name,CultureInfo.InvariantCulture);
            var commands=new List<SourceAnimationCommand>{new("loadspritegfx",["ANIM_TAG_MUD_SAND"],1),new("setarg",["7","777"],2)};
            SourceAnimationCommand Create(int index)=>new("createsprite",[Template(move),"ANIM_TARGET","2","15","15","1",index.ToString(CultureInfo.InvariantCulture),(-index).ToString(CultureInfo.InvariantCulture)],3);
            for(int i=0;i<30;i++)commands.Add(Create(i));commands.Add(new("waitforvisualfinish",[],4));
            commands.Add(Create(30));commands.Add(new("waitforvisualfinish",[],5));commands.Add(new("end",[],6));
            var runtime=new SourceBattleAnimation(source with{Commands=commands.ToArray()},Cue(move,0,1,seed),Battlers(true),true,new Audio());
            bool batch=false,continuation=false;int ticks=0;
            while(runtime.Active&&ticks++<50)
            {
                var frame=runtime.Tick();runtime.Snapshot();runtime.Snapshot();
                if(!batch&&frame.Sprites.Length==30)
                {
                    batch=true;var sprites=frame.Sprites.OrderBy(s=>s.Slot).ToArray();var rows=seedCase.Value.GetProperty("particles").EnumerateArray().ToArray();Assert.AreEqual(30,rows.Length);
                    for(int i=0;i<30;i++)
                    {
                        var row=rows[i].EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(i,row[0]);
                        Assert.AreEqual(new PixelPoint(row[1],row[2]),sprites[i].Position);Assert.AreEqual(new PixelPoint(row[3],row[4]),sprites[i].Offset);
                    }
                }
                else if(batch&&!continuation&&frame.Sprites.Length==1)
                {
                    continuation=true;var row=seedCase.Value.GetProperty("continuationParticle").EnumerateArray().Select(v=>v.GetInt32()).ToArray();var sprite=frame.Sprites.Single();
                    Assert.AreEqual(30,row[0]);Assert.AreEqual(new PixelPoint(row[1],row[2]),sprite.Position);Assert.AreEqual(new PixelPoint(row[3],row[4]),sprite.Offset);
                    CollectionAssert.AreEqual(new[]{15,15,1,30,-30},frame.AnimationArguments.Take(5).Select(v=>(int)v).ToArray());Assert.AreEqual(777,(int)frame.AnimationArguments[7]);
                }
            }
            Assert.IsTrue(batch);Assert.IsTrue(continuation);Assert.IsFalse(runtime.Active);
        }
    }

    [TestMethod][DataRow(28)][DataRow(189)]
    public void CompleteRootsUseThirtyParticlesAndReplayIndependentlyOfSnapshotsAndComposition(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();
            var cue=Cue(move,actor,target,null);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var nextInvocation=cue with{Sequence=cue.Sequence+1};nextInvocation=nextInvocation with{VisualSeed=SourceAnimationVisualSeed.For(nextInvocation)};
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);
            var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());
            var changedSeed=new SourceBattleAnimation(resolved.Script,nextInvocation,battlers,doubles,new Audio());
            int ticks=0,particleFrames=0;bool pixels=false,different=false;
            while(runtime.Active&&ticks++<300)
            {
                var frame=runtime.Tick();var repeated=replay.Tick();var differentFrame=changedSeed.Tick();
                Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(repeated),"Same identity/seed must replay the exact source presentation.");
                runtime.Snapshot();runtime.Snapshot();var rgba=composer.ComposeField(frame);composer.ComposeField(frame);
                particleFrames+=frame.Sprites.Count(s=>s.Template==Template(move));
                var particles=frame.Sprites.Where(s=>s.Template==Template(move)).ToArray();
                var changed=differentFrame.Sprites.Where(s=>s.Template==Template(move)).ToArray();
                different|=!particles.Select(s=>s.Offset).SequenceEqual(changed.Select(s=>s.Offset));
                if(particles.Length>0)pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Template(move)).ToArray()}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsFalse(changedSeed.Active);Assert.AreEqual(30*21,particleFrames);Assert.IsTrue(pixels);Assert.IsTrue(different);
            Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual("SE_M_SAND_ATTACK",audio.Cues[0].Id);
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
        }
    }

    [TestMethod][DataRow(28)][DataRow(189)]
    public void CancellationAtActiveAndCompletedStagesDoesNotLeakOrPerturbAFreshReplay(int move)
    {
        var source=Script(move);var cue=Cue(move,3,1,42);var battlers=Battlers(true);var baseline=new SourceBattleAnimation(source,cue,battlers,true,new Audio());var frames=new List<string>();
        while(baseline.Active&&frames.Count<300)frames.Add(JsonSerializer.Serialize(baseline.Tick()));Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,15,25,40,frames.Count-1,frames.Count,frames.Count+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,cue,battlers,true,new Audio());int reached=0;
            while(runtime.Active&&reached<requested){Assert.AreEqual(frames[reached],JsonSerializer.Serialize(runtime.Tick()));reached++;}
            Assert.AreEqual(Math.Min(frames.Count,requested),reached);Assert.AreEqual(requested<frames.Count,runtime.Active);
            runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
        }
        var replay=new SourceBattleAnimation(source,cue,battlers,true,new Audio());foreach(string frame in frames)Assert.AreEqual(frame,JsonSerializer.Serialize(replay.Tick()));Assert.IsFalse(replay.Active);
    }

    [TestMethod][DataRow(28)][DataRow(189)]
    public void MissingSeedInvalidDurationOrIncorrectSourceRecipesFailClosed(int move)
    {
        var source=Script(move);string template=Template(move);
        void Reject(SourceAnimationScript script,uint? seed=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(move,0,1,seed),Battlers(false),false,new Audio()));
        Reject(Particle(source,[15,15,20,0,0]),null);
        foreach(int duration in new[]{0,-1,256,32768})Reject(Particle(source,[15,15,duration,0,0]));
        Reject(Particle(source,[128,0,1,0,0]));Reject(Particle(source,[0,-129,1,0,0]));Reject(Particle(source,[0,0,1,32768,0]));
        foreach(var bad in new[]{source.SpriteRecipes[template] with{Width=32},source.SpriteRecipes[template] with{Blend=true},source.SpriteRecipes[template] with{AffineDoubleSize=true},source.SpriteRecipes[template] with{Callback="UnknownDirt"},source.SpriteRecipes[template] with{Animations=[[new(99,1,false,false)]]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Reject(Particle(source,[15,15,20,0,0]) with{SpriteRecipes=recipes});}
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(template);Reject(Particle(source,[15,15,20,0,0]) with{SpriteRecipes=missing});
    }
}
