using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PhysicalMoveAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Cues {get;}=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Cues.Add(cue);
        public void StopAnimationEffects(){}
    }
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    [DataRow(20)][DataRow(35)][DataRow(2)][DataRow(66)][DataRow(77)][DataRow(78)][DataRow(79)][DataRow(289)][DataRow(29)][DataRow(64)][DataRow(38)][DataRow(21)][DataRow(22)][DataRow(11)][DataRow(23)][DataRow(26)][DataRow(27)][DataRow(70)][DataRow(34)][DataRow(36)][DataRow(67)][DataRow(136)]
    public void ImportedPhysicalRootsCompleteInEveryCanonicalPosition(int move)
    {
        foreach(bool doubles in new[]{false,true})
        for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),new("battle",1,move,new(actor),new(actor^1)),Battlers(doubles),doubles,audio);
            bool visual=false;int count=0;
            while(runtime.Active&&count++<1000)
            {
                var frame=runtime.Tick();visual|=frame.Sprites.Any(s=>s.Asset is not null)||frame.Offsets.Any(p=>p!=default);
            }
            Assert.IsFalse(runtime.Active,$"move {move}, actor {actor}");Assert.IsTrue(visual);Assert.IsTrue(audio.Cues.Count>0);
            var final=runtime.Snapshot();
            if(move==289)Assert.AreEqual(new PixelPoint((actor&1)==0?0:-1,1),final.Offsets[actor]);
            else Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod]
    public void PowderParticleRetainsSourceDimensionsLoopsAndSideDependentSineDrift()
    {
        var source=Script(77);var particle=source.Commands.First(c=>c.Op=="createsprite");
        var script=source with{Commands=[source.Commands[0],particle,new("waitforvisualfinish",[],1),new("end",[],2)]};
        foreach(int actor in new[]{0,1})
        {
            var runtime=new SourceBattleAnimation(script,new("powder",1,77,new(actor),new(actor^1)),Battlers(false),false,new Audio());
            // loadspritegfx yields for the source one-frame loader callback before creation.
            for(int tick=0;tick<3;tick++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            var first=runtime.Tick().Sprites.Single();Assert.AreEqual(8,first.Width);Assert.AreEqual(16,first.Height);
            Assert.AreEqual(Battlers(false)[actor^1].Center(false,true).X-30,first.Position.X);
            bool wrapped=false;int previous=first.Tile;int age=0;
            while(runtime.Active&&age++<150)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var sprite=frame.Sprites.Single();
                wrapped|=sprite.Tile<previous;previous=sprite.Tile;
                Assert.AreEqual((age*80)>>8,sprite.Offset.Y);
                Assert.AreEqual((source.SineTable[age&255]*(actor==0?5:-5))>>8,sprite.Offset.X);
            }
            Assert.IsTrue(wrapped);Assert.IsFalse(runtime.Active);
        }
    }
    [TestMethod]
    public void ExistingDedicatedCatalogsStillTerminateWithSharedRuntime()
    {
        string folder=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var hit=SourceHitAnimationCatalog.Read(Path.Combine(folder,"hit"));
        var scripts=new List<SourceAnimationScript>();
        scripts.AddRange(new[]{1,10,15,33,39,68,148,163,206,233}.Select(id=>SourceHitAnimationCatalog.Script(hit,id)));
        scripts.AddRange(SourceTraceAnimationCatalog.Read(Path.Combine(folder,"trace.json"),hit).Values);
        scripts.AddRange(SourceStatMoveCatalog.Read(Path.Combine(folder,"stat-moves")).Scripts.Values);
        scripts.AddRange(SourceExplosionAnimationCatalog.Read(Path.Combine(folder,"explosion")).Scripts.Values);
        Assert.AreEqual(20,scripts.Count);
        foreach(var script in scripts)
        {
            var runtime=new SourceBattleAnimation(script,new("legacy",script.MoveId,script.MoveId,new(0),new(1)),Battlers(false),false,new Audio());
            int frames=0;while(runtime.Active&&frames++<2000)runtime.Tick();
            Assert.IsFalse(runtime.Active,script.Label);Assert.AreEqual(0,runtime.ActiveVisualCount,script.Label);
        }
    }
    [TestMethod]
    public void SourceSinkUsesFixedPointAccumulationAndLeavesOffsetForExplicitRestoration()
    {
        var script=new SourceAnimationScript(70,"sink-test",[new("createvisualtask",["AnimTask_ShakeAndSinkMon","5","ANIM_ATTACKER","2","0","96","30"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
        var runtime=new SourceBattleAnimation(script,new("b",1,70,new(0),new(1)),Battlers(false),false,new Audio());
        var first=runtime.Tick();Assert.AreEqual(new PixelPoint(-2,0),first.Offsets[0]);
        for(int i=0;i<40&&runtime.Active;i++)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(11,runtime.Snapshot().Offsets[0].Y);
        runtime.Cancel();Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
    }
    [TestMethod]
    public void MissingCallbackIsRejectedInsteadOfPlayingGenericImpact()
    {
        var script=Script(26);var commands=script.Commands.ToArray();int index=Array.FindIndex(commands,c=>c.Op=="createsprite"&&c.Args[0]=="gJumpKickSpriteTemplate");
        commands[index]=commands[index] with{Args=["gUnknownFootSpriteTemplate",..commands[index].Args.Skip(1)]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=commands},new("b",1,26,new(0),new(1)),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void CancellingPhysicalCallbackRestoresPresentationWithoutAcknowledgement()
    {
        var runtime=new SourceBattleAnimation(Script(27),new("b",1,27,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<15;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
    }
    [TestMethod]
    public void DelayedSoundWaitsExactSourceFramesWithoutBlockingVisualCommands()
    {
        var audio=new Audio();var script=new SourceAnimationScript(38,"sound-test",[new("waitplaysewithpan",["SE_M_SWAGGER","SOUND_PAN_ATTACKER","8"],1),new("end",[],2)]);
        var runtime=new SourceBattleAnimation(script,new("b",1,38,new(0),new(1)),Battlers(false),false,audio);
        for(int i=0;i<8;i++){runtime.Tick();Assert.AreEqual(0,audio.Cues.Count);}
        runtime.Tick();Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual(8,audio.Cues[0].Frame);Assert.AreEqual(-64,audio.Cues[0].Pan);
        runtime.Tick();Assert.IsFalse(runtime.Active);
    }
    [TestMethod]
    public void SpriteFrameVerticalAndHorizontalFlipsReachPixelComposer()
    {
        var sheet=new SourcePixelAsset(32,32,Enumerable.Range(0,1024).Select(i=>(ushort)(0x8000|(i&32767))).ToArray());
        var battlers=Battlers(false).Select(b=>b with{Visible=false}).ToArray();
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",sheet},{"test",sheet},{"b0",new(64,64,new ushort[4096])},{"b1",new(64,64,new ushort[4096])}};
        var composer=new SourceBattleFrameComposer(assets,battlers);
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[new(4,"test",new(16,16),default,0,true,true,false,256,"test",1){VFlip=true}],BattlerOamRanks=[0,1]};
        var pixels=composer.ComposeField(frame);int value=sheet.Pixels[1023];
        Assert.AreEqual((byte)((value&31)*255/31),pixels[0]);Assert.AreEqual((byte)(((value>>5)&31)*255/31),pixels[1]);Assert.AreEqual((byte)255,pixels[3]);
    }

}
