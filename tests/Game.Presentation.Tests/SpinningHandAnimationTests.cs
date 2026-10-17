using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SpinningHandAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static string Template(int move)=>move==299?"gSpinningHandOrFootSpriteTemplate":"gMegaPunchKickSpriteTemplate";
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("spinning",1,move,new(actor),new(target));
    private static SourceAnimationScript Spinning(SourceAnimationScript source,int[] args)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_HANDS_AND_FEET"],1),new("setarg",["7","777"],2),
        new("createsprite",new[]{Template(source.MoveId),"ANIM_TARGET","3"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),3),
        new("waitforvisualfinish",[],4),new("end",[],5)]};

    [TestMethod][DataRow(5,0)][DataRow(5,1)][DataRow(5,2)][DataRow(5,3)]
    [DataRow(299,0)][DataRow(299,1)][DataRow(299,2)][DataRow(299,3)]
    public void SpinResetPauseAndCallbackHandoffsMatchExtractedOriginalVectors(int move,int actor)
    {
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/spinning-sprite-original-vectors.json")));
        var configs=vectors.RootElement.GetProperty("configs").EnumerateArray().ToArray();
        foreach(int target in new[]{actor^1,actor^2})for(int index=0;index<configs.Length;index++)
        {
            int[] config=configs[index].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(config[0]!=(move==299?0:1)||config[4]<0)continue;
            int duration=config[4];var runtime=new SourceBattleAnimation(Spinning(Script(move),config[1..]),Cue(move,actor,target),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            int visible=0;string key=$"{actor}:{target}:{index}";
            foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                var frame=runtime.Tick();string message=$"{move}/{key} tick {v[0]}";
                CollectionAssert.AreEqual(config[1..],frame.AnimationArguments.Take(4).Select(x=>(int)x).ToArray(),message);Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);
                if(v[14]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                visible++;var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                Assert.AreEqual(v[5],sprite.Tile,message);CollectionAssert.AreEqual(v[10..14],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.IsTrue(sprite.Visible);Assert.IsTrue(sprite.Affine);Assert.IsTrue(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);Assert.AreEqual(32,sprite.Width);Assert.AreEqual(32,sprite.Height);
                if(v[0]==duration)Assert.IsTrue(v[7]>0&&v[7]<256,"The wait handoff still applies one final shrinking affine delta.");
                if(v[0]>=duration+1)CollectionAssert.AreEqual(new[]{256,0,0,256},new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},"Finish restarts identity despite the affine pause flag.");
            }
            Assert.AreEqual(duration+23,visible);while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod][DataRow(5)][DataRow(25)][DataRow(299)]
    public void CompleteSpinningRootsComposeRealPixelsSelectTargetSideAndRestore(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),battlers,doubles,audio);
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
            int ticks=0,spinFrames=0;bool pixels=false,impact=false,flames=false,background=false;
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                spinFrames+=frame.Sprites.Count(s=>s.Template==Template(move));
                if(frame.Sprites.Any(s=>s.Template==Template(move)))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Template(move)).ToArray()}));
                impact|=frame.Sprites.Any(s=>s.Asset=="impact");flames|=frame.Sprites.Any(s=>s.Template=="gFireSpreadSpriteTemplate");
                if(frame.MoveBackground?.Id is {} id){Assert.AreNotEqual(299,move);Assert.AreEqual((target&1)==0?5:4,id);background=true;}
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(pixels);Assert.IsTrue(impact);Assert.AreEqual(move==299,flames);Assert.AreEqual(move!=299,background);
            Assert.AreEqual((move==299?30:50)+23,spinFrames);Assert.IsTrue(audio.Cues.Count>=2);
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
            Assert.IsNull(final.MoveBackground!.Id);Assert.AreEqual(0,final.MoveBackground.Darkening);
        }
    }
    [TestMethod]
    public void ImpactHelperWritesSharedReturnArgumentAndChangesBackgroundWithoutFadeOrVisualTask()
    {
        var source=Script(5);
        foreach(int actor in Enumerable.Range(0,4))foreach(int target in new[]{actor^1,actor^2})
        {
            var script=source with{Commands=[new("setarg",["0","123"],1),new("setarg",["7","777"],2),new("call",["SetImpactBackground"],3),new("delay",["1"],4),new("end",[],5)]};
            var runtime=new SourceBattleAnimation(script,Cue(5,actor,target),Battlers(true),true,new Audio());bool seen=false;
            for(int tick=0;runtime.Active&&tick<12;tick++)
            {
                var frame=runtime.Tick();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.PaletteFadeActive);
                if(frame.MoveBackground?.Id is not {} id)continue;
                seen=true;Assert.AreEqual((target&1)==0?5:4,id);Assert.AreEqual(0,frame.MoveBackground.Darkening);Assert.AreEqual(1,frame.MoveBackground.Revision);
                Assert.AreEqual((target&1)==0?1:0,(int)frame.AnimationArguments[7]);Assert.AreEqual(123,(int)frame.AnimationArguments[0]);
            }
            Assert.IsTrue(seen);Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod][DataRow(5)][DataRow(25)][DataRow(299)]
    public void CancellationAtActiveAndCompletedStagesClearsOwnedState(int move)
    {
        var source=Script(move);var baseline=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int duration=0;
        while(baseline.Active&&duration<400){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,25,55,75,duration-1,duration,duration+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int reached=0;
            while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(duration,requested),reached);Assert.AreEqual(requested<duration,runtime.Active);
            runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsNull(frame.MoveBackground!.Id);Assert.AreEqual(0,frame.MoveBackground.Darkening);
        }
    }
    [TestMethod][DataRow(5)][DataRow(299)]
    public void InvalidScaleCrossingFrameCoordinatesOrRecipesFailClosed(int move)
    {
        var source=Script(move);string template=Template(move);
        void Reject(SourceAnimationScript script)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(move,0,1),Battlers(false),false,new Audio()));
        foreach(int duration in new[]{-32768,-1,move==299?31:63,255})Reject(Spinning(source,[0,0,0,duration]));
        Reject(Spinning(source,[128,0,0,1]));Reject(Spinning(source,[0,-129,0,1]));Reject(Spinning(source,[0,0,2,1]));
        foreach(var bad in new[]{source.SpriteRecipes[template] with{Width=16},source.SpriteRecipes[template] with{Blend=true},source.SpriteRecipes[template] with{AffineDoubleSize=false},source.SpriteRecipes[template] with{Callback="UnknownSpinningHand"},source.SpriteRecipes[template] with{AffineAnimations=[]},source.SpriteRecipes[template] with{LoopStarts=[0,0,0,0,0]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Reject(Spinning(source,[0,0,0,1]) with{SpriteRecipes=recipes});}
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(template);Reject(Spinning(source,[0,0,0,1]) with{SpriteRecipes=missing});
    }
}
