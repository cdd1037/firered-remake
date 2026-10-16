using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ClawAnimationTests
{
    private const string Claw="gClawSlashSpriteTemplate",Shake="gShakeMonOrTerrainSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static Dictionary<string,SourcePixelAsset> Pixels(SourceResolvedAnimation resolved,AnimationBattler[] battlers)
    {
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));
        foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)(0x83e0+b.Slot.Value),4096).ToArray()));return assets;
    }
    [TestMethod][DataRow(232)][DataRow(306)][DataRow(337)]
    public void CompleteClawRootsComposeAllFourSourceSlashesAndSharedEffectsForEveryActor(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,new("claw-complete",1,move,new(actor),new(target)),battlers,doubles,audio);
            var composer=new SourceBattleFrameComposer(Pixels(resolved,battlers),battlers,resolved.StatMask,resolved.Metallic);
            int ticks=0,clawObjectFrames=0;bool sampledClaw=false,seenGlobal=false,seenMetal=false,seenEmber=false,seenBlend=false;
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                var claws=frame.Sprites.Where(s=>s.Template==Claw).ToArray();clawObjectFrames+=claws.Length;
                if(claws.Length>0)sampledClaw|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Claw).ToArray()}));
                Assert.IsTrue(claws.All(s=>s.Asset=="claw-slash"&&!s.Affine&&!s.DoubleSize&&s.Width==32&&s.Height==32&&!s.Blend));
                if(move!=306)
                {
                    seenGlobal|=frame.ObjectOffsets[target].Y!=frame.Offsets[target].Y;
                    for(int slot=0;slot<battlers.Length;slot++)if(slot!=target&&slot!=actor)Assert.AreEqual(frame.Offsets[slot],frame.ObjectOffsets[slot]);
                    Assert.AreEqual(0,frame.MoveBackground!.ScrollX);Assert.AreEqual(0,frame.MoveBackground.ScrollY);
                }
                seenMetal|=frame.Metallic is not null;seenEmber|=frame.Sprites.Any(s=>s.Asset=="small-ember");seenBlend|=frame.BlendEnabled;
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(84,clawObjectFrames,"Four source slashes each survive21 callback frames.");Assert.IsTrue(sampledClaw);
            Assert.AreEqual(move!=306,seenGlobal);Assert.AreEqual(move==232,seenMetal);Assert.AreEqual(move==337,seenEmber);Assert.IsTrue(move!=306||seenBlend);
            Assert.AreEqual(2,audio.Cues.Count(c=>c.Id=="SE_M_RAZOR_WIND"));Assert.AreEqual(move==232?2:0,audio.Cues.Count(c=>c.Id=="SE_M_HARDEN"));Assert.AreEqual(move==337?1:0,audio.Cues.Count(c=>c.Id=="SE_M_SACRED_FIRE2"));
            int pan=actor%2==0?(target%2==0?-64:63):(target%2==0?-63:63);Assert.IsTrue(audio.Cues.Where(c=>c.Id=="SE_M_RAZOR_WIND").All(c=>c.Pan==pan));
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);
            Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsTrue(final.ObjectOffsets.All(p=>p==default));Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);
            if(final.Metallic is {} metallic)
            {Assert.IsFalse(metallic.WindowEnabled);Assert.IsFalse(metallic.BackgroundLoaded);Assert.IsFalse(metallic.MaskVisible);CollectionAssert.AreEqual(composer.ComposeField(final),composer.ComposeField(final with{Metallic=null}));}
        }
    }
    [TestMethod][DataRow(232)][DataRow(306)][DataRow(337)]
    public void CancellationClearsClawControllersGlobalOffsetFlagsAndLoadedDependencies(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];
        var baseline=new SourceBattleAnimation(source,new("cancel-duration",1,move,new(3),new(1)),Battlers(true),true,new Audio());
        int duration=0;while(baseline.Active&&duration<400){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int time in new[]{1,5,15,25,45,75,100,duration-1}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,new("cancel-claw",1,move,new(3),new(1)),Battlers(true),true,new Audio());
            int reached=0;while(reached<time&&runtime.Active){runtime.Tick();reached++;}
            Assert.AreEqual(Math.Min(time,duration),reached,"The requested tick is reached unless the complete source root ended earlier.");
            Assert.AreEqual(time<duration,runtime.Active,"Distinguish cancelling an active root from the naturally completed state.");
            Assert.AreEqual(reached-1,runtime.Snapshot().Frame);runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
            Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);
            Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.AreEqual(0,frame.PaletteBlends.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsNull(frame.Metallic);
            Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsTrue(frame.ObjectOffsets.All(p=>p==default));
        }
    }
    [TestMethod][DataRow(232)][DataRow(306)][DataRow(337)]
    public void CancelAfterNaturalRootCompletionIsExplicitlyIdempotent(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var runtime=new SourceBattleAnimation(resources.Scripts[move],new("drained-claw",1,move,new(3),new(1)),Battlers(true),true,new Audio());
        int ticks=0;while(runtime.Active&&ticks++<400)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        var completed=runtime.Snapshot();Assert.AreEqual(0,completed.Sprites.Length);Assert.AreEqual(0,completed.BackgroundSlots.Length);
        runtime.Cancel();var cancelled=runtime.Snapshot();runtime.Cancel();var twice=runtime.Snapshot();
        Assert.AreEqual(completed.Frame,cancelled.Frame);Assert.AreEqual(cancelled.Frame,twice.Frame);Assert.IsFalse(twice.Active);
        Assert.AreEqual(0,twice.Sprites.Length);Assert.AreEqual(0,twice.BackgroundSlots.Length);Assert.AreEqual(0,twice.PaletteBlends.Length);Assert.IsNull(twice.Metallic);Assert.IsFalse(twice.BlendEnabled);
        CollectionAssert.AreEqual(cancelled.Offsets,twice.Offsets);CollectionAssert.AreEqual(cancelled.ObjectOffsets,twice.ObjectOffsets);Assert.IsTrue(twice.Offsets.All(p=>p==default));Assert.IsTrue(twice.ObjectOffsets.All(p=>p==default));
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void VisualSe1WrapperPlaysImmediatelyKeepsArgumentSuffixAndOwnsNoVisualCount(int actor)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[337];
        foreach(int target in new[]{actor^1,actor^2})
        {
            var script=source with{SoundEffects=new Dictionary<string,int>{{"SE_GENERATED_WRAPPER_PROBE",129}},Commands=[
                new("setarg",["2","222"],1),new("setarg",["7","777"],2),
                new("createvisualtask",["SoundTask_PlaySE1WithPanning","5","SE_GENERATED_WRAPPER_PROBE","SOUND_PAN_TARGET"],3),
                new("waitforvisualfinish",[],4),new("setarg",["6","666"],5),new("delay",["2"],6),new("end",[],7)]};
            var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("claw-se1-wrapper",1,337,new(actor),new(target)),Battlers(true),true,audio);
            Assert.AreEqual(0,audio.Cues.Count);var frame=runtime.Tick();var cue=audio.Cues.Single();
            Assert.AreEqual("SE_GENERATED_WRAPPER_PROBE",cue.Id);Assert.AreEqual(0,cue.Frame);Assert.AreEqual(1,cue.PanningPlayer!.Value);
            int pan=actor%2==0?(target%2==0?-64:63):(target%2==0?-63:63);Assert.AreEqual(pan,cue.Pan);
            CollectionAssert.AreEqual(new short[]{129,63,222,0,0,0,666,777},frame.AnimationArguments);
            Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);
            for(int i=0;i<12&&runtime.Active;i++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(1,audio.Cues.Count);
        }
    }
    [TestMethod]
    public void GlobalObjectShakeDoesNotMoveMonbgCopiesOrUnrelatedEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(232);var battlers=Battlers(false);
        var script=resolved.Script with{Commands=[new("monbg",["ANIM_TARGET"],1),new("createsprite",[Shake,"ANIM_ATTACKER","2","-4","1","10","3","1"],2),new("waitforvisualfinish",[],3),new("clearmonbg",["ANIM_TARGET"],4),new("end",[],5)]};
        var runtime=new SourceBattleAnimation(script,new("claw-object-only",1,232,new(0),new(1)),battlers,false,new Audio());var composer=new SourceBattleFrameComposer(Pixels(resolved,battlers),battlers);
        runtime.Tick();var frame=runtime.Tick();Assert.AreEqual(new PixelPoint(0,4),frame.ObjectOffsets[1]);Assert.AreEqual(default(PixelPoint),frame.Offsets[1]);Assert.AreEqual(default(PixelPoint),frame.ObjectOffsets[0]);
        CollectionAssert.Contains(frame.BackgroundSlots,1);CollectionAssert.AreEqual(composer.ComposeField(frame),composer.ComposeField(frame with{ObjectOffsets=new PixelPoint[4]}));
        var exposed=frame with{BackgroundSlots=[]};Assert.IsFalse(composer.ComposeField(exposed).SequenceEqual(composer.ComposeField(exposed with{ObjectOffsets=new PixelPoint[4]})));
    }
    [TestMethod]
    public void ClawAndTerrainOperandsFailClosedBeforeCreatingControllers()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[232];
        void Reject(SourceAnimationCommand command,SourceAnimationScript? recipe=null)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation((recipe??source) with{Commands=[command,new("end",[],2)]},new("bad-claw",1,232,new(0),new(1)),Battlers(false),false,new Audio()));
        foreach(string[] args in new[]{new[]{"-4","1","10","3"},new[]{"-4","1","10","3","3"},new[]{"-4","1","10","4","1"},new[]{"32768","1","10","1"},new[]{"-4","1","-1","1"},new[]{"-4","1","10","1","0"}})
            Reject(new("createsprite",new[]{Shake,"ANIM_ATTACKER","2"}.Concat(args).ToArray(),1));
        foreach(string selection in new[]{"-1","2"})Reject(new("createsprite",[Claw,"ANIM_TARGET","2","0","0",selection],1));
        foreach(var bad in new[]{source.SpriteRecipes[Claw] with{Width=16},source.SpriteRecipes[Claw] with{Blend=true},source.SpriteRecipes[Claw] with{AffineDoubleSize=true},source.SpriteRecipes[Claw] with{Animations=[source.SpriteRecipes[Claw].Animations[0]]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Claw]=bad;Reject(new("createsprite",[Claw,"ANIM_TARGET","2","0","0","0"],1),source with{SpriteRecipes=recipes});}
    }
}
