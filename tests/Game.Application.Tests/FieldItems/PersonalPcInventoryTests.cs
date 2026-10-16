using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.FieldItems;

[TestClass, TestCategory("Game")]
public sealed class PersonalPcInventoryTests
{
    [TestMethod]
    [DataRow("ITEM_ANTIDOTE")]
    [DataRow("ITEM_LUCKY_EGG")]
    [DataRow("ITEM_ORANGE_MAIL")]
    public void OrdinaryItemsDepositWithdrawPartialQuantitiesAndSave(string symbol)
    {
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var id = new ItemId("pret:" + symbol);
        var inventory = baseline.Inventory! with { BagItems = [new(id, 7)], PcItems = [] };
        var session = Open(baseline with { Inventory = inventory }, "storage");
        Send(session, new("menu_select", Text: "deposit"));
        var original = session.State.Inventory;
        Send(session, new("menu_back"));
        Assert.AreSame(original, session.State.Inventory);
        Send(session, new("menu_select", Text: "deposit"));
        Send(session, new("pc_deposit", Text: id.Value, Quantity: 3));
        Assert.AreEqual(4, Quantity(session.State.Inventory!.BagItems, id));
        Assert.AreEqual(3, Quantity(session.State.Inventory.PcItems, id));
        StringAssert.Contains(session.VisibleItemMessage!, CurrentCampaignFixture.Catalog.Inventory.Items[id].Name);
        Send(session, new("menu_ack")); Send(session, new("menu_back"));
        Send(session, new("menu_select", Text: "withdraw"));
        Send(session, new("pc_withdraw", Text: id.Value, Quantity: 2));
        Send(session, new("menu_ack"));
        Assert.AreEqual(6, Quantity(session.State.Inventory.BagItems, id));
        Assert.AreEqual(1, Quantity(session.State.Inventory.PcItems, id));
        Assert.AreEqual(baseline.Party, session.State.Party);
        Assert.AreEqual(baseline.World.Rng, session.State.World.Rng);
        AssertSave(session, baseline);
    }

