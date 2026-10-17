using Game.Application;
using Game.Domain.Battle;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private SourceResultContinuation? WishHealingResultContinuation(bool doubles)
    {
        if(_sourceResultEntryCharge is null)return null;
        CampaignWishHealingAnimationDispatch? dispatch;
        if(doubles&&_session.State.DoubleBattle is {} d)
        {
            dispatch=CampaignWishHealingAnimationPresentation.AtResult(d,_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge);
            if(dispatch is not null&&(d.CurrentEvent is not SourceCalledDelayedEvent delayed||!CampaignWishResolutionPresentation.CasterAvailable(d.Frame,dispatch.Affected,delayed.SourcePartyIndex)))return null;
        }
        else if(PresentedWildBattle is {} b)
        {
            dispatch=CampaignWishHealingAnimationPresentation.AtResult(b,SourceTrainerPresentationActive(b),_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge);
            if(dispatch is not null&&(WildCurrentEvent(b) is not WildBattleCalledDelayedEvent delayed||!CampaignWishResolutionPresentation.CasterAvailable(b.Frame,dispatch.Affected,delayed.SourcePartyIndex)))return null;
        }
        else return null;
        if(dispatch is null)return null;
        bool substitute=doubles?DoubleDisplayedBody(dispatch.Affected.Value).CalledVolatile?.SubstituteHp>0:
            _sourceSubstituteHp.GetValueOrDefault((WildBattleSide)dispatch.Affected.Value)>0;
        return substitute?null:new(dispatch.BattleId,dispatch.Sequence,dispatch.ReceiptSequence,dispatch.Affected,"General_WishHeal",null,CampaignResultAnimationKind.DelayedHealing);
    }
}
