using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool RainbowFerryMenu(CampaignState state) => state.SeagallopMenu?.SourceMenu is "SEVII_PAGE_0" or "SEVII_PAGE_1";
    private static int RainbowDestination(CampaignState state, int choice)
    {
        int origin = state.Script.Variables.GetValueOrDefault("VAR_0x8004"), page = state.Script.Variables.GetValueOrDefault("VAR_0x8005");
        if (origin is < 0 or > 3 || page is < 0 or > 1) throw new InvalidDataException("seagallop_page_context");
        if (choice == 127 || choice == (page == 0 ? 5 : 4)) return 127;
        if (choice == (page == 0 ? 4 : 3)) return 254; // SEAGALLOP_MORE
        if (choice < 0 || choice >= (page == 0 ? 4 : 3)) throw new InvalidDataException("seagallop_menu_choice_invalid");
        return page == 0 ? choice >= origin ? choice + 1 : choice : choice + 5;
    }
    private bool TryApplyRainbowFerryEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != SeagallopSource || effect.Source.Symbol is not ("EventScript_SeviiDestinationsPage1" or "EventScript_SeviiDestinationsPage2")) return false;
        if (!state.Script.Locked || state.Script.Variables.GetValueOrDefault(RubyCenterScene) != 5 ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1)
            throw new InvalidDataException("seagallop_rainbow_context");
        int page = state.Script.Variables.GetValueOrDefault("VAR_0x8005"), origin = state.Script.Variables.GetValueOrDefault("VAR_0x8004");
        if (page != (effect.Source.Symbol.EndsWith('1') ? 0 : 1) || origin is < 0 or > 3) throw new InvalidDataException("seagallop_page_context");
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "DrawSeagallopDestinationMenu" }))
        {
            string[] labels = ["VERMILION", "ONE ISLAND", "TWO ISLAND", "THREE ISLAND", "FOUR ISLAND (unavailable)",
                "FIVE ISLAND (unavailable)", "SIX ISLAND (unavailable)", "SEVEN ISLAND (unavailable)"];
            var destinations = page == 0 ? Enumerable.Range(0, 5).Where(d => d != origin).Take(4) : Enumerable.Range(5, 3);
            next = state with { SeagallopMenu = new("SEVII_PAGE_" + page, destinations.Select(d => labels[d]).Concat(["OTHER", "EXIT"]).ToImmutableArray()),
                Dialogue = null, Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 255) } }; return true;
        }
        if (effect.Opcode == "waitstate")
        {
            if (!RainbowFerryMenu(state) || state.Script.Wait is not { Kind: StoryWaitKind.Presentation } wait) throw new InvalidDataException("seagallop_menu_wait");
            next = state with { Script = state.Script with { Wait = wait with { Kind = StoryWaitKind.MenuChoice } } }; return true;
        }
        if (effect.Opcode == "specialvar" && effect.Args.SequenceEqual(new[] { "VAR_0x8006", "GetSelectedSeagallopDestination" }))
        {
            int destination = RainbowDestination(state, state.Script.Variables.GetValueOrDefault("VAR_RESULT"));
            if (destination is >= 4 and <= 7) throw new InvalidDataException("seagallop_destination_not_yet_available");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8006", destination) } }; return true;
        }
        return false;
    }
}