    [TestMethod]
    public void FullPcBagAndStackRejectWithoutLossAndOtherPocketsCannotDeposit()
    {
        var c = CurrentCampaignFixture.Catalog;
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var id = new ItemId("pret:ITEM_ORANGE_MAIL");
        var ordinary = c.Inventory.Items.Values.Where(i => i.Pocket == "POCKET_ITEMS" && i.Id != id)
            .OrderBy(i => i.SourceItemId).Select(i => new InventoryStack(i.Id, 1)).ToImmutableArray();
        var session = Open(baseline with { Inventory = baseline.Inventory! with {
            BagItems = [new(id, 7)], PcItems = ordinary.Take(30).ToImmutableArray() } }, "deposit");
        RejectUnchanged(session, new("pc_deposit", Text: id.Value), "inventory_destination_full");
        Set(session, session.State with { Inventory = session.State.Inventory! with { PcItems = [new(id, 999)] } });
        RejectUnchanged(session, new("pc_deposit", Text: id.Value), "inventory_stack_full");
        foreach (string symbol in new[] { "ITEM_POKE_BALL", "ITEM_ORAN_BERRY", "ITEM_TM01", "ITEM_BICYCLE" })
            RejectUnchanged(session, new("pc_deposit", Text: "pret:" + symbol), "inventory_item_pocket");
        RejectUnchanged(session, new("pc_deposit", Text: id.Value, Quantity: 0), "inventory_transfer_quantity");
        RejectUnchanged(session, new("pc_deposit", Text: id.Value, Quantity: 8), "inventory_insufficient_quantity");
        Set(session, session.State with { ItemMenu = new("pc", "withdraw"), Inventory = session.State.Inventory! with {
            BagItems = ordinary.Take(42).ToImmutableArray(), PcItems = [new(id, 7)] } });
        RejectUnchanged(session, new("pc_withdraw", Text: id.Value), "inventory_destination_full");
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(true, false)]
    public void MailComposerCancelsAtomicallyThenGivesAndPreservesIdentity(bool exchange, bool fromPc)
    {
        var c = CurrentCampaignFixture.Catalog;
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var mailId = new ItemId("pret:ITEM_ORANGE_MAIL");
        var holder = baseline.Party[0] with { HeldItem = exchange ? "ITEM_LUCKY_EGG" : "ITEM_NONE", Mail = 255 };
        var session = Open(baseline with { Party = baseline.Party.SetItem(0, holder),
            Inventory = baseline.Inventory! with { BagItems = [], PcItems = [new(mailId, 2)] } }, "withdraw");
        if (!fromPc) Set(session, session.State with { PcPoweredOn = false, ItemMenu = new("bag", "bag"),
            Inventory = session.State.Inventory! with { BagItems = [new(mailId, 2)], PcItems = [] } });
        var inventory = session.State.Inventory;
        var mail = session.State.Mail;
        var give = new CampaignCommand(fromPc ? "pc_give" : "field_give", Text: mailId.Value, Choice: exchange ? 1 : null, PartyIndex: 0)
            { TargetInstanceId = holder.InstanceId };
        if (exchange) RejectUnchanged(session, give with { Choice = null }, "mail_replace_confirmation_required");
        Send(session, give);
        Assert.AreEqual(fromPc ? MailStationeryOrigin.Pc : MailStationeryOrigin.Bag, session.State.MailComposer!.Origin);
        Assert.AreSame(inventory, session.State.Inventory);
        Assert.AreEqual(holder, session.State.Party[0]);
        Compose(session, "mail_compose_cancel"); Compose(session, "mail_compose_confirm", choice: 0);
        Assert.IsNull(session.State.MailComposer); Assert.AreSame(inventory, session.State.Inventory);
        Assert.AreSame(mail, session.State.Mail); Assert.AreEqual(holder, session.State.Party[0]);
        Send(session, give);
        ushort word = c.MailComposer!.Words.Values.First(w => SourceEasyChatRules.IsSelectable(c.MailComposer, session.MailSelectionFacts, w.Value)).Value;
        Compose(session, "mail_compose_word", choice: 0, quantity: word);
        Compose(session, "mail_compose_done"); Compose(session, "mail_compose_confirm", choice: 0);
        Assert.IsNull(session.State.MailComposer);
        Assert.AreEqual(fromPc ? 1 : 0, Quantity(session.State.Inventory!.PcItems, mailId));
        Assert.AreEqual(fromPc ? 0 : 1, Quantity(session.State.Inventory.BagItems, mailId));
        Assert.AreEqual(exchange ? 1 : 0, Quantity(session.State.Inventory.BagItems, new("pret:ITEM_LUCKY_EGG")));
        var result = session.State.Party[0];
        Assert.AreEqual("ITEM_ORANGE_MAIL", result.HeldItem);
        Assert.AreEqual(holder with { HeldItem = result.HeldItem, Mail = result.Mail }, result);
        Assert.AreEqual(word, session.State.Mail!.Attached[result.Mail]!.Words[0]);
        Assert.AreEqual(fromPc ? "withdraw" : "bag", session.State.ItemMenu!.View);
        Assert.AreEqual(baseline.World.Rng, session.State.World.Rng);
        AssertSave(session, baseline);
    }

