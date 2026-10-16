using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ElementalMoveAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Cues {get;}=[];
        public List<(int Pan,int Frame)> Pans {get;}=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Cues.Add(cue);
        public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));
        public void StopAnimationEffects(){}
    }
    private static SourceAnimationScript Script(int move)
    {
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(TestAssets.Root,"content/generated/battle/animations/hit"));
        return SourceElementalAnimationCatalog.Read(Path.Combine(TestAssets.Root,"content/generated/battle/animations/elemental.json"),hit)[move];
    }
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    [DataRow(7)][DataRow(52)][DataRow(53)][DataRow(56)][DataRow(190)][DataRow(324)][DataRow(341)][DataRow(84)][DataRow(86)][DataRow(9)][DataRow(55)][DataRow(85)][DataRow(121)][DataRow(152)][DataRow(192)][DataRow(48)][DataRow(103)][DataRow(319)][DataRow(71)][DataRow(72)][DataRow(202)][DataRow(105)][DataRow(51)][DataRow(61)][DataRow(92)][DataRow(141)][DataRow(145)]
    public void SourceElementalRootsCompleteForEveryCanonicalActor(int move)
    {
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),new("elemental",1,move,new(actor),new(actor^1)),Battlers(doubles),doubles,audio);
            int frames=0;bool visual=false;
            while(runtime.Active&&frames++<1500){var f=runtime.Tick();visual|=f.Sprites.Any(s=>s.Asset is not null);}
            Assert.IsFalse(runtime.Active,$"move {move}, actor {actor}");Assert.IsTrue(visual);Assert.IsTrue(audio.Cues.Count>0);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.PaletteBlends.Length);
            if(move is 53 or 56 or 324 or 341)Assert.IsTrue(audio.Pans.Count>0);
        }
    }
    [TestMethod]
    public void EveryElementalFamilyComposesItsRealGeneratedSpriteSheets()
    {
        string folder=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(folder,"hit"));
        var assets=hit.RootElement.GetProperty("assets").EnumerateArray().Select(a=>a.GetProperty("id").GetString()!).ToDictionary(id=>id,id=>SourceHitAnimationCatalog.Asset(hit,Path.Combine(folder,"hit"),id));
        foreach(var pair in SourceElementalAnimationCatalog.Assets(Path.Combine(folder,"elemental.json")))assets.Add(pair.Key,pair.Value);
        assets.Add("background",new(240,112,new ushort[240*112]));
        foreach(var b in Battlers(true))assets.Add(b.Asset,new(64,64,new ushort[4096]));
        var composer=new SourceBattleFrameComposer(assets,Battlers(true));
        foreach(var script in SourceElementalAnimationCatalog.Read(Path.Combine(folder,"elemental.json"),hit).Values)
        {
            var runtime=new SourceBattleAnimation(script,new("pixels",1,script.MoveId,new(2),new(3)),Battlers(true),true,new Audio());
            int frames=0;while(runtime.Active&&frames++<1500)Assert.AreEqual(240*112*4,composer.ComposeField(runtime.Tick()).Length);
            Assert.IsFalse(runtime.Active,script.Label);
        }
    }
    [TestMethod]
    public void FireSpreadUsesSignedEightBitFractionsAndSourceAnimationLoop()
    {
        var source=Script(7);var sprite=source.Subroutines["FireSpreadEffect"][1];
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("spread",1,7,new(0),new(1)),Battlers(false),false,new Audio());
        int seen=0;bool wrapped=false;int last=-1;
        while(runtime.Active)
        {
            var f=runtime.Tick();if(f.Sprites.Length==0)continue;var s=f.Sprites.Single();seen++;
            Assert.AreEqual(new PixelPoint((-192*seen)>>8,(240*seen)>>8),s.Offset);
            wrapped|=last>s.Tile;last=s.Tile;
            if(seen==40)break;
        }
        Assert.AreEqual(40,seen);Assert.IsTrue(wrapped);
    }
    [TestMethod]
    public void EmberProjectileUsesSourceDirectionBitsAndCoordinateModes()
    {
        var source=Script(52);var sprite=source.Commands.First(c=>c.Op=="createsprite");
        var script=source with{Commands=[source.Commands[0],sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("ember",1,52,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int tick=0;tick<3;tick++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            var first=runtime.Tick().Sprites.Single();var start=Battlers(false)[actor].Center(false,true);int sign=actor==0?1:-1;
            Assert.AreEqual(new PixelPoint(start.X+sign*20,start.Y),first.Position);
            Assert.AreEqual(0,first.Tile);Assert.AreEqual("small-ember",first.Asset);
            int count=0;while(runtime.Active&&count++<100)runtime.Tick();Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod]
    public void ElementalUnsupportedCallbackAndUnloadedGraphicsFailExplicitly()
    {
        var source=Script(52);var commands=source.Commands.ToArray();int index=Array.FindIndex(commands,c=>c.Op=="createsprite");
        commands[index]=commands[index] with{Args=["gUnsupportedElementalSprite",..commands[index].Args.Skip(1)]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=commands},new("b",1,52,new(0),new(1)),Battlers(false),false,new Audio()));
        var runtime=new SourceBattleAnimation(source with{Commands=source.Commands.Skip(1).ToArray()},new("b",1,52,new(0),new(1)),Battlers(false),false,new Audio());Assert.Throws<SourceAnimationException>(()=>runtime.Tick());
    }
    [TestMethod]
    public void ElectricBoltRawSpritesDoNotExtendSourceVisualWait()
    {
        var source=Script(84);
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SPARK"],1),new("createvisualtask",["AnimTask_ElectricBolt","5","0","-44","0"],2),new("waitforvisualfinish",[],3),new("playsewithpan",["SE_M_THUNDERBOLT","SOUND_PAN_TARGET"],4),new("end",[],5)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("raw",1,84,new(0),new(1)),Battlers(false),false,audio);
        for(int tick=0;tick<3;tick++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        var created=runtime.Tick();Assert.AreEqual(1,created.Sprites.Length);Assert.IsFalse(created.Sprites[0].Visible);Assert.AreEqual(1,runtime.ActiveVisualCount);
        var visible=runtime.Tick();Assert.IsTrue(visible.Sprites[0].Visible);Assert.AreEqual(8,visible.Sprites[0].Width);Assert.AreEqual(16,visible.Sprites[0].Height);
        int frames=0;while(runtime.Active&&frames++<100)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(14,audio.Cues.Single().Frame);Assert.AreEqual(0,runtime.ActiveVisualCount);
        Assert.IsTrue(runtime.Snapshot().Sprites.Length>0,"Raw source sprites remain after counted task ends.");Assert.IsFalse(runtime.GraphicsLoaded);
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
    }
    [TestMethod]
    public void ThunderWaveCreatesBothSourceBandsAndBlinksEveryThreeTicks()
    {
        var source=Script(86);var sprite=source.Commands.First(c=>c.Op=="createsprite");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SPARK_H"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)]};
        var runtime=new SourceBattleAnimation(script,new("bands",1,86,new(0),new(1)),Battlers(false),false,new Audio());
        for(int tick=0;tick<3;tick++)runtime.Tick();
        var first=runtime.Tick().Sprites.OrderBy(s=>s.Position.X).ToArray();Assert.AreEqual(2,first.Length);Assert.AreEqual(32,first[1].Position.X-first[0].Position.X);Assert.AreEqual(0,first[0].Tile);Assert.AreEqual(8,first[1].Tile);Assert.IsTrue(first.All(s=>s.Visible));
        Assert.IsTrue(runtime.Tick().Sprites.All(s=>s.Visible));Assert.IsTrue(runtime.Tick().Sprites.All(s=>!s.Visible));
        int count=0;while(runtime.Active&&count++<100)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);
    }

    [TestMethod]
    public void SourceAffineFramesPreserveAbsoluteRelativeAndJumpTiming()
    {
        var recipe=Script(85).SpriteRecipes["gThunderboltOrbSpriteTemplate"];
        var affine=new SourceSpriteAffineProgram(recipe.AffineAnimations[0]);
        Assert.AreEqual(232,affine.XScale);
        for(int i=0;i<10;i++)affine.Step();
        Assert.AreEqual(152,affine.XScale);
        for(int i=0;i<10;i++)affine.Step();
        Assert.AreEqual(232,affine.XScale);
        affine.Step();Assert.AreEqual(224,affine.XScale);
        Assert.AreEqual(SourceAffineMatrix.FromScaleRotation(65536/224,65536/224,0),affine.Matrix);
        var rotating=new SourceSpriteAffineProgram(Script(192).SpriteRecipes["gZapCannonSparkSpriteTemplate"].AffineAnimations[0]);
        Assert.AreEqual(20<<8,rotating.Rotation);rotating.Step();Assert.AreEqual(40<<8,rotating.Rotation);
    }
    [TestMethod]
    public void DropletUsesTargetAnchorDespiteAttackerSubpriorityAndSourceAffineLoop()
    {
        var source=Script(55);var sprite=source.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]=="gWaterGunDropletSpriteTemplate");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_BUBBLES"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)]};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("droplet",1,55,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int tick=0;tick<3;tick++)runtime.Tick();
            var first=runtime.Tick().Sprites.Single();var target=Battlers(false)[actor^1].Center(false,true);
            Assert.AreEqual(new PixelPoint(target.X,target.Y-15),first.Position);
            Assert.AreEqual(new PixelPoint(0,1),first.Offset);Assert.IsTrue(first.Affine);Assert.IsTrue(first.DoubleSize);
            Assert.AreEqual(65536/224,first.MatrixA);Assert.AreEqual(65536/288,first.MatrixD);
        }
    }
    [TestMethod]
    public void RotatingSparksRejectUnreviewedZeroFlashDivisor()
    {
        var source=Script(85);var sprite=source.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]=="gSparkElectricityFlashingSpriteTemplate");
        var args=sprite.Args.ToArray();args[10]="0";
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=[sprite with{Args=args},new("end",[],1)]},new("zero",1,85,new(0),new(1)),Battlers(false),false,new Audio()));
    }

    [TestMethod]
    public void AbsorptionArcStartsAtTargetAndHeadsTowardActorWithShrinkingAffine()
    {
        var source=Script(71);var sprite=source.Subroutines["AbsorbEffect"].First(c=>c.Op=="createsprite");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_ORBS"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("absorb",1,71,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int tick=0;tick<3;tick++)runtime.Tick();
            var first=runtime.Tick().Sprites.Single();var target=Battlers(false)[actor^1].Center(false,true);
            Assert.AreEqual(new PixelPoint(target.X,target.Y+5),first.Position);
            Assert.AreEqual(actor==0?-4:4,first.Offset.X);Assert.AreEqual(65536/246,first.MatrixA);Assert.IsTrue(first.Affine);Assert.IsFalse(first.DoubleSize);
            int frames=0;while(runtime.Active&&frames++<100)runtime.Tick();Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod]
    public void GrowingRingUsesUnsignedDurationAndClippedDoubleSizeAffine()
    {
        var recipe=Script(48).SpriteRecipes["gSupersonicRingSpriteTemplate"];
        Assert.AreEqual(200,recipe.AffineAnimations[0][1].Duration);
        var affine=new SourceSpriteAffineProgram(recipe.AffineAnimations[0]);Assert.AreEqual(32,affine.XScale);
        for(int i=0;i<200;i++)affine.Step();Assert.AreEqual(1432,affine.XScale);Assert.IsFalse(affine.Ended);
        affine.Step();Assert.IsTrue(affine.Ended);Assert.IsTrue(recipe.AffineDoubleSize);
        Assert.AreEqual(0,recipe.LoopStarts[0],"The source END-only dummy table never sets animEnded.");
    }
    [TestMethod]
    public void PoisonBubbleLifetimeFollowsAffineEndRatherThanFinishedTileFrame()
    {
        var source=Script(92);var sprite=source.Subroutines["PoisonBubblesEffect"].First(c=>c.Op=="createsprite");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_POISON_BUBBLE"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("poison",1,92,new(0),new(1)),Battlers(false),false,new Audio());
        for(int tick=0;tick<3;tick++)runtime.Tick();
        for(int tick=1;tick<=21;tick++)
        {
            var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint((source.SineTable[(tick*11)&255]*4)>>8,-((tick*48)>>8)),frame.Offset);
            Assert.AreEqual(65536/(156+5*Math.Min(tick,20)),frame.MatrixA);
        }
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
    [TestMethod]
    public void WaterBubbleReservesRawHelperUntilArrivalThenRunsSourceEndHandoff()
    {
        var source=Script(145);var sprite=source.Commands.First(c=>c.Op=="createsprite");var args=sprite.Args.ToArray();args[9]="3";
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_BUBBLE"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("bubble",1,145,new(0),new(1)),Battlers(false),false,new Audio());
        for(int tick=0;tick<3;tick++)runtime.Tick();
        var first=runtime.Tick();Assert.AreEqual(2,first.Sprites.Length);Assert.AreEqual(1,runtime.ActiveVisualCount);Assert.AreEqual(1,first.Sprites.Count(s=>s.Visible));
        var arrival=runtime.Tick();Assert.AreEqual(1,arrival.Sprites.Length);Assert.AreEqual(0,arrival.Sprites[0].Tile);
        Assert.AreEqual(4,runtime.Tick().Sprites.Single().Tile);
        for(int tick=4;tick<=26;tick++)Assert.AreEqual(1,runtime.Tick().Sprites.Length);
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.HasResidualSprites);
    }
    [TestMethod]
    public void AcidTargetAverageIncludesHiddenPartnerAndSourcePictureOffsets()
    {
        var source=Script(51);var sprite=source.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]=="gAcidPoisonDropletSpriteTemplate");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_POISON_BUBBLE"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)]};
        var battlers=Battlers(true);battlers[3]=battlers[3] with{Visible=false};
        var runtime=new SourceBattleAnimation(script,new("acid",1,51,new(0),new(1)),battlers,true,new Audio());
        for(int tick=0;tick<3;tick++)runtime.Tick();
        var first=runtime.Tick().Sprites.Single();var a=battlers[1].Center(true,true);var b=battlers[3].Center(true,true);
        Assert.AreEqual(new PixelPoint((a.X+b.X)/2,(a.Y+b.Y)/2-22),first.Position);
    }

}
