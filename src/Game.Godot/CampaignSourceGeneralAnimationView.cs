using Game.Application;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private SourceResultContinuation? GeneralResultContinuation(bool doubles,CampaignGeneralAnimationBoundary boundary)
    {
        if(_sourceResultEntryCharge is null)return null;
        CampaignGeneralAnimationDispatch? dispatch;
        if(doubles&&_session.State.DoubleBattle is {} d)
        {
            var substitutes=Enumerable.Range(0,4).Select(i=>DoubleDisplayedBody(i).CalledVolatile?.SubstituteHp>0).ToArray();
            dispatch=CampaignGeneralAnimationPresentation.AtResult(d,_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge,substitutes);
        }
        else if(PresentedWildBattle is {} b)
        {
            var substitutes=Enumerable.Range(0,2).Select(i=>_sourceSubstituteHp.GetValueOrDefault((WildBattleSide)i)>0).ToArray();
            dispatch=CampaignGeneralAnimationPresentation.AtResult(b,SourceTrainerPresentationActive(b),_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge,substitutes);
        }
        else return null;
        return dispatch is null||dispatch.Boundary!=boundary?null:
            new(dispatch.BattleId,dispatch.Sequence,dispatch.ReceiptSequence,dispatch.Actor,dispatch.GeneralLabel,null,CampaignResultAnimationKind.General){General=dispatch};
    }
    private bool TryStartAfterTextGeneral(bool doubles)
    {
        if(SourceAnimationBlocking)return true;
        if(_sourceResultContinuation is not null||_sourceMoveContinuation is not null)return false;
        return GeneralResultContinuation(doubles,CampaignGeneralAnimationBoundary.AfterText) is {} dispatch&&StartSourceResultAnimation(dispatch,doubles);
    }
}
