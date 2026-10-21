namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool _sceneAttached;
    internal bool IsPrepared=>!_sceneAttached&&!_cancelled&&Active&&_frame==-1;
    internal void RequirePrepared()
    {if(!IsPrepared)throw new InvalidOperationException("A controller phase must be prepared and unadvanced.");}
    internal void RequireSameInvocation(SourceBattleAnimation move,string? specialLabel=null)
    {
        RequirePrepared();
        if(!ReferenceEquals(_spriteScene,move._spriteScene)||!ReferenceEquals(_audio,move._audio)||_doubles!=move._doubles||
            _cue.BattleId!=move._cue.BattleId||_cue.Sequence!=move._cue.Sequence||_cue.Actor!=move._cue.Actor)
            throw new ArgumentException("Controller phases must share their scene, audio and committed invocation.");
        if(specialLabel is {} label&&(_cue.MoveId!=0||_cue.GeneralAnimationLabel!=label||_script.Label!=label||
            _cue.StatAnimationArgument is not null||_cue.Target!=_cue.Actor))
            throw new ArgumentException("A substitute controller phase must use its named self-targeted source program.");
    }
    internal void LaunchControllerMove()
    {RestoreVisibleBattlerPriorities();SetControllerBattlerAffineMode();}
    internal void SetControllerBattlerAffineMode()
    {
        // Source OFF before DoMoveAnim and NORMAL after it both use a 64x64
        // footprint, never DOUBLE. The model stores coefficients directly instead
        // of physical matrix indexes: retain them when restoring NORMAL. At OFF
        // entry, prepared bodies and the non-affine reveal still have identity
        // matrices, so OFF and normal identity have exactly the same pixels.
        for(int slot=0;slot<_battlerCount;slot++)if(IsSourceBattlerPresent(slot))_affineMode[slot]=false;
    }
    internal void AttachPrepared(SourceBattleAnimation? completed=null)
    {
        RequirePrepared();
        if(completed is not null)
        {
            if(completed.Active||!ReferenceEquals(_spriteScene,completed._spriteScene)||_doubles!=completed._doubles)
                throw new InvalidOperationException("Only the completed phase may hand its battlers to the same scene.");
            _spriteScene.EnsureController(completed);
        }
        // Attach is the only acquisition. A failed preflight or an occupied scene
        // must neither take ownership nor cancel the previous controller.
        _spriteScene.Attach(this);_sceneAttached=true;
        if(completed is null)return;
        foreach(var (slot,battler) in completed._battlers)_battlers[slot]=battler;
        // Keep source x/y independent from x2/y2. A terminal Teleport can leave
        // raw x=272 and y2 displaced; the following swap only resets raw default Y.
        Array.Copy(completed._battlerPositionOffsets,_battlerPositionOffsets,4);
        Array.Copy(completed._offsets,_offsets,4);Array.Copy(completed._objectOffsets,_objectOffsets,4);
        Array.Copy(completed._sourcePresent,_sourcePresent,4);
        Array.Copy(completed._sourceLogicalInvisible,_sourceLogicalInvisible,4);
        Array.Copy(completed._sourceSpriteInvisible,_sourceSpriteInvisible,4);
        Array.Copy(completed._sourceOamInvisible,_sourceOamInvisible,4);
        Array.Copy(completed._affineMode,_affineMode,4);Array.Copy(completed._affineOamMode,_affineOamMode,4);
        Array.Copy(completed._xScale,_xScale,4);Array.Copy(completed._yScale,_yScale,4);
        Array.Copy(completed._matrixB,_matrixB,4);Array.Copy(completed._matrixC,_matrixC,4);
        Array.Copy(completed._restoredObject,_restoredObject,4);Array.Copy(completed._restoredObjectOam,_restoredObjectOam,4);
        Array.Copy(completed._blendedObject,_blendedObject,4);Array.Copy(completed._blendedObjectOam,_blendedObjectOam,4);
        Array.Copy(completed._battlerObjectPriorities,_battlerObjectPriorities,4);
        Array.Copy(completed._battlerObjectOamPriorities,_battlerObjectOamPriorities,4);
        _spriteCoordOffsetX=completed._spriteCoordOffsetX;_spriteCoordOffsetY=completed._spriteCoordOffsetY;
        Array.Copy(completed._spriteCoordOffsetEnabled,_spriteCoordOffsetEnabled,4);
        // The scene retains every sprite's original callback/material owner.
        _spriteOam=completed._spriteOam;_spriteOamOwners=completed._spriteOamOwners;
        CarryControllerDisplay(completed);
        // Native uncounted cleanup/fade tasks still get this frame's RunTasks.
        // Retain each task instance and ordering, under the new global arguments.
        _tasks.AddRange(completed._tasks);completed._tasks.Clear();_nextTaskOrder=completed._nextTaskOrder;
        _visualRandom=completed._visualRandom;
        _clearTicks=completed._clearTicks;completed._clearTicks=-1;
        // LaunchBattleAnimation repeats the visible-only priority initialization;
        // canonical battler subpriority is already derived from its source slot.
        RestoreVisibleBattlerPriorities();
    }
    private void CarryControllerDisplay(SourceBattleAnimation completed)
    {
        // Table launch does not clear global display registers or other battlers'
        // palettes. Carry those live latches; the swap resets only its actor bank.
        // Sprite tags, loaded graphics and sprite callbacks remain owner-scoped.
        foreach(var pair in completed._paletteBlends)_paletteBlends[pair.Key]=pair.Value;
        foreach(var pair in completed._unfadedPaletteStates)_unfadedPaletteStates[pair.Key]=pair.Value;
        foreach(var pair in completed._fadedBackgroundEntries)_fadedBackgroundEntries[pair.Key]=pair.Value;
        foreach(var pair in completed._unfadedBackgroundEntries)_unfadedBackgroundEntries[pair.Key]=pair.Value;
        _invertedPalettes.UnionWith(completed._invertedPalettes);_greyscale.UnionWith(completed._greyscale);
        _unfadedBackgroundPalette=completed._unfadedBackgroundPalette is {} normal?(ushort[])normal.Clone():null;
        _fadedBackgroundPalette=completed._fadedBackgroundPalette is {} faded?(ushort[])faded.Clone():null;
        _alphaA=completed._queuedAlphaRegister?completed._alphaA:completed.SnapshotAlpha(false);
        _alphaB=completed._queuedAlphaRegister?completed._alphaB:completed.SnapshotAlpha(true);_blendEnabled=completed._blendEnabled;
        _queuedAlphaRegister=completed._queuedAlphaRegister;Array.Copy(completed._queuedBackgroundRegisters,_queuedBackgroundRegisters,4);
        _blendFirstTargets=completed._blendFirstTargets;_blendSecondTargets=completed._blendSecondTargets;
        // LaunchBattleAnimation zeros WIN0/1 bounds, not their masks or enable bits.
        _window0=completed._window0 is {} w0?w0 with{Horizontal=0,Vertical=0}:null;
        _window1=completed._window1 is {} w1?w1 with{Horizontal=0,Vertical=0}:null;
        _objectWindow=completed._objectWindow;_brightness=completed._brightness;
        _moveBackground=completed._moveBackground;_backgroundRevision=completed._backgroundRevision;
        BackgroundScrollX=completed.BackgroundScrollX;BackgroundScrollY=completed.BackgroundScrollY;BackgroundScreenSize=completed.BackgroundScreenSize;
        _hardwareDarkening=completed._hardwareDarkening;_backgroundFadeState=completed._backgroundFadeState;
        _fade=completed._fade;_hardwareFade=completed._hardwareFade;
        _overlay=completed._overlay;_statMask=completed._statMask;_metallic=completed._metallic;
        Array.Copy(completed._battlerBackgroundPriorities,_battlerBackgroundPriorities,2);
        Array.Copy(completed._battlerBackgroundMosaic,_battlerBackgroundMosaic,2);
        Array.Copy(completed._battlerBackgroundEnabled,_battlerBackgroundEnabled,2);
        _backgroundMosaicHorizontal=completed._backgroundMosaicHorizontal;_backgroundMosaicVertical=completed._backgroundMosaicVertical;
    }
}

