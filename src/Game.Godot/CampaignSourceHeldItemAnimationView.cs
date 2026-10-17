using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private string? _sourceResultBattle;
    private (long First,long Last,int Count) _sourceResultBatch;
    private MoveId?[]? _sourceResultEntryCharge;
    private MoveId?[]? _sourceResultPreviousCharge;
    private void ClearSourceResultState()
    {_sourceResultBattle=null;_sourceResultBatch=default;_sourceResultEntryCharge=null;_sourceResultPreviousCharge=null;}
    private static MoveId?[] ChargeLocks(SourceBattleFrame frame,int count)=>Enumerable.Range(0,count).Select(i=>
        frame.Get(new(i)).Registers.Turn?.Lock is {Kind:WildBattleCalledLockKind.Charge} charge?(MoveId?)charge.Move:null).ToArray();
    // Capture the authoritative pre-command state once per event batch. The current
    // frame already includes later results and is retained only for the NEXT batch.
    private void SynchronizeSourceResultState(CampaignState? before,CampaignState after)
    {
        string? id;SourceBattleFrame? frame;SourceBattleFrame? previous=null;
        (long First,long Last,int Count) batch;bool started;int count;
        if(DoubleBattleVisible&&after.DoubleBattle is {} d)
        {
            id=d.Id;frame=d.Frame;count=4;batch=d.Events.IsEmpty?default:(d.Events[0].Sequence,d.Events[^1].Sequence,d.Events.Length);
            started=d.Events.FirstOrDefault() is SourceStartedEvent;
            if(before?.DoubleBattle is {} old&&old.Id==id)previous=old.Frame;
        }
        else if(PresentedWildBattle is {} b)
        {
            id=b.Id;frame=b.Frame;count=2;batch=b.Events.IsEmpty?default:(b.Events[0].Sequence,b.Events[^1].Sequence,b.Events.Length);
            started=b.Events.FirstOrDefault() is WildBattleStartedEvent;
            var old=before?.ForestTrainerBattle?.Presentation??before?.WildBattle;
            if(old?.Id==id)previous=old.Frame;
        }
        else
        {
            ClearSourceResultState();return;
        }
        if(_sourceResultBattle!=id||_sourceResultBatch!=batch)
        {
            _sourceResultEntryCharge=previous is not null?ChargeLocks(previous,count):
                _sourceResultBattle==id?_sourceResultPreviousCharge?.ToArray():started?new MoveId?[count]:null;
            _sourceResultBattle=id;_sourceResultBatch=batch;
        }
        _sourceResultPreviousCharge=ChargeLocks(frame,count);
    }
    private SourceResultContinuation? HeldItemResultContinuation(bool doubles)
    {
        if(_sourceResultEntryCharge is null)return null;
        CampaignHeldItemAnimationDispatch? dispatch;
        if(doubles&&_session.State.DoubleBattle is {} d)
            dispatch=CampaignHeldItemAnimationPresentation.AtResult(d,_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge);
        else if(PresentedWildBattle is {} b)
            dispatch=CampaignHeldItemAnimationPresentation.AtResult(b,SourceTrainerPresentationActive(b),_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge);
        else return null;
        if(dispatch is null)return null;
        bool substitute=doubles?DoubleDisplayedBody(dispatch.Affected.Value).CalledVolatile?.SubstituteHp>0:
            _sourceSubstituteHp.GetValueOrDefault((WildBattleSide)dispatch.Affected.Value)>0;
        return substitute?null:new(dispatch.BattleId,dispatch.Sequence,dispatch.ReceiptSequence,dispatch.Affected,"General_HeldItemEffect",null,CampaignResultAnimationKind.HeldItem);
    }
}
