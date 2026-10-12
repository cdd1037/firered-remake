using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Source Route23 badge guards and the bounded approach to the League.</summary>
public sealed partial class CampaignSession
{
    private static readonly string[] VictoryRoadBadgeNames=["BOULDER","CASCADE","THUNDER","RAINBOW","SOUL","MARSH","VOLCANO","EARTH"];
    private static bool VictoryRoadInterior(string map)=>map is "map.victory-road-1f" or "map.victory-road-2f" or "map.victory-road-3f";
    private static bool VictoryRoadDestination(string map)=>VictoryRoadInterior(map)||map=="map.indigo-plateau-exterior";
    private static void ValidateVictoryRoadCatalog(ICampaignCatalog content)
    {
        ValidatePokemonLeagueCatalog(content);
        if(!content.VictoryRoadEnabled)
        {
            if(content.VictoryRoadTrainerParties is not null||content.VictoryRoadOpponents is not null)throw new InvalidDataException("victory_road_profile_missing");
            return;
        }
        if(!content.LeagueRivalEnabled||content.Maps.Count!=(content.PokemonLeagueEnabled?218:211)||content.CinnabarOwnership is null||content.CinnabarOpponents is null||
            content.CinnabarOwnership.Catalog.Species.Count!=115||content.VictoryRoadTrainerParties is null||content.VictoryRoadOpponents is null||
            content.VictoryRoadOpponents.Catalog.Species.Count!=124||!ReferenceEquals(content.VictoryRoadOpponents.Predecessor,content.CinnabarOpponents)||
            VictoryRoadShopCatalog.For(content.EarlyShop) is null||VictoryRoadMachineCatalog.For(content.PewterMachines) is not {} machines||
            !ReferenceEquals(machines.Pokemon,content.CinnabarOwnership))throw new InvalidDataException("victory_road_catalog_scope");
        VictoryRoadTrainerPartyFactory.ValidateCatalog(content.VictoryRoadTrainerParties);
        var route=content.MapDefinition(new("map.route-23"));
        int[] triggerCounts=[5,4,4,10,7,4,8];
        for(int i=1;i<8;i++)
        {
            var prefix=VictoryRoadBadgeNames[i][..1]+VictoryRoadBadgeNames[i][1..].ToLowerInvariant();
            string script="Route23_EventScript_"+prefix+"BadgeGuardTrigger";
            if(route.CoordTriggers.Count(t=>t.ScriptId==script&&t.Variable=="VAR_MAP_SCENE_ROUTE23"&&t.Expected==i)!=triggerCounts[i-1])
                throw new InvalidDataException("victory_road_badge_trigger_source");
        }
    }

    private bool TryApplyVictoryRoadGateEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(!_content.VictoryRoadEnabled||effect.Opcode!="bufferstdstring"||effect.Source.Path!="data/maps/Route23/scripts.inc")return false;
        int badge=state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");
        if(state.World.Player.MapId.Value!="map.route-23"||badge is <2 or >8||effect.Args.Length!=2||effect.Args[0]!="STR_VAR_1"||
            effect.Args[1]!="STDSTRING_"+VictoryRoadBadgeNames[badge-1]+"_BADGE")throw new InvalidDataException("victory_road_badge_string_context");
        string title=VictoryRoadBadgeNames[badge-1][..1]+VictoryRoadBadgeNames[badge-1][1..].ToLowerInvariant();
        if(effect.Source.Symbol!="Route23_EventScript_"+title+"BadgeGuard"&&effect.Source.Symbol!="Route23_EventScript_"+title+"BadgeGuardTrigger")
            throw new InvalidDataException("victory_road_badge_string_source");
        next=state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",VictoryRoadBadgeNames[badge-1]+"BADGE")};return true;
    }

    private string? ValidateVictoryRoadProgress(CampaignState state)
    {
        string map=state.World.Player.MapId.Value;
        int gate=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE23");
        if(!_content.VictoryRoadEnabled)return VictoryRoadDestination(map)||gate>1?"save_victory_road_profile_missing":null;
        if(VictoryRoadDestination(map)&&(gate!=8||!Enumerable.Range(1,8).All(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"))))
            return "save_victory_road_badges_unrecognized";
        if(state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR")!=0)
            return "save_league_credits_not_admitted";
        return null;
    }
}
