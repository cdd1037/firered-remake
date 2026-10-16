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
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var script=resources.Scripts[move];return script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value)};
    }
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    [DataRow(7)][DataRow(52)][DataRow(53)][DataRow(56)][DataRow(190)][DataRow(324)][DataRow(341)][DataRow(84)][DataRow(86)][DataRow(9)][DataRow(55)][DataRow(85)][DataRow(121)][DataRow(152)][DataRow(192)][DataRow(48)][DataRow(103)][DataRow(319)][DataRow(71)][DataRow(72)][DataRow(202)][DataRow(105)][DataRow(51)][DataRow(61)][DataRow(92)][DataRow(141)][DataRow(145)][DataRow(83)][DataRow(250)][DataRow(328)][DataRow(44)][DataRow(128)][DataRow(116)][DataRow(203)][DataRow(8)][DataRow(58)][DataRow(181)][DataRow(82)][DataRow(126)][DataRow(172)][DataRow(225)][DataRow(204)][DataRow(343)][DataRow(231)][DataRow(342)][DataRow(75)][DataRow(129)][DataRow(223)][DataRow(200)]
    public void SourceElementalRootsCompleteForEveryCanonicalActor(int move)
    {
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),new("elemental",1,move,new(actor),new(actor^1)),Battlers(doubles),doubles,audio);
            int frames=0;bool visual=false;
            while(runtime.Active&&frames++<1500){var f=runtime.Tick();visual|=f.Sprites.Any(s=>s.Asset is not null);}
            Assert.IsFalse(runtime.Active,$"move {move}, actor {actor}");Assert.IsTrue(visual);Assert.IsTrue(audio.Cues.Count>0);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.AreEqual(Script(move).Commands.LastOrDefault(c=>c.Op is "setalpha" or "blendoff")?.Op=="setalpha",final.BlendEnabled);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.PaletteBlends.Length);
            if(move is 53 or 56 or 324 or 341)Assert.IsTrue(audio.Pans.Count>0);
        }
    }
    [TestMethod]
    public void EveryElementalFamilyComposesItsRealGeneratedSpriteSheets()
    {
        string folder=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var resources=new SourceAnimationResources(folder);
        foreach(var script in resources.Scripts.Values.Where(s=>SourceElementalAnimationCatalog.Supports(s.MoveId)))
        {
            var resolved=resources.Resolve(script.MoveId);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in Battlers(true))assets.Add(b.Asset,new(64,64,new ushort[4096]));
            var composer=new SourceBattleFrameComposer(assets,Battlers(true),resolved.StatMask,resolved.Metallic);
            var runtime=new SourceBattleAnimation(resolved.Script,new("pixels",1,script.MoveId,new(2),new(3)),Battlers(true),true,new Audio());
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

    [TestMethod]
    public void EmberDiagonalPictureModeRetainsChosenMonAndAppliesBothInitialOffsets()
    {
        var source=Script(52);var sprite=source.Subroutines["EmberFireHit"].First(c=>c.Op=="createsprite");
        foreach(int actor in new[]{0,1})foreach(int mode in new[]{0,1})
        {
            var args=sprite.Args.ToArray();args[3]="5";args[4]="-3";args[8]=mode.ToString();args[9]="0";
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("embermode",1,52,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int i=0;i<3;i++)runtime.Tick();
            var frame=runtime.Tick().Sprites.Single();var mon=Battlers(false)[mode==0?actor:actor^1].Center(false,true);
            Assert.AreEqual(new PixelPoint(mon.X+(actor==0?10:-10),mon.Y-6),frame.Position);
        }
    }
    [TestMethod]
    public void VortexUsesBaseCoordinatesSignedAccumulationAndPostdecrementLifetime()
    {
        var source=Script(83);var sprite=source.Subroutines["FireSpinEffect"].First(c=>c.Op=="createsprite");
        foreach(bool doubles in new[]{false,true})foreach(int actor in new[]{0,1})foreach(int participant in new[]{0,1})
        {
            var args=sprite.Args.ToArray();args[3]="5";args[4]="-3";args[5]="-528";args[6]="4";args[7]="13";args[8]="-46";args[9]=participant.ToString();
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("vortex",1,83,new(actor),new(actor^1)),Battlers(doubles),doubles,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            var mon=Battlers(doubles)[participant==0?actor:actor^1].Base(doubles);
            for(int tick=1;tick<=4;tick++)
            {
                var frame=runtime.Tick().Sprites.Single();
                Assert.AreEqual(new PixelPoint(mon.X+(actor==0?5:-5),mon.Y-3),frame.Position);
                Assert.AreEqual(new PixelPoint((source.SineTable[(tick-1)*13]*-46)>>8,-(((short)(tick*-528))>>8)),frame.Offset);
            }
            Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            for(int i=0;i<3&&runtime.Active;i++)runtime.Tick();Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod]
    public void WhirlpoolAffineOscillatesWithoutInventingSourceBlendOff()
    {
        var source=Script(250);var recipe=source.SpriteRecipes["gWhirlpoolSpriteTemplate"];
        Assert.IsTrue(recipe.Blend);var affine=new SourceSpriteAffineProgram(recipe.AffineAnimations[0]);
        Assert.AreEqual(192,affine.XScale);Assert.AreEqual(192,affine.YScale);
        for(int i=0;i<5;i++)affine.Step();Assert.AreEqual(202,affine.XScale);Assert.AreEqual(177,affine.YScale);
        for(int i=0;i<5;i++)affine.Step();Assert.AreEqual(192,affine.XScale);Assert.AreEqual(192,affine.YScale);
        affine.Step();Assert.AreEqual(194,affine.XScale);Assert.AreEqual(189,affine.YScale);
        Assert.IsFalse(source.Commands.Any(c=>c.Op=="blendoff"));
        var runtime=new SourceBattleAnimation(source,new("whirlpool",1,250,new(0),new(1)),Battlers(false),false,new Audio());
        int frames=0;while(runtime.Active&&frames++<1500)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(runtime.Snapshot().BlendEnabled);
    }
    [TestMethod]
    public void BiteUsesTargetAnchorUnmirroredOffsetsAndTwoExactFixedPointLegs()
    {
        var source=Script(44);var sprite=source.Commands.First(c=>c.Op=="createsprite");
        foreach(int actor in new[]{0,1})foreach(int rotation in new[]{0,2,4,6})
        {
            var args=sprite.Args.ToArray();args[3]="5";args[4]="-3";args[5]=rotation.ToString();args[6]="819";args[7]="-819";args[8]="3";
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SHARP_TEETH"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("bite",1,44,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int i=0;i<3;i++)runtime.Tick();
            var center=Battlers(false)[actor^1].Center(false,true);int[] distances=[1,2,3,2,1];
            foreach(int distance in distances)
            {
                var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(center.X+5,center.Y-3),frame.Position);
                Assert.AreEqual(new PixelPoint((distance*819)>>8,(-distance*819)>>8),frame.Offset);
                Assert.AreEqual(64,frame.Width);Assert.AreEqual(64,frame.Height);Assert.IsTrue(frame.Affine);Assert.IsTrue(frame.Blend);
                var matrix=SourceAffineMatrix.FromScaleRotation(256,256,(rotation*32)<<8);
                Assert.AreEqual(matrix.A,frame.MatrixA);Assert.AreEqual(matrix.B,frame.MatrixB);Assert.AreEqual(matrix.C,frame.MatrixC);Assert.AreEqual(matrix.D,frame.MatrixD);
            }
            Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        }
    }
    [TestMethod]
    public void EndureEnergyUsesBasePositionTimerAccelerationAndFrameEndLifetime()
    {
        var source=Script(116);var sprite=source.Subroutines["EndureEffect"].First(c=>c.Op=="createsprite");
        foreach(int actor in new[]{0,1})foreach(int participant in new[]{0,1})
        {
            var args=sprite.Args.ToArray();args[3]=participant.ToString();args[4]="5";args[5]="26";args[6]="2";
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_FOCUS_ENERGY"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("energy",1,116,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int i=0;i<3;i++)runtime.Tick();var center=Battlers(false)[participant==0?actor:actor^1].Base(false);int y=center.Y+26,timer=0;
            for(int tick=1;tick<=24;tick++)
            {
                if(++timer>2){timer=0;y--;}y-=timer;
                var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(center.X+5,y),frame.Position);Assert.AreEqual(default(PixelPoint),frame.Offset);
                Assert.AreEqual(tick<4?0:tick<16?8:tick<20?16:24,frame.Tile);
            }
            Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        }
    }
    [TestMethod]
    public void IceHitWaitsForAffineEndThenHandsOffToTwentyFlickerCallbacks()
    {
        var source=Script(8);var sprite=source.Subroutines["IceCrystalEffectShort"].First(c=>c.Op=="createsprite");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_ICE_CRYSTALS"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("icehit",1,8,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();
        for(int tick=1;tick<=37;tick++)
        {
            var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(tick<=18||(tick-18)%2==0,frame.Visible);
            Assert.AreEqual(65536/(206+5*Math.Min(tick,10)),frame.MatrixA);
        }
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
    [TestMethod]
    public void PowderSnowUsesFourBitReverseEntryAndForwardAccumulators()
    {
        var source=Script(181);var sprite=source.Subroutines["PowderSnowSnowballs"].First(c=>c.Op=="createsprite");
        foreach(int actor in new[]{0,1})
        {
            var args=sprite.Args.ToArray();args[8]="0";args[10]="0";
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_ICE_CRYSTALS"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("snow",1,181,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            var start=Battlers(false)[actor].Center(false,true);var target=Battlers(false)[actor^1].Center(false,true);
            int dx=target.X-start.X,dy=target.Y-start.Y,duration=(Math.Abs(dx)<<4)/56;
            int vx=(((Math.Abs(dx)<<4)/duration)&~1)|(dx<0?1:0),vy=(((Math.Abs(dy)<<4)/duration)&~1)|(dy<0?1:0),rx=0,ry=0,bx=0,by=0;
            do{rx=(ushort)(rx+(vx^1));ry=(ushort)(ry+(vy^1));bx=((vx^1)&1)!=0?-(rx>>4):rx>>4;by=((vy^1)&1)!=0?-(ry>>4):ry>>4;}while(start.X+bx is >=-16 and <=256&&start.Y+by is >=-16 and <=160);
            for(int i=0;i<3;i++)runtime.Tick();
            for(int tick=1;tick<=duration;tick++)
            {
                var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(start.X+bx,start.Y+by),frame.Position);
                Assert.AreEqual(new PixelPoint((vx&1)!=0?-((ushort)(tick*vx)>>4):(ushort)(tick*vx)>>4,(vy&1)!=0?-((ushort)(tick*vy)>>4):(ushort)(tick*vy)>>4),frame.Offset);
            }
            int remaining=0;while(runtime.Active&&remaining++<200)runtime.Tick();Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod]
    public void DragonFirePreservesOpponentInitialXAsymmetryAndAffineMode()
    {
        var source=Script(82);var sprite=source.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]=="gDragonRageFireSpitSpriteTemplate");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)]};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("dragon",1,82,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int i=0;i<3;i++)runtime.Tick();var frame=runtime.Tick().Sprites.Single();var center=Battlers(false)[actor].Center(false,true);
            Assert.AreEqual(new PixelPoint(center.X+(actor==0?30:-15),center.Y+15),frame.Position);
            var matrix=SourceAffineMatrix.FromScaleRotation(65536/356,65536/356,actor==0?127<<8:0);Assert.AreEqual(matrix.A,frame.MatrixA);Assert.AreEqual(matrix.B,frame.MatrixB);
        }
    }
    [TestMethod]
    public void OutwardFireWaitsForCallbackHandoffBeforeGrowingSpiral()
    {
        var source=Script(172);var sprite=source.Commands.First(c=>c.Op=="createsprite");var args=sprite.Args.ToArray();args[5]="3";args[6]="2";
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)]};
        var runtime=new SourceBattleAnimation(script,new("spiral",1,172,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();for(int i=0;i<3;i++)Assert.IsFalse(runtime.Tick().Sprites.Single().Visible);
        for(int step=0;step<3;step++)
        {
            var frame=runtime.Tick().Sprites.Single();Assert.IsTrue(frame.Visible);int radius=(step*208)>>8,angle=step*10;
            Assert.AreEqual(new PixelPoint((source.SineTable[angle]*radius)>>8,(source.SineTable[angle+64]*radius)>>8),frame.Offset);
        }
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
    [TestMethod]
    public void FireRingCirclesThenTranslatesThenResetsTargetAnchorForFinalCircle()
    {
        var source=Script(126);var sprite=source.Subroutines["FireBlastRing"].First(c=>c.Op=="createsprite");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite,new("waitforvisualfinish",[],2),new("end",[],3)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("ring",1,126,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();var actor=Battlers(false)[0].Center(false,true);var target=Battlers(false)[1].Center(false,true);
        for(int tick=1;tick<=73;tick++)
        {
            var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(tick<44?actor:target,frame.Position);
            if(tick<=18||tick>=44){int angle=((tick-1)*20)&255;Assert.AreEqual(new PixelPoint((source.SineTable[angle]*28)>>8,(source.SineTable[angle+64]*28)>>8),frame.Offset);}
        }
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
    [TestMethod]
    public void MagentaHeartStartsAtActorBaseAndRunsInitializerBeforeCreationFrameStep()
    {
        var source=Script(204);var sprite=source.Commands.First(c=>c.Op=="createsprite");var args=sprite.Args.ToArray();args[3]="5";
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_MAGENTA_HEART"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)]};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("heart",1,204,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            for(int i=0;i<3;i++)runtime.Tick();var center=Battlers(false)[actor].Base(false);
            for(int tick=1;tick<=58;tick++)
            {
                var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(center.X+(actor==0?5:-5),center.Y+20),frame.Position);
                Assert.AreEqual(new PixelPoint((source.SineTable[(tick*7)&255]*8)>>8,(-tick*128)>>8),frame.Offset);
            }
            Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        }
    }
    [TestMethod]
    public void SwiftContinuesSourceLinearAccumulatorsBeyondTranslationDuration()
    {
        var source=Script(129);var sprite=source.Commands.First(c=>c.Op=="createsprite");var args=sprite.Args.ToArray();args[8]="0";args[9]="0";
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_YELLOW_STAR"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)]};
        var runtime=new SourceBattleAnimation(script,new("swift",1,129,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();var start=Battlers(false)[0].Center(false,true);start=new(start.X+20,start.Y-10);var target=Battlers(false)[1].Center(false,true);target=new(target.X+20,target.Y);
        int vx=((Math.Abs(target.X-start.X)<<8)/22&~1)|(target.X<start.X?1:0),vy=((Math.Abs(target.Y-start.Y)<<8)/22&~1)|(target.Y<start.Y?1:0);
        for(int tick=1;tick<=23;tick++)
        {
            var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(start,frame.Position);
            Assert.AreEqual(new PixelPoint(((ushort)(tick*vx)>>8)*((vx&1)==0?1:-1),((ushort)(tick*vy)>>8)*((vy&1)==0?1:-1)),frame.Offset);
        }
        int left=0;while(runtime.Active&&left++<100)runtime.Tick();Assert.IsFalse(runtime.Active);
    }
    [TestMethod]
    public void RazorLeafParticleHasSeparateRiseHandoffAndEightyOneFallCallbacks()
    {
        var source=Script(75);var sprite=source.Commands.First(c=>c.Op=="createsprite");var args=sprite.Args.ToArray();args[3]="2";args[4]="-1";args[5]="3";
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_LEAF"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)]};
        var runtime=new SourceBattleAnimation(script,new("leaf",1,75,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();var center=Battlers(false)[0].Center(false,true);
        for(int tick=1;tick<=4;tick++){var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(center.X+2*Math.Min(tick,3),center.Y-Math.Min(tick,3)),frame.Position);Assert.AreEqual(default(PixelPoint),frame.Offset);}
        for(int tick=1;tick<=80;tick++){var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint((source.SineTable[(128+(tick-1)*2)&255]*25)>>8,tick/2),frame.Offset);}
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
    [TestMethod]
    [DataRow(0,1)][DataRow(1,0)]
    public void OutrageSamplesBeforeAccumulationMirrorsBothAxesAndHandsOffBeforeDestroy(int actor,int target)
    {
        var source=Script(200);var sprite=source.Commands.First(c=>c.Op=="createsprite");var args=sprite.Args.ToArray();args[3]="4";args[4]="-3";args[5]="6";args[6]="384";args[7]="-128";args[8]="2";
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SMALL_EMBER"],1),sprite with{Args=args},new("waitforvisualfinish",[],2),new("end",[],3)]};
        var runtime=new SourceBattleAnimation(script,new("outrage",1,200,new(actor),new(target)),Battlers(false),false,new Audio());
        for(int i=0;i<3;i++)runtime.Tick();var center=Battlers(false)[actor].Center(false,true);int direction=actor==0?1:-1;
        for(int tick=1;tick<=7;tick++)
        {
            var frame=runtime.Tick().Sprites.Single();Assert.AreEqual(new PixelPoint(center.X+direction*4,center.Y-3),frame.Position);
            int sample=Math.Min(tick-1,5);Assert.AreEqual(new PixelPoint((short)(sample*384*direction)>>8,(short)(sample*-128*direction)>>8),frame.Offset);
            Assert.AreEqual(tick is 2 or 3 or 6 or 7,frame.Visible);
        }
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
}
