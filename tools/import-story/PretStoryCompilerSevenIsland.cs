using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableHashSet<string>SevenIslandMapSources = new[] {"data/maps/SevenIsland/scripts.inc","data/maps/SevenIsland_Harbor/scripts.inc","data/maps/SevenIsland_House_Room1/scripts.inc","data/maps/SevenIsland_Mart/scripts.inc","data/maps/SevenIsland_PokemonCenter_1F/scripts.inc","data/maps/SevenIsland_SevaultCanyon/scripts.inc","data/maps/SevenIsland_SevaultCanyon_Entrance/scripts.inc","data/maps/SevenIsland_SevaultCanyon_House/scripts.inc","data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc","data/maps/SevenIsland_TanobyRuins/scripts.inc","data/maps/SevenIsland_TrainerTower/scripts.inc","data/maps/SevenIsland_TanobyRuins_MoneanChamber/scripts.inc","data/maps/SevenIsland_TanobyRuins_LiptooChamber/scripts.inc","data/maps/SevenIsland_TanobyRuins_WeepthChamber/scripts.inc","data/maps/SevenIsland_TanobyRuins_DilfordChamber/scripts.inc","data/maps/SevenIsland_TanobyRuins_ScufibChamber/scripts.inc","data/maps/SevenIsland_TanobyRuins_RixyChamber/scripts.inc","data/maps/SevenIsland_TanobyRuins_ViapoisChamber/scripts.inc"}.ToImmutableHashSet(StringComparer.Ordinal);
    private static bool IsSevenIslandMapSource(PretSourceScriptFile file) =>
        SevenIslandMapSources.Contains(file.Path) && FirstSeviiSource(file,file.Path);
    private static bool IsSevenIslandDanceAlias(PretSourceScriptFile file,string line) =>
        IsSevenIslandMapSource(file) && file.Path=="data/maps/SevenIsland_SevaultCanyon_House/scripts.inc" &&
        line==".set DID_CHANSEY_DANCE, FLAG_TEMP_2";
    private static bool IsSevenIslandAssemblyData(PretSourceScriptFile file,string label,string[] body)
    {
        if(!IsSevenIslandMapSource(file))return false;
        if(label==file.Path.Split('/')[2]+"_MapScripts"&&body.SequenceEqual(new[]{".byte 0"}))return true;
        return file.Path=="data/maps/SevenIsland_Mart/scripts.inc"&&label=="SevenIsland_Mart_Items"&&body.SequenceEqual(new[]{
            ".2byte ITEM_ULTRA_BALL",".2byte ITEM_GREAT_BALL",".2byte ITEM_FULL_RESTORE",".2byte ITEM_MAX_POTION",".2byte ITEM_HYPER_POTION",".2byte ITEM_REVIVE",".2byte ITEM_FULL_HEAL",".2byte ITEM_ESCAPE_ROPE",".2byte ITEM_MAX_REPEL",".2byte ITEM_NONE","release","end"});
    }
    private static (StoryOp,int,int)? SevenIslandInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if(IsSevenIslandMapSource(file)&&file.Path=="data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc"&&
            label=="SevenIsland_SevaultCanyon_TanobyKey_EventScript_SwitchPressed"&&opcode=="addvar")
        {
            if(!operands.SequenceEqual(new[]{"VAR_TEMP_8","1"}))throw new InvalidDataException("seven_island_switch_increment_source");
            return(StoryOp.Special,2,2);
        }
        if(FirstSeviiSource(file,"data/event_scripts.s")&&
            label=="EventScript_TryDarkenRuins"&&opcode is "setweather" or "doweather")
        {
            if(opcode=="setweather"&&!operands.SequenceEqual(new[]{"WEATHER_SHADE"})||opcode=="doweather"&&!operands.IsEmpty)
                throw new InvalidDataException("seven_island_weather_source");
            return(StoryOp.Special,opcode=="setweather"?1:0,opcode=="setweather"?1:0);
        }
        if(FirstSeviiSource(file,"data/scripts/trainers.inc")&&
            opcode is "trainerbattle_double" or "trainerbattle_rematch_double")
        {
            string? trainer=label switch
            {
                "SevenIsland_SevaultCanyon_Entrance_EventScript_Eve" or "SevenIsland_SevaultCanyon_Entrance_EventScript_Jon" or
                "SevenIsland_SevaultCanyon_Entrance_EventScript_EveRematch" or "SevenIsland_SevaultCanyon_Entrance_EventScript_JonRematch" => "TRAINER_YOUNG_COUPLE_EVE_JON",
                "SevenIsland_SevaultCanyon_EventScript_Lex" or "SevenIsland_SevaultCanyon_EventScript_Nya" or
                "SevenIsland_SevaultCanyon_EventScript_LexRematch" or "SevenIsland_SevaultCanyon_EventScript_NyaRematch" => "TRAINER_COOL_COUPLE_LEX_NYA", _=>null
            };
            if(trainer is not null)
            {
                if(operands.Length!=4||operands[0]!=trainer)throw new InvalidDataException("seven_island_double_source");
                return(StoryOp.TrainerBattle,4,4);
            }
        }
        if(file.Path.StartsWith("data/maps/SevenIsland",StringComparison.Ordinal)&&!SevenIslandMapSources.Contains(file.Path))
            return(StoryOp.Unsupported,0,int.MaxValue);
        return null;
    }
}
