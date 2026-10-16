using Game.Application;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Game.Presentation.BattleAnimations;
using Godot;
public partial class CampaignView
{
    private readonly SourceBattleAnimationGate _sourceResultAnimationGate=new();
    private sealed record SourceResultContinuation(string BattleId,long Sequence,long ReceiptSequence,BattlerSlot Affected,string? GeneralLabel,int? StatArgument);
    private SourceResultContinuation? _sourceResultContinuation;
    private bool SourceAnimationBlocking=>_sourceMoveAnimationGate.IsBlocking||_sourceResultAnimationGate.IsBlocking;
    private bool SourceAnimationPresented=>_sourceMoveContinuation is not null||_sourceResultContinuation is not null;
    private void SynchronizeSourceResultPresentation()
    {
        if(_sourceResultContinuation is not null)
        {if(!SourceResultContextMatches())CancelSourceResultAnimation("context-changed");else return;}
        if(_sourceMoveContinuation is not null)return;
        bool doubles=DoubleBattleVisible;
        SourceResultContinuation? dispatch=HeldItemResultContinuation(doubles);
        if(dispatch is null)
        {
            CampaignStatAnimationDispatch? statDispatch=null;
            if(doubles&&_session.State.DoubleBattle is {} d)
                statDispatch=CampaignStatAnimationPresentation.AtResult(d);
            else if(PresentedWildBattle is {} b)
                statDispatch=CampaignStatAnimationPresentation.AtResult(b,SourceTrainerPresentationActive(b));
            if(statDispatch is {} result)
            {
                bool substitute=doubles?DoubleDisplayedBody(result.Affected.Value).CalledVolatile?.SubstituteHp>0:
                    _sourceSubstituteHp.GetValueOrDefault((WildBattleSide)result.Affected.Value)>0;
                if(!substitute)dispatch=new(result.BattleId,result.Sequence,result.Sequence,result.Affected,null,result.AnimationArgument);
            }
        }
        if(dispatch is null||!CampaignResultAnimationPolicy.Allows(dispatch.GeneralLabel is null?CampaignResultAnimationKind.StatChange:CampaignResultAnimationKind.HeldItem,_session.EffectiveOptions.BattleScene))return;
        EnsureSourceMoveAssets();
        var resolved=dispatch.GeneralLabel is {} label?_sourceAnimationResources!.ResolveGeneral(label):_sourceAnimationResources!.Resolve(0);
        var cue=new CommittedMoveAnimation(dispatch.BattleId,dispatch.ReceiptSequence,0,new(dispatch.Affected.Value),new(dispatch.Affected.Value))
        {StatAnimationArgument=dispatch.StatArgument,GeneralAnimationLabel=dispatch.GeneralLabel};
        var (assets,battlers)=CreateSourceAnimationScene(doubles);
        foreach(var (id,asset) in resolved.Assets)assets[id]=asset;
        SourceBattleAnimationAudioAdapter? sound=null;
        if(!_sourceResultAnimationGate.TryStart(cue,()=>
        {
            sound=_sourceAudio is SynthSourceAudioPlayer synth&&!_audioFaulted?new(synth,c=>Write("sourceResultAnimationSound",new{dispatch.BattleId,dispatch.Sequence,c.Id,c.Frame,c.Pan})):null;
            return new(resolved.Script,cue,battlers,doubles,(ISourceAnimationAudio?)sound??new SilentMoveAudio());
        }))return;
        _sourceMoveSound=sound;_sourceMoveComposer=new(assets,battlers.ToArray(),resolved.StatMask,backgroundSurface:SourceTerrainSurface(assets["background"]));_sourceResultContinuation=dispatch;
        _sourceMoveSession=_session;_sourceMoveRevision=_session.Revision;_sourceMoveRemainder=0;_sourceMoveDoubles=doubles;
        PresentSourceMoveFrame(_sourceResultAnimationGate.Current!.Tick());
        Write("sourceResultAnimationStarted",new{dispatch,doubles,revision=_sourceMoveRevision,scene=_session.EffectiveOptions.BattleScene.ToString()});
    }
    private bool SourceResultContextMatches()
    {
        if(_sourceResultContinuation is not {} pending||_sourceMoveSession!=_session||_sourceMoveRevision!=_session.Revision)return false;
        return _sourceMoveDoubles?_session.State.DoubleBattle is {Stage:CampaignDoubleBattleStage.Combat} d&&d.Id==pending.BattleId&&d.CurrentEvent?.Sequence==pending.Sequence:
            PresentedWildBattle is {} b&&b.Id==pending.BattleId&&WildCurrentEvent(b)?.Sequence==pending.Sequence;
    }
    private bool ProcessCommittedResultAnimation(double delta)
    {
        if(_sourceResultContinuation is not {} pending)return false;
        if(!SourceResultContextMatches()){CancelSourceResultAnimation("context-changed");return false;}
        _sourceMoveRemainder+=delta;
        while(_sourceMoveRemainder>=SourceBattleAnimation.SourceFrameSeconds&&_sourceResultAnimationGate.IsBlocking)
        {_sourceMoveRemainder-=SourceBattleAnimation.SourceFrameSeconds;PresentSourceMoveFrame(_sourceResultAnimationGate.Current!.Tick());}
        if(!_sourceResultAnimationGate.IsBlocking)
        {
            _sourceResultContinuation=null;_sourceMoveSession=null;_sourceMoveComposer=null;_sourceMoveSound=null;_wildElapsed=_doubleElapsed=0;
            Write("sourceResultAnimationFinished",new{pending.BattleId,pending.Sequence,sourceFrame=_sourceResultAnimationGate.Current!.Snapshot().Frame,acknowledged=false});
        }
        QueueRedraw();return true;
    }
    private void CancelSourceResultAnimation(string reason)
    {
        var pending=_sourceResultContinuation;_sourceResultContinuation=null;_sourceResultAnimationGate.Current?.Cancel();
        if(pending is null)return;
        _sourceMoveSession=null;_sourceMoveSound=null;_sourceMoveComposer=null;_sourceMoveRemainder=0;
        Write("sourceResultAnimationCancelled",new{pending.BattleId,pending.Sequence,reason});
    }
}
