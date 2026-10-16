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

}
