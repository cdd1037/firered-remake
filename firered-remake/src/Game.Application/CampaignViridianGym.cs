using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Source door latch, Giovanni callback and deferred TM26 receipt. No synthetic trainer histories.</summary>
public sealed partial class CampaignSession
{
    private const string ViridianGymMap="map.viridian-city-gym";
    private const string ViridianGymDoor="VAR_MAP_SCENE_VIRIDIAN_CITY_GYM_DOOR";
    private const string GiovanniRewardFlag="FLAG_GOT_TM26_FROM_GIOVANNI";
    private static readonly ItemId GiovanniMachine=new("pret:ITEM_TM26");

    private static void ValidateViridianGymCatalog(ICampaignCatalog content)
    {
        ValidateVictoryRoadCatalog(content);
        ValidateLeagueRivalCatalog(content);
        if(!content.ViridianGymEnabled)return;
        if(!content.FirstSeviiEnabled||!content.CinnabarEnabled||content.ViridianGymTrainerParties is null||
            content.CinnabarOwnership is null||content.CinnabarOpponents is null||
            !ReferenceEquals(content.CinnabarOpponents.Owned,content.CinnabarOwnership)||
            (((CelioFiveMachines(content)?.Predecessor ?? SixIslandMachineCatalog.For(content.PewterMachines))?.Predecessor ?? FourIslandMachineCatalog.For(content.PewterMachines))?.Predecessor.Predecessor.Predecessor??SeviiRubyMachineCatalog.For(content.PewterMachines)?.Predecessor.Predecessor??VictoryRoadMachineCatalog.For(content.PewterMachines)?.Predecessor??ViridianGymMachineCatalog.For(content.PewterMachines)) is not {} proof||
            !ReferenceEquals(proof.Pokemon,content.CinnabarOwnership)||!content.Maps.ContainsKey(new(ViridianGymMap))||content.Maps.ContainsKey(new("map.viridian-gym"))||
            content.MapFromSourceSymbol("MAP_VIRIDIAN_CITY_GYM").Value!=ViridianGymMap||
            content.MapDefinition(new(ViridianGymMap)).Actors.Length!=10||!content.SupportsRegionalFlavor(new(ViridianGymMap)))
            throw new InvalidDataException("viridian_gym_source_profile_missing");
        ViridianGymTrainerPartyFactory.ValidateCatalog(content.ViridianGymTrainerParties);
        var warp=content.Maps[new("map.viridian-city")].FieldWarps.Single(w=>w.Index==2);
        if(warp.DestinationMapId.Value!=ViridianGymMap||warp.DestinationWarpId!=1)
            throw new InvalidDataException("viridian_gym_runtime_warp_missing");
    }

    private static bool ViridianGymVictory(CampaignState state)=>(state.ForestTrainerHistory??[])
        .Any(h=>h is not null&&h.TrainerId==ViridianGymTrainerPartyFactory.Giovanni.Value&&h.Outcome==WildBattleOutcome.Won);
    private static bool ViridianGymDoorBadges(CampaignState state)=>Enumerable.Range(2,6)
        .All(i=>state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET"));

    private bool TryApplyViridianGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.ViridianGymEnabled)return false;
        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetViridianGymTrainers")
        {
            if(effect.Args.Length!=1||!ViridianGymTrainerPartyFactory.GymTrainers.Any(t=>t.Value=="pret:"+effect.Args[0])||
                state.World.Player.MapId.Value!=ViridianGymMap||!ViridianGymVictory(state)||
                !state.Script.Flags.GetValueOrDefault("FLAG_BADGE08_GET")||!state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_LEADER_GIOVANNI"))
                throw new InvalidDataException("viridian_gym_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+effect.Args[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):
            int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("viridian_gym_item_value_missing");
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=Value("ITEM_TM26"))return false;
        if(effect.Source.Path is not("data/maps/ViridianCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s")||
            state.World.Player.MapId.Value!=ViridianGymMap||state.LastTalked!="LOCALID_VIRIDIAN_GIOVANNI"||
            !state.Actors.TryGetValue(state.LastTalked,out var actor)||!actor.Active||
            !ViridianGymVictory(state)||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE08_GET"))
            throw new InvalidDataException("viridian_gym_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM26")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var machines=_content.PewterMachines??throw new InvalidDataException("machines_missing");
            int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(quantity!=1||state.Script.Flags.GetValueOrDefault(GiovanniRewardFlag)||
                effect.Opcode=="additem"&&OpeningInventoryRules.Quantity(inventory.Machines??[],GiovanniMachine)!=0)
                throw new InvalidDataException("viridian_gym_tm_reward_context");
            if(effect.Opcode=="checkitemspace")result=PewterMachineRules.CanAddMachine(machines,inventory,GiovanniMachine)?1:0;
            else
            {
                var added=PewterMachineRules.AddMachine(machines,inventory,GiovanniMachine);
                next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }

    private string? ValidateViridianGymProgress(CampaignState state)
    {
        bool Flag(string name)=>state.Script.Flags.GetValueOrDefault(name);
        bool badge=Flag("FLAG_BADGE08_GET"),defeated=Flag("FLAG_DEFEATED_LEADER_GIOVANNI"),got=Flag(GiovanniRewardFlag),
            hidden=Flag("FLAG_HIDE_VIRIDIAN_GIOVANNI"),rockets=Flag("FLAG_HIDE_MISC_KANTO_ROCKETS");
        int quantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],GiovanniMachine);
        if(!_content.ViridianGymEnabled)return badge||defeated||got||hidden||rockets||quantity!=0?"save_viridian_gym_profile_missing":null;
        if(Flag("FLAG_HIDDEN_ITEM_VIRIDIAN_CITY_GYM_MACHO_BRACE"))return "save_viridian_gym_itemfinder_not_yet_available";
        bool won=ViridianGymVictory(state);
        if(badge!=won||defeated!=won||rockets!=won)return "save_giovanni_badge_history_mismatch";
        if(won&&ViridianGymTrainerPartyFactory.SupportedTrainers.Any(t=>!state.BattledTrainers.Contains(t.Value)))
            return "save_giovanni_trainer_flags_missing";
        if(got&&!won||!got&&quantity!=0||quantity>1||hidden&&(!got||!won))return "save_giovanni_tm_reward_mismatch";
        int door=state.Script.Variables.GetValueOrDefault(ViridianGymDoor);
        if(door is <0 or >1)return "save_viridian_gym_door_scene";
        // An inherited unlocked latch is preserved on all maps. Entering the new
        // interior requires the original six badge conditions; First Sevii is not a gate.
        if(state.World.Player.MapId.Value==ViridianGymMap)
        {
            if(door!=1||!ViridianGymDoorBadges(state))return "save_viridian_gym_door_locked";
            if(!state.Actors.TryGetValue("LOCALID_VIRIDIAN_GIOVANNI",out var actor))return "save_viridian_giovanni_actor_missing";
            var definition=_content.MapDefinition(state.World.Player.MapId).Actors.Single(a=>a.LocalId==actor.LocalId);
            var player=state.World.Player;
            bool inView=actor.X+7>=player.X-2&&actor.X+7<=player.X+17&&actor.Y+7>=player.Y&&actor.Y+7<=player.Y+16;
            if(actor.Active!=(!hidden&&inView)||actor.Invisible||actor.X!=definition.X||actor.Y!=definition.Y||
                actor.TemplateX!=definition.X||actor.TemplateY!=definition.Y)
                return "save_viridian_giovanni_actor_hidden";
        }
        return null;
    }
}
