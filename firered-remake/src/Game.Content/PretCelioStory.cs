using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

/// <summary>Literal source witness for the Celio handoff and its immediate callbacks; no invented service effects.</summary>
public static class PretCelioStory
{
    public const string SourcePath = "data/maps/OneIsland_PokemonCenter_1F/scripts.inc";
    public const string Prefix = "OneIsland_PokemonCenter_1F_";
    public const string HandoffLabel = Prefix + "EventScript_GiveCelioSapphire";
    private static readonly ImmutableDictionary<string, ImmutableArray<string>> Programs = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
    {
        ["OneIsland_PokemonCenter_1F_OnLoad"] = [
            "call_if_ge VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 5, OneIsland_PokemonCenter_1F_EventScript_SetRubyMetatile",
            "call_if_set FLAG_SYS_CAN_LINK_WITH_RS, OneIsland_PokemonCenter_1F_EventScript_SetNetworkMachineOn",
            "end"
        ],["OneIsland_PokemonCenter_1F_OnTransition"] = [
            "setrespawn HEAL_LOCATION_ONE_ISLAND",
            "call_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 6, OneIsland_PokemonCenter_1F_EventScript_SetCelioQuestDone",
            "call_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 0, OneIsland_PokemonCenter_1F_EventScript_SetBillCelioFirstMeetingPos",
            "call_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 2, OneIsland_PokemonCenter_1F_EventScript_SetBillCelioReadyToLeavePos",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_SetCelioQuestDone"] = [
            "setvar VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 7",
            "return"
        ],["OneIsland_PokemonCenter_1F_EventScript_SetRubyMetatile"] = [
            "setmetatile 13, 4, METATILE_PokemonCenter_NetworkMachine_Ruby, 1",
            "return"
        ],["OneIsland_PokemonCenter_1F_EventScript_SetSapphireMetatile"] = [
            "setmetatile 16, 4, METATILE_PokemonCenter_NetworkMachine_Sapphire, 1",
            "return"
        ],["OneIsland_PokemonCenter_1F_EventScript_SetNetworkMachineOn"] = [
            "call OneIsland_PokemonCenter_1F_EventScript_SetSapphireMetatile",
            "setmetatile 14, 4, METATILE_PokemonCenter_NetworkMachine_ScreenLeft_On, 1",
            "setmetatile 15, 4, METATILE_PokemonCenter_NetworkMachine_ScreenRight_On, 1",
            "return"
        ],["OneIsland_PokemonCenter_1F_EventScript_Celio"] = [
            "lock",
            "faceplayer",
            "goto_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 7, OneIsland_PokemonCenter_1F_EventScript_CelioGiveBillFact",
            "goto_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 6, OneIsland_PokemonCenter_1F_EventScript_CelioJustGivenSapphire",
            "goto_if_set FLAG_RECOVERED_SAPPHIRE, OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire",
            "goto_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 5, OneIsland_PokemonCenter_1F_EventScript_ExplainRainbowPass",
            "goto_if_set FLAG_GOT_RUBY, OneIsland_PokemonCenter_1F_EventScript_GiveCelioRuby",
            "goto_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 4, OneIsland_PokemonCenter_1F_EventScript_CelioWaitingForRuby",
            "specialvar VAR_RESULT, IsNationalPokedexEnabled",
            "goto_if_eq VAR_RESULT, TRUE, OneIsland_PokemonCenter_1F_EventScript_CelioRequestRuby",
            "goto_if_eq VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 3, OneIsland_PokemonCenter_1F_EventScript_CelioPlayerMissingNationalDex",
            "msgbox OneIsland_PokemonCenter_1F_Text_SorryForBeingPoorHost",
            "closemessage",
            "applymovement LOCALID_CELIO, Common_Movement_FaceOriginalDirection",
            "waitmovement 0",
            "release",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_CelioGiveBillFact"] = [
            "msgbox OneIsland_PokemonCenter_1F_Text_CelioHearingRumorsAboutYou",
            "random 3",
            "goto_if_eq VAR_RESULT, 0, OneIsland_PokemonCenter_1F_EventScript_BillFact1",
            "goto_if_eq VAR_RESULT, 1, OneIsland_PokemonCenter_1F_EventScript_BillFact2",
            "famechecker FAMECHECKER_BILL, 5",
            "msgbox OneIsland_PokemonCenter_1F_Text_BillCantStomachMilk",
            "release",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_BillFact1"] = [
            "famechecker FAMECHECKER_BILL, 3",
            "msgbox OneIsland_PokemonCenter_1F_Text_BillsFirstMonWasAbra",
            "release",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_BillFact2"] = [
            "famechecker FAMECHECKER_BILL, 4",
            "msgbox OneIsland_PokemonCenter_1F_Text_BillsHometownInGoldenrod",
            "release",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_CelioJustGivenSapphire"] = [
            "msgbox OneIsland_PokemonCenter_1F_Text_WishYouBestOfLuck",
            "release",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire"] = [
            "textcolor NPC_TEXT_COLOR_NEUTRAL",
            "playfanfare MUS_OBTAIN_TMHM",
            "message OneIsland_PokemonCenter_1F_Text_HandedSapphireToCelio",
            "waitmessage",
            "waitfanfare",
            "call EventScript_RestorePrevTextColor",
            "removeitem ITEM_SAPPHIRE",
            "msgbox OneIsland_PokemonCenter_1F_Text_ThankYouGiveMeTime",
            "closemessage",
            "applymovement LOCALID_CELIO, OneIsland_PokemonCenter_1F_Movement_CelioPutGemInMachine",
            "waitmovement 0",
            "playse SE_CLICK",
            "call OneIsland_PokemonCenter_1F_EventScript_SetSapphireMetatile",
            "special DrawWholeMapView",
            "msgbox OneIsland_PokemonCenter_1F_Text_OkayThisIsGood",
            "closemessage",
            "delay 60",
            "applymovement LOCALID_CELIO, OneIsland_PokemonCenter_1F_Movement_CelioCheckMachine",
            "waitmovement 0",
            "delay 30",
            "applymovement LOCALID_CELIO, OneIsland_PokemonCenter_1F_Movement_CelioCheckMachine",
            "waitmovement 0",
            "delay 40",
            "playse SE_PC_ON",
            "call OneIsland_PokemonCenter_1F_EventScript_SetNetworkMachineOn",
            "special DrawWholeMapView",
            "msgbox OneIsland_PokemonCenter_1F_Text_LinkedUpWithLanette",
            "applymovement LOCALID_CELIO, Common_Movement_FacePlayer",
            "waitmovement 0",
            "msgbox OneIsland_PokemonCenter_1F_Text_ManagedToLinkWithHoennThankYou",
            "special SetPostgameFlags",
            "setflag FLAG_SYS_CAN_LINK_WITH_RS",
            "setflag FLAG_HIDE_CERULEAN_CAVE_GUARD",
            "setflag FLAG_HIDE_LORELEI_IN_HER_HOUSE",
            "setvar VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F, 6",
            "special InitRoamer",
            "release",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_NetworkMachine"] = [
            "lockall",
            "goto_if_set FLAG_SYS_CAN_LINK_WITH_RS, OneIsland_PokemonCenter_1F_EventScript_NetworkMachineLinkedWithHoenn",
            "goto_if_set FLAG_SEVII_DETOUR_FINISHED, OneIsland_PokemonCenter_1F_EventScript_NetworkMachineLinkedWithKanto",
            "msgbox OneIsland_PokemonCenter_1F_Text_MachineUnderAdjustment",
            "releaseall",
            "end"
        ],["OneIsland_PokemonCenter_1F_EventScript_NetworkMachineLinkedWithHoenn"] = [
            "msgbox OneIsland_PokemonCenter_1F_Text_MachineLinkedWithKantoAndHoenn",
            "releaseall",
            "end"
        ]    }.ToImmutableDictionary(StringComparer.Ordinal);
    public static ImmutableHashSet<string> ProgramLabels { get; } = Programs.Keys.ToImmutableHashSet(StringComparer.Ordinal);

