using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string VermilionGymMap="map.vermilion-city-gym";
    private const string VermilionGymSource="data/maps/VermilionCity_Gym/scripts.inc";
    private const string GymFirstSwitch="FLAG_TEMP_1";
    private const string GymBothSwitches="FLAG_FOUND_BOTH_VERMILION_GYM_SWITCHES";
    private static readonly ItemId SurgeMachine=new("pret:ITEM_TM34");
    // Original ordered rows of sVermilionGymTrashCanSwitches. The second draw indexes
    // this order, rather than a sorted/geometrically regenerated set of neighbors.
    private static readonly int[][] GymSwitchNeighbors=[[],[2,6],[3,7,1],[4,8,2],[5,9,3],[10,4],
        [1,7,11],[2,8,12,6],[3,9,13,7],[4,10,14,8],[5,15,9],[6,12],[7,13,11],[8,14,12],[9,15,13],[10,14]];
    private int GymValue(CampaignState state,string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)
        ?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)?n
        :_content.TryConstant(token,out n)?n:throw new InvalidDataException("vermilion_gym_operand_missing: "+token);
    private ImmutableSortedDictionary<string,FieldMetatileEdit> GymBeamEdits(CampaignState state,string phase)
    {
        var program=_content.FindProgram("VermilionCity_Gym_EventScript_"+phase)??throw new InvalidDataException("gym_beam_program_missing");
        var edits=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        foreach(var i in program.Instructions.Where(i=>i.Opcode=="setmetatile"))
        {
            int x=GymValue(state,i.Args[0]),y=GymValue(state,i.Args[1]),id=GymValue(state,i.Args[2]),collision=GymValue(state,i.Args[3]);
            if(x is <3 or >7||y is <6 or >7||collision is <0 or >1)throw new InvalidDataException("gym_beam_source_bounds");
            var cell=_content.Maps[new(VermilionGymMap)].GetTile(x,y)??throw new InvalidDataException("gym_beam_cell_missing");
            _=_content.MetatileDefinition(new(VermilionGymMap),id);
            edits=edits.Add(x+":"+y,new(x,y,id,collision==0?0:3,cell.Elevation));
        }
        if(edits.Count!=10)throw new InvalidDataException("gym_beam_source_count");
        return edits;
    }
    private bool TryApplyVermilionGymEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.VermilionGymEnabled)return false;
        var a=effect.Args;
        bool inside=state.World.Player.MapId.Value==VermilionGymMap;
        void GymSource(){if(!inside||effect.Source.Path!=VermilionGymSource)throw new InvalidDataException("vermilion_gym_effect_context");}
        if(effect.Opcode=="special"&&a.Length==1&&a[0] is "SetVermilionTrashCans" or "DrawWholeMapView")
        {
            GymSource();
            if(a[0]=="DrawWholeMapView")return true;
            if(effect.Source.Symbol is not("VermilionCity_Gym_EventScript_InitTrashCans" or "VermilionCity_Gym_EventScript_TrySwitchTwo")||state.Script.Flags.GetValueOrDefault(GymBothSwitches))
                throw new InvalidDataException("vermilion_gym_switch_reset_context");
            var first=Gen3Rng.Next(state.World.Rng,"vermilion-gym.first-switch",15);
            int id=first.Value%15+1;
            var second=Gen3Rng.Next(first.State,"vermilion-gym.adjacent-switch",GymSwitchNeighbors[id].Length);
            next=state with{World=state.World with{Rng=second.State},Script=state.Script with{Variables=state.Script.Variables
                .SetItem("VAR_0x8004",id).SetItem("VAR_0x8005",GymSwitchNeighbors[id][second.Value%GymSwitchNeighbors[id].Length])}};
            return true;
        }
        if(effect.Opcode=="special"&&a.Length==1&&a[0]=="QuestLog_CutRecording"&&effect.Source.Path==VermilionGymSource)
        {GymSource();if(state.Script.QuestLogState!=0)throw new InvalidDataException("gym_questlog_unsupported");return true;}
        if(effect.Opcode=="setmetatile"&&effect.Source.Path==VermilionGymSource)
        {
            GymSource();string phase=effect.Source.Symbol.Replace("VermilionCity_Gym_EventScript_","",StringComparison.Ordinal);
            if(phase is not("SetBeamsOn" or "SetOneBeamOff" or "SetBeamsOff")||a.Length!=4)throw new InvalidDataException("gym_beam_effect_context");
            var expected=GymBeamEdits(state,phase);string key=GymValue(state,a[0])+":"+GymValue(state,a[1]);
            if(!expected.TryGetValue(key,out var edit)||edit.SourceMetatileId!=GymValue(state,a[2])||edit.Collision!=(GymValue(state,a[3])==0?0:3))
                throw new InvalidDataException("gym_beam_effect_mismatch");
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(key,edit)};return true;
        }
        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetVermilionGymTrainers")
        {
            if(a.Length!=1||!VermilionGymTrainerPartyFactory.SupportedTrainers.Any(id=>id.Value=="pret:"+a[0])||a[0]=="TRAINER_LEADER_LT_SURGE"||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"))
                throw new InvalidDataException("vermilion_gym_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+a[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(a.Length<(buffer?2:1)||GymValue(state,a[buffer?1:0])!=GymValue(state,"ITEM_TM34"))return false;
        if(!inside||effect.Source.Path is not(VermilionGymSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))throw new InvalidDataException("surge_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],"TM34")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=GymValue(state,"POCKET_TM_CASE");
        else
        {
            if((a.Length>1?GymValue(state,a[1]):1)!=1||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"))throw new InvalidDataException("surge_tm_reward_context");
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            if(effect.Opcode=="checkitemspace")result=MachineRules.CanAddMachine(_content.PewterMachines!,inventory,SurgeMachine)?1:0;
            else
            {
                if(state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM34_FROM_SURGE"))throw new InvalidDataException("surge_tm_already_received");
                var added=MachineRules.AddMachine(_content.PewterMachines!,inventory,SurgeMachine);next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }
    private string? ValidateVermilionGymProgress(CampaignState state)
    {
        if(!_content.VermilionGymEnabled)return state.Script.Flags.GetValueOrDefault(GymBothSwitches)?"save_vermilion_gym_profile_missing":null;
        bool both=state.Script.Flags.GetValueOrDefault(GymBothSwitches),badge=state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET");
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==VermilionGymTrainerPartyFactory.LtSurge.Value&&h.Outcome==WildBattleOutcome.Won);
        if(badge!=won||state.Script.Flags.GetValueOrDefault("FLAG_DEFEATED_LT_SURGE")!=won||won&&!both)return "save_surge_badge_history_mismatch";
        if(won&&VermilionGymTrainerPartyFactory.SupportedTrainers.Any(id=>!state.BattledTrainers.Contains(id.Value)))return "save_surge_trainer_flags_missing";
        bool got=state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM34_FROM_SURGE");int count=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],SurgeMachine);
        if(got&&!won||!got&&count!=0||count>1)return "save_surge_tm_reward_mismatch";
        if(state.World.Player.MapId.Value!=VermilionGymMap)return null;
        int first=state.Script.Variables.GetValueOrDefault("VAR_TEMP_0"),second=state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");
        if(!both&&(first is <1 or >15||!GymSwitchNeighbors[first].Contains(second)))return "save_gym_switch_pair_invalid";
        if(!both&&state.World.Player.Y<=5)return "save_gym_closed_beam_position";
        string phase=both?"SetBeamsOff":state.Script.Flags.GetValueOrDefault(GymFirstSwitch)?"SetOneBeamOff":"SetBeamsOn";
        if(phase=="SetBeamsOn"&&state.MapEdits is not{Count:>0})return null;
        return state.MapEdits is {} edits&&edits.SequenceEqual(GymBeamEdits(state,phase))?null:"save_gym_beam_edits_invalid";
    }
}
