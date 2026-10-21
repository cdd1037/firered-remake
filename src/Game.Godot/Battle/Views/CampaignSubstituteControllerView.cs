using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Presentation.BattleAnimations;

public partial class CampaignView
{
    private bool SourceSubstituteRevealed(bool doubles,int slot)=>
        (doubles?_session.State.DoubleBattle is not null:PresentedWildBattle?.DisplayBatch is not null)&&
        _sourceSubstituteLifetime.IsRevealed(slot,SourceDisplayedMember(doubles,slot));

    private void RefreshSubstituteController(bool doubles)
    {
        if(doubles?_session.State.DoubleBattle is null:PresentedWildBattle?.DisplayBatch is null)
        {_sourceSubstituteLifetime.Clear();return;}
        for(int slot=0;slot<(doubles?4:2);slot++)
        {
            var body=doubles?DoubleDisplayedBody(slot):SingleDisplayedBody(PresentedWildBattle!,(WildBattleSide)slot);
            _sourceSubstituteLifetime.Observe(slot,SourceDisplayedMember(doubles,slot),body?.CalledVolatile?.SubstituteHp>0);
        }
    }

    private AnimationBattler SubstituteControllerBody(WildBattleCombatant body,AnimationBattler initial,
        Dictionary<string,SourcePixelAsset> assets)
    {
        var material=FormBattleMaterial(body,initial.Slot);
        string key="battler-"+initial.Slot.Value+"-controller-body";assets[key]=material.Pixels;
        return material.Geometry with{Slot=initial.Slot,Asset=key,Visible=initial.Visible,SourceVisibility=initial.SourceVisibility};
    }

    private static AnimationBattler SubstituteControllerDoll(AnimationBattler body,bool doubles)
    {
        var center=body.Base(doubles);
        return body with{Asset=body.Slot.IsPlayer?"substitute-back":"substitute-front",
            DisplayCenter=new(center.X,center.Y+(body.Slot.IsPlayer?17:16))};
    }
}
