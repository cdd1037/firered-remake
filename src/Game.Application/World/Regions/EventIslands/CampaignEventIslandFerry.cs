using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string EventTicketFerrySource = "data/maps/VermilionCity/scripts.inc";
    private static string? EventIslandHarbor(int id) => id switch
    {
        9 => "map.navel-rock-harbor", 10 => "map.birth-island-harbor", _ => null
    };
    private static bool EventTicketFerryStoryGate(CampaignState state) =>
        state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VERMILION_CITY") == 3 &&
        state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F") >= 5;
    private static bool EventIslandTicketAvailable(CampaignState state, int destination) => destination switch
    {
        9 => state.Script.Flags.GetValueOrDefault("FLAG_ENABLE_SHIP_NAVEL_ROCK") &&
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], EventMysticTicket) >= 1,
        10 => state.Script.Flags.GetValueOrDefault("FLAG_ENABLE_SHIP_BIRTH_ISLAND") &&
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], EventAuroraTicket) >= 1,
        _ => false
    };
    private bool EventIslandFerryDeparture(CampaignState state, int origin, int destination) =>
        (origin == 0 && destination is 9 or 10 && state.World.Player.MapId.Value == "map.vermilion-city" &&
            EventTicketFerryStoryGate(state) && EventIslandTicketAvailable(state, destination) ||
         origin is 9 or 10 && destination == 0 && state.World.Player.MapId.Value == EventIslandHarbor(origin));

    private bool TryApplyEventIslandFerryEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var a = effect.Args;
        if (effect.Source.Path == EventTicketFerrySource && effect.Opcode == "checkitem" &&
            effect.Source.Symbol is "VermilionCity_EventScript_CheckHasMysticTicket" or "VermilionCity_EventScript_CheckHasAuroraTicket")
        {
            bool mystic = effect.Source.Symbol.EndsWith("MysticTicket", StringComparison.Ordinal);
            if (!state.Script.Locked || state.World.Player.MapId.Value != "map.vermilion-city" || !EventTicketFerryStoryGate(state) ||
                a.Length is < 1 or > 2 || SeviiValue(state, a[0]) != (mystic ? 370 : 371) || a.Length == 2 && SeviiValue(state, a[1]) != 1)
                throw new InvalidDataException("event_ticket_check_context");
            int quantity = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], mystic ? EventMysticTicket : EventAuroraTicket);
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", quantity >= 1 ? 1 : 0) } };
            return true;
        }
        if (effect.Source.Path == EventTicketFerrySource && effect.Source.Symbol == "VermilionCity_EventScript_ChooseSeagallopDestRainbowPass" &&
            effect.Opcode == "special" && a.SequenceEqual(new[] { "QuestLog_CutRecording" }))
        {
            if (!state.Script.Locked || state.World.Player.MapId.Value != "map.vermilion-city" || !EventTicketFerryStoryGate(state) || state.Script.QuestLogState != 0)
                throw new InvalidDataException("event_ticket_ferry_context");
            return true;
        }
        if (effect.Opcode != "multichoice" || a.Length != 4) return false;
        var context = (effect.Source.Path, effect.Source.Symbol, a[2]);
        (string X, string Y, int Origin, ImmutableArray<(int? Destination, string TextSymbol)> Options)? menu = context switch
        {
            (EventTicketFerrySource, "VermilionCity_EventScript_HasMysticTicket", "MULTICHOICE_SEVII_NAVEL") =>
                ("17", "6", 0, [(null, "gText_SeviiIslands"), (9, "gText_NavelRock"), (null, "gOtherText_Exit")]),
            (EventTicketFerrySource, "VermilionCity_EventScript_HasAuroraTicket", "MULTICHOICE_SEVII_BIRTH") =>
                ("13", "6", 0, [(null, "gText_SeviiIslands"), (10, "gText_BirthIsland"), (null, "gOtherText_Exit")]),
            (EventTicketFerrySource, "VermilionCity_EventScript_HasMysticAndAuroraTickets", "MULTICHOICE_SEVII_NAVEL_BIRTH") =>
                ("13", "5", 0, [(null, "gText_SeviiIslands"), (9, "gText_NavelRock"), (10, "gText_BirthIsland"), (null, "gOtherText_Exit")]),
            ("data/maps/NavelRock_Harbor/scripts.inc", "NavelRock_Harbor_EventScript_Sailor", "MULTICHOICE_SEAGALLOP_VERMILION") =>
                ("20", "8", 9, [(0, "gText_Vermilion"), (null, "gOtherText_Exit")]),
            ("data/maps/BirthIsland_Harbor/scripts.inc", "BirthIsland_Harbor_EventScript_Sailor", "MULTICHOICE_SEAGALLOP_VERMILION") =>
                ("20", "8", 10, [(0, "gText_Vermilion"), (null, "gOtherText_Exit")]),
            _ => null
        };
        if (menu is not { } selected) return false;
        if (a[0] != selected.X || a[1] != selected.Y || a[3] != "FALSE" || !state.Script.Locked ||
            state.Script.Wait?.Kind != StoryWaitKind.MenuChoice || state.Script.Variables.GetValueOrDefault("VAR_0x8004") != selected.Origin ||
            state.World.Player.MapId.Value != (selected.Origin == 0 ? "map.vermilion-city" : EventIslandHarbor(selected.Origin)))
            throw new InvalidDataException("event_ticket_ferry_menu_context");
        if (selected.Origin == 0 && (!EventTicketFerryStoryGate(state) ||
            selected.Options.Any(option => option.Destination == 9) != EventIslandTicketAvailable(state, 9) ||
            selected.Options.Any(option => option.Destination == 10) != EventIslandTicketAvailable(state, 10)))
            throw new InvalidDataException("event_ticket_ferry_menu_access");
        next = state with { SeagallopMenu = new(a[2], selected.Options
            .Select(option => BattleContent.Texts[option.TextSymbol]).ToImmutableArray()) };
        return true;
    }
}
