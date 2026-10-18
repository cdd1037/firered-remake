using System.Reflection;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SubstituteMoveControllerTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public int Stops,CryStops;public List<SourceAnimationSound> Sounds{get;}=[];
        public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);
        public void StopAnimationEffects()=>Stops++;public void StopAnimationCries()=>CryStops++;
    }
    private sealed class Residual(int slot):SourceBattleAnimation.VisualSprite(slot,40)
    {
        public int Steps;public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"residual",new(20,30),default,0,true,false,false,256,null,40);
        public override void Step(SourceBattleAnimation owner)=>Steps++;
    }
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Move(int id=1)=>new(id,"Move_ControllerTest",[C("delay","2"),C("end")]);
    private static string Label(bool reveal)=>reveal?"Special_SubstituteToMon":"Special_MonToSubstitute";
    private static SourceAnimationScript Special(bool reveal)=>new(0,Label(reveal),[
        C("createvisualtask","AnimTask_SwapMonSpriteToFromSubstitute","2",reveal?"TRUE":"FALSE"),C("end")]);
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"body-"+i,7,0,64,64)).ToArray();
    private static AnimationBattler Doll(AnimationBattler body,bool doubles)
    {var p=body.Base(doubles);return body with{Asset="doll-"+body.Slot.Value,DisplayCenter=new(p.X,p.Y+(body.Slot.IsPlayer?17:16))};}
    private static CommittedMoveAnimation Cue(int actor=0,int target=1,int move=1,long sequence=1)=>new("controller",sequence,move,new(actor),new(target));
    private static SourceBattleAnimation Prepared(SourceAnimationScript script,CommittedMoveAnimation cue,AnimationBattler[] bodies,
        bool doubles,Audio audio,SourceBattleAnimation.SpriteScene scene,AnimationBattler? replacement=null)=>
        new(script,cue,bodies,doubles,audio,deferSceneAttachment:true,battlerReplacements:replacement is null?null:new Dictionary<int,AnimationBattler>{{cue.Actor.Value,replacement}},spriteScene:scene);
    private static SourceMoveAnimationSequence Sequence(CommittedMoveAnimation cue,bool doubles,Audio audio,SourceBattleAnimation.SpriteScene scene,
        bool reveal=true,bool restore=true,SourceAnimationScript? moveScript=null)
    {
        var body=Bodies(doubles);var doll=(AnimationBattler[])body.Clone();int actor=cue.Actor.Value;doll[actor]=Doll(body[actor],doubles);
        var move=Prepared(moveScript??Move(cue.MoveId),cue,body,doubles,audio,scene);
        var pre=reveal?Prepared(Special(true),cue with{MoveId=0,Target=cue.Actor,GeneralAnimationLabel=Label(true)},doll,doubles,audio,scene,body[actor]):null;
        var post=restore?Prepared(Special(false),cue with{MoveId=0,Target=cue.Actor,GeneralAnimationLabel=Label(false)},body,doubles,audio,scene,doll[actor]):null;
        return new(move,pre,post);
    }
    private static T Field<T>(SourceBattleAnimation owner,string name)=>(T)typeof(SourceBattleAnimation).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
    private static PixelPoint Raw(SourceBattleAnimation owner,int slot)=>(PixelPoint)typeof(SourceBattleAnimation).GetMethod("BattlerPosition",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,[slot])!;
    private static (SourceBattleAnimation Owner,Residual Sprite,Audio Audio) Foreign(SourceBattleAnimation.SpriteScene scene,bool doubles,bool active=false)
    {
        var audio=new Audio();var owner=new SourceBattleAnimation(active?Move():Move() with{Commands=[C("end")]},Cue(sequence:900),Bodies(doubles),doubles,audio,spriteScene:scene);
        if(!active)owner.Tick();var sprite=new Residual(doubles?4:2){Owner=owner};scene.Sprites.Add(sprite);return(owner,sprite,audio);
    }
    private static void Finish(SourceBattleAnimationGate gate,int bound=500)
    {int ticks=0;while(gate.IsBlocking&&ticks++<bound)gate.Tick();Assert.IsFalse(gate.IsBlocking,"The controller exceeded its bounded source sequence.");}

    [TestMethod]
    public void RevealMoveRestoreKeepOneReceiptAndRestoreTheRealMoveTarget()
    {
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            var cue=Cue(actor,actor^1);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var gate=new SourceBattleAnimationGate();
            SourceMoveAnimationSequence? sequence=null;
            Assert.IsTrue(gate.TryStartSequence(cue,()=>sequence=Sequence(cue,doubles,audio,scene)));
            Assert.AreEqual("doll-"+actor,gate.Snapshot().Battlers[actor].Asset);int ticks=0;bool move=false,post=false;
            while(gate.IsBlocking&&ticks++<500)
            {
                var frame=gate.Tick();Assert.AreEqual(gate.IsBlocking,frame.Active);
                Assert.IsFalse(gate.TryStartSequence(cue,()=>throw new InvalidOperationException("Receipt replay.")));
                if(gate.IsBlocking)Assert.IsFalse(gate.TryStartSequence(cue with{Sequence=2},()=>throw new InvalidOperationException("Premature continuation.")),"All phases block the next receipt.");
                if(sequence!.Phase==SourceMoveAnimationPhase.Move)
                {move=true;Assert.AreEqual(cue,gate.Current!.Cue);Assert.AreEqual("body-"+actor,frame.Battlers[actor].Asset);}
                if(sequence.Phase==SourceMoveAnimationPhase.Restore)post=true;
            }
            Assert.IsTrue(move&&post);Assert.IsFalse(gate.IsBlocking);Assert.AreEqual(SourceMoveAnimationPhase.Complete,sequence!.Phase);
            Assert.AreEqual("doll-"+actor,gate.Snapshot().Battlers[actor].Asset);Assert.AreEqual(0,audio.Stops);
            Assert.IsFalse(gate.TryStartSequence(cue,()=>throw new InvalidOperationException("Completed receipt replay.")));
        }
    }

    [TestMethod]
    public void PhaseHandoffsAdvanceForeignResidualsExactlyOncePerControllerTick()
    {
        var cue=Cue();var scene=new SourceBattleAnimation.SpriteScene();var foreign=Foreign(scene,false);var audio=new Audio();var gate=new SourceBattleAnimationGate();
        SourceMoveAnimationSequence? sequence=null;gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,audio,scene));int ticks=0;bool sameFrameRestore=false;
        while(gate.IsBlocking&&ticks++<500)
        {
            var before=sequence!.Phase;var frame=gate.Tick();Assert.AreEqual(ticks,foreign.Sprite.Steps);
            Assert.AreSame(foreign.Owner,foreign.Sprite.Owner);Assert.IsTrue(frame.Sprites.Any(s=>s.Template=="residual"));
            if(before==SourceMoveAnimationPhase.Move&&sequence.Phase==SourceMoveAnimationPhase.Restore)
            {sameFrameRestore=true;Assert.AreEqual(0,gate.Current!.Snapshot().Frame);Assert.AreEqual(1,gate.Current.ActiveVisualCount);}
        }
        Assert.IsTrue(sameFrameRestore);Assert.IsFalse(gate.IsBlocking);Assert.AreEqual(0,foreign.Audio.Stops);Assert.AreEqual(1,scene.Count);
    }

    [TestMethod]
    public void ControllerEntryAndMoveLaunchDoNotExecuteTheFirstMoveCallback()
    {
        foreach(bool reveal in new[]{false,true})
        {
            var cue=Cue();var scene=new SourceBattleAnimation.SpriteScene();var foreign=Foreign(scene,false);var audio=new Audio();var gate=new SourceBattleAnimationGate();
            var script=Move() with{Commands=[C("playsewithpan","SE_M_HEADBUTT","SOUND_PAN_ATTACKER"),C("delay","2"),C("end")]};
            SourceMoveAnimationSequence? sequence=null;gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,audio,scene,reveal:reveal,restore:false,moveScript:script));
            int ticks=0;
            if(reveal)
            {
                do{gate.Tick();ticks++;Assert.AreEqual(0,audio.Sounds.Count);}while(gate.Current!.Active&&ticks<300);
                Assert.IsTrue(ticks<300);Assert.AreEqual(SourceMoveAnimationPhase.Reveal,sequence!.Phase);
            }
            else
            {
                Field<int[]>(gate.Current!,"_battlerObjectPriorities")[0]=3;Field<int[]>(gate.Current!,"_battlerObjectPriorities")[1]=1;
                Field<bool[]>(gate.Current!,"_sourceLogicalInvisible")[1]=true;Field<bool[]>(gate.Current!,"_sourceSpriteInvisible")[1]=true;
                var entry=gate.Tick();ticks++;Assert.AreEqual(0,entry.Frame);Assert.AreEqual(0,audio.Sounds.Count);
                Assert.AreEqual(3,entry.BattlerObjectPriorities[0]);Assert.AreEqual(1,entry.BattlerObjectPriorities[1]);
            }
            var launch=gate.Tick();ticks++;Assert.AreEqual(SourceMoveAnimationPhase.Move,sequence!.Phase);
            Assert.AreEqual(reveal?0:1,launch.Frame);Assert.AreEqual(0,audio.Sounds.Count);Assert.AreEqual(0,gate.Current!.ActiveVisualCount);
            Assert.AreEqual(2,launch.BattlerObjectPriorities[0]);if(!reveal)Assert.AreEqual(1,launch.BattlerObjectPriorities[1]);
            Assert.AreEqual(ticks,foreign.Sprite.Steps);
            var firstCallback=gate.Tick();ticks++;Assert.AreEqual(1,audio.Sounds.Count);Assert.AreEqual(launch.Frame+1,firstCallback.Frame);
            Assert.AreEqual(firstCallback.Frame,audio.Sounds[0].Frame);Assert.AreEqual(ticks,foreign.Sprite.Steps);Finish(gate);
        }
        var standaloneAudio=new Audio();var standalone=new SourceBattleAnimation(Move() with{Commands=[C("playsewithpan","SE_M_HEADBUTT","SOUND_PAN_ATTACKER"),C("end")]},Cue(),Bodies(false),false,standaloneAudio);
        Assert.AreEqual(0,standalone.Tick().Frame);Assert.AreEqual(1,standaloneAudio.Sounds.Count);
    }

    [TestMethod]
    public void CallerSelectedOptionalStagesSupportTheExistingMultihitBodyLifetime()
    {
        foreach(bool reveal in new[]{false,true})foreach(bool restore in new[]{false,true})
        {
            var cue=Cue();var audio=new Audio();var gate=new SourceBattleAnimationGate();SourceMoveAnimationSequence? sequence=null;
            gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,audio,new(),reveal,restore));
            Assert.AreEqual(reveal?SourceMoveAnimationPhase.Reveal:SourceMoveAnimationPhase.Move,sequence!.Phase);
            Finish(gate);Assert.AreEqual(restore?"doll-0":"body-0",gate.Snapshot().Battlers[0].Asset);
        }
    }

    [TestMethod]
    public void TeleportRawPositionSecondaryOffsetAndInvisibleFlagsReachThePostStageUnchanged()
    {
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            var script=new SourceAnimationScript(100,"Move_TELEPORT",[C("createvisualtask","AnimTask_Teleport","2"),C("end")])
            {TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>{{"sAffineAnim_Teleport",[new("frame",64,-4,0,20),new("frame",0,0,0,200),new("end")]}}};
            var cue=Cue(actor,actor^1,100);var gate=new SourceBattleAnimationGate();var audio=new Audio();SourceMoveAnimationSequence? sequence=null;
            gate.TryStartSequence(cue,()=>sequence=Sequence(cue,doubles,audio,new(),moveScript:script));
            int ticks=0;while(sequence!.Phase!=SourceMoveAnimationPhase.Restore&&gate.IsBlocking&&ticks++<400)gate.Tick();
            Assert.AreEqual(SourceMoveAnimationPhase.Restore,sequence.Phase);
            Assert.AreEqual(272,Raw(gate.Current!,actor).X);int y2=Field<PixelPoint[]>(gate.Current!,"_offsets")[actor].Y;
            int footOffset=SourceTaskAffineProgram.FootOffset(176,Bodies(doubles)[actor].YOffset);
            Assert.AreEqual(footOffset+((actor&1)==0?-64:-32),y2);var visibility=gate.Snapshot().BattlerVisibility[actor];
            Assert.IsTrue(visibility.SpriteInvisible);Assert.IsFalse(visibility.LogicalInvisible);
            Finish(gate);Assert.AreEqual(272,Raw(gate.Current!,actor).X);Assert.AreEqual(new PixelPoint(0,y2),Field<PixelPoint[]>(gate.Current!,"_offsets")[actor]);
            Assert.AreEqual(Doll(Bodies(doubles)[actor],doubles).DisplayCenter!.Value.Y,Raw(gate.Current!,actor).Y);
            Assert.AreEqual(visibility,gate.Snapshot().BattlerVisibility[actor]);Assert.AreEqual("doll-"+actor,gate.Snapshot().Battlers[actor].Asset);
        }
    }

    [TestMethod]
    public void InvalidMoveAndUnavailableCryFailBeforeRevealWithoutStealingOrCancellingTheScene()
    {
        foreach(bool cry in new[]{false,true})
        {
            var cue=Cue();var scene=new SourceBattleAnimation.SpriteScene();var foreign=Foreign(scene,false);var audio=new Audio();var gate=new SourceBattleAnimationGate();
            var bad=Move() with{Commands=cry?[C("createvisualtask","SoundTask_PlayCryWithEcho","2"),C("end")]:[C("unaccepted-operation"),C("end")]};
            SourceBattleAnimation? reveal=null;
            var failure=Assert.Throws<SourceAnimationException>(()=>gate.TryStartSequence(cue,()=>
            {
                var bodies=Bodies(false);var dolls=(AnimationBattler[])bodies.Clone();dolls[0]=Doll(bodies[0],false);
                reveal=Prepared(Special(true),cue with{MoveId=0,Target=cue.Actor,GeneralAnimationLabel=Label(true)},dolls,false,audio,scene,bodies[0]);
                var move=Prepared(bad,cue,bodies,false,audio,scene);return new(move,reveal);
            }));
            if(cry)Assert.AreEqual("source-cry-audio-unavailable",failure.Diagnostic.Reason);
            Assert.IsTrue(reveal!.IsPrepared);Assert.Throws<InvalidOperationException>(()=>reveal.Tick());
            reveal.Cancel();Assert.AreEqual(0,audio.Stops);Assert.AreEqual(0,audio.CryStops);Assert.AreEqual(0,foreign.Audio.Stops);
            Assert.AreEqual(0,foreign.Sprite.Steps);Assert.AreEqual(1,scene.Count);Assert.IsNull(gate.Current);
            scene.TickIdle(Bodies(false),false);Assert.AreEqual(1,foreign.Sprite.Steps);
            Assert.IsTrue(gate.TryStartSequence(cue,()=>Sequence(cue,false,audio,scene)));Finish(gate);
        }
    }

    [TestMethod]
    public void OccupiedSceneAndMismatchedReceiptRejectBeforeAcquiringAnyPreparedPhase()
    {
        foreach(bool occupied in new[]{false,true})
        {
            var cue=Cue();var scene=new SourceBattleAnimation.SpriteScene();var foreign=Foreign(scene,false,occupied);var audio=new Audio();var gate=new SourceBattleAnimationGate();
            var sequence=Sequence(cue,false,audio,scene);Assert.Throws<InvalidOperationException>(()=>sequence.Tick());
            if(occupied)Assert.Throws<InvalidOperationException>(()=>gate.TryStartSequence(cue,()=>sequence));
            else Assert.Throws<ArgumentException>(()=>gate.TryStartSequence(cue with{Sequence=2},()=>sequence));
            Assert.IsTrue(sequence.Current.IsPrepared);Assert.AreEqual(0,audio.Stops);Assert.AreEqual(0,foreign.Audio.Stops);Assert.IsNull(gate.Current);
            if(occupied)foreign.Owner.Tick();else scene.TickIdle(Bodies(false),false);
            Assert.AreEqual(1,foreign.Sprite.Steps);
        }
    }

    [TestMethod]
    public void CancellationAtEveryStageClearsOnlyInvocationOwnersAndStopsAudioOnce()
    {
        foreach(var phase in new[]{SourceMoveAnimationPhase.Reveal,SourceMoveAnimationPhase.Move,SourceMoveAnimationPhase.Restore,SourceMoveAnimationPhase.AwaitAcknowledgement,SourceMoveAnimationPhase.Complete})
        {
            var cue=Cue();var scene=new SourceBattleAnimation.SpriteScene();var foreign=Foreign(scene,false);var audio=new Audio();var gate=new SourceBattleAnimationGate();SourceMoveAnimationSequence? sequence=null;
            gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,audio,scene));int ticks=0;Residual? owned=null;
            while(sequence!.Phase!=phase&&gate.IsBlocking&&ticks++<500)
            {
                gate.Tick();if(owned is null&&sequence.Phase==SourceMoveAnimationPhase.Move){owned=new(3){Owner=gate.Current};scene.Sprites.Add(owned);}
            }
            Assert.AreEqual(phase,sequence.Phase);gate.Cancel();gate.Cancel();Assert.IsFalse(gate.IsBlocking);
            Assert.AreEqual(SourceMoveAnimationPhase.Cancelled,sequence.Phase);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(1,audio.CryStops);
            Assert.AreEqual(0,foreign.Audio.Stops);Assert.AreEqual(1,scene.Count);Assert.AreSame(foreign.Sprite,scene.Sprites[2]);
            Assert.AreEqual("doll-0",gate.Snapshot().Battlers[0].Asset);Assert.Throws<InvalidOperationException>(()=>gate.Tick());
            Assert.IsFalse(gate.TryStartSequence(cue,()=>throw new InvalidOperationException("Cancelled receipt replay.")));
        }
    }

    [TestMethod]
    public void ResolvedTackleTeleportAndTransformUseTheSameFullControllerAndComposer()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var preScript=resources.ResolveGeneral(Label(true)).Script;var postScript=resources.ResolveGeneral(Label(false)).Script;
        foreach(int id in new[]{33,100,144})foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            var resolved=resources.Resolve(id);var bodies=Bodies(doubles);var initial=(AnimationBattler[])bodies.Clone();initial[actor]=Doll(bodies[actor],doubles);
            var transformed=bodies[actor] with{Asset="transformed-"+actor,YOffset=15,SourceWidth=48,SourceHeight=56};
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8421,240*112).ToArray());
            foreach(var body in bodies)assets[body.Asset]=new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray());
            assets[initial[actor].Asset]=new(64,64,Enumerable.Repeat((ushort)0xfc00,4096).ToArray());
            assets[transformed.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var surface=resources.CreateBackgroundSurface(0);var palette=Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(actor,id==100?actor:actor^1,id);var gate=new SourceBattleAnimationGate();
            SourceBattleAnimation? move=null;SourceMoveAnimationSequence? sequence=null;
            Assert.IsTrue(gate.TryStartSequence(cue,()=>
            {
                move=new(resolved.Script,cue,bodies,doubles,audio,deferSceneAttachment:true,backgroundPalette:palette,
                    battlerReplacements:id==144?new Dictionary<int,AnimationBattler>{{actor,transformed}}:null,spriteScene:scene,spriteMaterials:assets);
                var pre=new SourceBattleAnimation(preScript,cue with{MoveId=0,Target=cue.Actor,GeneralAnimationLabel=Label(true)},initial,doubles,audio,
                    deferSceneAttachment:true,battlerReplacements:new Dictionary<int,AnimationBattler>{{actor,bodies[actor]}},spriteScene:scene,spriteMaterials:assets);
                var post=new SourceBattleAnimation(postScript,cue with{MoveId=0,Target=cue.Actor,GeneralAnimationLabel=Label(false)},bodies,doubles,audio,
                    deferSceneAttachment:true,battlerReplacements:new Dictionary<int,AnimationBattler>{{actor,initial[actor]}},spriteScene:scene,spriteMaterials:assets);
                return sequence=new(move,pre,post);
            }));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,initial,surface);var original=composer.ComposeField(gate.Snapshot());
            int ticks=0;bool changed=false,transformedVisible=false,postHandoff=false;int terminalY2=0;
            while(gate.IsBlocking&&ticks++<1000)
            {
                var before=sequence!.Phase;var frame=gate.Tick();var pixels=composer.ComposeField(frame);Assert.AreEqual(240*112*4,pixels.Length);
                changed|=!original.SequenceEqual(pixels);
                transformedVisible|=frame.Battlers[actor].Asset==transformed.Asset;
                if(before==SourceMoveAnimationPhase.Move&&sequence.Phase==SourceMoveAnimationPhase.Restore)
                {
                    postHandoff=true;Assert.AreEqual(move!.Snapshot().Battlers[actor],frame.Battlers[actor]);
                    if(id==144)Assert.AreEqual(transformed,frame.Battlers[actor]);
                    if(id==100)
                    {
                        Assert.AreEqual(272,Raw(gate.Current!,actor).X);terminalY2=Field<PixelPoint[]>(move!,"_offsets")[actor].Y;
                        Assert.AreEqual(terminalY2,Field<PixelPoint[]>(gate.Current!,"_offsets")[actor].Y);
                        Assert.IsTrue(frame.BattlerVisibility[actor].SpriteInvisible);
                    }
                }
            }
            Assert.IsFalse(gate.IsBlocking);Assert.IsTrue(changed&&postHandoff);Assert.AreEqual(id==144,transformedVisible);
            Assert.AreEqual(initial[actor],gate.Snapshot().Battlers[actor]);Assert.AreEqual(0,gate.Current!.ActiveVisualCount);
            if(id==100)
            {
                Assert.AreEqual(272,Raw(gate.Current,actor).X);Assert.AreEqual(new PixelPoint(0,terminalY2),Field<PixelPoint[]>(gate.Current,"_offsets")[actor]);
                Assert.IsTrue(gate.Snapshot().BattlerVisibility[actor].SpriteInvisible);
            }
        }
    }

    [TestMethod]
    public void TerminalTableEndWaitsForTheNextControllerCallbackBeforeTheOnlyAcknowledgement()
    {
        foreach(bool restore in new[]{false,true})
        {
            var cue=Cue();var gate=new SourceBattleAnimationGate();SourceMoveAnimationSequence? sequence=null;
            gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,new(),new(),restore:restore));
            int ticks=0;while(sequence!.Phase!=SourceMoveAnimationPhase.AwaitAcknowledgement&&ticks++<500)gate.Tick();
            Assert.IsTrue(ticks<500);Assert.IsFalse(gate.Current!.Active);Assert.IsTrue(gate.IsBlocking);Assert.IsTrue(gate.Snapshot().Active);
            int lastScriptFrame=gate.Current.Snapshot().Frame;var terminal=gate.Tick();
            Assert.AreEqual(lastScriptFrame+1,terminal.Frame);Assert.IsFalse(terminal.Active);Assert.IsFalse(gate.IsBlocking);
            Assert.AreEqual(SourceMoveAnimationPhase.Complete,sequence.Phase);Assert.Throws<InvalidOperationException>(()=>gate.Tick());
        }
    }

    [TestMethod]
    public void CompletionUsesNormalFootprintWithoutDiscardingTheTerminalMatrix()
    {
        foreach(bool restore in new[]{false,true})
        {
            var cue=Cue();var gate=new SourceBattleAnimationGate();var audio=new Audio();SourceMoveAnimationSequence? sequence=null;
            gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,audio,new(),reveal:false,restore:restore));
            var move=gate.Current!;Field<bool[]>(move,"_affineMode")[0]=true;
            Field<int[]>(move,"_xScale")[0]=320;Field<int[]>(move,"_yScale")[0]=384;Field<int[]>(move,"_matrixB")[0]=12;Field<int[]>(move,"_matrixC")[0]=-12;
            int ticks=0;while(sequence!.Phase==SourceMoveAnimationPhase.Move&&ticks++<20)gate.Tick();
            Assert.IsTrue(ticks<20);var affine=gate.Snapshot().BattlerAffine[0];
            Assert.IsFalse(affine.DoubleSize);Assert.AreEqual(320,affine.XScale);Assert.AreEqual(384,affine.YScale);
            Assert.AreEqual(12,affine.MatrixB);Assert.AreEqual(-12,affine.MatrixC);gate.Cancel();
        }
    }

    [TestMethod]
    public void PostStageRetainsOtherBattlersPalettesAndDisplayRegistersAndResetsOnlyTheActorPalette()
    {
        var cue=Cue();var gate=new SourceBattleAnimationGate();SourceMoveAnimationSequence? sequence=null;
        gate.TryStartSequence(cue,()=>sequence=Sequence(cue,false,new(),new(),reveal:false));var move=gate.Current!;
        gate.Tick();gate.Tick(); // source case0, then case1 launch; inject terminal display state after launch.
        var actorBlend=new SourcePaletteBlend(16,5,31);var targetBlend=new SourcePaletteBlend(17,8,1023);
        Field<Dictionary<int,SourcePaletteBlend>>(move,"_paletteBlends")[16]=actorBlend;
        Field<Dictionary<int,SourcePaletteBlend>>(move,"_paletteBlends")[17]=targetBlend;
        Field<Dictionary<int,ushort>>(move,"_fadedBackgroundEntries")[33]=543;
        Field<Dictionary<int,ushort>>(move,"_unfadedBackgroundEntries")[33]=123;
        void Set(string name,object value)=>typeof(SourceBattleAnimation).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(move,value);
        Set("_alphaA",6);Set("_alphaB",10);Set("_blendEnabled",true);Set("_blendFirstTargets",2);
        var window=new SourceWindow0Frame(true,0x2050,0x1060,63,61);Set("_window0",window);
        Field<int[]>(move,"_battlerObjectPriorities")[0]=3;Field<int[]>(move,"_battlerObjectPriorities")[1]=1;
        Field<bool[]>(move,"_sourceLogicalInvisible")[1]=true;Field<bool[]>(move,"_sourceSpriteInvisible")[1]=true;
        int ticks=0;while(sequence!.Phase==SourceMoveAnimationPhase.Move&&ticks++<20)gate.Tick();
        Assert.AreEqual(SourceMoveAnimationPhase.Restore,sequence.Phase);Assert.IsTrue(gate.Snapshot().PaletteBlends.Contains(actorBlend));
        CollectionAssert.AreEqual(new[]{2,1,2,2},gate.Snapshot().BattlerObjectPriorities);
        while(gate.IsBlocking&&ticks++<300)
        {
            var frame=gate.Tick();Assert.IsTrue(frame.PaletteBlends.Contains(targetBlend));
            Assert.IsTrue(frame.BackgroundPaletteEntries.Contains(new SourcePaletteEntry(33,543)));
            Assert.AreEqual(6,frame.AlphaA);Assert.AreEqual(10,frame.AlphaB);Assert.IsTrue(frame.BlendEnabled);Assert.AreEqual(window with{Horizontal=0,Vertical=0},frame.Window0);
        }
        Assert.IsFalse(gate.IsBlocking);Assert.IsFalse(gate.Snapshot().PaletteBlends.Any(p=>p.Palette==16));
        Assert.AreEqual((ushort)123,Field<Dictionary<int,ushort>>(gate.Current!,"_unfadedBackgroundEntries")[33]);
    }

    [TestMethod]
    public void UncountedBackgroundFadeAndHardwareEngineContinueOnceAcrossTheMoveTableHandoff()
    {
        ushort[] palette=Enumerable.Range(0,16).Select(i=>(ushort)(i*31)).ToArray();
        var script=Move() with{Commands=[C("fadetobg","BG_CONTROLLER"),C("end")],
            BackgroundSymbols=new Dictionary<string,int>{{"BG_CONTROLLER",1}},BackgroundPalettes=new Dictionary<int,ushort[]>{{1,palette}}};
        var cue=Cue();var gate=new SourceBattleAnimationGate();var audio=new Audio();
        gate.TryStartSequence(cue,()=>Sequence(cue,false,audio,new(),reveal:false,moveScript:script));
        var reference=new SourceBattleAnimation(script,cue,Bodies(false),false,new Audio());bool loaded=false;int ticks=0;
        while(gate.IsBlocking&&ticks++<300)
        {
            var expected=ticks<=2?reference.Tick(null,controllerLaunch:true):reference.Active?reference.Tick():reference.Tick(null,controllerCompletion:true);var actual=gate.Tick();
            Assert.AreEqual(expected.MoveBackground!.Id,actual.MoveBackground!.Id);Assert.AreEqual(expected.MoveBackground.Darkening,actual.MoveBackground.Darkening);
            Assert.AreEqual(expected.MoveBackground.ScrollX,actual.MoveBackground.ScrollX);Assert.AreEqual(expected.MoveBackground.ScrollY,actual.MoveBackground.ScrollY);
            CollectionAssert.AreEqual(expected.MoveBackground.FadedPalette,actual.MoveBackground.FadedPalette);loaded|=actual.MoveBackground.Id==1;
        }
        Assert.IsFalse(gate.IsBlocking);Assert.IsTrue(loaded);Assert.IsFalse(gate.Current!.PaletteFadeActive);
        CollectionAssert.AreEqual(palette,gate.Snapshot().MoveBackground!.FadedPalette);
    }

    [TestMethod]
    public void InvalidRestoreTargetOrPhaseCannotAcquireTheScene()
    {
        var cue=Cue();var scene=new SourceBattleAnimation.SpriteScene();var foreign=Foreign(scene,false);var audio=new Audio();
        foreach(bool wrongTarget in new[]{false,true})
        {
            var bodies=Bodies(false);var move=Prepared(Move(),cue,bodies,false,audio,scene);
            bool reveal=!wrongTarget;var specialCue=cue with{MoveId=0,Target=wrongTarget?cue.Target:cue.Actor,GeneralAnimationLabel=Label(reveal)};
            var wrong=Prepared(Special(reveal),specialCue,bodies,false,audio,scene,Doll(bodies[0],false));
            Assert.Throws<ArgumentException>(()=>new SourceMoveAnimationSequence(move,restore:wrong));
            Assert.IsTrue(move.IsPrepared&&wrong.IsPrepared);Assert.AreEqual(0,foreign.Sprite.Steps);Assert.AreEqual(0,audio.Stops);
        }
        scene.TickIdle(Bodies(false),false);Assert.AreEqual(1,foreign.Sprite.Steps);
    }

    [TestMethod]
    public void CancellingRetiredSequenceDoesNotStopNewControllerAudio()
    {
        var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var gate=new SourceBattleAnimationGate();var cue=Cue();
        gate.TryStartSequence(cue,()=>Sequence(cue,false,audio,scene));Finish(gate);
        var nextAudio=new Audio();var next=new SourceBattleAnimation(Move(),cue with{Sequence=2},Bodies(false),false,nextAudio,spriteScene:scene);
        gate.Cancel();Assert.AreEqual(1,audio.Stops);Assert.AreEqual(0,nextAudio.Stops);Assert.IsTrue(next.Active);
        next.Tick();next.Cancel();Assert.AreEqual(1,nextAudio.Stops);
    }
}
