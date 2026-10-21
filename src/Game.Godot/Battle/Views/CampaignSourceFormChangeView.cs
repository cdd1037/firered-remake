using Game.Application;
using Game.Presentation.BattleAnimations;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private SourceResultContinuation? CastformResultContinuation(bool doubles)
    {
        if(_sourceResultEntryCharge is null)return null;
        CampaignCastformAnimationDispatch? dispatch;
        if(doubles&&_session.State.DoubleBattle is {} d)
            dispatch=CampaignCastformAnimationPresentation.AtResult(d,_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge);
        else if(PresentedWildBattle is {} b)
            dispatch=CampaignCastformAnimationPresentation.AtResult(b,SourceTrainerPresentationActive(b),_session.EffectiveOptions.BattleScene,_sourceResultEntryCharge);
        else return null;
        return dispatch is null?null:new(dispatch.BattleId,dispatch.Sequence,dispatch.Sequence,dispatch.Affected,
            "General_CastformChange",null,CampaignResultAnimationKind.FormChange){FormChange=dispatch};
    }
    private Dictionary<int,AnimationBattler> PrepareCastformMaterials(CampaignCastformAnimationDispatch dispatch,
        Dictionary<string,SourcePixelAsset> assets,List<AnimationBattler> battlers,bool doubles)
    {
        if(dispatch.SpriteVisibility.Length!=battlers.Count)throw new InvalidDataException("Source form visibility positions differ.");
        for(int i=0;i<battlers.Count;i++)
        {
            int position=battlers[i].Slot.Value;
            var body=doubles?DoubleDisplayedBody(position):SingleDisplayedBody(PresentedWildBattle!,(WildBattleSide)position)??throw new InvalidDataException("Source form scene body is missing.");
            battlers[i]=battlers[i] with{Visible=body.Pokemon.CurrentHp>0&&dispatch.SpriteVisibility[position]};
        }
        int slot=dispatch.Affected.Value;var before=FormBattleMaterial(dispatch.BeforeBody,new(slot));
        var after=FormBattleMaterial(dispatch.AfterBody,new(slot));
        string oldKey="battler-"+slot,newKey=oldKey+"-replacement";
        assets[oldKey]=before.Pixels;assets[newKey]=after.Pixels;
        var initial=before.Geometry with{Slot=new(slot),Asset=oldKey,Visible=!dispatch.Immediate};
        var replacement=after.Geometry with{Slot=new(slot),Asset=newKey,Visible=initial.Visible};
        int index=battlers.FindIndex(b=>b.Slot.Value==slot);
        if(index<0)throw new InvalidDataException("Source form transition actor is missing.");
        battlers[index]=initial;
        return new(){{slot,replacement}};
    }
}
