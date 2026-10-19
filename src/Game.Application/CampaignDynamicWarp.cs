using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // The dynamic destination has already passed its owning script's validation.
    // Commit the same movement facts as an ordinary arrow warp before EnterMap.
    private static CampaignState CommitDynamicFieldWarp(CampaignState state,TilePosition destination,
        TilePosition arrivalAnchor,int arrivalBehavior)=>state with
    {
        World=state.World with{Revision=checked(state.World.Revision+1),Player=destination},
        FieldTransition=new(FieldTransitionCause.PlainWarp,FieldTransitionDeparture.Immediate,
            FieldTransitionReceipt.ArrivalAt(arrivalBehavior),state.World.Player,destination,arrivalAnchor)
    };
}
