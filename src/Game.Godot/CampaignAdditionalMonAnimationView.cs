using Game.Application;
using Game.Presentation.BattleAnimations;

public partial class CampaignView
{
    private SourceBattlerFormMaterial? AdditionalMonAnimationMaterial(CampaignMoveAnimationDispatch dispatch,bool doubles)
    {
        var battle=doubles?_session.State.DoubleBattle:null;
        var wild=doubles?null:PresentedWildBattle;
        var displayed=battle is not null?new CampaignBattleDisplayBodies(battle.DisplayedSlots,battle.DisplayedMembers):wild?.DisplayBatch?.Display;
        if(displayed is null)return null;
        var request=CampaignAdditionalMonPresentation.RolePlay(dispatch,displayed,battle?.Frame??wild!.Frame,battle?.Events??wild!.Events);
        if(request is null)return null;
        string face=request.Back?"back":"front";
        if(!_sourceBattlerGeometry.TryGetValue((request.Species,face),out var geometry))return null;
        SourcePixelAsset pixels;
        if(BattleForms.TryBaseFrame(request.Species,face,request.Shiny,out var baseFrame))pixels=baseFrame;
        else if(BattleForms.HasForms(request.Species))pixels=BattleForms.Resolve(request.Species,new(request.Back?0:1),0,request.Shiny).Pixels;
        else
        {
            if(!_battleArt.TryGetValue((request.RenderSpecies,face,request.Shiny),out var art))return null;
            pixels=SourcePixels(art.Texture,64,64);
        }
        // Native additional-mon loading uses the input species coordinate table,
        // even when personality selects a different Unown picture table entry.
        return new(geometry with{Asset="additional-mon",Slot=new(dispatch.Actor.Value)},pixels);
    }
}
