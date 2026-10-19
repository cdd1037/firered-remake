using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Application.Story;

[TestClass, TestCategory("Game")]
public sealed class SourceSpecialMenuLabelTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Lazy<PretCampaignCatalog> Translated = new(() => WithLabels(false));
    private static readonly Lazy<PretCampaignCatalog> Colliding = new(() => WithLabels(true));

    private static PretCampaignCatalog WithLabels(bool collide)
    {
        var original = CurrentCampaignFixture.Catalog;
        string[] symbols = ["gText_Vermilion", "gText_OneIsland", "gText_TwoIsland", "gText_ThreeIsland",
            "gText_FourIsland", "gText_FiveIsland", "gText_SixIsland", "gText_SevenIsland", "gText_Other",
            "gOtherText_Exit", "gText_SeviiIslands", "gText_NavelRock", "gText_BirthIsland"];
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source/battle.json"), JsonSerializer.Serialize(
            symbols.ToDictionary(symbol => symbol, symbol => collide ? "same-label" : "display-" + symbol)));
        var texts = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        var catalog = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        typeof(PretCampaignCatalog).GetField("<BattleContent>k__BackingField", Private)!
            .SetValue(catalog, original.BattleContent with { Texts = texts });
        Assert.AreSame(original.BattleContent.Trainers, catalog.BattleContent.Trainers);
        Assert.AreSame(original.BattleContent.MoveNames, catalog.BattleContent.MoveNames);
        return catalog;
    }

    private static (CampaignState State, StoryEffect Effect) AtMenu(PretCampaignCatalog catalog, string program)
    {
        var instructions = catalog.FindProgram(program)!.Instructions;
        int index = Enumerable.Range(0, instructions.Length).Single(i => instructions[i].Opcode == "multichoice");
        var instruction = instructions[index];
        var basis = CurrentCampaignFixture.Checkpoint.State;
        var state = basis with { SeagallopMenu = null, FossilMenu = null, Script = StoryState.Empty with {
            Locked = true, Completed = false, Stack = [new(program, index + 1)],
            Wait = new(StoryWaitKind.MenuChoice, "fixture:source-menu") } };
        return (state, new(1, instruction.Opcode, instruction.Args, instruction.Source));
    }

    private static CampaignState Apply(CampaignSession session, string method, CampaignState state, StoryEffect effect)
    {
        object?[] args = [state, effect, null];
        Assert.IsTrue((bool)typeof(CampaignSession).GetMethod(method, Private)!.Invoke(session, args)!);
        return (CampaignState)args[2]!;
    }

    private static void SameSourceChoices(PretCampaignCatalog catalog, CampaignState state, int optionCount)
    {
        foreach (int choice in Enumerable.Range(0, optionCount).Append(127))
        {
            var expected = StoryRules.Continue(state.Script, state.Script.Wait!.CorrelationId,
                CurrentCampaignFixture.Catalog, choice);
            var actual = StoryRules.Continue(state.Script, state.Script.Wait.CorrelationId, catalog, choice);
            Assert.AreEqual(TransitionStatus.Accepted, actual.Status, actual.Diagnostic?.Message);
            Assert.AreEqual(CanonicalJson.Hash(expected), CanonicalJson.Hash(actual), "Source choice " + choice);
        }
    }

    [TestMethod]
    [DataRow("EventScript_ChooseDestFromOneIsland", "gText_TwoIsland,gText_ThreeIsland,gOtherText_Exit")]
    [DataRow("EventScript_ChooseDestFromTwoIsland", "gText_OneIsland,gText_ThreeIsland,gOtherText_Exit")]
    [DataRow("EventScript_ChooseDestFromIsland", "gText_OneIsland,gText_TwoIsland,gOtherText_Exit")]
    [DataRow("EventScript_ChooseDestFromOneIslandVermilionAllowed", "gText_Vermilion,gText_TwoIsland,gText_ThreeIsland,gOtherText_Exit")]
    [DataRow("EventScript_ChooseDestFromTwoIslandVermilionAllowed", "gText_Vermilion,gText_OneIsland,gText_ThreeIsland,gOtherText_Exit")]
    [DataRow("EventScript_ChooseDestFromIslandVermilionAllowed", "gText_Vermilion,gText_OneIsland,gText_TwoIsland,gOtherText_Exit")]
    [DataRow("VermilionCity_EventScript_ChooseSeagallopDestTriPass", "gText_OneIsland,gText_TwoIsland,gText_ThreeIsland,gOtherText_Exit")]
    public void FirstSeviiMenusUseSourceLabelsAtOriginalPositions(string program, string expectedSymbols)
    {
        var catalog = Translated.Value;
        var session = CampaignSession.CreateCurrent(catalog);
        var (state, effect) = AtMenu(catalog, program);
        var next = Apply(session, "TryApplyFirstSeviiEffect", state, effect);
        Assert.AreEqual(effect.Args[2], next.SeagallopMenu!.SourceMenu);
        Assert.AreSequenceEqual(expectedSymbols.Split(',').Select(symbol => catalog.BattleContent.Texts[symbol]), next.SeagallopMenu.Options);
        Assert.AreEqual(state, next with { SeagallopMenu = state.SeagallopMenu });
        SameSourceChoices(catalog, next, next.SeagallopMenu.Options.Length);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void RainbowPagesKeepDestinationIdsOtherAndCancellation(int page)
    {
        var catalog = Translated.Value;
        var session = CampaignSession.CreateCurrent(catalog);
        string[] symbols = ["gText_Vermilion", "gText_OneIsland", "gText_TwoIsland", "gText_ThreeIsland",
            "gText_FourIsland", "gText_FiveIsland", "gText_SixIsland", "gText_SevenIsland"];
        var program = catalog.FindProgram("EventScript_SeviiDestinationsPage" + (page + 1))!;
        var instruction = program.Instructions.Single(i => i.Opcode == "special" && i.Args[0] == "DrawSeagallopDestinationMenu");
        for (int origin = 0; origin <= 7; origin++)
        {
            var basis = CurrentCampaignFixture.Checkpoint.State;
            var state = basis with { SeagallopMenu = null, Script = StoryState.Empty with { Locked = true,
                Variables = StoryState.Empty.Variables.SetItem("VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F", 5)
                    .SetItem("VAR_0x8004", origin).SetItem("VAR_0x8005", page) },
                Inventory = basis.Inventory! with { KeyItems = [new(new("pret:ITEM_RAINBOW_PASS"), 1)] } };
            var next = Apply(session, "TryApplyRainbowFerryEffect", state, new(1, instruction.Opcode, instruction.Args, instruction.Source));
            int[] destinations = (page == 0 ? new[] { 0, 1, 2, 3, 4 } : origin < 5 ? new[] { 5, 6, 7 } : new[] { 4, 5, 6, 7 })
                .Where(id => id != origin).Take(page == 0 ? 4 : 3).ToArray();
            Assert.AreSequenceEqual(destinations.Select(id => symbols[id]).Concat(["gText_Other", "gOtherText_Exit"])
                .Select(symbol => catalog.BattleContent.Texts[symbol]), next.SeagallopMenu!.Options);
            Assert.AreEqual("SEVII_PAGE_" + page, next.SeagallopMenu.SourceMenu);
            int Destination(int choice) => (int)typeof(CampaignSession).GetMethod("RainbowDestination", Private)!.Invoke(session, [next, choice])!;
            for (int i = 0; i < destinations.Length; i++) Assert.AreEqual(destinations[i], Destination(i));
            Assert.AreEqual(254, Destination(destinations.Length));
            Assert.AreEqual(127, Destination(destinations.Length + 1));
            Assert.AreEqual(127, Destination(127));
            Assert.AreEqual(state, next with { SeagallopMenu = state.SeagallopMenu, Dialogue = state.Dialogue, Script = state.Script });
            Assert.AreEqual(CanonicalJson.Hash(state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 255) }), CanonicalJson.Hash(next.Script));
        }
    }

    [TestMethod]
    [DataRow("VermilionCity_EventScript_HasMysticTicket", 0, true, false, "gText_SeviiIslands,gText_NavelRock,gOtherText_Exit")]
    [DataRow("VermilionCity_EventScript_HasAuroraTicket", 0, false, true, "gText_SeviiIslands,gText_BirthIsland,gOtherText_Exit")]
    [DataRow("VermilionCity_EventScript_HasMysticAndAuroraTickets", 0, true, true, "gText_SeviiIslands,gText_NavelRock,gText_BirthIsland,gOtherText_Exit")]
    [DataRow("NavelRock_Harbor_EventScript_Sailor", 9, false, false, "gText_Vermilion,gOtherText_Exit")]
    [DataRow("BirthIsland_Harbor_EventScript_Sailor", 10, false, false, "gText_Vermilion,gOtherText_Exit")]
    public void EventFerryAccessUsesDestinationIdentityEvenWhenEveryLabelCollides(string program, int origin,
        bool mystic, bool aurora, string expectedSymbols)
    {
        foreach (var catalog in new[] { Translated.Value, Colliding.Value })
        {
            var session = CampaignSession.CreateCurrent(catalog);
            var (basis, effect) = AtMenu(catalog, program);
            var keys = ImmutableArray.CreateBuilder<InventoryStack>();
            if (mystic) keys.Add(new(new("pret:ITEM_MYSTIC_TICKET"), 1));
            if (aurora) keys.Add(new(new("pret:ITEM_AURORA_TICKET"), 1));
            string map = origin switch { 9 => "map.navel-rock-harbor", 10 => "map.birth-island-harbor", _ => "map.vermilion-city" };
            var state = basis with { World = basis.World with { Player = basis.World.Player with { MapId = new(map) } },
                Inventory = basis.Inventory! with { KeyItems = keys.ToImmutable() },
                Script = basis.Script with { Variables = basis.Script.Variables.SetItem("VAR_0x8004", origin)
                    .SetItem("VAR_MAP_SCENE_VERMILION_CITY", 3).SetItem("VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F", 5),
                    Flags = basis.Script.Flags.SetItem("FLAG_ENABLE_SHIP_NAVEL_ROCK", mystic).SetItem("FLAG_ENABLE_SHIP_BIRTH_ISLAND", aurora) } };
            var next = Apply(session, "TryApplyEventIslandFerryEffect", state, effect);
            Assert.AreEqual(effect.Args[2], next.SeagallopMenu!.SourceMenu);
            Assert.AreSequenceEqual(expectedSymbols.Split(',').Select(symbol => catalog.BattleContent.Texts[symbol]), next.SeagallopMenu.Options);
            Assert.AreEqual(state, next with { SeagallopMenu = state.SeagallopMenu });
            SameSourceChoices(catalog, next, next.SeagallopMenu.Options.Length);
            if (origin != 0) continue;
            foreach (var (enabled, flag, item) in new[] { (mystic, "FLAG_ENABLE_SHIP_NAVEL_ROCK", new ItemId("pret:ITEM_MYSTIC_TICKET")),
                (aurora, "FLAG_ENABLE_SHIP_BIRTH_ISLAND", new ItemId("pret:ITEM_AURORA_TICKET")) })
            {
                var wrong = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(flag, !enabled) },
                    Inventory = state.Inventory! with { KeyItems = enabled ? state.Inventory!.KeyItems!.Value : state.Inventory!.KeyItems!.Value.Add(new(item, 1)) } };
                AccessRejected(wrong);
                if (enabled) AccessRejected(state with { Inventory = state.Inventory! with { KeyItems = state.Inventory!.KeyItems!.Value.RemoveAll(row => row.Item == item) } });
            }
            AccessRejected(state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_VERMILION_CITY", 2) } });
            void AccessRejected(CampaignState invalid)
            {
                var error = Assert.ThrowsExactly<TargetInvocationException>(() => Apply(session, "TryApplyEventIslandFerryEffect", invalid, effect));
                Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
                Assert.AreEqual("event_ticket_ferry_menu_access", error.InnerException!.Message);
            }
        }
    }

    [TestMethod]
    [DataRow("Helix", "HELIX", false)]
    [DataRow("Dome", "DOME", false)]
    [DataRow("Amber", "OLD_AMBER", false)]
    [DataRow("HelixAmber", "HELIX", true)]
    [DataRow("DomeAmber", "DOME", true)]
    public void FossilMenusKeepItemNamesAndUseSharedExit(string branch, string kind, bool amber)
    {
        var catalog = Translated.Value;
        var session = CampaignSession.CreateCurrent(catalog);
        const string prefix = "CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_";
        var (basis, effect) = AtMenu(catalog, prefix + "ChooseFossil" + branch);
        string itemSymbol = kind == "OLD_AMBER" ? "ITEM_OLD_AMBER" : "ITEM_" + kind.ToUpperInvariant() + "_FOSSIL";
        ItemId[] items = amber ? [new("pret:" + itemSymbol), new("pret:ITEM_OLD_AMBER")] : [new("pret:" + itemSymbol)];
        var actor = catalog.MapDefinition(new("map.cinnabar-island-pokemon-lab-experiment-room")).Actors.Single(a => a.SourceLocalId == 2);
        var state = basis with { World = basis.World with { Player = basis.World.Player with { MapId = new("map.cinnabar-island-pokemon-lab-experiment-room") } },
            LastTalked = actor.LocalId, Actors = ImmutableSortedDictionary<string, CampaignActor>.Empty.Add(actor.LocalId,
                new(actor.LocalId, actor.SourceLocalId, actor.GraphicsId, actor.X, actor.Y, actor.Elevation, Direction.South,
                    actor.MovementType, actor.ScriptId, actor.HideFlag, true, false, actor.X, actor.Y)),
            Inventory = basis.Inventory! with { KeyItems = items.Select(id => new InventoryStack(id, 1)).ToImmutableArray() },
            Script = basis.Script with { Variables = basis.Script.Variables.SetItem("VAR_LAST_TALKED", 2),
                Flags = items.Aggregate(basis.Script.Flags, (flags, id) => flags.SetItem("FLAG_GOT_" + id.Value["pret:ITEM_".Length..], true)) } };
        var next = Apply(session, "TryApplyFossilLabEffect", state, effect);
        Assert.AreEqual(effect.Args[2], next.FossilMenu!.MenuId);
        Assert.AreSequenceEqual(items.Select(id => catalog.EarlyShop!.Items.Single(item => item.Id == id).Name)
            .Append(catalog.BattleContent.Texts["gOtherText_Exit"]), next.FossilMenu.Options);
        Assert.AreEqual(state, next with { FossilMenu = state.FossilMenu });
        SameSourceChoices(catalog, next, next.FossilMenu.Options.Length);
    }
}
