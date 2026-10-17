using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SharedAnimationCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Sounds {get;}=[];
        public List<(int Pan,int Frame)> Pans {get;}=[];
        public bool IsPlaying{get;set;}
        public int Stops{get;private set;}
        public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);
        public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));
        public void StopAnimationEffects(){Stops++;IsPlaying=false;}
    }
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    public void PaletteFadeCompletesBeforeTaskCallbacksInSourceMainLoop()
    {
        var script=new SourceAnimationScript(105,"fade-order",[new("createvisualtask",["AnimTask_BlendColorCycle","2","F_PAL_ATTACKER","0","2","0","2","RGB_WHITE"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
        var runtime=new SourceBattleAnimation(script,new("fade-order",1,105,new(0),new(1)),Battlers(),true,new Audio());
        int ticks=0;while(runtime.Active&&ticks++<30)runtime.Tick();Assert.IsFalse(runtime.Active);
        Assert.AreEqual(16,runtime.Snapshot().Frame);Assert.AreEqual(0,runtime.Snapshot().PaletteBlends.Length);
    }
    [TestMethod][DataRow(-3,0)][DataRow(0,0)][DataRow(1,1)][DataRow(2,2)][DataRow(8,2)]
    public void RockMonUsesThreeSignedLegsClampedSpeedAndDeferredCleanup(int requested,int speed)
    {
        foreach(int actor in new[]{0,1})
        {
            var script=new SourceAnimationScript(204,"rock",[new("createvisualtask",["AnimTask_RockMonBackAndForth","5","ANIM_ATTACKER","2",requested.ToString()],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
            var runtime=new SourceBattleAnimation(script,new("rock",1,204,new(actor),new(actor^1)),Battlers(),true,new Audio());int direction=actor==0?1:-1;
            var first=runtime.Tick();var matrix=SourceAffineMatrix.FromScaleRotation(256,256,-(256+128*speed)*direction);
            Assert.AreEqual((speed+2)*direction,first.Offsets[actor].X);Assert.AreEqual(Math.Abs(matrix.C)>>3,first.Offsets[actor].Y);Assert.AreEqual(matrix.C,first.BattlerAffine[actor].MatrixC);
            int movement=(8-2*speed)*8;for(int tick=1;tick<movement;tick++)runtime.Tick();
            Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[actor]);Assert.IsTrue(runtime.Active);
            runtime.Tick();Assert.IsTrue(runtime.Active);runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.Snapshot().BattlerAffine[actor].DoubleSize);
        }
    }
    [TestMethod][DataRow(0,0)][DataRow(1,0)][DataRow(0,1)][DataRow(1,1)]
    public void MetallicPaletteModesFollowRestoreFlagThenExplicitOriginalCommand(int keep,int tint)
    {
        var script=new SourceAnimationScript(231,"metal",[new("createvisualtask",["AnimTask_MetallicShine","5",keep.ToString(),tint.ToString(),"RGB(24, 6, 23)"],1),new("waitforvisualfinish",[],2),new("createvisualtask",["AnimTask_SetGrayscaleOrOriginalPal","5","ANIM_ATTACKER","1"],3),new("end",[],4)]);
        var runtime=new SourceBattleAnimation(script,new("metal",1,231,new(0),new(1)),Battlers(),true,new Audio());var first=runtime.Tick();
        if(tint==0)CollectionAssert.AreEqual(new[]{0},first.GreyscaleSlots);else Assert.AreEqual(11,first.PaletteBlends.Single().Coefficient);
        for(int i=1;i<64;i++)runtime.Tick();var secondPass=runtime.Snapshot();
        Assert.AreEqual(keep==1&&tint==0,secondPass.GreyscaleSlots.Contains(0));Assert.AreEqual(keep==1&&tint==1,secondPass.PaletteBlends.Length!=0);
        Assert.IsFalse(secondPass.Metallic!.BackgroundLoaded);int ticks=0;while(runtime.Active&&ticks++<40)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.Snapshot().GreyscaleSlots.Length);Assert.AreEqual(0,runtime.Snapshot().PaletteBlends.Length);
    }
    [TestMethod][DataRow(0)][DataRow(1)]
    public void FireSoundTaskUsesSetupOnlyInitializerAndTwoSourcePhases(int actor)
    {
        var script=new SourceAnimationScript(126,"fire",[new("createsoundtask",["SoundTask_FireBlast","SE_M_FLAME_WHEEL","SE_M_FLAME_WHEEL2"],1),new("waitsound",[],2),new("end",[],3)]){SoundEffects=new Dictionary<string,int>{{"SE_M_FLAME_WHEEL",137},{"SE_M_FLAME_WHEEL2",138}}};
        var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("fire",1,126,new(actor),new(actor^1)),Battlers(),true,audio);
        int ticks=0;while(runtime.Active&&ticks++<140)runtime.Tick();Assert.IsFalse(runtime.Active);
        CollectionAssert.AreEqual(Enumerable.Range(0,10).Select(x=>x*11).Concat(new[]{111,117}).ToArray(),audio.Sounds.Select(x=>x.Frame).ToArray());
        Assert.AreEqual(actor==0?-64:63,audio.Sounds[0].Pan);Assert.AreEqual(actor==0?63:-63,audio.Sounds[^1].Pan);
        Assert.AreEqual(10,audio.Sounds.Count(x=>x.Id=="SE_M_FLAME_WHEEL"));
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void BattlerPaletteCycleUsesIndependentDelayAndRestoresAfterEveryCycle(int participant)
    {
        var script=new SourceAnimationScript(172,"color",[new("createvisualtask",["AnimTask_BlendMonInAndOut","3",participant.ToString(),"RGB_RED","3","2","2"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
        var runtime=new SourceBattleAnimation(script,new("color",1,172,new(0),new(1)),Battlers(),true,new Audio());
        var coefficients=new List<int>();for(int i=0;i<24;i++)coefficients.Add(runtime.Tick().PaletteBlends.SingleOrDefault()?.Coefficient??0);
        CollectionAssert.AreEqual(new[]{0,1,1,2,2,3,3,2,2,1,1,0,0,1,1,2,2,3,3,2,2,1,1,0},coefficients);
        runtime.Tick();Assert.IsFalse(runtime.Active);
        var hidden=Battlers().Select(b=>b with{Visible=b.Slot.Value!=participant}).ToArray();
        var absent=new SourceBattleAnimation(script,new("color",1,172,new(0),new(1)),hidden,true,new Audio());Assert.IsFalse(absent.Tick().Active);
    }
    [TestMethod][DataRow(0)][DataRow(1)]
    public void SoundCallbackInitializesImmediatelyThenAdvancesTwiceInCreationFrame(int actor)
    {
        var script=new SourceAnimationScript(58,"sound",[new("createsoundtask",["SoundTask_LoopSEAdjustPanning","SE_M_BUBBLE_BEAM2","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","4","4","0","10"],1),new("waitsound",[],2),new("end",[],3)]){SoundEffects=new Dictionary<string,int>{{"SE_M_BUBBLE_BEAM2",176}}};
        var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("sound",1,58,new(actor),new(actor^1)),Battlers(),true,audio);
        int ticks=0;while(runtime.Active&&ticks++<100)runtime.Tick();
        Assert.IsFalse(runtime.Active);CollectionAssert.AreEqual(new[]{0,10,21,32},audio.Sounds.Select(x=>x.Frame).ToArray());
        CollectionAssert.AreEqual(actor==0?new[]{-64,-20,24,63}:new[]{63,19,-25,-64},audio.Sounds.Select(x=>x.Pan).ToArray());
    }
    [TestMethod]
    public void SoundWaitStopsStuckPlaybackOnNinetyFirstPollThenResumesNextFrame()
    {
        var script=new SourceAnimationScript(58,"wait",[new("waitsound",[],1),new("playsewithpan",["SE_M_DOUBLE_SLAP","SOUND_PAN_ATTACKER"],2),new("end",[],3)]);
        var audio=new Audio{IsPlaying=true};var runtime=new SourceBattleAnimation(script,new("wait",1,58,new(0),new(1)),Battlers(),true,audio);
        for(int i=0;i<90;i++)runtime.Tick();Assert.AreEqual(0,audio.Stops);Assert.AreEqual(0,audio.Sounds.Count);
        runtime.Tick();Assert.AreEqual(1,audio.Stops);Assert.AreEqual(0,audio.Sounds.Count);Assert.IsTrue(runtime.Active);
        runtime.Tick();Assert.AreEqual(91,audio.Sounds.Single().Frame);Assert.IsFalse(runtime.Active);
    }
    [TestMethod][DataRow(0)][DataRow(1)]
    public void PanSweepUsesSourceCreationFrameStepAndExactAsymmetricEndpoints(int actor)
    {
        var script=new SourceAnimationScript(53,"pan",[new("panse",["SE_M_FLAMETHROWER","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","+2","0"],1),new("end",[],2)]){SoundEffects=new Dictionary<string,int>{{"SE_M_FLAMETHROWER",139}}};
        var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("pan",1,53,new(actor),new(actor^1)),Battlers(),true,audio);
        int frames=0;while(runtime.Active&&frames++<100)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(1,audio.Sounds.Count);Assert.AreEqual(actor==0?-64:63,audio.Sounds[0].Pan);
        Assert.AreEqual(actor==0?64:63,audio.Pans.Count);Assert.AreEqual((actor==0?-62:61,0),audio.Pans[0]);Assert.AreEqual((actor==0?63:-63,actor==0?63:62),audio.Pans[^1]);
    }
    [TestMethod]
    public void PanSweepUsesGeneratedSongDependenciesAndRejectsMissingOrMalformedOperands()
    {
        var command=new SourceAnimationCommand("panse",["SE_M_BLIZZARD","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","2","0"],1);
        var script=new SourceAnimationScript(59,"pan-dependency",[command,new("end",[],2)])
        {SoundEffects=new Dictionary<string,int>{{"SE_M_BLIZZARD",146}}};
        var cue=new CommittedMoveAnimation("pan",1,59,new(0),new(1));var audio=new Audio();
        var runtime=new SourceBattleAnimation(script,cue,Battlers(),true,audio);runtime.Tick();
        Assert.AreEqual("SE_M_BLIZZARD",audio.Sounds.Single().Id);runtime.Cancel();
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{SoundEffects=new Dictionary<string,int>()},cue,Battlers(),true,new Audio()));
        foreach(var args in new[]{
            new[]{"SE_NOT_REGISTERED","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","2","0"},
            new[]{"SE_M_BLIZZARD","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","128","0"},
            new[]{"SE_M_BLIZZARD","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","2","256"}})
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[command with{Args=args},new("end",[],2)]},cue,Battlers(),true,new Audio()));
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void SplitBackgroundPriorityAppliesOnlyForSourceLeadPositions(int target)
    {
        var script=new SourceAnimationScript(53,"priority",[new("monbg",["ANIM_TARGET"],1),new("splitbgprio",["ANIM_TARGET"],2),new("delay",["2"],3),new("end",[],4)]);
        var runtime=new SourceBattleAnimation(script,new("priority",1,53,new(target^1),new(target)),Battlers(),true,new Audio());
        var frame=runtime.Tick();CollectionAssert.AreEqual(target is 0 or 3?new[]{1,2}:new[]{2,2},frame.BattlerBackgroundPriorities);
        runtime.Cancel();CollectionAssert.AreEqual(new[]{0,1},runtime.Snapshot().BattlerBackgroundPriorities);
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void BackgroundCopyWritesOnlyVisibleBankAndClearRetainsHardwarePriorities(int target)
    {
        var battlers=Battlers().Select(b=>b with{Visible=b.Slot.Value==target}).ToArray();
        var script=new SourceAnimationScript(53,"registers",[new("monbg",["ANIM_TARGET"],1),new("delay",["2"],2),new("clearmonbg",["ANIM_TARGET"],3),new("delay",["4"],4),new("end",[],5)]);
        var runtime=new SourceBattleAnimation(script,new("registers",1,53,new(target^1),new(target)),battlers,true,new Audio());
        CollectionAssert.AreEqual(new[]{0,1},runtime.Snapshot().BattlerBackgroundPriorities);
        var first=runtime.Tick();CollectionAssert.AreEqual(new[]{target},first.BackgroundSlots);
        var expected=target is 0 or 3?new[]{0,2}:new[]{2,1};
        CollectionAssert.AreEqual(expected,first.BattlerBackgroundPriorities);
        for(int ticks=0;runtime.Active&&ticks<20;ticks++)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.Snapshot().BackgroundSlots.Length);
        CollectionAssert.AreEqual(expected,runtime.Snapshot().BattlerBackgroundPriorities);
        runtime.Cancel();CollectionAssert.AreEqual(new[]{0,1},runtime.Snapshot().BattlerBackgroundPriorities);
    }
    [TestMethod][DataRow(0,2,false)][DataRow(1,2,false)][DataRow(2,2,true)][DataRow(3,2,true)][DataRow(3,3,true)][DataRow(2,3,false)]
    public void NumericBackgroundPriorityUsesHardwareObjectTieRule(int backgroundPriority,int objectPriority,bool objectWins)
    {
        var battlers=Battlers().Select(b=>b with{Visible=b.Slot.Value==1,DisplayCenter=new PixelPoint(40,40)}).ToArray();
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,Enumerable.Repeat((ushort)0x83e0,1024).ToArray())}};
        foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
        var composer=new SourceBattleFrameComposer(assets,battlers);
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[1],0,0,[],[]){BattlerBackgroundPriorities=[backgroundPriority,1],Sprites=[new(4,"test",new(40,40),default,0,true,false,false,256,"impact",1){OamPriority=objectPriority}]};
        var pixels=composer.ComposeField(frame);int at=(40*240+40)*4;
        Assert.AreEqual((byte)(objectWins?0:255),pixels[at]);Assert.AreEqual((byte)(objectWins?255:0),pixels[at+1]);
    }

    [TestMethod]
    public void PromotedBackgroundOccludesPriorityTwoEffectPixels()
    {
        var battlers=Battlers().Select(b=>b with{Visible=b.Slot.Value==1,DisplayCenter=new PixelPoint(40,40)}).ToArray();
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,Enumerable.Repeat((ushort)0x83e0,1024).ToArray())}};
        foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
        var composer=new SourceBattleFrameComposer(assets,battlers);
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[1],0,0,[],[]){Doubles=true,BattlerBackgroundPriorities=[2,2],BattlerOamRanks=[0,1,2,3],Sprites=[new(4,"test",new(40,40),default,0,true,false,false,256,"impact",1){OamRank=4}]};
        int at=(40*240+40)*4;var ordinary=composer.ComposeField(frame);Assert.AreEqual((byte)255,ordinary[at+1]);
        var promoted=composer.ComposeField(frame with{BattlerBackgroundPriorities=[1,2]});Assert.AreEqual((byte)255,promoted[at]);Assert.AreEqual((byte)0,promoted[at+1]);
    }

    [TestMethod]
    public void PlainEllipseRetainsScriptDirectionOnBothAttackerSides()
    {
        var shared=AnimationCompositionFixture.Script(1);
        var script=shared with{MoveId=66,Commands=[new("createvisualtask",["AnimTask_TranslateMonElliptical","2","ANIM_ATTACKER","18","6","6","4"],1),new("waitforvisualfinish",[],2),new("end",[],3)]};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("ellipse",1,66,new(actor),new(actor^1)),Battlers(),true,new Audio());
            var frame=runtime.Tick();Assert.AreEqual((18*shared.SineTable[16])>>8,frame.Offsets[actor].X);
            int frames=0;while(runtime.Active&&frames++<120)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[actor]);
        }
    }
    [TestMethod]
    public void SharedSubroutinesExecuteSourceCommandsWithoutLabelSpecificRuntimeCases()
    {
        var script=new SourceAnimationScript(66,"call",[new("call",["ImportedSourceRoutine"],1),new("end",[],2)]){Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"ImportedSourceRoutine",[new("delay",["2"],3),new("return",[],4)]}}};
        var runtime=new SourceBattleAnimation(script,new("call",1,66,new(0),new(1)),Battlers(),true,new Audio());
        int frames=0;while(runtime.Active&&frames++<10)runtime.Tick();Assert.IsFalse(runtime.Active);
        var recursive=script with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"ImportedSourceRoutine",[new("call",["ImportedSourceRoutine"],3),new("return",[],4)]}}};
        Assert.Throws<InvalidDataException>(()=>new SourceBattleAnimation(recursive,new("call",1,66,new(0),new(1)),Battlers(),true,new Audio()));
    }

    [TestMethod]
    public void SceneRetiredRawSpritesHaveBoundedLifetimeAndRestoreTheIdleField()
    {
        string folder=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var resources=new SourceAnimationResources(folder);
        var source=resources.Scripts[84];
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SPARK"],1),new("createvisualtask",["AnimTask_ElectricBolt","5","0","-44","0"],2),new("waitforvisualfinish",[],3),new("end",[],4)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var resolved=resources.Resolve(script);script=resolved.Script;
        var cue=new CommittedMoveAnimation("retire",1,84,new(0),new(1));var gate=new SourceBattleAnimationGate();
        var scene=new SourceBattleAnimation.SpriteScene();
        Assert.IsTrue(gate.TryStart(cue,()=>new(script,cue,Battlers(),true,new Audio(),spriteScene:scene)));
        var retired=gate.Current!;int ticks=0;while(gate.IsBlocking&&ticks++<40)retired.Tick();
        Assert.IsFalse(gate.IsBlocking);Assert.IsTrue(retired.HasResidualSprites);Assert.IsFalse(retired.GraphicsLoaded);int endFrame=retired.Snapshot().Frame;
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
        assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in Battlers())assets.Add(b.Asset,new(64,64,new ushort[4096]));
        var composer=AnimationCompositionFixture.Create(resources,resolved,assets,Battlers());
        var idle=scene.CaptureIdle(Battlers(),true);
        var baseline=composer.ComposeField(idle with{Sprites=[]});
        var first=composer.ComposeField(idle);Assert.IsFalse(first.SequenceEqual(baseline));
        int tailTicks=0;
        while(retired.HasResidualSprites&&tailTicks++<40)
        {
            var frame=scene.TickIdle(Battlers(),true);Assert.IsFalse(frame.Active);Assert.AreEqual(0,retired.ActiveVisualCount);Assert.AreEqual(endFrame,retired.Snapshot().Frame);
            composer.ComposeField(frame);
        }
        Assert.AreEqual(11,tailTicks);Assert.IsFalse(gate.IsBlocking);Assert.AreEqual(0,scene.CaptureIdle(Battlers(),true).Sprites.Length);Assert.IsFalse(retired.HasResidualSprites);
        CollectionAssert.AreEqual(baseline,composer.ComposeField(scene.CaptureIdle(Battlers(),true)));
    }

    [TestMethod]
    public void FoeBackgroundSplitKeepsSameSidePartnersAtOriginalPriority()
    {
        foreach(int target in new[]{2,3})
        {
            var script=new SourceAnimationScript(48,"foes",[new("monbg",["ANIM_ATK_PARTNER"],1),new("splitbgprio_foes",["ANIM_ATTACKER"],2),new("delay",["2"],3),new("end",[],4)]);
            var runtime=new SourceBattleAnimation(script,new("foes",1,48,new(0),new(target)),Battlers(),true,new Audio());
            var frame=runtime.Tick();CollectionAssert.AreEqual(new[]{0,2},frame.BackgroundSlots);CollectionAssert.AreEqual(target==3?new[]{1,2}:new[]{2,2},frame.BattlerBackgroundPriorities);runtime.Cancel();
        }
    }
    [TestMethod]
    public void SwayMonUsesUnsignedPhaseSideSignAndHalfWaveCount()
    {
        var source=AnimationCompositionFixture.Script(1);
        var script=source with{Commands=[new("createvisualtask",["AnimTask_SwayMon","5","0","6","2048","2","ANIM_TARGET"],1),new("waitforvisualfinish",[],2),new("end",[],3)]};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("sway",1,1,new(actor),new(actor^1)),Battlers(),true,new Audio());
            var first=runtime.Tick();Assert.AreEqual((source.SineTable[8]*(actor==0?6:-6))>>8,first.Offsets[actor^1].X);
            int ticks=1;while(runtime.Active&&ticks++<100)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(ticks>=32&&ticks<=34);Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[actor^1]);
        }
    }
    [TestMethod]
    public void GeneralizedPaletteCycleRunsSixAlternatingFadesAndRestoresTargetBank()
    {
        var script=new SourceAnimationScript(105,"cycle",[new("createvisualtask",["AnimTask_BlendColorCycle","2","F_PAL_TARGET","0","6","0","11","RGB(31,31,11)"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
        var runtime=new SourceBattleAnimation(script,new("cycle",1,105,new(0),new(1)),Battlers(),true,new Audio());
        int peaks=0,last=0,ticks=0;
        while(runtime.Active&&ticks++<250)
        {
            var frame=runtime.Tick();Assert.IsTrue(frame.PaletteBlends.All(p=>p.Palette==17));
            int current=frame.PaletteBlends.FirstOrDefault()?.Coefficient??0;if(current==11&&last!=11)peaks++;last=current;
        }
        Assert.IsFalse(runtime.Active);Assert.AreEqual(3,peaks);Assert.AreEqual(0,runtime.Snapshot().PaletteBlends.Length);
    }

    [TestMethod]
    public void StartingAnotherCycleDoesNotOverwriteAnActiveGlobalFade()
    {
        var script=new SourceAnimationScript(105,"busy-fade",[
            new("createvisualtask",["AnimTask_BlendColorCycle","2","F_PAL_ATTACKER","0","2","0","8","RGB_WHITE"],1),
            new("createvisualtask",["AnimTask_BlendColorCycle","2","F_PAL_TARGET","0","1","0","11","RGB(31,31,11)"],2),
            new("waitforvisualfinish",[],3),new("end",[],4)]);
        var runtime=new SourceBattleAnimation(script,new("busy",1,105,new(0),new(1)),Battlers(),true,new Audio());
        bool sawActor=false;int ticks=0;
        while(runtime.Active&&ticks++<100)
        {var frame=runtime.Tick();Assert.IsTrue(frame.PaletteBlends.All(p=>p.Palette==16));sawActor|=frame.PaletteBlends.Any(p=>p.Coefficient==8);}
        Assert.IsFalse(runtime.Active);Assert.IsTrue(sawActor);Assert.AreEqual(0,runtime.Snapshot().PaletteBlends.Length);
    }

    [TestMethod]
    public void PartnerShakeResolvesSourceConstantAndSkipsAbsentPartner()
    {
        var script=new SourceAnimationScript(51,"partner",[new("createvisualtask",["AnimTask_ShakeMon2","2","ANIM_DEF_PARTNER","3","0","4","1"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
        foreach(bool visible in new[]{false,true})
        {
            var battlers=Battlers().Select(b=>b.Slot.Value==3?b with{Visible=visible}:b).ToArray();
            var runtime=new SourceBattleAnimation(script,new("partner",1,51,new(0),new(1)),battlers,true,new Audio());
            bool moved=false;int ticks=0;
            while(runtime.Active&&ticks++<30){var f=runtime.Tick();moved|=f.Offsets[3]!=default;Assert.IsTrue(f.Offsets.Take(3).All(p=>p==default));}
            Assert.IsFalse(runtime.Active);Assert.AreEqual(visible,moved);Assert.AreEqual(default(PixelPoint),runtime.Snapshot().Offsets[3]);
        }
    }

    [TestMethod]
    public void GeneratedLabelJumpSkipsUnreachableCommandsAndRejectsMissingLabels()
    {
        var script=new SourceAnimationScript(20,"jump",[new("goto",["shared"],1),new("delay",["200"],2),new("delay",["1"],3),new("end",[],4)]){Labels=new Dictionary<string,int>{{"shared",2}}};
        var runtime=new SourceBattleAnimation(script,new("jump",1,20,new(0),new(1)),Battlers(),true,new Audio());
        int ticks=0;while(runtime.Active&&ticks++<10)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(ticks<6);
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Labels=new Dictionary<string,int>()},new("bad",1,20,new(0),new(1)),Battlers(),true,new Audio()));
        var joined=script with{Commands=[new("goto",["alternate"],1),new("end",[],2),new("delay",["1"],3),new("goto",["finish"],4)],Labels=new Dictionary<string,int>{{"alternate",2},{"finish",1}}};
        var joinedRuntime=new SourceBattleAnimation(joined,new("joined",1,20,new(0),new(1)),Battlers(),true,new Audio());
        int joinedTicks=0;while(joinedRuntime.Active&&joinedTicks++<10)joinedRuntime.Tick();Assert.IsFalse(joinedRuntime.Active);
        var falloff=joined with{Commands=[new("delay",["1"],1)],Labels=new Dictionary<string,int>()};
        Assert.Throws<InvalidDataException>(()=>new SourceBattleAnimation(falloff,new("falloff",1,20,new(0),new(1)),Battlers(),true,new Audio()));
        var cycle=script with{Commands=[new("goto",["shared"],1),new("end",[],2)],Labels=new Dictionary<string,int>{{"shared",0}}};
        Assert.Throws<InvalidDataException>(()=>new SourceBattleAnimation(cycle,new("bounded",1,20,new(0),new(1)),Battlers(),true,new Audio()));
    }
    [TestMethod]
    public void ParameterizedScaleUsesIndependentSignedAxesAndRestoresSelectedBattler()
    {
        foreach(int actor in new[]{0,1})foreach(int participant in new[]{0,1})
        {
            var script=new SourceAnimationScript(20,"scale",[new("createvisualtask",["AnimTask_ScaleMonAndRestore","5","10","-5","5",participant.ToString(),"0"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
            int slot=participant==0?actor:actor^1;var runtime=new SourceBattleAnimation(script,new("scale",1,20,new(actor),new(actor^1)),Battlers(),true,new Audio());
            SourceAnimationFrame frame=runtime.Tick();Assert.AreEqual(266,frame.BattlerAffine[slot].XScale);Assert.AreEqual(251,frame.BattlerAffine[slot].YScale);
            for(int tick=1;tick<5;tick++)frame=runtime.Tick();Assert.AreEqual(306,frame.BattlerAffine[slot].XScale);Assert.AreEqual(231,frame.BattlerAffine[slot].YScale);
            int remaining=0;while(runtime.Active&&remaining++<15)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(256,runtime.Snapshot().BattlerAffine[slot].XScale);Assert.AreEqual(256,runtime.Snapshot().BattlerAffine[slot].YScale);Assert.IsFalse(runtime.Snapshot().BattlerAffine[slot].DoubleSize);
        }
    }

}