    [TestMethod]
    public void PcMailExchangeWithFullBagRejectsBeforeComposerOrConsumption()
    {
        var c = CurrentCampaignFixture.Catalog;
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var holder = baseline.Party[0] with { HeldItem = "ITEM_LUCKY_EGG", Mail = 255 };
        var full = c.Inventory.Items.Values.Where(i => i.Pocket == "POCKET_ITEMS" && i.SourceItemSymbol != "ITEM_LUCKY_EGG")
            .Take(42).Select(i => new InventoryStack(i.Id, 1)).ToImmutableArray();
        var session = Open(baseline with { Party = baseline.Party.SetItem(0, holder), Inventory = baseline.Inventory! with {
            BagItems = full, PcItems = [new(new("pret:ITEM_ORANGE_MAIL"), 1)] } }, "withdraw");
        RejectUnchanged(session, new("pc_give", Text: "pret:ITEM_ORANGE_MAIL", Choice: 1, PartyIndex: 0)
            { TargetInstanceId = holder.InstanceId }, "inventory_destination_full");
        Assert.IsNull(session.State.MailComposer);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void SelectReorderMovesWholeStackAndSavePreservesOrder(int pocket)
    {
        var c = CurrentCampaignFixture.Catalog;
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var kind = (InventoryOrderPocket)pocket;
        var inventory = baseline.Inventory!;
        string sourcePocket = pocket switch { 1 => "POCKET_KEY_ITEMS", 2 => "POCKET_POKE_BALLS", _ => "POCKET_ITEMS" };
        var stacks = c.Inventory.Items.Values.Where(i => i.Pocket == sourcePocket).OrderBy(i => i.SourceItemId)
            .Take(3).Select(i => new InventoryStack(i.Id, 1)).ToImmutableArray();
        if (kind == InventoryOrderPocket.KeyItems) stacks = inventory.KeyItems!.Value;
        Assert.IsTrue(stacks.Length >= 3);
        inventory = kind switch {
            InventoryOrderPocket.Items => inventory with { BagItems = stacks },
            InventoryOrderPocket.KeyItems => inventory with { KeyItems = stacks },
            InventoryOrderPocket.Balls => inventory with { Balls = stacks }, _ => inventory with { PcItems = stacks } };
        var session = pocket == 3 ? Open(baseline with { Inventory = inventory }, "withdraw")
            : CampaignSession.RestoreCurrent(c, baseline with { Inventory = inventory }, 0);
        if (pocket != 3) { Set(session, session.State with { ItemMenu = new("bag", "bag") { Pocket = pocket } }); }
        Send(session, new("item_move", Text: stacks[0].Item.Value, Choice: 0, Quantity: stacks.Length));
        var moved = InventoryOrderRules.Pocket(session.State.Inventory!, kind);
        CollectionAssert.AreEqual(stacks.Skip(1).Append(stacks[0]).ToArray(), moved.ToArray());
        Assert.AreSame(stacks[0], moved[^1]);
        RejectUnchanged(session, new("item_move", Text: stacks[0].Item.Value, Choice: 0, Quantity: 1), "inventory_move_stale");
        RejectUnchanged(session, new("item_move", Text: stacks[1].Item.Value, Choice: 0, Quantity: stacks.Length + 1), "inventory_move_index");
        Send(session, new("item_move", Text: stacks[0].Item.Value, Choice: stacks.Length - 1, Quantity: 0));
        CollectionAssert.AreEqual(stacks.ToArray(), InventoryOrderRules.Pocket(session.State.Inventory!, kind).ToArray());
        AssertSave(session, baseline);
    }

    [TestMethod]
    public void ReorderRejectsBerryPouchDepositAndTargetSelectionContexts()
    {
        var baseline = CurrentCampaignFixture.Checkpoint.State;
        var session = Open(baseline, "deposit");
        var move = new CampaignCommand("item_move", Text: "pret:ITEM_POTION", Choice: 0, Quantity: 1);
        RejectUnchanged(session, move, "inventory_move_context");
        Set(session, session.State with { PcPoweredOn = false, ItemMenu = new("bag", "berries") });
        RejectUnchanged(session, move, "inventory_move_context");
        Set(session, session.State with { ItemMenu = new("bag", "bag") { TargetInstanceId = baseline.Party[0].InstanceId, TargetPartyIndex = 0 } });
        RejectUnchanged(session, move, "inventory_move_context");
    }

    private static CampaignSession Open(CampaignState state, string view)
    {
        var session = CampaignSession.RestoreCurrent(CurrentCampaignFixture.Catalog, state, 0);
        Set(session, state with { PcPoweredOn = true, ItemMenu = new("pc", view) });
        return session;
    }
    private static int Quantity(ImmutableArray<InventoryStack> stacks, ItemId item) => OpeningInventoryRules.Quantity(stacks, item);
    private static void Compose(CampaignSession session, string kind, int? choice = null, int quantity = 1) =>
        Send(session, new(kind, Text: session.State.MailComposer!.WaitId, Choice: choice, Quantity: quantity));
    private static void AssertSave(CampaignSession session, CampaignState baseline)
    {
        var stable = session.State with { ItemMenu = null, PcPoweredOn = false, Script = baseline.Script };
        Assert.IsNull(session.ValidatePlayerSave(stable));
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(stable, 1, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion),
            session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, validate: session.ValidatePlayerSave);
        Assert.AreEqual(CanonicalJson.Hash(stable), CanonicalJson.Hash(decoded.State));
    }
    private static void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, session.Revision));
    private static void Send(CampaignSession session, CampaignCommand command)
    {
        var result = session.Execute("pc-items:" + session.Revision, session.Revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, command.Kind + ": " + result.Error);
    }
    private static void RejectUnchanged(CampaignSession session, CampaignCommand command, string error)
    {
        var before = session.State; long revision = session.Revision;
        var result = session.Execute("pc-items-reject:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status, command.Kind + ": " + result.Error);
        Assert.AreEqual(error, result.Error); Assert.AreSame(before, session.State); Assert.AreEqual(revision, session.Revision);
    }
}
