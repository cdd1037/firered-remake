using Game.Domain.Ids;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private MapId OrdinaryTrainerMap(TrainerId trainer)
    {
        if(IsPokemonLeagueTrainer(trainer))return PokemonLeagueTrainerMap(trainer);
        var definition=OrdinaryTrainerScript(trainer);
        var sourceMap=definition.Source.Path.StartsWith("data/maps/",StringComparison.Ordinal)?definition.Source.Path.Split('/')[2]:definition.ScriptEntry.Split("_EventScript_",StringSplitOptions.None)[0];
        return sourceMap switch
        {
            _ when _content.SsAnneEnabled && sourceMap.StartsWith("SSAnne_",StringComparison.Ordinal)=>_content.MapFromSourceSymbol("MAP_"+System.Text.RegularExpressions.Regex.Replace(sourceMap,"([a-z])([A-Z])","$1_$2").ToUpperInvariant()),
            _ when _content.VictoryRoadEnabled&&sourceMap.StartsWith("VictoryRoad_",StringComparison.Ordinal)=>new("map.victory-road-"+sourceMap.Split('_')[1].ToLowerInvariant()),
            "OneIsland_KindleRoad" when _content.SeviiRubyEnabled=>new("map.one-island-kindle-road"),
            "MtEmber_Exterior" when _content.SeviiRubyEnabled=>new("map.mt-ember-exterior"),
            "ViridianCity_Gym" when _content.ViridianGymEnabled=>new("map.viridian-city-gym"),
            "VermilionCity_Gym" when _content.VermilionGymEnabled=>new("map.vermilion-city-gym"),
            _ when _content.PokemonTowerEnabled&&sourceMap.StartsWith("PokemonTower_",StringComparison.Ordinal)=>new("map.pokemon-tower-"+sourceMap.Split('_')[1].ToLowerInvariant()),
            "Route22" when _content.LeagueRivalEnabled=>new("map.route-22"),
            "Route11" when _content.Route11Enabled=>new("map.route-11"),
            "Route12" or "Route13" or "Route14" or "Route15" when _content.FuchsiaRouteEnabled=>new("map.route-"+sourceMap[5..]),
            "CeladonCity_GameCorner" when _content.RocketHideoutEnabled=>new("map.celadon-city-game-corner"),
            _ when _content.RocketHideoutEnabled&&sourceMap.StartsWith("RocketHideout_",StringComparison.Ordinal)=>new("map.rocket-hideout-"+sourceMap.Split('_')[1].ToLowerInvariant()),
            _ when _content.SilphEnabled&&sourceMap.StartsWith("SilphCo_",StringComparison.Ordinal)=>new("map.silph-co-"+sourceMap.Split('_')[1].ToLowerInvariant()),
            _ when _content.CinnabarEnabled&&sourceMap.StartsWith("PokemonMansion_",StringComparison.Ordinal)=>new("map.pokemon-mansion-"+sourceMap.Split('_')[1].ToLowerInvariant()),
            "ThreeIsland" when _content.FirstSeviiEnabled=>new("map.three-island"),
            "ThreeIsland_BondBridge" when _content.FirstSeviiEnabled=>new("map.three-island-bond-bridge"),
            "Route21_North" when _content.CinnabarEnabled=>new("map.route-21-north"),
            "Route21_South" when _content.CinnabarEnabled=>new("map.route-21-south"),
            "CinnabarIsland_Gym" when _content.CinnabarEnabled=>new("map.cinnabar-island-gym"),
            "SaffronCity_Gym" when _content.SaffronGymEnabled=>new("map.saffron-city-gym"),
            "FuchsiaCity_Gym" when _content.FuchsiaGymEnabled=>new("map.fuchsia-city-gym"),
            "CeladonCity_Gym" when _content.CeladonGymEnabled=>new("map.celadon-city-gym"),
            "Route8" when _content.CeladonApproachEnabled=>new("map.route-8"),
            "Route9" when _content.RockTunnelEnabled=>new("map.route-9"),
            "Route10" when _content.RockTunnelEnabled=>new("map.route-10"),
            "RockTunnel_1F" when _content.RockTunnelEnabled=>new("map.rock-tunnel-1f"),
            "RockTunnel_B1F" when _content.RockTunnelEnabled=>new("map.rock-tunnel-b1f"),
            "Route6" when _content.VermilionEnabled=>new("map.route-6"),
            "Route24"=>new("map.route-24"),"Route25"=>new("map.route-25"),
            "CeruleanCity"=>new("map.cerulean-city"),"CeruleanCity_Gym"=>new("map.cerulean-city-gym"),
            "MtMoon_1F"=>new("map.mt-moon-1f"),"MtMoon_B2F"=>new("map.mt-moon-b2f"),
            "Route3"=>new("map.route-3"),"PewterCity_Gym"=>new("map.pewter-gym"),
            "ViridianForest"=>new("map.viridian-forest"),
            _=>throw new InvalidDataException("ordinary_trainer_source_map")
        };
    }
    private string OrdinaryNoIntroActorScript(TrainerId trainer)=>IsSeviiRubyTrainer(trainer)?SeviiRubyNoIntroActorScript(trainer):IsPokemonLeagueTrainer(trainer)?PokemonLeagueTrainerActorScript(trainer):IsLeagueRivalTrainer(trainer)?"0x0":IsFirstSeviiTrainer(trainer)?"ThreeIsland_EventScript_Biker":IsCinnabarTrainer(trainer)?OrdinaryTrainerScript(trainer).ScriptEntry:IsSilphTrainer(trainer)?"0x0":IsRocketHideoutTrainer(trainer)?"RocketHideout_B4F_EventScript_Giovanni":IsMtMoonTrainer(trainer)?"MtMoon_B2F_EventScript_Miguel":
        IsPokemonTowerRival(trainer)||IsCeruleanRival(trainer)||IsSsAnneRival(trainer)?"0x0":
        IsCeruleanTrainer(trainer)?"CeruleanCity_EventScript_Grunt":
        trainer==Game.Domain.Trainers.BillTrainerPartyFactory.Rocket?"Route24_EventScript_Rocket":
        throw new InvalidDataException("ordinary_no_intro_actor");

    // BattleSetup_GetTerrainId (battle_setup.c:466), metatile_behavior.c and fixed
    // map headers. This rule terrain is independent from presentation background overrides.
    private WildBattleTerrain CampaignBattleTerrain(CampaignState state)
    {
        if(!_content.BillEnabled)return WildBattleTerrain.Plain;
        var p=state.World.Player;
        int b=_content.Maps[p.MapId].SourceBehaviorAt(p.X,p.Y)??throw new InvalidDataException("battle_terrain_cell");
        if(b is 0x02 or 0xd1)return WildBattleTerrain.Grass;
        // FireRed's IsLongGrass always returns false.
        if(b is 0x21 or 0x17)return WildBattleTerrain.Sand;
        bool surfable=b is >=0x10 and <=0x13 or 0x15 or 0x1a or 0x1b or >=0x50 and <=0x53;
        switch(_content.MapDefinition(p.MapId).MapType)
        {
            case "MAP_TYPE_UNDERGROUND":return b==0x0b?WildBattleTerrain.Building:surfable?WildBattleTerrain.Pond:WildBattleTerrain.Cave;
            case "MAP_TYPE_INDOOR":case "MAP_TYPE_SECRET_BASE":return WildBattleTerrain.Building;
            case "MAP_TYPE_UNDERWATER":return WildBattleTerrain.Underwater;
            case "MAP_TYPE_OCEAN_ROUTE":return surfable?WildBattleTerrain.Water:WildBattleTerrain.Plain;
        }
        if(b is 0x11 or 0x12 or 0x15)return WildBattleTerrain.Water;
        if(surfable)return WildBattleTerrain.Pond;
        return b==0x0c?WildBattleTerrain.Mountain:WildBattleTerrain.Plain;
    }
}
