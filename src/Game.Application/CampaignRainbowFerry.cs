using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool RainbowFerryMenu(CampaignState state) => state.SeagallopMenu?.SourceMenu is "SEVII_PAGE_0" or "SEVII_PAGE_1";
    private bool RainbowIslandAvailable(int id) => id == 7 || id is >= 0 and <= 3 || id == 4 || id == 6 || id == 5;
    private static ImmutableArray<int> RainbowPageDestinations(int origin, int page)
    {
        // script_menu.c DrawSeagallopDestinationMenu: page2 starts at Four
        // when the current island is Five or later; skip the current island.
        int first = page == 0 ? 0 : origin < 5 ? 5 : 4;
        int count = page == 0 ? 4 : 3;
        return Enumerable.Range(first, 8 - first).Where(x => x != origin).Take(count).ToImmutableArray();
    }
    private int RainbowDestination(CampaignState state, int choice)
    {
        int origin = state.Script.Variables.GetValueOrDefault("VAR_0x8004"), page = state.Script.Variables.GetValueOrDefault("VAR_0x8005");
        if (!RainbowIslandAvailable(origin) || page is < 0 or > 1) throw new InvalidDataException("seagallop_page_context");
        if (choice == 127 || choice == (page == 0 ? 5 : 4)) return 127;
        if (choice == (page == 0 ? 4 : 3)) return 254; // SEAGALLOP_MORE
        if (choice < 0 || choice >= (page == 0 ? 4 : 3)) throw new InvalidDataException("seagallop_menu_choice_invalid");
        return RainbowPageDestinations(origin, page)[choice];
    }
    private bool TryApplyRainbowFerryEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != SeagallopSource || effect.Source.Symbol is not ("EventScript_SeviiDestinationsPage1" or "EventScript_SeviiDestinationsPage2")) return false;
        if (!state.Script.Locked || !RainbowQuestComplete(state) ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1)
            throw new InvalidDataException("seagallop_rainbow_context");
        int page = state.Script.Variables.GetValueOrDefault("VAR_0x8005"), origin = state.Script.Variables.GetValueOrDefault("VAR_0x8004");
        if (page != (effect.Source.Symbol.EndsWith('1') ? 0 : 1) || !RainbowIslandAvailable(origin)) throw new InvalidDataException("seagallop_page_context");
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "DrawSeagallopDestinationMenu" }))
        {
            string[] textSymbols = ["gText_Vermilion", "gText_OneIsland", "gText_TwoIsland", "gText_ThreeIsland", "gText_FourIsland",
                "gText_FiveIsland", "gText_SixIsland", "gText_SevenIsland"];
            var destinations = RainbowPageDestinations(origin, page);
            next = state with { SeagallopMenu = new("SEVII_PAGE_" + page, destinations.Select(d => textSymbols[d]).Concat(["gText_Other", "gOtherText_Exit"])
                .Select(symbol => BattleContent.Texts[symbol]).ToImmutableArray()),
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
            if (destination is >= 0 and <= 7 && !RainbowIslandAvailable(destination)) throw new InvalidDataException("seagallop_destination_not_yet_available");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8006", destination) } }; return true;
        }
        return false;
    }
}
