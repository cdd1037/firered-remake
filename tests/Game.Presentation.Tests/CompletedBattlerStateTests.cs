using System.Text.Json;
using System.Reflection;
using System.Globalization;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CompletedBattlerStateTests
{
    private static AnimationBattler[] Battlers(bool doubles=false)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static SourceAnimationFrame Frame(bool doubles=false,bool active=false)=>
        new(0,active,new PixelPoint[4],[],0,0,[],[]){Battlers=Battlers(doubles),Doubles=doubles};
    private static SourceAnimationFrame Changed(SourceAnimationFrame frame,params int[] slots)
    {
        var offsets=(PixelPoint[])frame.Offsets.Clone();var objects=(PixelPoint[])frame.ObjectOffsets.Clone();
        var visible=frame.Battlers.Select(b=>new SourceBattlerVisibilityFrame(b.Slot.Value,true,false,false,false)).ToArray();
        var affine=frame.Battlers.Select(_=>new SourceBattlerAffine(false,256,256)).ToArray();
        foreach(int slot in slots)
        {
            offsets[slot]=new(207+slot,-64);objects[slot]=new(205+slot,-56);
            visible[slot]=new(slot,true,false,true,true);
            affine[slot]=new(true,128,512){MatrixB=-3,MatrixC=5};
        }
        return frame with{Active=false,Offsets=offsets,ObjectOffsets=objects,BattlerVisibility=visible,BattlerAffine=affine};
    }
    private static SourceVisualSpriteFrame Sprite(int frame)=>new(4,"residual",new(frame,20),default,frame,true,false,false,256,"live",10);

    [TestMethod]
    public void UnchangedNormalRootHasNoHoldAndReturnsTheSameLiveFrame()
    {
        var held=new SourceCompletedBattlerState();var initial=Frame(active:true);var completed=initial with{Active=false,Frame=9};
        Assert.IsFalse(held.Capture(initial,completed));Assert.IsFalse(held.HasState);Assert.AreEqual(0,held.Slots.Length);
        var idle=Frame() with{Sprites=[Sprite(17)]};Assert.AreSame(idle,held.Apply(idle));
    }

    [TestMethod]
    public void DepartedGeometrySurvivesLiveIdleSpritesWithoutCapturingTheirMaterials()
    {
        var initial=Frame(active:true);var completed=Changed(initial,1);var held=new SourceCompletedBattlerState();
        Assert.IsTrue(held.Capture(initial,completed));CollectionAssert.AreEqual(new[]{1},held.Slots);
        for(int tick=0;tick<8;tick++)
        {
            var material=new SourcePixelAsset(1,1,[(ushort)(0x8000+tick)]);
            var sprite=Sprite(tick) with{Material=material,MaterialPalette=[1,2,3]};
            var idle=Frame() with{Frame=100+tick,Sprites=[sprite],SpritePalettes=new Dictionary<int,ushort[]>{{42,[(ushort)tick]}},
                BattlerOamRanks=[8,9],PaletteBlends=[new(16,2,31)],Trace=["fresh-idle-"+tick]};
            string before=JsonSerializer.Serialize(idle);var merged=held.Apply(idle);
            Assert.AreEqual(completed.Offsets[1],merged.Offsets[1]);Assert.AreEqual(completed.ObjectOffsets[1],merged.ObjectOffsets[1]);
            Assert.AreEqual(completed.BattlerVisibility[1],merged.BattlerVisibility[1]);Assert.AreEqual(completed.BattlerAffine[1],merged.BattlerAffine[1]);
            Assert.AreEqual(default(PixelPoint),merged.Offsets[0]);Assert.IsFalse(merged.BattlerVisibility[0].OamInvisible);
            Assert.AreSame(idle.Battlers,merged.Battlers);Assert.AreSame(idle.Sprites,merged.Sprites);Assert.AreSame(material,merged.Sprites[0].Material);
            Assert.AreSame(idle.SpritePalettes,merged.SpritePalettes);Assert.AreSame(idle.PaletteBlends,merged.PaletteBlends);
            Assert.AreSame(idle.BattlerOamRanks,merged.BattlerOamRanks);Assert.AreEqual(idle.Frame,merged.Frame);Assert.AreSame(idle.Trace,merged.Trace);
            Assert.AreEqual(before,JsonSerializer.Serialize(idle));
        }
    }

    [TestMethod]
    public void CapturedAndReturnedArraysCannotMutateTheHeldPose()
    {
        var initial=Frame();var completed=Changed(initial,1);var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,completed));
        var expected=held.Apply(Frame());string before=JsonSerializer.Serialize(expected);
        completed.Offsets[1]=new(9,9);completed.ObjectOffsets[1]=new(9,9);
        completed.BattlerVisibility[1]=new(1,true,false,false,false);completed.BattlerAffine[1]=new(false,256,256);
        var list=held.Slots;list[0]=0;var first=held.Apply(Frame());
        first.Offsets[1]=default;first.ObjectOffsets[1]=default;first.BattlerVisibility[1]=new(1,false,true,true,true);first.BattlerAffine[1]=new(false,256,256);
        Assert.AreEqual(before,JsonSerializer.Serialize(held.Apply(Frame())));CollectionAssert.AreEqual(new[]{1},held.Slots);
    }

    [TestMethod]
    public void DroppingOneReplacedSlotKeepsOtherPosesAndClearReleasesEverything()
    {
        var initial=Frame(true);var final=Changed(initial,0,3);var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,final));
        held.DropSlot(0);CollectionAssert.AreEqual(new[]{3},held.Slots);
        var idle=Frame(true);var merged=held.Apply(idle);
        Assert.AreEqual(default(PixelPoint),merged.Offsets[0]);Assert.AreEqual(final.Offsets[3],merged.Offsets[3]);
        held.DropSlot(0);Assert.IsTrue(held.HasState);held.Clear();held.Clear();Assert.IsFalse(held.HasState);Assert.AreSame(idle,held.Apply(idle));
        Assert.Throws<ArgumentOutOfRangeException>(()=>held.DropSlot(4));
    }

    [TestMethod]
    public void ActiveMalformedAndUnrestoredMaterialCapturesClearPriorPose()
    {
        var initial=Frame();var changed=Changed(initial,1);var held=new SourceCompletedBattlerState();
        var bad=new[]{changed with{Active=true},changed with{Offsets=[]},changed with{BackgroundSlots=[0]},
            changed with{StaticBackgroundCopies=[new(0,default,null,false,false)]},changed with{Overlay=new("unfinished",0,0)},
            changed with{BlendEnabled=true},changed with{BlendedObjectSlots=[1]},changed with{PaletteBlends=[new(17,1,31)]},
            changed with{InvertedPalettes=[17]},changed with{GreyscaleSlots=[1]},changed with{BackgroundPaletteEntries=[new(33,4)]},
            changed with{BattlerBackgroundEnabled=[false,true]},changed with{BattlerBackgroundMosaic=[true,false]},
            changed with{BackgroundMosaicHorizontal=1},changed with{MoveBackground=new(3,0,0,0)},
            changed with{MoveBackground=new(null,1,0,0)},changed with{MoveBackground=new(null,0,0,1)},
            changed with{StatMask=new(true,true,1,0,0,0,true,0)},changed with{Metallic=new(true,true,0,0,0,true,0)},
            changed with{BattlerVisibility=[new(1,true,false,true,true),new(1,true,false,true,true)]},
            changed with{Battlers=[initial.Battlers[0],initial.Battlers[1] with{Asset="replacement"}]}};
        foreach(var final in bad)
        {
            Assert.IsTrue(held.Capture(initial,changed));Assert.IsFalse(held.Capture(initial,final));Assert.IsFalse(held.HasState);
        }
        Assert.IsTrue(held.Capture(initial,changed));
        Assert.Throws<InvalidDataException>(()=>held.Apply(Frame(active:true)));
        Assert.Throws<InvalidDataException>(()=>held.Apply(Frame(true)));
    }

    [TestMethod]
    public void RestoredBackgroundRevisionAndZeroPaletteEntriesDoNotInventAnEffect()
    {
        var initial=Frame();var final=Changed(initial,1) with{
            MoveBackground=new(null,0,0,0){Revision=4,FadedPalette=[1,2,3]},PaletteBlends=[new(17,0,31)],RestoredObjectSlots=[0]};
        var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,final));
        var live=Frame();var merged=held.Apply(live);Assert.IsNull(merged.MoveBackground);Assert.AreEqual(0,merged.PaletteBlends.Length);
        Assert.AreEqual(0,merged.RestoredObjectSlots.Length);Assert.AreEqual(final.Offsets[1],merged.Offsets[1]);
    }

    [TestMethod]
    public void HeldVisibilityCannotResurrectAnAbsentLiveBody()
    {
        var initial=Frame();var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,Changed(initial,1)));
        var bodies=Battlers();bodies[1]=bodies[1] with{Visible=false,SourceVisibility=new(false,true,true)};
        var idle=Frame() with{Battlers=bodies};var merged=held.Apply(idle);
        Assert.IsFalse(merged.BattlerVisibility[1].Present);Assert.AreSame(bodies,merged.Battlers);
        idle=Frame() with{BattlerVisibility=[new(0,true,false,false,false),new(1,false,true,true,true)]};
        Assert.IsFalse(held.Apply(idle).BattlerVisibility[1].Present);
        Assert.IsTrue(held.Apply(Frame()).BattlerVisibility[1].Present);
    }

    private static SourceAnimationFrame Replaced(SourceAnimationFrame frame,params int[] slots)
    {
        var battlers=(AnimationBattler[])frame.Battlers.Clone();
        foreach(int slot in slots)
        {
            int at=Array.FindIndex(battlers,b=>b.Slot.Value==slot);
            battlers[at]=battlers[at] with{Asset="replacement-"+slot,YOffset=14,Elevation=3,
                SourceWidth=48,SourceHeight=56,DisplayCenter=new(80+slot*25,60)};
        }
        return frame with{Battlers=battlers};
    }
    private static SourcePixelAsset BodyPixels(ushort color=0x83e0)
    {
        var palette=new ushort[16];palette[1]=(ushort)(color&32767);
        return new(64,64,Enumerable.Repeat(color,4096).ToArray()){
            PaletteIndices=Enumerable.Repeat((byte)1,4096).ToArray(),Palette=palette,PaletteTag=42};
    }
    private static Dictionary<string,SourcePixelAsset> ReplacementMaterials(params int[] slots)=>
        slots.ToDictionary(slot=>"replacement-"+slot,_=>BodyPixels());

    [TestMethod]
    public void AuthenticatedMaterialAndPoseShareFreshIdleFramesWithoutRetainingEffects()
    {
        var initial=Frame(true);var completed=Replaced(Changed(initial,1),0,1);
        var held=new SourceCompletedBattlerState();var materials=ReplacementMaterials(0,1);
        materials["unused-invocation-effect"]=new(1,1,[31]);
        Assert.IsTrue(held.Capture(initial,completed,materials));CollectionAssert.AreEqual(new[]{0,1},held.Slots);
        Assert.IsTrue(held.HasMaterial(0));Assert.IsTrue(held.HasMaterial(1));Assert.IsFalse(held.HasMaterial(2));
        for(int tick=0;tick<3;tick++)
        {
            var idle=Frame(true) with{Frame=100+tick,Sprites=[Sprite(tick)],MoveBackground=new(null,0,0,0){Revision=tick},
                SpritePalettes=new Dictionary<int,ushort[]>{{42,[(ushort)tick]}},Trace=["live-"+tick]};
            string before=JsonSerializer.Serialize(idle);var merged=held.Apply(idle);
            Assert.AreEqual(completed.Battlers[0],merged.Battlers[0]);Assert.AreEqual(completed.Battlers[1],merged.Battlers[1]);
            Assert.AreSame(idle.Battlers[2],merged.Battlers[2]);Assert.AreEqual(completed.Offsets[1],merged.Offsets[1]);
            Assert.AreEqual(completed.ObjectOffsets[1],merged.ObjectOffsets[1]);Assert.AreEqual(completed.BattlerAffine[1],merged.BattlerAffine[1]);
            Assert.AreEqual(completed.BattlerVisibility[1],merged.BattlerVisibility[1]);
            Assert.AreSame(idle.Sprites,merged.Sprites);Assert.AreSame(idle.MoveBackground,merged.MoveBackground);
            Assert.AreSame(idle.SpritePalettes,merged.SpritePalettes);Assert.AreSame(idle.Trace,merged.Trace);Assert.AreEqual(idle.Frame,merged.Frame);
            var output=new Dictionary<string,SourcePixelAsset>{{"live-background",new(1,1,[(ushort)tick])}};held.AddMaterials(output);
            CollectionAssert.AreEquivalent(new[]{"live-background","replacement-0","replacement-1"},output.Keys.ToArray());
            Assert.AreEqual(before,JsonSerializer.Serialize(idle));
        }
    }

    [TestMethod]
    public void MaterialOnlyHoldUsesNormalComposerAndDoesNotAdvanceSceneTime()
    {
        var initial=Frame();var completed=Replaced(initial,1) with{Battlers=[initial.Battlers[0],
            Replaced(initial,1).Battlers[1] with{DisplayCenter=new(176,40)}]};
        var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,completed,ReplacementMaterials(1)));
        var idle=Frame();var assets=idle.Battlers.ToDictionary(b=>b.Asset,_=>new SourcePixelAsset(64,64,new ushort[4096]));
        assets["background"]=new(240,112,new ushort[240*112]);assets["impact"]=new(32,32,new ushort[1024]);held.AddMaterials(assets);
        var scene=new SourceBattleAnimation.SpriteScene();var frame=scene.CaptureIdle(idle.Battlers,false,held);
        var composer=new SourceBattleFrameComposer(assets,idle.Battlers);var pixels=composer.ComposeField(frame);int at=(40*240+176)*4;
        Assert.AreEqual((byte)0,pixels[at]);Assert.AreEqual((byte)255,pixels[at+1]);Assert.AreEqual((byte)0,pixels[at+2]);
        Assert.AreEqual(-1,frame.Frame);Assert.IsFalse(frame.Active);Assert.AreEqual(0,scene.Count);
        Assert.IsTrue(held.HasState);held.DropMaterial(1);Assert.IsFalse(held.HasState);Assert.AreSame(idle,held.Apply(idle));
    }

    [TestMethod]
    public void IncompleteReplacementCaptureClearsAllPriorAndCandidateSlotsAtomically()
    {
        var initial=Frame(true);var completed=Replaced(Changed(initial,0,3),0,3);var held=new SourceCompletedBattlerState();
        var invalid=new[]{new Dictionary<string,SourcePixelAsset>(),ReplacementMaterials(0),
            new Dictionary<string,SourcePixelAsset>{{"replacement-0",BodyPixels()},{"replacement-3",new(32,64,new ushort[2048])}},
            new Dictionary<string,SourcePixelAsset>{{"replacement-0",BodyPixels()},{"replacement-3",new(64,64,new ushort[4095])}},
            new Dictionary<string,SourcePixelAsset>{{"replacement-0",BodyPixels()},{"replacement-3",BodyPixels() with{PaletteIndices=[1]}}}};
        foreach(var materials in invalid)
        {
            Assert.IsTrue(held.Capture(initial,completed,ReplacementMaterials(0,3)));
            Assert.IsFalse(held.Capture(initial,completed,materials));Assert.IsFalse(held.HasState);Assert.AreEqual(0,held.Slots.Length);
            Assert.IsFalse(held.HasMaterial(0));Assert.IsFalse(held.HasMaterial(3));
            var output=new Dictionary<string,SourcePixelAsset>();held.AddMaterials(output);Assert.AreEqual(0,output.Count);
            var live=Frame(true);Assert.AreSame(live,held.Apply(live));
        }
    }

    [TestMethod]
    public void ReplacementMaterialRequiresBothFramesToRestoreSharedBackgroundsAndPalettes()
    {
        var initial=Frame();var completed=Replaced(initial,1);var held=new SourceCompletedBattlerState();var materials=ReplacementMaterials(1);
        var dirty=new[]{initial with{BackgroundSlots=[1]},initial with{Overlay=new("unfinished",1,2)},
            initial with{PaletteBlends=[new(17,1,31)]},initial with{PaletteBlends=[new(17,0,0){Unfaded=new(null,true,false)}]},
            initial with{BlendedObjectSlots=[1]},initial with{MoveBackground=new(3,0,0,0)}};
        foreach(var frame in dirty)
        {
            Assert.IsTrue(held.Capture(initial,completed,materials));Assert.IsFalse(held.Capture(frame,completed,materials));Assert.IsFalse(held.HasState);
            Assert.IsTrue(held.Capture(initial,completed,materials));Assert.IsFalse(held.Capture(initial,frame with{Battlers=completed.Battlers},materials));Assert.IsFalse(held.HasState);
        }
    }

    [TestMethod]
    public void CapturedAndInjectedMaterialArraysCannotMutateHeldPixelsOrGeometry()
    {
        var initial=Frame();var completed=Replaced(initial,1);var materials=ReplacementMaterials(1);var pixels=materials["replacement-1"];
        var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,completed,materials));
        var output=new Dictionary<string,SourcePixelAsset>();held.AddMaterials(output);string expected=JsonSerializer.Serialize(output);
        string expectedFrame=JsonSerializer.Serialize(held.Apply(Frame()));
        pixels.Pixels[0]=0;pixels.PaletteIndices[0]=0;pixels.Palette[1]=0;materials.Clear();completed.Battlers[1]=initial.Battlers[1];
        output["replacement-1"].Pixels[1]=0;output["replacement-1"].PaletteIndices[1]=0;output["replacement-1"].Palette[2]=31;
        var returned=held.Apply(Frame());returned.Battlers[1]=initial.Battlers[1];
        output.Clear();held.AddMaterials(output);
        Assert.AreEqual(expected,JsonSerializer.Serialize(output));Assert.AreEqual(expectedFrame,JsonSerializer.Serialize(held.Apply(Frame())));
    }

    [TestMethod]
    public void AcknowledgedMaterialRelinquishesOnlyReplacementGeometryAndPixels()
    {
        var initial=Frame();var completed=Replaced(Changed(initial,1),1);var held=new SourceCompletedBattlerState();
        Assert.IsTrue(held.Capture(initial,completed,ReplacementMaterials(1)));held.DropMaterial(1);held.DropMaterial(1);
        Assert.IsFalse(held.HasMaterial(1));Assert.IsTrue(held.HasState);CollectionAssert.AreEqual(new[]{1},held.Slots);
        var live=Frame();var merged=held.Apply(live);Assert.AreSame(live.Battlers,merged.Battlers);
        Assert.AreEqual(completed.Offsets[1],merged.Offsets[1]);Assert.AreEqual(completed.ObjectOffsets[1],merged.ObjectOffsets[1]);
        Assert.AreEqual(completed.BattlerVisibility[1],merged.BattlerVisibility[1]);Assert.AreEqual(completed.BattlerAffine[1],merged.BattlerAffine[1]);
        var output=new Dictionary<string,SourcePixelAsset>();held.AddMaterials(output);Assert.AreEqual(0,output.Count);
        Assert.Throws<ArgumentOutOfRangeException>(()=>held.DropMaterial(-1));Assert.Throws<ArgumentOutOfRangeException>(()=>held.HasMaterial(4));
    }

    [TestMethod]
    public void CurrentFaintPresenceWinsOverHeldMaterialWithOrWithoutTerminalPose()
    {
        foreach(bool pose in new[]{false,true})
        {
            var initial=Frame();var completed=Replaced(pose?Changed(initial,1):initial,1);var held=new SourceCompletedBattlerState();
            Assert.IsTrue(held.Capture(initial,completed,ReplacementMaterials(1)));
            var bodies=Battlers();bodies[1]=bodies[1] with{Visible=false,SourceVisibility=new(false,true,true)};
            var live=Frame() with{Battlers=bodies};var merged=held.Apply(live);
            Assert.IsFalse(merged.BattlerVisibility[1].Present);Assert.IsFalse(merged.Battlers[1].Visible);
            Assert.AreEqual(bodies[1].SourceVisibility,merged.Battlers[1].SourceVisibility);Assert.AreEqual("replacement-1",merged.Battlers[1].Asset);
            live=Frame() with{BattlerVisibility=[new(0,true,false,false,false),new(1,false,true,true,true)]};
            Assert.IsFalse(held.Apply(live).BattlerVisibility[1].Present);
        }
    }

    [TestMethod]
    public void SwitchDropSlotAndCancellationReleaseMaterialAndPoseTogether()
    {
        var initial=Frame(true);var completed=Replaced(Changed(initial,0,3),0,3);var held=new SourceCompletedBattlerState();
        Assert.IsTrue(held.Capture(initial,completed,ReplacementMaterials(0,3)));held.DropSlot(0);
        CollectionAssert.AreEqual(new[]{3},held.Slots);Assert.IsFalse(held.HasMaterial(0));Assert.IsTrue(held.HasMaterial(3));
        var live=Frame(true);var merged=held.Apply(live);Assert.AreSame(live.Battlers[0],merged.Battlers[0]);
        Assert.AreEqual(default(PixelPoint),merged.Offsets[0]);Assert.AreEqual(completed.Battlers[3],merged.Battlers[3]);
        Assert.AreEqual(completed.Offsets[3],merged.Offsets[3]);held.Clear();held.Clear();
        Assert.IsFalse(held.HasState);Assert.IsFalse(held.HasMaterial(3));Assert.AreSame(live,held.Apply(live));
        var output=new Dictionary<string,SourcePixelAsset>();held.AddMaterials(output);Assert.AreEqual(0,output.Count);
    }

    [TestMethod]
    public void ReplacementGeometryBindsByCanonicalSlotRatherThanArrayOrder()
    {
        var initial=Frame(true);var completed=Replaced(initial,3);var held=new SourceCompletedBattlerState();
        Assert.IsTrue(held.Capture(initial with{Battlers=initial.Battlers.Reverse().ToArray()},completed,ReplacementMaterials(3)));
        var live=Frame(true) with{Battlers=Battlers(true).Reverse().ToArray()};var merged=held.Apply(live);
        Assert.AreEqual(completed.Battlers[3],merged.Battlers[0]);Assert.AreSame(live.Battlers[3],merged.Battlers[3]);
    }

    private sealed class Audio:ISourceAnimationAudio
    {
        public bool SupportsCries=>true;public bool IsCryPlaying=>false;public bool IsPlaying=>false;
        public void PlayCry(SourceAnimationCry cue){}public void Play(SourceAnimationSound cue){}
        public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}public void StopAnimationCries(){}
    }

    [TestMethod][DataRow(18)][DataRow(46)][DataRow(100)]
    public void ActualCompleteExpulsionRootsRetainOnlyTheirDepartingSlotOnFreshIdleFrames(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(move);int cases=0;
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in move==100?new[]{actor}:Enumerable.Range(0,doubles?4:2).Where(slot=>slot!=actor))
        {
            var bodies=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);
            var palette=Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();
            var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,new("completed-pose",1,move,new(actor),new(target)),
                bodies,doubles,new Audio(),palette,spriteScene:scene,spriteMaterials:resolved.Assets,
                originalPartySpecies:bodies.ToDictionary(b=>b.Slot.Value,_=>132));
            var initial=runtime.Snapshot();string initialJson=JsonSerializer.Serialize(initial);int ticks=0;
            while(runtime.Active&&ticks++<1024)runtime.Tick();
            Assert.IsFalse(runtime.Active,$"move {move}, actor {actor}, target {target}");
            var completed=runtime.Snapshot();string completedJson=JsonSerializer.Serialize(completed);
            var held=new SourceCompletedBattlerState();
            Assert.IsTrue(held.Capture(initial,completed),$"clean restored terminal frame: move {move}, actor {actor}, target {target}");
            int departing=move==100?actor:target;CollectionAssert.AreEqual(new[]{departing},held.Slots);
            for(int tick=0;tick<3;tick++)
            {
                var idle=scene.TickIdle(bodies,doubles) with{Sprites=[Sprite(tick)]};var merged=held.Apply(idle);
                Assert.AreEqual(completed.Offsets[departing],merged.Offsets[departing]);
                Assert.AreEqual(completed.ObjectOffsets[departing],merged.ObjectOffsets[departing]);
                Assert.AreEqual(completed.BattlerVisibility[departing],merged.BattlerVisibility[departing]);
                Assert.AreEqual(completed.BattlerAffine[departing],merged.BattlerAffine[departing]);
                Assert.AreSame(idle.Sprites,merged.Sprites);Assert.AreSame(idle.Battlers,merged.Battlers);
                if(move==100)Assert.IsTrue(merged.BattlerVisibility[departing].SpriteInvisible);
                else{int x=bodies[departing].Center(doubles).X+merged.Offsets[departing].X;Assert.IsTrue(x< -32||x>272);}
            }
            Assert.AreEqual(initialJson,JsonSerializer.Serialize(initial));Assert.AreEqual(completedJson,JsonSerializer.Serialize(completed));
            held.DropSlot(departing);Assert.IsFalse(held.HasState);cases++;
        }
        Assert.AreEqual(move==100?6:14,cases);
    }

    [TestMethod]
    public void ActualNormalPoundRootRestoresGeometryWithoutCreatingAHold()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(1);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Battlers(doubles);var runtime=new SourceBattleAnimation(resolved.Script,new("normal-pose",1,1,new(actor),new(actor^1)),
                bodies,doubles,new Audio(),new ushort[16],spriteMaterials:resolved.Assets);
            var initial=runtime.Snapshot();int ticks=0;while(runtime.Active&&ticks++<1024)runtime.Tick();
            Assert.IsFalse(runtime.Active);var held=new SourceCompletedBattlerState();Assert.IsFalse(held.Capture(initial,runtime.Snapshot()));
            Assert.IsFalse(held.HasState);
        }
    }


    [TestMethod]
    public void IdleSceneSortsHeldObjectGeometryAndAffineAgainstAdvancingResidualsWithStableTies()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(309);var bodies=Battlers(true);var scene=new SourceBattleAnimation.SpriteScene();
        var script=resolved.Script with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
        var runtime=new SourceBattleAnimation(script,new("held-order",1,309,new(0),new(1)){VisualSeed=0x12345678},
            bodies,true,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);
        var initial=runtime.Snapshot();
        // Instantiate the genuine uncounted callback, rather than a synthetic frame.
        // Matching subpriority makes native built-OAM Y and its stable ties observable.
        var type=typeof(SourceBattleAnimation).GetNestedType("MiniTwinklingStar",BindingFlags.NonPublic);Assert.IsNotNull(type);
        var star=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
            [4,resolved.Script.SpriteRecipes["gMiniTwinklingStarSpriteTemplate"],new PixelPoint(32,80),bodies[0].Subpriority],CultureInfo.InvariantCulture);
        Assert.IsNotNull(star);var registry=typeof(SourceBattleAnimation).GetField("_sprites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!;
        registry.GetType().GetMethod("Add")!.Invoke(registry,[star]);
        var completed=runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(scene.HasResidualSprites);
        var idle=scene.CaptureIdle(bodies,true);var sprite=idle.Sprites.Single();Assert.IsTrue(sprite.Visible);
        Assert.IsTrue(sprite.OamRank<idle.BattlerOamRanks[0]);
        SourceCompletedBattlerState Hold(int objectY,bool doubled=false)
        {
            var offsets=new PixelPoint[4];offsets[0]=new(0,-40); // deliberately differs from the built OAM latch
            var objects=new PixelPoint[4];objects[0]=new(0,objectY);
            var affine=bodies.Select(_=>new SourceBattlerAffine(false,256,256)).ToArray();affine[0]=new(doubled,256,256);
            var held=new SourceCompletedBattlerState();Assert.IsTrue(held.Capture(initial,completed with{Offsets=offsets,ObjectOffsets=objects,BattlerAffine=affine}));
            return held;
        }
        var lower=Hold(40);var lowerFrame=scene.CaptureIdle(bodies,true,lower);
        Assert.AreEqual(new PixelPoint(0,-40),lowerFrame.Offsets[0]);Assert.AreEqual(new PixelPoint(0,40),lowerFrame.ObjectOffsets[0]);
        Assert.IsTrue(lowerFrame.BattlerOamRanks[0]<lowerFrame.Sprites.Single().OamRank,"Sorting uses the displayed object latch, not raw task geometry.");
        int spriteY=(sprite.Position.Y+sprite.Offset.Y-sprite.Height/2)&255;
        var tied=Hold(spriteY-bodies[0].Center(true).Y+32);
        for(int repeat=0;repeat<3;repeat++)
        {
            var frame=scene.CaptureIdle(bodies,true,tied);
            Assert.IsTrue(frame.BattlerOamRanks[0]<frame.Sprites.Single().OamRank,"An exact-Y tie retains the preceding battler-first order.");
        }
        var affineFrame=scene.CaptureIdle(bodies,true,Hold(40,true));
        Assert.IsTrue(affineFrame.Sprites.Single().OamRank<affineFrame.BattlerOamRanks[0],"Double-size affine OAM subtracts 64 instead of 32.");
        var reverseTie=scene.CaptureIdle(bodies,true,tied);
        Assert.IsTrue(reverseTie.Sprites.Single().OamRank<reverseTie.BattlerOamRanks[0],"The same tie also retains the preceding sprite-first order.");
        foreach(int y in new[]{120,20})
        {
            var replacements=(AnimationBattler[])bodies.Clone();replacements[0]=bodies[0] with{Asset="replacement-0",DisplayCenter=new(32,y)};
            var materialHeld=new SourceCompletedBattlerState();
            Assert.IsTrue(materialHeld.Capture(initial,completed with{Battlers=replacements},ReplacementMaterials(0)));
            var frame=scene.CaptureIdle(bodies,true,materialHeld);
            Assert.AreEqual(y==120,frame.BattlerOamRanks[0]<frame.Sprites.Single().OamRank,
                "Idle OAM sorting uses retained material geometry rather than the pre-result body.");
        }
        bool blinked=false;int ticks=0;
        while(scene.HasResidualSprites&&ticks++<80)
        {
            var frame=scene.TickIdle(bodies,true,lower);blinked|=frame.Sprites.Any(s=>!s.Visible);
            Assert.AreEqual(new PixelPoint(0,40),frame.ObjectOffsets[0]);
        }
        Assert.IsTrue(blinked);Assert.IsFalse(scene.HasResidualSprites);Assert.IsTrue(lower.HasState);
    }

}
