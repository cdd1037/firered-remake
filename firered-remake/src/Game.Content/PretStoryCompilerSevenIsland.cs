using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static readonly ImmutableDictionary<string,string> SevenIslandMapSourceHashes = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["data/maps/SevenIsland/scripts.inc"] = "93577c3de7ef6fa248dc3be8b3c048679cee33cbae98094c70b685a63f9c4305",
        ["data/maps/SevenIsland_Harbor/scripts.inc"] = "97f6a759b5ff346fbea7b2c42a0002e8522c63c34d3e9eca0cfeffb9863a7a08",
        ["data/maps/SevenIsland_House_Room1/scripts.inc"] = "6a41336f1ccfb6784a3be281c5c8fc9b40226bba9e39c22b6a27a6a050d4bbcd",
        ["data/maps/SevenIsland_Mart/scripts.inc"] = "4ff054a406673e6b9c50fcd330c76994dd14b62d88113fbcc6c515ed85e28cd9",
        ["data/maps/SevenIsland_PokemonCenter_1F/scripts.inc"] = "5b6a75f2c85b7da72305ffbf321a02e7f94d5eaf937a3aebe74ef864466dcd36",
        ["data/maps/SevenIsland_SevaultCanyon/scripts.inc"] = "47118f14f00a3892c48de834c46f34f60bb4948ce9fa73ff050512d0c8718ce4",
        ["data/maps/SevenIsland_SevaultCanyon_Entrance/scripts.inc"] = "6e737b5448a03909283dacbfa3c37fd136c6ddadcdbd665518922cf131635cae",
        ["data/maps/SevenIsland_SevaultCanyon_House/scripts.inc"] = "79058dfe0c83a1d2a1da9a671d078f289239fdfbc1ee28fab318990d69f8c0cf",
        ["data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc"] = "b049d0009685686732941369e62a9c04d345d5325035e463c28505804b600e1b",
        ["data/maps/SevenIsland_TanobyRuins/scripts.inc"] = "e6ab24b80c3c484efe15406e9cb4a51fe4e443ee5a7029726b8c0703c68a930d",
        ["data/maps/SevenIsland_TrainerTower/scripts.inc"] = "9fddf00c108dcba448c5ec29803b54e6533f49485c11d494827186f38a9ce822",
        ["data/maps/SevenIsland_TanobyRuins_MoneanChamber/scripts.inc"] = "3f1255dd52c8d3cb6a7e470205c39b2b33e6a10823112086cf1a43d5f9fc30db",
        ["data/maps/SevenIsland_TanobyRuins_LiptooChamber/scripts.inc"] = "999aaa378fbd5bbdd919fb51d6968e3b7a9d01670c47ba4e57c14d78547defa9",
        ["data/maps/SevenIsland_TanobyRuins_WeepthChamber/scripts.inc"] = "a7e65f5f3db3818aed65c6dbc557e8d063955eca4648e910592a56a17a0c7842",
        ["data/maps/SevenIsland_TanobyRuins_DilfordChamber/scripts.inc"] = "9d58b4cab58df65f54f70bb5f2a367d0856f87325f58d51f84c9948bea431139",
        ["data/maps/SevenIsland_TanobyRuins_ScufibChamber/scripts.inc"] = "aef500ae89b7928b804cbeb46cf57bcffd4f5a86da1a6ec8ceaecf6d587f5e69",
        ["data/maps/SevenIsland_TanobyRuins_RixyChamber/scripts.inc"] = "adf67c02957dbd2e2d35911cbe787f2dda058c9819672fe64723d5fb6d055c74",
        ["data/maps/SevenIsland_TanobyRuins_ViapoisChamber/scripts.inc"] = "308c2a2d383d0ef68156d9bc6814773c854c849357f73f264f7293e3eea19b3c",
    }.ToImmutableDictionary(StringComparer.Ordinal);
    private static bool IsSevenIslandMapSource(PretSourceScriptFile file) =>
        SevenIslandMapSourceHashes.TryGetValue(file.Path,out var hash) && FirstSeviiSource(file,file.Path,hash);
    private static bool IsSevenIslandDanceAlias(PretSourceScriptFile file,string line) =>
        IsSevenIslandMapSource(file) && file.Path=="data/maps/SevenIsland_SevaultCanyon_House/scripts.inc" &&
        line==".set DID_CHANSEY_DANCE, FLAG_TEMP_2";
    private static bool IsSevenIslandAssemblyData(PretSourceScriptFile file,string label,string[] body)
    {
        if(!IsSevenIslandMapSource(file))return false;
        if(label==file.Path.Split('/')[2]+"_MapScripts"&&body.SequenceEqual(new[]{".byte 0"}))return true;
        return file.Path=="data/maps/SevenIsland_Mart/scripts.inc"&&label=="SevenIsland_Mart_Items"&&body.SequenceEqual(new[]{
            ".2byte ITEM_ULTRA_BALL", ".2byte ITEM_GREAT_BALL", ".2byte ITEM_FULL_RESTORE", ".2byte ITEM_MAX_POTION",
            ".2byte ITEM_HYPER_POTION", ".2byte ITEM_REVIVE", ".2byte ITEM_FULL_HEAL", ".2byte ITEM_ESCAPE_ROPE",
            ".2byte ITEM_MAX_REPEL", ".2byte ITEM_NONE", "release", "end"});
    }
    private static (StoryOp,int,int)? SevenIslandInstructionShape(PretSourceScriptFile file,string label,string opcode,ImmutableArray<string> operands)
    {
        if(IsSevenIslandMapSource(file)&&file.Path=="data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc"&&
            label=="SevenIsland_SevaultCanyon_TanobyKey_EventScript_SwitchPressed"&&opcode=="addvar")
        {
            if(!operands.SequenceEqual(new[]{"VAR_TEMP_8","1"}))throw new InvalidDataException("seven_island_switch_increment_source");
            return(StoryOp.Special,2,2);
        }
        if(FirstSeviiSource(file,"data/event_scripts.s","3b1bd10d26c0a74fb3452ab42fc9d14dfca785f8b71389db06c71f5dfdd50a27")&&
            label=="EventScript_TryDarkenRuins"&&opcode is "setweather" or "doweather")
        {
            if(opcode=="setweather"&&!operands.SequenceEqual(new[]{"WEATHER_SHADE"})||opcode=="doweather"&&!operands.IsEmpty)
                throw new InvalidDataException("seven_island_weather_source");
            return(StoryOp.Special,opcode=="setweather"?1:0,opcode=="setweather"?1:0);
        }
        if(FirstSeviiSource(file,"data/scripts/trainers.inc","d145a6d45e4143e3b215fdff82de07b78fd361aa695855e4065c043ffba36945")&&
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
        if(file.Path.StartsWith("data/maps/SevenIsland",StringComparison.Ordinal)&&!SevenIslandMapSourceHashes.ContainsKey(file.Path))
            return(StoryOp.Unsupported,0,int.MaxValue);
        return null;
    }
}
