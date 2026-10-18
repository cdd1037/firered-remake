using Game.Application;
using Game.Presentation.BattleAnimations;

public partial class CampaignView
{
    private bool PrepareMoveBattlerMaterial(CampaignMoveAnimationDispatch dispatch,bool doubles,
        Dictionary<string,SourcePixelAsset> assets,List<AnimationBattler> battlers,
        out Dictionary<int,AnimationBattler>? replacements)
    {
        replacements=null;
        var battle=doubles?_session.State.DoubleBattle:null;var wild=doubles?null:PresentedWildBattle;
        var display=battle is not null?new CampaignBattleDisplayBodies(battle.DisplayedSlots,battle.DisplayedMembers):wild?.DisplayBatch?.Display;
        if(display is null)return false;
        var request=CampaignMoveAnimationPresentation.MaterialChangeAtInvocation(dispatch,display,
            battle?.Events??wild!.SourceEvents,battle?.EventIndex??wild!.DisplayBatch!.NextEventIndex,
            _catalog.PokemonContent!.Catalog.Moves);
        if(request is null)return false;
        int slot=request.Affected.Value,index=battlers.FindIndex(b=>b.Slot.Value==slot);
        if(index<0)return false;
        var initial=battlers[index];SourceBattlerFormMaterial material;
        if(request.AfterBody.CalledVolatile?.SubstituteHp is not >0&&request.AfterBody.Transform is not null)
        {
            // Front Spinda requires the source indexed personality spot masks, which
            // are not in the current material catalog. Never substitute static pixels.
            var appearance=CampaignBattleFormAppearance.From(request.AfterBody);
            if(!initial.Slot.IsPlayer&&appearance.RenderPokemon.SourceSpeciesId==308)return false;
            material=FormBattleMaterial(request.AfterBody,initial.Slot);
        }
        else
        {
            string face=initial.Slot.IsPlayer?"back":"front";
            var center=initial.Base(doubles);
            material=new(initial with{DisplayCenter=new(center.X,center.Y+(initial.Slot.IsPlayer?17:16))},assets["substitute-"+face]);
        }
        string key="battler-"+slot+"-replacement";assets[key]=material.Pixels;
        replacements=new(){{slot,material.Geometry with{Slot=initial.Slot,Asset=key,Visible=initial.Visible,SourceVisibility=initial.SourceVisibility}}};
        return true;
    }
}
