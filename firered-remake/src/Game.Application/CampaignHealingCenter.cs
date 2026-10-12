using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.World;
namespace Game.Application;

public sealed record CampaignHealingEffect(string Symbol,int PartyCount);

public sealed partial class CampaignSession
{
    private const string ViridianCenterMap="map.viridian-pokemon-center-1f";
    private bool IsSupportedHealingCenter(Game.Domain.Ids.MapId map)=>_content.CinnabarEnabled&&map.Value=="map.cinnabar-island-pokemon-center-1f"||_content.SaffronGymEnabled&&map.Value=="map.saffron-city-pokemon-center-1f"||_content.FuchsiaRouteEnabled&&map.Value=="map.fuchsia-city-pokemon-center-1f"||_content.CeladonApproachEnabled&&map.Value is "map.lavender-town-pokemon-center-1f" or "map.celadon-city-pokemon-center-1f"||_content.RockTunnelEnabled&&map.Value=="map.route-10-pokemon-center-1f"||map.Value==ViridianCenterMap||_content.VermilionEnabled&&map.Value=="map.vermilion-city-pokemon-center-1f"||_content.CeruleanEnabled&&map.Value=="map.cerulean-city-pokemon-center-1f"||_content.PewterEnabled&&map.Value=="map.pewter-pokemon-center-1f"||_content.Route3Enabled&&map.Value=="map.route-4-pokemon-center-1f";
    private bool HasCenterRespawn(CampaignState state)=>_content.ViridianCenterEnabled&&(_content.CinnabarEnabled&&state.LastHealingLocation=="HEAL_LOCATION_CINNABAR_ISLAND"||_content.SaffronGymEnabled&&state.LastHealingLocation=="HEAL_LOCATION_SAFFRON_CITY"||_content.FuchsiaRouteEnabled&&state.LastHealingLocation=="HEAL_LOCATION_FUCHSIA_CITY"||_content.CeladonApproachEnabled&&state.LastHealingLocation is "HEAL_LOCATION_LAVENDER_TOWN" or "HEAL_LOCATION_CELADON_CITY"||_content.RockTunnelEnabled&&state.LastHealingLocation=="HEAL_LOCATION_ROUTE10"||state.LastHealingLocation=="HEAL_LOCATION_VIRIDIAN_CITY"||_content.VermilionEnabled&&state.LastHealingLocation=="HEAL_LOCATION_VERMILION_CITY"||_content.CeruleanEnabled&&state.LastHealingLocation=="HEAL_LOCATION_CERULEAN_CITY"||_content.PewterEnabled&&state.LastHealingLocation=="HEAL_LOCATION_PEWTER_CITY"||_content.Route3Enabled&&state.LastHealingLocation=="HEAL_LOCATION_ROUTE4");
    private FieldActorDefinition HealingNurse(Game.Domain.Ids.MapId map)=>_content.MapDefinition(map).Actors.Single(a=>a.GraphicsId=="OBJ_EVENT_GFX_NURSE");
    private const string CenterUsageStat="GAME_STAT_USED_POKECENTER";
    private bool TryApplyCenterEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.ViridianCenterEnabled)return false;
        var a=effect.Args;bool inside=IsSupportedHealingCenter(state.World.Player.MapId);
        bool Is(string symbol,string expected)=>symbol==expected||(_content.TryConstant(symbol,out var x)&&_content.TryConstant(expected,out var y)&&x==y)||int.TryParse(symbol,out var literal)&&_content.TryConstant(expected,out var value)&&literal==value;
        void RequireCenter(){if(!inside)throw new InvalidDataException("healing_center_effect_outside_supported_map");}
        switch(effect.Opcode)
        {
            case "multichoice":
                RequireCenter();
                if(a[0]!="19"||a[1]!="8"||!Is(a[2],"MULTICHOICE_YES_NO")||a[3].Replace(" ","",StringComparison.Ordinal) is not ("FALSE|(TRUE<<1)" or "2"))throw new InvalidDataException("center_menu_profile_unsupported");
                // Result indices are 0=YES/1=NO, and bit 1 of flags 2 does not disable B.
                return true;
            case "incrementgamestat":
                RequireCenter();if(!Is(a[0],CenterUsageStat))throw new InvalidDataException("game_stat_profile_unsupported");
                var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty.WithComparers(StringComparer.Ordinal);
                next=state with{GameStats=stats.SetItem(CenterUsageStat,Math.Min(0xffffff,stats.GetValueOrDefault(CenterUsageStat)+1))};return true;
            case "dofieldeffect":
                RequireCenter();if(!Is(a[0],"FLDEFF_POKECENTER_HEAL")||state.HealingEffect is not null||state.Party.Length is <1 or >6)throw new InvalidDataException("center_healing_effect_invalid");
                next=state with{HealingEffect=new("FLDEFF_POKECENTER_HEAL",state.Party.Length)};return true;
            case "waitfieldeffect":
                RequireCenter();if(!Is(a[0],"FLDEFF_POKECENTER_HEAL")||state.HealingEffect is null)throw new InvalidDataException("center_healing_wait_without_effect");
                // Completion is acknowledged by presentation. Actual healing remains the later source special.
                return true;
            case "specialvar":
                RequireCenter();if(!a[0].StartsWith("VAR_",StringComparison.Ordinal))throw new InvalidDataException("specialvar_destination_invalid");
                int result=a[1] switch{"IsPlayerNotInTrainerTowerLobby"=>1,"BufferUnionRoomPlayerName"=>0,_=>throw new InvalidDataException("center_specialvar_unsupported: "+a[1])};
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(a[0],result)}};return true;
            case "special" when a[0]=="InitUnionRoom":
                RequireCenter();
                // Offline profile has no populated Union Room name. Source clears it here.
                // RFU task/frame RNG and real link play remain outside the semantic rule stream.
                return true;
            case "special" when a[0]=="SetUsedPkmnCenterQuestLogEvent":
                RequireCenter();if(state.Script.QuestLogState!=0)throw new InvalidDataException("quest_log_recording_not_supported");
                // With stopped playback, ShouldRegisterEvent_HandlePartyActions rejects this
                // particular event before starting a scene. Enabled step recording is an invariant
                // of this profile; retain the source invocation in PresentationEffects/trace.
                return true;
            default:return false;
        }
    }
    private string? ValidateCenterState(CampaignState state)
    {
        if(!_content.ViridianCenterEnabled)return state.GameStats is not null||state.HealingEffect is not null?"save_center_profile_missing":null;
        if(state.HealingEffect is not null)return "save_center_healing_active";
        if(state.LastHealingLocation!="HEAL_LOCATION_PALLET_TOWN"&&!HasCenterRespawn(state))return "save_heal_location_unsupported";
        if(state.GameStats is {} stats&&stats.Any(x=>(x.Key!=CenterUsageStat&&(!_content.CityServicesEnabled||x.Key!="GAME_STAT_FISHING_CAPTURES")&&(!_content.ForestEnabled||x.Key!=EvolutionCountStat)&&(!_content.VermilionGymEnabled||x.Key!=CutUsageStat))||x.Value is <0 or >0xffffff))return "save_game_stat_invalid";
        return null;
    }
}
