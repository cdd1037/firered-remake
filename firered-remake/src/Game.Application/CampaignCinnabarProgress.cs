using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Original Blaine callback, badge and deferred single TM38 reward; no new save schema.</summary>
public sealed partial class CampaignSession
{
    private static readonly ItemId BlaineMachine=new("pret:ITEM_TM38");
    private bool TryApplyCinnabarEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.CinnabarEnabled)return false;
        if(effect.Opcode=="goto_if_not_defeated")
        {
            if(effect.Source.Path!="data/maps/CinnabarIsland_Gym/scripts.inc"||state.World.Player.MapId.Value!="map.cinnabar-island-gym"||effect.Args.Length!=2)
                throw new InvalidDataException("cinnabar_quiz_branch_context");
            string[] names=["Quinn","Avery","Ramon","Derek","Dusty","Zac"];
            string[] trainers=["TRAINER_BURGLAR_QUINN","TRAINER_SUPER_NERD_AVERY","TRAINER_BURGLAR_RAMON","TRAINER_SUPER_NERD_DEREK","TRAINER_BURGLAR_DUSTY","TRAINER_SUPER_NERD_ZAC"];
            int index=Array.IndexOf(trainers,effect.Args[0]);
            if(index<0||effect.Source.Symbol!=$"CinnabarIsland_Gym_EventScript_Quiz{index+1}Incorrect"||effect.Args[1]!="CinnabarIsland_Gym_EventScript_Battle"+names[index])
                throw new InvalidDataException("cinnabar_quiz_branch_source");
            if(!state.BattledTrainers.Contains("pret:"+effect.Args[0]))next=state with{Script=state.Script with{Stack=state.Script.Stack.SetItem(state.Script.Stack.Length-1,new(effect.Args[1],0))}};
            return true;
        }
        if(effect.Opcode=="getplayerxy"&&effect.Source.Path=="data/maps/CinnabarIsland/scripts.inc"&&
            effect.Source.Symbol=="CinnabarIsland_EventScript_ReadyObjectsSailToOneIsland")
        {
            if(state.World.Player.MapId.Value!="map.cinnabar-island"||!effect.Args.SequenceEqual(new[]{"VAR_0x8004","VAR_0x8005"}))
                throw new InvalidDataException("cinnabar_playerxy_context");
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem(effect.Args[0],state.World.Player.X)
                .SetItem(effect.Args[1],state.World.Player.Y)}};return true;
        }
        if(effect.Opcode=="settrainerflag"&&effect.Source.Path=="data/scripts/set_gym_trainers.inc"&&effect.Source.Symbol=="EventScript_SetCinnabarGymTrainers")
        {
            if(effect.Args.Length!=1||effect.Args[0] is not("TRAINER_SUPER_NERD_ERIK" or "TRAINER_SUPER_NERD_AVERY" or "TRAINER_SUPER_NERD_DEREK" or "TRAINER_SUPER_NERD_ZAC" or "TRAINER_BURGLAR_QUINN" or "TRAINER_BURGLAR_RAMON" or "TRAINER_BURGLAR_DUSTY")||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"))
                throw new InvalidDataException("cinnabar_trainer_flag_context");
            next=state with{BattledTrainers=state.BattledTrainers.Add("pret:"+effect.Args[0])};return true;
        }
        if(effect.Opcode is not("checkitemspace" or "additem" or "bufferitemname" or "bufferitemnameplural" or "checkitemtype"))return false;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out int n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("cinnabar_item_value_missing");
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        if(Value(effect.Args[buffer?1:0])!=Value("ITEM_TM38"))return false;
        if(effect.Source.Path is not("data/maps/CinnabarIsland_Gym/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s")||state.World.Player.MapId.Value!="map.cinnabar-island-gym")
            throw new InvalidDataException("cinnabar_tm_source_context");
        if(buffer){next=state with{TextBuffers=state.TextBuffers.SetItem(effect.Args[0],"TM38")};return true;}
        int result;
        if(effect.Opcode=="checkitemtype")result=Value("POCKET_TM_CASE");
        else
        {
            var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
            var machines=_content.PewterMachines??throw new InvalidDataException("machines_missing");
            int quantity=effect.Args.Length>1?Value(effect.Args[1]):1;
            if(quantity!=1||!state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET")||!(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==CinnabarTrainerPartyFactory.Blaine.Value&&h.Outcome==WildBattleOutcome.Won))throw new InvalidDataException("cinnabar_tm_reward_context");
            if(effect.Opcode=="checkitemspace")result=MachineRules.CanAddMachine(machines,inventory,BlaineMachine)?1:0;
            else
            {
                if(state.Script.Flags.GetValueOrDefault("FLAG_GOT_TM38_FROM_BLAINE"))throw new InvalidDataException("cinnabar_tm_already_received");
                var added=MachineRules.AddMachine(machines,inventory,BlaineMachine);next=state with{Inventory=added.State};result=added.Succeeded?1:0;
            }
        }
        next=next with{Script=next.Script with{Variables=next.Script.Variables.SetItem("VAR_RESULT",result)}};return true;
    }
    private string? ValidateCinnabarProgress(CampaignState state)
    {
        bool Flag(string name)=>state.Script.Flags.GetValueOrDefault(name);
        bool badge=Flag("FLAG_BADGE07_GET"),got=Flag("FLAG_GOT_TM38_FROM_BLAINE");
        int quantity=OpeningInventoryRules.Quantity(state.Inventory?.Machines??[],BlaineMachine);
        bool key=Flag("FLAG_HIDE_POKEMON_MANSION_B1F_SECRET_KEY");
        if(!_content.CinnabarEnabled)return badge||got||Flag("FLAG_DEFEATED_BLAINE")||key||quantity!=0||
            Flag("FLAG_POKEMON_MANSION_SWITCH_STATE")||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CINNABAR_ISLAND")!=0?
            "save_cinnabar_profile_missing":null;
        bool won=(state.ForestTrainerHistory??[]).Any(h=>h.TrainerId==CinnabarTrainerPartyFactory.Blaine.Value&&h.Outcome==WildBattleOutcome.Won);
        if(badge!=won||Flag("FLAG_DEFEATED_BLAINE")!=won)return "save_blaine_badge_history_mismatch";
        if((won||state.World.Player.MapId.Value=="map.cinnabar-island-gym")&&!key)return "save_cinnabar_gym_without_key";
        if(OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],new("pret:ITEM_SECRET_KEY"))!=(key?1:0))return "save_cinnabar_secret_key_receipt";
        if(won&&CinnabarGymTrainerSymbols.Any(id=>!state.BattledTrainers.Contains("pret:"+id)))return "save_blaine_trainer_flags_missing";
        if(got&&!won||!got&&quantity!=0||quantity>1)return "save_blaine_tm_reward_mismatch";
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CINNABAR_ISLAND");
        if(scene<0||scene>(_content.FirstSeviiEnabled?4:2)||!won&&scene!=0||won&&scene==0)return "save_cinnabar_bill_scene";
        return null;
    }
    private static readonly string[] CinnabarGymTrainerSymbols=["TRAINER_SUPER_NERD_ERIK","TRAINER_SUPER_NERD_AVERY",
        "TRAINER_SUPER_NERD_DEREK","TRAINER_SUPER_NERD_ZAC","TRAINER_BURGLAR_QUINN","TRAINER_BURGLAR_RAMON","TRAINER_BURGLAR_DUSTY"];
    private bool ValidCinnabarHiddenFerryTemplate(CampaignState state,CampaignActor actor)
    {
        // Original setobjectxyperm parks the still-hidden boat beyond the 24-wide
        // map before Bill's invitation. Declining keeps that dormant template.
        if(!_content.CinnabarEnabled||state.World.Player.MapId.Value!="map.cinnabar-island"||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_CINNABAR_ISLAND")!=2||
            actor.LocalId!="LOCALID_CINNABAR_SEAGALLOP"||actor.Active||
            actor.HideFlag!="FLAG_HIDE_CINNABAR_SEAGALLOP"||!state.Script.Flags.GetValueOrDefault(actor.HideFlag))return false;
        bool lower=state.Script.Flags.GetValueOrDefault("FLAG_TEMP_2");
        var source=_content.FindProgram(lower?"CinnabarIsland_EventScript_ReadyObjectsSailToOneIsland":
            "CinnabarIsland_EventScript_MoveSeagallopDown");
        return actor.TemplateX==30&&actor.TemplateY==(lower?12:8)&&source is not null&&
            source.Instructions.Any(i=>i.Opcode=="setobjectxyperm"&&i.Source.Path=="data/maps/CinnabarIsland/scripts.inc"&&
                i.Args.SequenceEqual(new[]{actor.LocalId,"30",lower?"12":"8"}));
    }
    private bool CinnabarFerryChoice(CampaignState state)=>_content.CinnabarEnabled&&state.Dialogue?.TextSymbol is
        "CinnabarIsland_Text_ComeWithMeToOneIsland" or "CinnabarIsland_PokemonCenter_1F_Text_ReadyToSailToOneIsland";
}
