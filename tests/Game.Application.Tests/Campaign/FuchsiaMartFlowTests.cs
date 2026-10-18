using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled checkpoint placement and inventory; all travel, conversations and transactions use public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class FuchsiaMartFlowTests : WildBattleFixture
{
    private const string Mart = "map.fuchsia-city-mart";
    private const string City = "map.fuchsia-city";
    private static readonly ItemId UltraBall = new("pret:ITEM_ULTRA_BALL");
    private static readonly ItemId SuperPotion = new("pret:ITEM_SUPER_POTION");

    private CampaignSession At(string name = Mart, int x = 4, int y = 3, Direction facing = Direction.West,
        int money = 30000, bool fullBag = false, bool fullBallStack = false)
    {
        var state = FieldFixture();
        var map = content.Maps[new(name)];
        var inventory = state.Inventory! with { Balls = [new(UltraBall, fullBallStack ? 999 : 20)] };
        if (fullBag)
        {
            var bag = inventory.BagItems.RemoveAll(stack => stack.Item == SuperPotion);
            bag = bag.AddRange(content.EarlyShop!.Items.Where(item => item.Pocket == "POCKET_ITEMS" &&
                item.Id != SuperPotion && !bag.Any(stack => stack.Item == item.Id))
                .Take(inventory.Limits.BagItemSlots - bag.Length).Select(item => new InventoryStack(item.Id, 1)));
            Assert.AreEqual(inventory.Limits.BagItemSlots, bag.Length);
            inventory = inventory with { BagItems = bag };
        }
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, facing) },
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER"), LastTalked = null, SelectedObjectSlot = 0, MapEdits = null, MapLayout = null,
            FlashLevel = 0, IsSurfing = false, IsCycling = false, Money = money, Inventory = inventory
        };
        // Materialize the source camera-bounded actor pool and entry scripts only at the
        // controlled starting point. All travel and interactions below are public commands.
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var session = CampaignSession.CreateCurrent(content);
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", hidden)!
            .Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", hidden)!.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", hidden)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, checkpoint.Generation));
        DrainField(session);
        Assert.IsNull(session.ValidatePlayerSave(session.State), "controlled Mart fixture");
        return session;
    }

    private List<string> DrainField(CampaignSession session)
    {
        var texts = new List<string>();
        for (int n = 0; n < 100; n++)
        {
            if (session.State.Dialogue is { } dialogue) texts.Add(dialogue.TextSymbol);
            if (session.State.Shop is not null || session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return texts;
            Send(session, new("ack"));
        }
        Assert.Fail("field presentation did not finish");
        return texts;
    }

    private void Step(CampaignSession session, Direction direction)
    {
        Send(session, new("move", Direction: direction));
        DrainField(session);
    }

    private void Face(CampaignSession session, Direction direction)
    {
        if (session.State.World.Player.Facing != direction) Send(session, new("turn", Direction: direction));
    }

    private void Open(CampaignSession session, string stock = "FuchsiaCity_Mart_Items")
    {
        Send(session, new("interact"));
        DrainField(session);
        Assert.AreEqual(stock, session.State.Shop?.ShopId);
        Assert.AreEqual(CampaignShopStage.Menu, session.State.Shop!.Stage);
        Assert.IsTrue(session.State.Script.Locked);
    }

    private void Close(CampaignSession session)
    {
        for (int n = 0; n < 10 && session.State.Shop is not null; n++) Send(session, new("shop_back"));
        Assert.IsNull(session.State.Shop);
        Assert.IsTrue(DrainField(session).Contains("Text_PleaseComeAgain"));
        Assert.IsFalse(session.State.Script.Locked);
    }

    private void Transaction(CampaignSession session, ItemId item, int quantity, bool cancel = false)
    {
        Send(session, new("shop_select", Text: item.Value));
        Assert.AreEqual(CampaignShopStage.Quantity, session.State.Shop!.Stage);
        Send(session, new("shop_quantity", Quantity: quantity));
        Assert.AreEqual(CampaignShopStage.Confirmation, session.State.Shop!.Stage);
        Send(session, new("shop_confirm", Choice: cancel ? 1 : 0));
    }

    private CampaignSession Continue(CampaignSession session)
    {
        StrictRoundtrip(session);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var save = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(content, save.State, save.Generation);
    }

    [TestMethod]
    public void DoorCounterBuySellContinueAndExitUseSharedCommands()
    {
        var session = At(City, 11, 16, Direction.North);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == City; n++) Step(session, Direction.North);
        Assert.AreEqual(Mart, session.State.World.Player.MapId.Value);
        Assert.AreEqual((4, 7), (session.State.World.Player.X, session.State.World.Player.Y));
        for (int n = 0; n < 4; n++) Step(session, Direction.North);
        Face(session, Direction.West);
        Open(session);
        Send(session, new("shop_menu", Text: "buy"));
        CollectionAssert.AreEqual(new[] { "ULTRA_BALL", "GREAT_BALL", "SUPER_POTION", "REVIVE", "FULL_HEAL", "MAX_REPEL" },
            session.VisibleShopItems.Select(item => item.Value.Replace("pret:ITEM_", "", StringComparison.Ordinal)).ToArray());
        Transaction(session, UltraBall, 3);
        Assert.AreEqual("gText_HereYouGoThankYou", session.State.Shop!.MessageSymbol);
        Assert.AreEqual(26400, session.State.Money);
        Assert.AreEqual(23, OpeningInventoryRules.Quantity(session.State.Inventory!.Balls ?? [], UltraBall));
        Send(session, new("shop_ack"));
        Transaction(session, UltraBall, 1, cancel: true);
        Assert.AreEqual(26400, session.State.Money);
        Send(session, new("shop_back")); Send(session, new("shop_ack"));
        Send(session, new("shop_menu", Text: "sell"));
        Transaction(session, UltraBall, 2);
        Assert.AreEqual("gText_TurnedOverItemsWorthYen", session.State.Shop!.MessageSymbol);
        Assert.AreEqual(27600, session.State.Money);
        Assert.AreEqual(21, OpeningInventoryRules.Quantity(session.State.Inventory!.Balls ?? [], UltraBall));
        Close(session);
        session = Continue(session);
        for (int n = 0; n < 6 && session.State.World.Player.MapId.Value == Mart; n++) Step(session, Direction.South);
        Assert.AreEqual(City, session.State.World.Player.MapId.Value);
        Assert.AreEqual((11, 16), (session.State.World.Player.X, session.State.World.Player.Y));
        session = Continue(session);
        Assert.AreEqual(27600, session.State.Money);
        Assert.AreEqual(21, OpeningInventoryRules.Quantity(session.State.Inventory!.Balls ?? [], UltraBall));
    }

    [TestMethod]
    [DataRow(6, 4, "DontTheyHaveSafariZonePennants")]
    [DataRow(9, 6, "DidYouTryXSpeed")]
    public void BothCustomersUseOriginalDialogueAndReturnToField(int x, int y, string text)
    {
        var session = At(x: x, y: y, facing: Direction.North);
        Send(session, new("interact"));
        Assert.IsTrue(DrainField(session).Contains("FuchsiaCity_Mart_Text_" + text));
        Assert.IsNull(session.State.Shop);
        Assert.IsFalse(session.State.Script.Locked);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void NoMoneyAndUnstockedSelectionDoNotMutateMoneyOrInventory()
    {
        var session = At(money: 0);
        Open(session); Send(session, new("shop_menu", Text: "buy"));
        var before = CanonicalJson.Hash(session.State.Inventory);
        var result = session.Execute("unstocked-item", session.Revision, new("shop_select", Text: "pret:ITEM_POTION"));
        Assert.AreEqual(CommandStatus.Rejected, result.Status);
        Assert.AreEqual("shop_item_not_stocked", result.Error);
        Send(session, new("shop_select", Text: UltraBall.Value));
        Assert.AreEqual("gText_YouDontHaveMoney", session.State.Shop!.MessageSymbol);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State.Inventory));
        Assert.AreEqual(0, session.State.Money);
        Close(session); StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FullPocketOrStackRejectsCommitAtomicallyAndSurvivesContinue(bool fullBallStack)
    {
        var session = At(fullBag: !fullBallStack, fullBallStack: fullBallStack);
        Open(session); Send(session, new("shop_menu", Text: "buy"));
        var before = CanonicalJson.Hash(session.State.Inventory);
        Transaction(session, fullBallStack ? UltraBall : SuperPotion, 1);
        Assert.AreEqual("gText_NoMoreRoomForThis", session.State.Shop!.MessageSymbol);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State.Inventory));
        Assert.AreEqual(30000, session.State.Money);
        Close(session); StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow("map.viridian-mart")]
    [DataRow("map.pewter-mart")]
    [DataRow("map.cerulean-city-mart")]
    [DataRow("map.vermilion-city-mart")]
    [DataRow("map.lavender-town-mart")]
    [DataRow("map.saffron-city-mart")]
    [DataRow("map.cinnabar-island-mart")]
    [DataRow("map.four-island-mart")]
    [DataRow("map.six-island-mart")]
    [DataRow("map.seven-island-mart")]
    public void ExistingOrdinaryMartsStillOpenBuyAndSaveThroughSharedBinding(string map)
    {
        var session = At(map);
        var counter = content.OrdinaryMarts.ByActor.Single(pair => pair.Key.Map.Value == map).Value;
        Open(session, counter.Stock.Id);
        Send(session, new("shop_menu", Text: "buy"));
        CollectionAssert.AreEqual(counter.Stock.Items.ToArray(), session.VisibleShopItems.ToArray());
        var item = session.ShopItem(session.VisibleShopItems[0]);
        Transaction(session, item.Id, 1);
        Assert.AreEqual("gText_HereYouGoThankYou", session.State.Shop!.MessageSymbol);
        Assert.AreEqual(30000 - item.Price, session.State.Money);
        Assert.AreEqual(item.Quantity + 1, session.ShopItem(item.Id).Quantity);
        Close(session);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow("map.celadon-city-department-store-2f", "CeladonCity_DepartmentStore_2F_Items", 1, 10, Direction.North)]
    [DataRow("map.celadon-city-department-store-2f", "CeladonCity_DepartmentStore_2F_TMs", 3, 6, Direction.West)]
    [DataRow("map.celadon-city-department-store-4f", "CeladonCity_DepartmentStore_4F_Items", 3, 11, Direction.South)]
    [DataRow("map.celadon-city-department-store-5f", "CeladonCity_DepartmentStore_5F_XItems", 1, 9, Direction.North)]
    [DataRow("map.celadon-city-department-store-5f", "CeladonCity_DepartmentStore_5F_Vitamins", 3, 6, Direction.West)]
    [DataRow("map.indigo-plateau-pokemon-center-1f", "IndigoPlateau_PokemonCenter_1F_Items", 2, 7, Direction.West)]
    [DataRow("map.three-island-mart", "ThreeIsland_Mart_Items", 4, 3, Direction.West)]
    [DataRow("map.trainer-tower-lobby", "TrainerTower_Lobby_Mart_Items", 14, 11, Direction.North)]
    public void StaticCountersBuySellAndContinueThroughSharedBinding(string map, string stock, int x, int y, Direction facing)
    {
        var session = At(map, x, y, facing);
        Open(session, stock);
        Send(session, new("shop_menu", Text: "buy"));
        var shelf = content.EarlyShop!.Shops.Single(shop => shop.Id == stock);
        CollectionAssert.AreEqual(shelf.Items.ToArray(), session.VisibleShopItems.ToArray());
        var item = session.ShopItem(session.VisibleShopItems[0]);
        Transaction(session, item.Id, 2);
        Assert.AreEqual("gText_HereYouGoThankYou", session.State.Shop!.MessageSymbol);
        Assert.AreEqual(30000 - 2 * item.Price, session.State.Money);
        Assert.AreEqual(item.Quantity + 2, session.ShopItem(item.Id).Quantity);
        Send(session, new("shop_ack"));
        Transaction(session, item.Id, 1, cancel: true);
        Assert.AreEqual(30000 - 2 * item.Price, session.State.Money);
        Send(session, new("shop_back")); Send(session, new("shop_ack"));
        Send(session, new("shop_menu", Text: "sell"));
        Transaction(session, item.Id, 1);
        Assert.AreEqual(30000 - 2 * item.Price + item.Price / 2, session.State.Money);
        Assert.AreEqual(item.Quantity + 1, session.ShopItem(item.Id).Quantity);
        Close(session);
        Continue(session);
    }

    [TestMethod]
    public void MachineShelfInsertsTmCaseAndRejectsUnstockedOrOrdinaryPurchasePaths()
    {
        var session = At("map.celadon-city-department-store-2f", 3, 6, Direction.West);
        const string stock = "CeladonCity_DepartmentStore_2F_TMs";
        Open(session, stock); Send(session, new("shop_menu", Text: "buy"));
        var tm = new ItemId("pret:ITEM_TM15");
        var wrong = session.Execute("unstocked-machine", session.Revision, new("shop_select", Text: "pret:ITEM_TM39"));
        Assert.AreEqual(CommandStatus.Rejected, wrong.Status);
        Assert.AreEqual("shop_item_not_stocked", wrong.Error);
        var before = session.State.Inventory!;
        var plain = EarlyShopRules.Buy(content.EarlyShop!, before, session.State.Money, stock, tm, 1);
        Assert.IsFalse(plain.Succeeded);
        Assert.AreEqual("shop_machine_catalog_required", plain.Error);
        Assert.AreSame(before, plain.Inventory);
        // The progressed campaign must retain earned HMs for save validation. Exercise
        // first-CASE insertion separately through the same atomic inventory transaction.
        var firstMachine = before with { Machines = [],
            KeyItems = (before.KeyItems ?? []).RemoveAll(stack => stack.Item == MachineRules.TmCaseId) };
        var inserted = EarlyShopRules.Buy(content.EarlyShop!, content.Machines!, firstMachine, 30000, stock, tm, 2);
        Assert.IsTrue(inserted.Succeeded, inserted.Error);
        Assert.AreEqual(15000, inserted.Money);
        Assert.AreEqual(2, OpeningInventoryRules.Quantity(inserted.Inventory.Machines ?? [], tm));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(inserted.Inventory.KeyItems ?? [], MachineRules.TmCaseId));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(inserted.Inventory.BagItems, tm));
        Assert.IsNull(content.Inventory.Validate(inserted.Inventory));
        int previouslyOwned = session.ShopItem(tm).Quantity;
        Transaction(session, tm, 2);
        Assert.AreEqual(15000, session.State.Money);
        Assert.AreEqual(previouslyOwned + 2, OpeningInventoryRules.Quantity(session.State.Inventory!.Machines ?? [], tm));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory.KeyItems ?? [], MachineRules.TmCaseId));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory.BagItems, tm));
        Close(session); Continue(session);
    }

    [TestMethod]
    public void MachineStackOverflowAndMissingCaseSpaceRollBackMoneyAndAllPockets()
    {
        var items = content.EarlyShop!;
        var machines = content.Machines!;
        var tm = new ItemId("pret:ITEM_TM15");
        var state = FieldFixture().Inventory!;
        var fullStack = state with { Machines = [new(tm, 999)],
            KeyItems = OpeningInventoryRules.SetQuantity(state.KeyItems ?? [], MachineRules.TmCaseId, 1) };
        var overflow = EarlyShopRules.Buy(items, machines, fullStack, 30000, "CeladonCity_DepartmentStore_2F_TMs", tm, 1);
        Assert.IsFalse(overflow.Succeeded);
        Assert.AreEqual("inventory_stack_full", overflow.Error);
        Assert.AreSame(fullStack, overflow.Inventory);
        Assert.AreEqual(30000, overflow.Money);
        var noCase = state with { Machines = [], KeyItems = (state.KeyItems ?? []).RemoveAll(stack => stack.Item == MachineRules.TmCaseId) };
        var limitedMachines = machines with { KeyItemSlots = noCase.KeyItems!.Value.Length };
        var noRoom = EarlyShopRules.Buy(items, limitedMachines, noCase, 30000, "CeladonCity_DepartmentStore_2F_TMs", tm, 1);
        Assert.IsFalse(noRoom.Succeeded);
        Assert.AreEqual("inventory_key_items_full", noRoom.Error);
        Assert.AreSame(noCase, noRoom.Inventory);
        Assert.AreEqual(30000, noRoom.Money);
    }

    [TestMethod]
    [DataRow(1, "TwoIsland_Items_ShopInitial", "TwoIsland_EventScript_ShopInitial")]
    [DataRow(2, "TwoIsland_Items_ShopExpanded1", "TwoIsland_EventScript_ShopExpanded1")]
    public void TwoIslandStillSelectsItsConditionalShelf(int scene, string stock, string script)
    {
        var session = At("map.two-island", 28, 6, Direction.North);
        var clerk = session.State.Actors.Single(pair => pair.Value.ScriptId == "TwoIsland_EventScript_Clerk");
        var state = session.State with { LastTalked = clerk.Key, Script = session.State.Script with {
            Locked = true, Wait = new(StoryWaitKind.NativeEffect, "two-island-test"),
            Variables = session.State.Script.Variables.SetItem("VAR_MAP_SCENE_TWO_ISLAND", scene) } };
        var instruction = content.FindProgram(script)!.Instructions.Single(instruction => instruction.Opcode == "pokemart");
        var effect = new StoryEffect(1, instruction.Opcode, instruction.Args, instruction.Source);
        var method = typeof(CampaignSession).GetMethod("OpenShop", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var opened = (CampaignState)method.Invoke(session, [state, effect])!;
        Assert.AreEqual(stock, opened.Shop!.ShopId);
        Assert.AreEqual(StoryWaitKind.Inventory, opened.Script.Wait!.Kind);
        var wrong = effect with { Args = [scene == 1 ? "TwoIsland_Items_ShopExpanded1" : "TwoIsland_Items_ShopInitial"] };
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(session, [state, wrong]));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
    }

    [TestMethod]
    public void DirectCounterRejectsForgedInstructionAndActorIdentity()
    {
        var session = At("map.celadon-city-department-store-5f", 1, 9, Direction.North);
        var counter = content.OrdinaryMarts.ByActor.Single(pair => pair.Value.Stock.Id == "CeladonCity_DepartmentStore_5F_XItems").Value;
        var state = session.State with { LastTalked = counter.Actor.LocalId, Script = session.State.Script with {
            Locked = true, Wait = new(StoryWaitKind.NativeEffect, "static-counter-test") } };
        var instruction = counter.Instruction;
        var effect = new StoryEffect(1, instruction.Opcode, instruction.Args, instruction.Source);
        var method = typeof(CampaignSession).GetMethod("OpenShop", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var opened = (CampaignState)method.Invoke(session, [state, effect])!;
        Assert.AreEqual(counter.Stock.Id, opened.Shop!.ShopId);
        foreach (var forged in new[] {
            effect with { Source = effect.Source with { Line = effect.Source.Line + 1 } },
            effect with { Args = ["CeladonCity_DepartmentStore_5F_Vitamins"] },
            effect with { Opcode = "pokemartdecoration" } })
        {
            var error = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(session, [state, forged]));
            Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        }
        var clerk = state.Actors[counter.Actor.LocalId];
        foreach (var forged in new[] { clerk with { Invisible = true }, clerk with { Active = false },
            clerk with { SourceLocalId = clerk.SourceLocalId + 1 }, clerk with { X = clerk.X + 1 },
            clerk with { TemplateX = clerk.TemplateX + 1 }, clerk with { GraphicsId = "OBJ_EVENT_GFX_BOY" } })
        {
            var changed = state with { Actors = state.Actors.SetItem(counter.Actor.LocalId, forged) };
            var error = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(session, [changed, effect]));
            Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        }
    }

    [TestMethod]
    public void BoughtRetroMailUsesCurrentBlankPreviewAndComposerAdmission()
    {
        var session = At("map.celadon-city-department-store-4f", 3, 11, Direction.South);
        Open(session, "CeladonCity_DepartmentStore_4F_Items");
        Send(session, new("shop_menu", Text: "buy"));
        Transaction(session, ItemIds.RetroMailId, 1);
        Assert.AreEqual(29950, session.State.Money);
        Close(session);
        Assert.IsTrue(session.CanGiveMail(ItemIds.RetroMailId));
        Send(session, new("bag_open"));
        var inventory = session.State.Inventory;
        Send(session, new("mail_preview_open", Text: ItemIds.RetroMailId.Value));
        Assert.AreEqual(CampaignMailComposePhase.Preview, session.State.MailComposer!.Phase);
        Assert.AreEqual((ushort)132, session.State.MailComposer.ItemId);
        Assert.IsTrue(session.State.MailComposer.Words.All(word => word == ushort.MaxValue));
        Send(session, new("mail_compose_back", Text: session.State.MailComposer.WaitId));
        Assert.IsNull(session.State.MailComposer);
        Assert.AreSame(inventory, session.State.Inventory);
        Send(session, new("menu_back"));
        Continue(session);
    }

}
