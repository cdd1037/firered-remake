using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SharedAnimationCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Sounds {get;}=[];
        public List<(int Pan,int Frame)> Pans {get;}=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);
        public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));
        public void StopAnimationEffects(){}
    }
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod][DataRow(0)][DataRow(1)]
    public void PanSweepUsesSourceCreationFrameStepAndExactAsymmetricEndpoints(int actor)
    {
        var script=new SourceAnimationScript(53,"pan",[new("panse",["SE_M_FLAMETHROWER","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","+2","0"],1),new("end",[],2)]);
        var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("pan",1,53,new(actor),new(actor^1)),Battlers(),true,audio);
        int frames=0;while(runtime.Active&&frames++<100)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(1,audio.Sounds.Count);Assert.AreEqual(actor==0?-64:63,audio.Sounds[0].Pan);
        Assert.AreEqual(actor==0?64:63,audio.Pans.Count);Assert.AreEqual((actor==0?-62:61,0),audio.Pans[0]);Assert.AreEqual((actor==0?63:-63,actor==0?63:62),audio.Pans[^1]);
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void SplitBackgroundPriorityAppliesOnlyForSourceLeadPositions(int target)
    {
        var script=new SourceAnimationScript(53,"priority",[new("monbg",["ANIM_TARGET"],1),new("splitbgprio",["ANIM_TARGET"],2),new("delay",["2"],3),new("end",[],4)]);
        var runtime=new SourceBattleAnimation(script,new("priority",1,53,new(target^1),new(target)),Battlers(),true,new Audio());
        var frame=runtime.Tick();CollectionAssert.AreEqual(target is 0 or 3?new[]{target^2}:Array.Empty<int>(),frame.ForegroundBackgroundSlots);
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().ForegroundBackgroundSlots.Length);
    }
    [TestMethod]
    public void PromotedBackgroundOccludesPriorityTwoEffectPixels()
    {
        var battlers=Battlers().Select(b=>b with{Visible=b.Slot.Value==1,DisplayCenter=new PixelPoint(40,40)}).ToArray();
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,Enumerable.Repeat((ushort)0x83e0,1024).ToArray())}};
        foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
        var composer=new SourceBattleFrameComposer(assets,battlers);
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[1],0,0,[],[]){Doubles=true,BattlerOamRanks=[0,1,2,3],Sprites=[new(4,"test",new(40,40),default,0,true,false,false,256,"impact",1){OamRank=4}]};
        int at=(40*240+40)*4;var ordinary=composer.ComposeField(frame);Assert.AreEqual((byte)255,ordinary[at+1]);
        var promoted=composer.ComposeField(frame with{ForegroundBackgroundSlots=[1]});Assert.AreEqual((byte)255,promoted[at]);Assert.AreEqual((byte)0,promoted[at+1]);
    }

    [TestMethod]
    public void PlainEllipseRetainsScriptDirectionOnBothAttackerSides()
    {
        var source=SourceHitAnimationCatalog.Read(Path.Combine(TestAssets.Root,"content/generated/battle/animations/hit"));
        var shared=SourceHitAnimationCatalog.Script(source,1);
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
    public void RetiredRawSpritesAdvanceAsTransparentNonblockingLayerWhileNextScriptRuns()
    {
        string folder=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(folder,"hit"));
        var source=SourceElementalAnimationCatalog.Read(Path.Combine(folder,"elemental.json"),hit)[84];
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SPARK"],1),new("createvisualtask",["AnimTask_ElectricBolt","5","0","-44","0"],2),new("waitforvisualfinish",[],3),new("end",[],4)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var cue=new CommittedMoveAnimation("retire",1,84,new(0),new(1));var gate=new SourceBattleAnimationGate();
        Assert.IsTrue(gate.TryStart(cue,()=>new(script,cue,Battlers(),true,new Audio())));
        var retired=gate.Current!;int ticks=0;while(gate.IsBlocking&&ticks++<40)retired.Tick();
        Assert.IsFalse(gate.IsBlocking);Assert.IsTrue(retired.HasResidualSprites);Assert.IsFalse(retired.GraphicsLoaded);int endFrame=retired.Snapshot().Frame;
        var nextCue=cue with{Sequence=2};var next=script with{Commands=[new("delay",["2"],1),new("end",[],2)]};
        Assert.IsTrue(gate.TryStart(nextCue,()=>new(next,nextCue,Battlers(),true,new Audio())));
        var assets=SourceElementalAnimationCatalog.Assets(Path.Combine(folder,"elemental.json")).ToDictionary(p=>p.Key,p=>p.Value);
        assets.Add("impact",SourceHitAnimationCatalog.Asset(hit,Path.Combine(folder,"hit"),"impact"));assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in Battlers())assets.Add(b.Asset,new(64,64,new ushort[4096]));
        var composer=new SourceBattleFrameComposer(assets,Battlers());
        var first=composer.ComposeResidualSprites(retired.Snapshot());Assert.IsTrue(Enumerable.Range(0,240*112).Any(i=>first[i*4+3]!=0));Assert.AreEqual((byte)0,first[3]);
        int tailTicks=0;
        while(retired.HasResidualSprites&&tailTicks++<40)
        {
            if(gate.IsBlocking)gate.Current!.Tick();
            var frame=retired.TickResidualSprites();Assert.IsFalse(frame.Active);Assert.AreEqual(0,retired.ActiveVisualCount);Assert.AreEqual(endFrame+tailTicks,frame.Frame);
            composer.ComposeResidualSprites(frame);
        }
        Assert.AreEqual(11,tailTicks);Assert.IsFalse(gate.IsBlocking);Assert.AreEqual(0,retired.Snapshot().Sprites.Length);Assert.IsFalse(retired.HasResidualSprites);
        Assert.IsTrue(composer.ComposeResidualSprites(retired.Snapshot()).All(v=>v==0));
    }

    [TestMethod]
    public void FoeBackgroundSplitKeepsSameSidePartnersAtOriginalPriority()
    {
        foreach(int target in new[]{2,3})
        {
            var script=new SourceAnimationScript(48,"foes",[new("monbg",["ANIM_ATK_PARTNER"],1),new("splitbgprio_foes",["ANIM_ATTACKER"],2),new("delay",["2"],3),new("end",[],4)]);
            var runtime=new SourceBattleAnimation(script,new("foes",1,48,new(0),new(target)),Battlers(),true,new Audio());
            var frame=runtime.Tick();CollectionAssert.AreEqual(new[]{0,2},frame.BackgroundSlots);CollectionAssert.AreEqual(target==3?new[]{2}:Array.Empty<int>(),frame.ForegroundBackgroundSlots);runtime.Cancel();
        }
    }
    [TestMethod]
    public void SwayMonUsesUnsignedPhaseSideSignAndHalfWaveCount()
    {
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(TestAssets.Root,"content/generated/battle/animations/hit"));
        var source=SourceHitAnimationCatalog.Script(hit,1);
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

}
