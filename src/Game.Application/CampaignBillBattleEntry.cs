using Game.Domain.Ids;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private MapId OrdinaryTrainerMap(TrainerId trainer) => ResolveTrainerOccurrence(trainer).Map;

    private string OrdinaryNoIntroActorScript(TrainerId trainer)=>IsLostCaveTrainer(trainer)?"0x0":IsFourIslandTrainer(trainer)?"0x0":IsSeviiRubyTrainer(trainer)?SeviiRubyNoIntroActorScript(trainer):IsPokemonLeagueTrainer(trainer)?PokemonLeagueTrainerActorScript(trainer):IsLeagueRivalTrainer(trainer)?"0x0":IsFirstSeviiTrainer(trainer)?"ThreeIsland_EventScript_Biker":IsCinnabarTrainer(trainer)?OrdinaryTrainerScript(trainer).ScriptEntry:IsSilphTrainer(trainer)?"0x0":IsRocketHideoutTrainer(trainer)?"RocketHideout_B4F_EventScript_Giovanni":IsMtMoonTrainer(trainer)?"MtMoon_B2F_EventScript_Miguel":
        IsPokemonTowerRival(trainer)||IsCeruleanRival(trainer)||IsSsAnneRival(trainer)?"0x0":
        IsCeruleanTrainer(trainer)?"CeruleanCity_EventScript_Grunt":
        trainer==Game.Domain.Trainers.BillTrainerPartyFactory.Rocket?"Route24_EventScript_Rocket":
        throw new InvalidDataException("ordinary_no_intro_actor");

    // BattleSetup_GetTerrainId (battle_setup.c:466), metatile_behavior.c and fixed
    // map headers. This rule terrain is independent from presentation background overrides.
    private WildBattleTerrain CampaignBattleTerrain(CampaignState state)
    {
        var p=state.World.Player;
        int b=SourceBehavior(state,p.X,p.Y)??throw new InvalidDataException("battle_terrain_cell");
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
