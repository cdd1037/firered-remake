using Game.Application;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Game.Presentation.BattleAnimations;
using Godot;
public partial class CampaignView
{
    private readonly SourceBattleAnimationGate _sourceStatAnimationGate=new();
    private CampaignStatAnimationDispatch? _sourceStatContinuation;
    private SourceAnimationScript? _sourceStatScript;
    private SourceStatMaskMaterial? _sourceStatMaterial;
    private bool SourceAnimationBlocking=>_sourceMoveAnimationGate.IsBlocking||_sourceStatAnimationGate.IsBlocking;
    private bool SourceAnimationPresented=>_sourceMoveContinuation is not null||_sourceStatContinuation is not null;
    private void SynchronizeSourceStatPresentation()
    {
        if(_sourceStatContinuation is not null)
        {if(!SourceStatContextMatches())CancelSourceStatAnimation("context-changed");else return;}
        if(_sourceMoveContinuation is not null)return;
        CampaignStatAnimationDispatch? dispatch=null;bool doubles=DoubleBattleVisible;
        if(doubles&&_session.State.DoubleBattle is {} d&&d.CurrentEvent is SourceStatChangedEvent ds)
            dispatch=CampaignStatAnimationPresentation.AtResult(d,DoubleDisplayedBody(ds.Target.Value).CalledVolatile?.SubstituteHp>0);
        else if(PresentedWildBattle is {} b&&WildCurrentEvent(b) is WildBattleStatChangedEvent stat)
            dispatch=CampaignStatAnimationPresentation.AtResult(b,SourceTrainerPresentationActive(b),_sourceSubstituteHp.GetValueOrDefault(stat.Target)>0);
        if(dispatch is null)return;
        EnsureSourceMoveAssets();
        if(_sourceStatScript is null)(_sourceStatScript,_sourceStatMaterial)=SourceStatAnimationCatalog.Read(System.IO.Path.Combine(_root,"content/generated/battle/animations/stat-mask"));
        var cue=new CommittedMoveAnimation(dispatch.BattleId,dispatch.Sequence,0,new(dispatch.Affected.Value),new(dispatch.Affected.Value)){StatAnimationArgument=dispatch.AnimationArgument};
        var (assets,battlers)=CreateSourceAnimationScene(doubles);
        SourceBattleAnimationAudioAdapter? sound=null;
        if(!_sourceStatAnimationGate.TryStart(cue,()=>
        {
            sound=_sourceAudio is SynthSourceAudioPlayer synth&&!_audioFaulted?new(synth,c=>Write("sourceStatAnimationSound",new{dispatch.BattleId,dispatch.Sequence,c.Id,c.Frame,c.Pan})):null;
            return new(_sourceStatScript,cue,battlers,doubles,(ISourceAnimationAudio?)sound??new SilentMoveAudio());
        }))return;
        _sourceMoveSound=sound;_sourceMoveComposer=new(assets,battlers.ToArray(),_sourceStatMaterial);_sourceStatContinuation=dispatch;
        _sourceMoveSession=_session;_sourceMoveRevision=_session.Revision;_sourceMoveRemainder=0;_sourceMoveDoubles=doubles;
        PresentSourceMoveFrame(_sourceStatAnimationGate.Current!.Tick());
        Write("sourceStatAnimationStarted",new{dispatch,doubles,revision=_sourceMoveRevision,scene=_session.EffectiveOptions.BattleScene.ToString()});
    }
    private bool SourceStatContextMatches()
    {
        if(_sourceStatContinuation is not {} pending||_sourceMoveSession!=_session||_sourceMoveRevision!=_session.Revision)return false;
        return _sourceMoveDoubles?_session.State.DoubleBattle is {Stage:CampaignDoubleBattleStage.Combat} d&&d.Id==pending.BattleId&&d.CurrentEvent?.Sequence==pending.Sequence:
            PresentedWildBattle is {} b&&b.Id==pending.BattleId&&WildCurrentEvent(b)?.Sequence==pending.Sequence;
    }
    private bool ProcessCommittedStatAnimation(double delta)
    {
        if(_sourceStatContinuation is not {} pending)return false;
        if(!SourceStatContextMatches()){CancelSourceStatAnimation("context-changed");return false;}
        _sourceMoveRemainder+=delta;
        while(_sourceMoveRemainder>=SourceBattleAnimation.SourceFrameSeconds&&_sourceStatAnimationGate.IsBlocking)
        {_sourceMoveRemainder-=SourceBattleAnimation.SourceFrameSeconds;PresentSourceMoveFrame(_sourceStatAnimationGate.Current!.Tick());}
        if(!_sourceStatAnimationGate.IsBlocking)
        {
            _sourceStatContinuation=null;_sourceMoveSession=null;_sourceMoveComposer=null;_sourceMoveSound=null;_wildElapsed=_doubleElapsed=0;
            Write("sourceStatAnimationFinished",new{pending.BattleId,pending.Sequence,sourceFrame=_sourceStatAnimationGate.Current!.Snapshot().Frame,acknowledged=false});
        }
        QueueRedraw();return true;
    }
    private void CancelSourceStatAnimation(string reason)
    {
        var pending=_sourceStatContinuation;_sourceStatContinuation=null;_sourceStatAnimationGate.Current?.Cancel();
        if(pending is null)return;
        _sourceMoveSession=null;_sourceMoveSound=null;_sourceMoveComposer=null;_sourceMoveRemainder=0;
        Write("sourceStatAnimationCancelled",new{pending.BattleId,pending.Sequence,reason});
    }
}
