using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Original Koga callback, badge and deferred single TM06 reward; no new save schema.</summary>
public sealed partial class CampaignSession
{
    private static readonly ItemId KogaMachine=new("pret:ITEM_TM06");
    private bool TryApplyFuchsiaGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.FuchsiaGymEnabled)return false;
        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetFuchsiaGymTrainers")
        {
            if(effect.Args.Length!=1||effect.Args[0] is not("TRAINER_TAMER_PHIL" or "TRAINER_TAMER_EDGAR" or "TRAINER_JUGGLER_KIRK" or "TRAINER_JUGGLER_SHAWN" or "TRAINER_JUGGLER_KAYDEN" or "TRAINER_JUGGLER_NATE")||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"))
                throw new InvalidDataException("fuchsia_gym_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+effect.Args[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("fuchsia_gym_item_value_missing");
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=Value("ITEM_TM06"))return false;
        if(effect.Source.Path is not("data/maps/FuchsiaCity_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s")||state.World.Player.MapId.Value!="map.fuchsia-city-gym")
            throw new InvalidDataException("fuchsia_gym_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM06")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var machines=_content.PewterMachines??throw new InvalidDataException("machines_missing");
            int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(quantity!=1||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"))throw new InvalidDataException("fuchsia_gym_tm_reward_context");
            if(effect.Opcode=="checkitemspace")result=PewterMachineRules.CanAddMachine(machines,inventory,KogaMachine)?1:0;
            else
            {
                if(state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM06_FROM_KOGA"))throw new InvalidDataException("fuchsia_gym_tm_already_received");
                var added=PewterMachineRules.AddMachine(machines,inventory,KogaMachine);next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }
    private string? ValidateFuchsiaGymProgress(CampaignState state)
    {
        if(!_content.FuchsiaGymEnabled)return null;
        bool badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET");
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==FuchsiaGymTrainerPartyFactory.Koga.Value&&h.Outcome==WildBattleOutcome.Won);
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_KOGA")!=won)return "save_koga_badge_history_mismatch";
        if(won&&state.Script.Flags.GetValueOrDefault("FLAG_HIDE_FAME_CHECKER_KOGA_JOURNAL"))return "save_koga_journal_still_hidden";
        if(won&&FuchsiaGymTrainerPartyFactory.SupportedTrainers.Any(id=>!state.BattledTrainers.Contains(id.Value)))return "save_koga_trainer_flags_missing";
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM06_FROM_KOGA");
        int quantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],KogaMachine);
        if(got&&!won||!got&&quantity!=0||quantity>1)return "save_koga_tm_reward_mismatch";
        return null;
    }
}
