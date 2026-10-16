using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Original Erika callback, badge and deferred single TM19 reward; no new save schema.</summary>
public sealed partial class CampaignSession
{
    private static readonly ItemId ErikaMachine=new("pret:ITEM_TM19");
    private bool TryApplyCeladonGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetCeladonGymTrainers")
        {
            if(effect.Args.Length!=1||effect.Args[0] is not("TRAINER_LASS_KAY" or "TRAINER_BEAUTY_BRIDGET" or "TRAINER_PICNICKER_TINA" or "TRAINER_BEAUTY_TAMIA" or "TRAINER_BEAUTY_LORI" or "TRAINER_LASS_LISA" or "TRAINER_COOLTRAINER_MARY")||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE04_GET"))
                throw new InvalidDataException("celadon_gym_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+effect.Args[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("celadon_gym_item_value_missing");
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=Value("ITEM_TM19"))return false;
        if(effect.Source.Path is not("data/maps/CeladonCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s")||state.World.Player.MapId.Value!="map.celadon-city-gym")
            throw new InvalidDataException("celadon_gym_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM19")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var machines=_content.Machines??throw new InvalidDataException("machines_missing");
            int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(quantity!=1||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE04_GET"))throw new InvalidDataException("celadon_gym_tm_reward_context");
            if(effect.Opcode=="checkitemspace")result=MachineRules.CanAddMachine(machines,inventory,ErikaMachine)?1:0;
            else
            {
                if(state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM19_FROM_ERIKA"))throw new InvalidDataException("celadon_gym_tm_already_received");
                var added=MachineRules.AddMachine(machines,inventory,ErikaMachine);next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }
    private string? ValidateCeladonGymProgress(CampaignState state)
    {
        bool badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE04_GET");
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==CeladonGymTrainerPartyFactory.Erika.Value&&h.Outcome==WildBattleOutcome.Won);
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_ERIKA")!=won)return "save_erika_badge_history_mismatch";
        if(won&&CeladonGymTrainerPartyFactory.SupportedTrainers.Any(id=>!state.BattledTrainers.Contains(id.Value)))return "save_erika_trainer_flags_missing";
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM19_FROM_ERIKA");
        int quantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],ErikaMachine);
        if(got&&!won||!got&&quantity!=0||quantity>1)return "save_erika_tm_reward_mismatch";
        return null;
    }
}