public enum SourceMoveAnimationPhase { Reveal,Move,Restore,AwaitAcknowledgement,Complete,Cancelled }

/// <summary>One source controller command, with its optional Substitute table
/// animations. The caller authenticates stage selection and owns multihit lifetime.</summary>
public sealed class SourceMoveAnimationSequence
{
    private readonly SourceBattleAnimation _move,_first;
    private readonly SourceBattleAnimation? _reveal,_restore;
    private bool _started;
    private int _moveLaunchFrames;
    public SourceBattleAnimation Current{get;private set;}
    public CommittedMoveAnimation Cue=>_move.Cue;
    public SourceMoveAnimationPhase Phase{get;private set;}
    public bool Active=>Phase is not (SourceMoveAnimationPhase.Complete or SourceMoveAnimationPhase.Cancelled);
    public SourceMoveAnimationSequence(SourceBattleAnimation move,SourceBattleAnimation? reveal=null,SourceBattleAnimation? restore=null)
    {
        ArgumentNullException.ThrowIfNull(move);move.RequirePrepared();
        if(move.Cue.MoveId<=0||move.Cue.GeneralAnimationLabel is not null||move.Cue.StatAnimationArgument is not null)
            throw new ArgumentException("A move controller needs an ordinary committed move program.");
        reveal?.RequireSameInvocation(move,"Special_SubstituteToMon");
        restore?.RequireSameInvocation(move,"Special_MonToSubstitute");
        _move=move;_reveal=reveal;_restore=restore;_first=reveal??move;Current=_first;
        Phase=reveal is null?SourceMoveAnimationPhase.Move:SourceMoveAnimationPhase.Reveal;
        _moveLaunchFrames=reveal is null?2:1;
    }
    internal void Start()
    {
        if(_started||!Active)throw new InvalidOperationException("The move controller was already started or cancelled.");
        // The gate has verified every selected program and its receipt before
        // this single acquisition can reveal a doll or affect the shared scene.
        Current.AttachPrepared();_started=true;
    }
    public SourceAnimationFrame Snapshot()=>Current.Snapshot() with{Active=Active};
    public SourceAnimationFrame Tick()
    {
        if(!_started||!Active)throw new InvalidOperationException("The move controller has not started or already ended.");
        if(Phase==SourceMoveAnimationPhase.AwaitAcknowledgement)
        {
            // Source controller case3 checks specialAnimActive on the following
            // callback1, not in the frame that its priority-10 table task ends.
            var completed=Current.Tick(null,controllerCompletion:true);Phase=SourceMoveAnimationPhase.Complete;return completed;
        }
        if(Phase==SourceMoveAnimationPhase.Reveal&&!Current.Active)
        {
            _move.AttachPrepared(Current);Current=_move;Phase=SourceMoveAnimationPhase.Move;
        }
        if(Phase==SourceMoveAnimationPhase.Move&&_moveLaunchFrames>0)
        {
            // case0 without reveal only advances the controller state. case1
            // launches DoMoveAnim but does not call its script until case2 on
            // the following frame. Each still advances the shared scene once.
            if(--_moveLaunchFrames==0)Current.LaunchControllerMove();
            return Current.Tick(null,controllerLaunch:true) with{Active=Active};
        }
        var frame=Phase==SourceMoveAnimationPhase.Move?Current.Tick(CompleteMove):Current.Tick();
        if(!Current.Active&&(Phase==SourceMoveAnimationPhase.Restore||Phase==SourceMoveAnimationPhase.Move&&_restore is null))
            Phase=SourceMoveAnimationPhase.AwaitAcknowledgement;
        return frame with{Active=Active};
    }
    private SourceBattleAnimation? CompleteMove()
    {
        if(_restore is null){Current.SetControllerBattlerAffineMode();return null;}
        _restore.AttachPrepared(_move);Current=_restore;Current.SetControllerBattlerAffineMode();Phase=SourceMoveAnimationPhase.Restore;return Current;
    }
    public void Cancel()
    {
        if(Phase==SourceMoveAnimationPhase.Cancelled)return;
        // Stop only the current phase's audio, once. Older phases relinquish
        // their own residuals, never another invocation's sprites or sound.
        Current.Cancel();
        if(!ReferenceEquals(Current,_move))_move.CancelCore(false);
        if(_reveal is not null&&!ReferenceEquals(Current,_reveal))_reveal.CancelCore(false);
        if(_restore is not null&&!ReferenceEquals(Current,_restore))_restore.CancelCore(false);
        Current=_first;Phase=SourceMoveAnimationPhase.Cancelled;
    }
}

public sealed partial class SourceBattleAnimationGate
{
    public bool TryStartSequence(CommittedMoveAnimation cue,Func<SourceMoveAnimationSequence> create)
    {
        if(IsBlocking||_started.Contains((cue.BattleId,cue.Sequence)))return false;
        var candidate=create();
        if(candidate.Cue!=cue)throw new ArgumentException("The prepared controller differs from its committed gate receipt.");
        candidate.Start();_started.Add((cue.BattleId,cue.Sequence));_current=null;_sequence=candidate;return true;
    }
    public SourceAnimationFrame Tick()=>_sequence?.Tick()??(_current??throw new InvalidOperationException("No animation was started.")).Tick();
    public SourceAnimationFrame Snapshot()=>_sequence?.Snapshot()??(_current??throw new InvalidOperationException("No animation was started.")).Snapshot();
    public void Cancel(){if(_sequence is {} sequence)sequence.Cancel();else _current?.Cancel();}
}
