using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private static readonly ItemId MistyMachine=new("pret:ITEM_TM03");
    private bool TryApplyCeruleanEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetCeruleanGymTrainers")
        {
            if(effect.Args.Length!=1||effect.Args[0] is not("TRAINER_PICNICKER_DIANA" or "TRAINER_SWIMMER_MALE_LUIS")||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET"))
                throw new InvalidDataException("cerulean_gym_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+effect.Args[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("cerulean_item_value_missing");
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=Value("ITEM_TM03"))return false;
        if(effect.Source.Path is not("data/maps/CeruleanCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s")||state.World.Player.MapId.Value!="map.cerulean-city-gym")
            throw new InvalidDataException("cerulean_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM03")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var machines=_content.Machines??throw new InvalidDataException("machines_missing");
            int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(quantity!=1||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET"))throw new InvalidDataException("cerulean_tm_reward_context");
            if(effect.Opcode=="checkitemspace")result=MachineRules.CanAddMachine(machines,inventory,MistyMachine)?1:0;
            else
            {
                if(state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM03_FROM_MISTY"))throw new InvalidDataException("cerulean_tm_already_received");
                var added=MachineRules.AddMachine(machines,inventory,MistyMachine);next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }
    private string? ValidateCeruleanProgress(CampaignState state)
    {
        bool badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET");
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId=="pret:TRAINER_LEADER_MISTY"&&h.Outcome==WildBattleOutcome.Won);
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_MISTY")!=won)return "save_misty_badge_history_mismatch";
        if(won&&(!state.BattledTrainers.Contains("pret:TRAINER_PICNICKER_DIANA")||!state.BattledTrainers.Contains("pret:TRAINER_SWIMMER_MALE_LUIS")))return "save_misty_trainer_flags_missing";
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM03_FROM_MISTY");
        int quantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],MistyMachine);
        if(got&&!won||!got&&quantity!=0||quantity>1)return "save_misty_tm_reward_mismatch";
        return null;
    }
}