    private static readonly ImmutableDictionary<string, int> ProgramStartLines = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["OneIsland_PokemonCenter_1F_OnLoad"] = 8,["OneIsland_PokemonCenter_1F_OnTransition"] = 27,["OneIsland_PokemonCenter_1F_EventScript_SetCelioQuestDone"] = 34,["OneIsland_PokemonCenter_1F_EventScript_SetRubyMetatile"] = 13,["OneIsland_PokemonCenter_1F_EventScript_SetSapphireMetatile"] = 17,["OneIsland_PokemonCenter_1F_EventScript_SetNetworkMachineOn"] = 21,["OneIsland_PokemonCenter_1F_EventScript_Celio"] = 198,["OneIsland_PokemonCenter_1F_EventScript_CelioGiveBillFact"] = 217,["OneIsland_PokemonCenter_1F_EventScript_BillFact1"] = 227,["OneIsland_PokemonCenter_1F_EventScript_BillFact2"] = 233,["OneIsland_PokemonCenter_1F_EventScript_CelioJustGivenSapphire"] = 239,["OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire"] = 244,["OneIsland_PokemonCenter_1F_EventScript_NetworkMachine"] = 395,["OneIsland_PokemonCenter_1F_EventScript_NetworkMachineLinkedWithHoenn"] = 408    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static void Validate(PretStoryCompilation scripts)
    {
        foreach (var (label, expected) in Programs)
        {
            var instructions = scripts.FindProgram(label)?.Instructions ?? throw new InvalidDataException("celio_story_missing:" + label);
            if (instructions.Length != expected.Length || instructions.Where((i, index) => i.Op == StoryOp.Unsupported || i.Source.Path != SourcePath || i.Source.Symbol != label ||
                    i.Source.Line != ProgramStartLines[label] + index + 1).Any() ||
                !instructions.Select(i => i.Opcode + (i.Args.IsEmpty ? "" : " " + string.Join(", ", i.Args))).SequenceEqual(expected))
                throw new InvalidDataException("celio_story_source_order:" + label);
        }
        foreach (var (label, expected) in MovementWitnesses)
            if (!scripts.Movements.TryGetValue(label, out var actual) || !actual.Select(x => x.Opcode).SequenceEqual(expected))
                throw new InvalidDataException("celio_story_movement:" + label);
        var headers = scripts.MapHeaders.GetValueOrDefault(Prefix + "MapScripts", []);
        if (!headers.Select(h => h.EventKind + ":" + h.Target).SequenceEqual(new[] {
            "MAP_SCRIPT_ON_TRANSITION:" + Prefix + "OnTransition","MAP_SCRIPT_ON_RESUME:CableClub_OnResume","MAP_SCRIPT_ON_LOAD:" + Prefix + "OnLoad","MAP_SCRIPT_ON_FRAME_TABLE:" + Prefix + "OnFrame" }))
            throw new InvalidDataException("celio_story_callback_order");
    }

    private static readonly ImmutableDictionary<string, ImmutableArray<string>> MovementWitnesses = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
    {
        ["OneIsland_PokemonCenter_1F_Movement_CelioCheckMachine"] = ["walk_in_place_up", "delay_8", "walk_in_place_up", "step_end"],["OneIsland_PokemonCenter_1F_Movement_CelioPutGemInMachine"] = ["walk_in_place_faster_up", "walk_in_place_up", "delay_16", "walk_in_place_up", "walk_in_place_up", "step_end"]    }.ToImmutableDictionary(StringComparer.Ordinal);
}
