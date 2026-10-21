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
            CollectionAssert.AreEqual(new short[]{129,63,222,0,0,0,666,777}.Concat(new short[8]).ToArray(),frame.AnimationArguments);
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
        runtime.Tick();var frame=runtime.Tick();
        // Pinned sprite.c UpdateOamCoords skips invisible OBJ sprites. monbg
        // hides the OBJ, so its OAM latch is not the live global shake register.
        Assert.AreEqual(default(PixelPoint),frame.ObjectOffsets[1]);Assert.AreEqual(default(PixelPoint),frame.Offsets[1]);Assert.AreEqual(default(PixelPoint),frame.ObjectOffsets[0]);
        var visibility=frame.BattlerVisibility.Single(v=>v.Slot==1);Assert.IsTrue(visibility.SpriteInvisible);Assert.IsTrue(visibility.OamInvisible);
        CollectionAssert.Contains(frame.BackgroundSlots,1);
        var injected=(PixelPoint[])frame.ObjectOffsets.Clone();injected[1]=new(0,4);
        CollectionAssert.AreEqual(composer.ComposeField(frame),composer.ComposeField(frame with{ObjectOffsets=injected}),"A nonzero OBJ offset must not displace the monbg copy.");
        CollectionAssert.AreEqual(composer.ComposeField(frame with{BackgroundSlots=[]}),composer.ComposeField(frame with{BackgroundSlots=[],ObjectOffsets=injected}),"Removing a BG copy does not unhide an explicitly invisible OBJ.");

        SourceBattleAnimation VisibleRuntime(string amplitude)=>new(resolved.Script with{Commands=[
            new("loadspritegfx",["ANIM_TAG_CLAW_SLASH"],1),
            new("createsprite",[Claw,"ANIM_TARGET","2","-80","24","0"],2),
            new("createsprite",[Shake,"ANIM_ATTACKER","2",amplitude,"1","10","3","1"],3),
            new("waitforvisualfinish",[],4),new("end",[],5)]},new("visible-claw-shake",1,232,new(0),new(1)),battlers,false,new Audio());
        var moving=VisibleRuntime("-4");var still=VisibleRuntime("0");
        for(int tick=0;tick<3;tick++){Assert.AreEqual(0,moving.Tick().Sprites.Length);Assert.AreEqual(0,still.Tick().Sprites.Length);}
        moving.Tick();still.Tick();var visible=moving.Tick();var stationary=still.Tick();
        Assert.AreEqual(new PixelPoint(0,4),visible.ObjectOffsets[1]);Assert.AreEqual(default(PixelPoint),visible.ObjectOffsets[0]);
        Assert.IsFalse(visible.BattlerVisibility.Single(v=>v.Slot==1).OamInvisible);Assert.AreEqual(0,visible.BackgroundSlots.Length);
        Assert.IsFalse(composer.ComposeField(visible).SequenceEqual(composer.ComposeField(visible with{ObjectOffsets=new PixelPoint[4]})),"The same shake must visibly move an exposed target OBJ.");
        var effect=visible.Sprites.Single(s=>s.Template==Claw);var control=stationary.Sprites.Single(s=>s.Template==Claw);
        Assert.IsTrue(effect.Visible);Assert.AreEqual(control.Position,effect.Position);Assert.AreEqual(control.Offset,effect.Offset);Assert.AreEqual(control.Tile,effect.Tile);
        // Isolate the real, VM-created effect from battler occlusion. It must
        // produce pixels, and its complete rendered image must ignore the shake.
        var hidden=visible.BattlerVisibility.Select(v=>v with{Present=false}).ToArray();
        var effectOnly=visible with{BattlerVisibility=hidden};var controlOnly=stationary with{BattlerVisibility=hidden};
        CollectionAssert.AreEqual(composer.ComposeField(controlOnly),composer.ComposeField(effectOnly));
        Assert.IsFalse(composer.ComposeField(effectOnly).SequenceEqual(composer.ComposeField(effectOnly with{Sprites=[]})),"Unrelated-effect immunity must exercise visible pixels.");
    }
    [TestMethod]
    public void HiddenMonbgObjectRetainsNonzeroOamUntilSourceVisibilityResumes()
    {
        using var resources=new SourceAnimationResources(Folder);
        var script=resources.Scripts[232] with{Commands=[
            new("createsprite",[Shake,"ANIM_ATTACKER","2","-4","1","10","3","1"],1),new("delay",["0"],2),
            new("monbg",["ANIM_TARGET"],3),new("delay",["0"],4),new("visible",["ANIM_TARGET"],5),
            new("waitforvisualfinish",[],6),new("clearmonbg",["ANIM_TARGET"],7),new("end",[],8)]};
        var runtime=new SourceBattleAnimation(script,new("oam-latch",1,232,new(0),new(1)),Battlers(false),false,new Audio());
        runtime.Tick();Assert.AreEqual(new PixelPoint(0,4),runtime.Tick().ObjectOffsets[1]);
        var hidden=runtime.Tick();Assert.IsTrue(hidden.BattlerVisibility.Single(v=>v.Slot==1).OamInvisible);
        Assert.AreEqual(new PixelPoint(0,4),hidden.ObjectOffsets[1]);
        var retained=runtime.Tick();Assert.AreEqual(new PixelPoint(0,4),retained.ObjectOffsets[1]);Assert.AreEqual(default(PixelPoint),retained.Offsets[1]);
        var exposed=runtime.Tick();Assert.IsFalse(exposed.BattlerVisibility.Single(v=>v.Slot==1).OamInvisible);
        Assert.AreEqual(default(PixelPoint),exposed.ObjectOffsets[1],"The current zero global register replaces the retained nonzero OAM only when visible again.");
        for(int tick=0;runtime.Active&&tick<20;tick++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);
    }
    [TestMethod][DataRow(0)][DataRow(1)]
    public void TerrainBackgroundModesIgnoreOptionalFifthArgumentAndRestore(int mode)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[232];
        SourceBattleAnimation Runtime(string? ignored)
        {
            string[] args=[Shake,"ANIM_ATTACKER","2","-4","1","10",mode.ToString()];
            if(ignored is not null)args=[..args,ignored];
            return new(source with{Commands=[new("createsprite",args,1),new("waitforvisualfinish",[],2),new("end",[],3)]},new("ignored-bg-operand",1,232,new(0),new(1)),Battlers(false),false,new Audio());
        }
        // battle_anim_normal.c consults argument 4 only for coordinate modes
        // 2/3. Original battle_anim_scripts.s also supplies it in BG mode 0.
        foreach(string ignored in new[]{"-32768","-1","0","1","2","3","32767"})
        {
            var omitted=Runtime(null);var supplied=Runtime(ignored);int ticks=0;
            while(omitted.Active&&ticks<20)
            {
                int tick=ticks++;var expected=omitted.Tick();var actual=supplied.Tick();
                int offset=tick<10&&((tick+1)/2)%2==1?4:0;
                Assert.AreEqual(mode==0?offset:0,actual.MoveBackground!.ScrollX);Assert.AreEqual(mode==1?offset:0,actual.MoveBackground.ScrollY);
                Assert.AreEqual(expected.MoveBackground!.ScrollX,actual.MoveBackground.ScrollX);Assert.AreEqual(expected.MoveBackground.ScrollY,actual.MoveBackground.ScrollY);
                CollectionAssert.AreEqual(expected.ObjectOffsets,actual.ObjectOffsets);CollectionAssert.AreEqual(expected.Offsets,actual.Offsets);
                CollectionAssert.AreEqual(expected.BattlerVisibility,actual.BattlerVisibility);
                Assert.IsTrue(actual.ObjectOffsets.All(p=>p==default));Assert.IsTrue(actual.Offsets.All(p=>p==default));
                Assert.AreEqual(omitted.ActiveVisualCount,supplied.ActiveVisualCount);Assert.AreEqual(expected.Active,actual.Active);
            }
            Assert.IsFalse(omitted.Active);Assert.IsFalse(supplied.Active);Assert.AreEqual(0,supplied.ActiveVisualCount);
            Assert.AreEqual(0,supplied.Snapshot().MoveBackground!.ScrollX);Assert.AreEqual(0,supplied.Snapshot().MoveBackground!.ScrollY);
        }
    }
    [TestMethod]
    public void ClawAndTerrainOperandsFailClosedBeforeCreatingControllers()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[232];
        void Reject(SourceAnimationCommand command,SourceAnimationScript? recipe=null)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation((recipe??source) with{Commands=[command,new("end",[],2)]},new("bad-claw",1,232,new(0),new(1)),Battlers(false),false,new Audio()));
        foreach(string[] args in new[]{new[]{"-4","1","10","3"},new[]{"-4","1","10","3","3"},new[]{"-4","1","10","4","1"},new[]{"32768","1","10","1"},new[]{"-4","1","-1","1"},new[]{"-4","1","10","2"},new[]{"-4","1","10","2","-1"},new[]{"-4","1","10","2","3"},new[]{"-4","1","10","3","-1"},new[]{"-4","1","10","-1","1"},new[]{"-32769","1","10","1"},new[]{"-4","32768","10","1"},new[]{"-4","-32769","10","1"},new[]{"-4","1","32768","1"},new[]{"-4","1","10","1","32768"},new[]{"-4","1","10"},new[]{"-4","1","10","1","0","0"}})
            Reject(new("createsprite",new[]{Shake,"ANIM_ATTACKER","2"}.Concat(args).ToArray(),1));
        foreach(string selection in new[]{"-1","2"})Reject(new("createsprite",[Claw,"ANIM_TARGET","2","0","0",selection],1));
        foreach(var bad in new[]{source.SpriteRecipes[Claw] with{Width=16},source.SpriteRecipes[Claw] with{Height=16},source.SpriteRecipes[Claw] with{Blend=true},source.SpriteRecipes[Claw] with{AffineDoubleSize=true},source.SpriteRecipes[Claw] with{Animations=[source.SpriteRecipes[Claw].Animations[0]]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Claw]=bad;Reject(new("createsprite",[Claw,"ANIM_TARGET","2","0","0","0"],1),source with{SpriteRecipes=recipes});}
    }
}
