using Game.Domain;
using Game.Domain.Items;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private string? ValidateBicycleState(CampaignState state)
    {
        if(state.IsCycling&&state.IsSurfing)return "save_conflicting_movement_modes";
        if(!state.IsCycling)return null;
        if(!_content.CityServicesEnabled||OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],BicycleId)!=1)
            return "save_bicycle_not_owned";
        if(!_content.AllowsCycling(state.World.Player.MapId))return "save_bicycle_map_disallowed";
        return null;
    }
    private Decision<CampaignState> UseFieldBicycle(CampaignState state,CampaignCommand command)
    {
        Decision<CampaignState> No(string error)=>new(CommandStatus.Rejected,state,error);
        if(!_content.CityServicesEnabled||state.ItemMenu is not {Origin:"bag",View:"bag"}||command.Text!=BicycleId.Value)
            return No("bicycle_context_invalid");
        if(OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],BicycleId)!=1)return No("bicycle_not_owned");
        var p=state.World.Player;
        if(state.IsSurfing)return No("bicycle_not_allowed_while_surfing");
        if(state.Script.Flags.GetValueOrDefault("FLAG_SYS_ON_CYCLING_ROAD"))return No("bicycle_cannot_dismount");
        // Fixed FireRed rail/bridge predicates all return FALSE. Ordinary field
        // use still observes the source map bit and MB_RUNNING_DISALLOWED.
        if(!_content.AllowsCycling(p.MapId)||SourceBehavior(state,p.X,p.Y)==0x0a)return No("bicycle_not_allowed_here");
        return new(CommandStatus.Accepted,state with{IsCycling=!state.IsCycling,ItemMenu=null});
    }
}
