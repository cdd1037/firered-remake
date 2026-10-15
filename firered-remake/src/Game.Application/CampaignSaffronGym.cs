using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Original Sabrina callback, badge and deferred single TM04 reward; no new save schema.</summary>
public sealed partial class CampaignSession
{
    private static readonly ItemId SabrinaMachine=new("pret:ITEM_TM04");
    private bool TryApplySaffronGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.SaffronGymEnabled)return false;
        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetSaffronGymTrainers")
        {
            if(effect.Args.Length!=1||effect.Args[0] is not("TRAINER_PSYCHIC_JOHAN" or "TRAINER_PSYCHIC_TYRON" or "TRAINER_PSYCHIC_CAMERON" or "TRAINER_PSYCHIC_PRESTON" or "TRAINER_CHANNELER_AMANDA" or "TRAINER_CHANNELER_STACY" or "TRAINER_CHANNELER_TASHA")||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE06_GET"))
                throw new InvalidDataException("saffron_gym_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+effect.Args[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("saffron_gym_item_value_missing");
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=Value("ITEM_TM04"))return false;
        if(effect.Source.Path is not("data/maps/SaffronCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s")||state.World.Player.MapId.Value!="map.saffron-city-gym")
            throw new InvalidDataException("saffron_gym_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM04")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var machines=_content.PewterMachines??throw new InvalidDataException("machines_missing");
            int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(quantity!=1||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE06_GET")||!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFFRON_ROCKETS")||!(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==SaffronGymTrainerPartyFactory.Sabrina.Value&&h.Outcome==WildBattleOutcome.Won))throw new InvalidDataException("saffron_gym_tm_reward_context");
            if(effect.Opcode=="checkitemspace")result=MachineRules.CanAddMachine(machines,inventory,SabrinaMachine)?1:0;
            else
            {
                if(state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM04_FROM_SABRINA"))throw new InvalidDataException("saffron_gym_tm_already_received");
                var added=MachineRules.AddMachine(machines,inventory,SabrinaMachine);next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }
    private string? ValidateSaffronGymProgress(CampaignState state)
    {
        if(!_content.SaffronGymEnabled)return state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_SABRINA")||state.Script.Flags.GetValueOrDefault("FLAG_BADGE06_GET")||state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM04_FROM_SABRINA")||OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],SabrinaMachine)!=0?"save_saffron_gym_profile_missing":null;
        if(state.World.Player.MapId.Value=="map.saffron-city-gym"&&!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFFRON_ROCKETS"))return "save_saffron_gym_rocket_blockade";
        bool badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE06_GET");
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==SaffronGymTrainerPartyFactory.Sabrina.Value&&h.Outcome==WildBattleOutcome.Won);
        if(won&&!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFFRON_ROCKETS"))return "save_saffron_gym_rocket_blockade";
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_SABRINA")!=won)return "save_sabrina_badge_history_mismatch";
        if(state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFFRON_CITY_POKECENTER_SABRINA_JOURNALS")==won)return won?"save_sabrina_journal_still_hidden":"save_sabrina_journal_prematurely_visible";
        if(won&&SaffronGymTrainerPartyFactory.SupportedTrainers.Any(id=>!state.BattledTrainers.Contains(id.Value)))return "save_sabrina_trainer_flags_missing";
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM04_FROM_SABRINA");
        int quantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],SabrinaMachine);
        if(got&&!won||!got&&quantity!=0||quantity>1)return "save_sabrina_tm_reward_mismatch";
        return null;
    }
}
