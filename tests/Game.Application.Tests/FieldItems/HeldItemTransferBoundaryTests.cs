using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.FieldItems;

[TestClass, TestCategory("Game")]
public sealed class HeldItemTransferBoundaryTests
{
    public static IEnumerable<object[]> AcquiredItems => new[]
    {
        "ITEM_SPELL_TAG", "ITEM_FOCUS_BAND", "ITEM_THICK_CLUB", "ITEM_POISON_BARB", "ITEM_SHARP_BEAK",
        "ITEM_LUCKY_EGG", "ITEM_SILVER_POWDER", "ITEM_METAL_POWDER", "ITEM_TWISTED_SPOON", "ITEM_MAGNET",
        "ITEM_SPELON_BERRY", "ITEM_PAMTRE_BERRY", "ITEM_WATMEL_BERRY", "ITEM_DURIN_BERRY", "ITEM_BELUE_BERRY"
    }.Select(item => new object[] { item });

    [TestMethod]
    public void EveryActualWildAndPickupAwardUsesTheCompleteInventoryDefinition()
    {
        var c = CurrentCampaignFixture.Catalog;
        var species = c.LandEncounters!.Values.SelectMany(t => t.Slots.Select(s => s.Species))
            .Concat(c.WaterEncounters!.Values.SelectMany(t => t.Slots.Select(s => s.Species)))
            .Concat(c.FishingTables!.Tables.Values.SelectMany(t => t.Slots.Select(s => s.Species))).Distinct();
        var awards = species.Select(s => c.GamePokemon.Mechanics.SpeciesMechanics[s])
            .SelectMany(s => new[] { s.ItemCommon, s.ItemRare }).Concat(VermilionPickupRules.Items)
            .Where(s => s != "ITEM_NONE").Distinct().ToArray();
        Assert.IsTrue(awards.Length > 15);
        foreach (var award in awards)
        {
            var id = new ItemId("pret:" + award);
            var ordinary = c.EarlyShop!.Items.SingleOrDefault(i => i.Id == id);
            var machine = c.Machines.Machines.SingleOrDefault(m => m.Machine.Item.Id == id)?.Machine;
            Assert.AreSame(c.Inventory.Items[id], ordinary?.Item ?? machine?.Item, award);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ordinary?.Description ?? machine?.Description), award);
            Assert.IsTrue(HeldItemTransferRules.CanHold(c.EarlyShop, id, c.Machines), award);
        }
    }

    [TestMethod, DynamicData(nameof(AcquiredItems))]
    public void PartyTakeGiveAndPcMoveItemsPreserveEveryAcquiredItem(string symbol)
    {
        var c = CurrentCampaignFixture.Catalog;
        var state = HeldFixture(symbol);
        var pokemon = state.Party[0];
        var id = new ItemId("pret:" + symbol);
        var session = CampaignSession.RestoreCurrent(c, state, 0);
        int count = Count(state.Inventory!, id);
        Send(session, new("party_item_open", PartyIndex: 0) { TargetInstanceId = pokemon.InstanceId });
        Send(session, new("field_take", PartyIndex: 0) { TargetInstanceId = pokemon.InstanceId });
        Assert.AreEqual(count + 1, Count(session.State.Inventory!, id));
        Assert.AreEqual(pokemon with { HeldItem = "ITEM_NONE" }, session.State.Party[0]);
        Send(session, new("held_bag_open", PartyIndex: 0) { TargetInstanceId = pokemon.InstanceId });
        if (c.Inventory.Items[id].Pocket == "POCKET_BERRY_POUCH") Send(session, new("berry_open"));
        Send(session, new("field_give", Text: id.Value, PartyIndex: 0) { TargetInstanceId = pokemon.InstanceId });
        Send(session, new("menu_back"));
        Assert.AreEqual(pokemon, session.State.Party[0]);
        Assert.AreEqual(count, Count(session.State.Inventory!, id));
        Assert.IsNull(session.ValidatePlayerSave(session.State));

        var stable = session.State;
        Set(session, stable with { Pc = new(CampaignPcView.MoveItems), PcPoweredOn = true,
            Script = stable.Script with { Wait = new(StoryWaitKind.Presentation, "held-boundary-pc") } });
        CampaignCommand Target(string kind, string held) => new(kind)
        {
            StorageLocation = PokemonStorageMoveLocation.Party(0), TargetInstanceId = pokemon.InstanceId,
            TargetHeldItemId = "pret:" + held, HeldItemId = id.Value
        };
        Send(session, Target("storage_item_take", symbol));
        Send(session, Target("storage_item_give", "ITEM_NONE"));
        Assert.AreEqual(pokemon, session.State.Party[0]);
        Send(session, Target("storage_item_take", symbol));
        Send(session, new("pc_back"));
        Send(session, new("storage_item_prompt", Choice: 0) { HeldItemId = id.Value });
        Assert.IsNull(session.State.Pc!.ItemCursor);
        Assert.AreEqual(count + 1, Count(session.State.Inventory!, id));
        Send(session, Target("storage_item_bag_open", "ITEM_NONE"));
        Send(session, Target("storage_item_from_bag", "ITEM_NONE") with { Text = id.Value });
        Assert.AreEqual(pokemon, session.State.Party[0]);
        Assert.AreEqual(count, Count(session.State.Inventory!, id));
        Assert.AreEqual(state.World.Rng, session.State.World.Rng);
        Assert.IsNull(session.ValidatePlayerSave(session.State with { Pc = null, PcPoweredOn = false, Script = stable.Script }));
    }

    [TestMethod]
    [DataRow("party")]
    [DataRow("exchange")]
    [DataRow("pc_direct")]
    [DataRow("pc_cursor")]
    public void FirstHeldBerryAtomicallyAcquiresPouchAndFlag(string route)
    {
        var c = CurrentCampaignFixture.Catalog;
        var state = HeldFixture("ITEM_SPELON_BERRY");
        state = state with { Inventory = state.Inventory! with { Berries = [],
                KeyItems = state.Inventory!.KeyItems!.Value.RemoveAll(s => s.Item == SsAnneItemRules.BerryPouchId) },
            Script = state.Script with { Flags = state.Script.Flags.Remove("FLAG_SYS_GOT_BERRY_POUCH") } };
        Assert.IsNull(CampaignSession.CreateCurrent(c).ValidatePlayerSave(state));
        var session = CampaignSession.RestoreCurrent(c, state, 0);
        long instance = state.Party[0].InstanceId;
        if (route.StartsWith("pc_", StringComparison.Ordinal))
        {
            Set(session, state with { Pc = new(CampaignPcView.MoveItems), PcPoweredOn = true,
                Script = state.Script with { Wait = new(StoryWaitKind.Presentation, "held-boundary-pc") } });
            Send(session, new(route == "pc_direct" ? "storage_item_to_bag" : "storage_item_take")
            { StorageLocation = PokemonStorageMoveLocation.Party(0), TargetInstanceId = instance, TargetHeldItemId = "pret:ITEM_SPELON_BERRY" });
            if (route == "pc_cursor")
            {
                Send(session, new("pc_back"));
                Send(session, new("storage_item_prompt", Choice: 0) { HeldItemId = "pret:ITEM_SPELON_BERRY" });
            }
        }
        else
        {
            if (route == "exchange")
            {
                var inventory = EarlyInventoryAddRules.Add(c.EarlyShop!, session.State.Inventory!, OpeningInventoryRules.PotionId, 1);
                Assert.IsTrue(inventory.Succeeded, inventory.Error);
                Set(session, session.State with { Inventory = inventory.State });
            }
            Send(session, new("party_item_open", PartyIndex: 0) { TargetInstanceId = instance });
            if (route == "exchange") Send(session, new("held_bag_open", PartyIndex: 0) { TargetInstanceId = instance });
            Send(session, new(route == "exchange" ? "field_give" : "field_take", Text: OpeningInventoryRules.PotionId.Value,
                PartyIndex: 0, Choice: 1) { TargetInstanceId = instance });
        }
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.KeyItems ?? [], SsAnneItemRules.BerryPouchId));
        Assert.AreEqual(1, Count(session.State.Inventory, new("pret:ITEM_SPELON_BERRY")));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_SYS_GOT_BERRY_POUCH"));
        var settled = session.State with { Pc = null, PcPoweredOn = false, ItemMenu = null,
            Script = state.Script with { Flags = session.State.Script.Flags } };
        Assert.IsNull(session.ValidatePlayerSave(settled));
        var loaded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(settled, 1, session.RulesVersion, c.ContentVersion),
            session.RulesVersion, c.ContentVersion, validate: session.ValidatePlayerSave);
        Assert.AreEqual(CanonicalJson.Hash(settled), CanonicalJson.Hash(loaded.State));
    }

    [TestMethod]
    public void FullDestinationRejectsHeldReturnWithoutLosingThePokemonOrItem()
    {
        var c = CurrentCampaignFixture.Catalog;
        var state = HeldFixture("ITEM_LUCKY_EGG");
        var full = c.EarlyShop!.Items.Where(i => i.Pocket == "POCKET_ITEMS" && i.Id.Value != "pret:ITEM_LUCKY_EGG")
            .Take(state.Inventory!.Limits.BagItemSlots).Select(i => new InventoryStack(i.Id, 1)).ToImmutableArray();
        Assert.AreEqual(state.Inventory.Limits.BagItemSlots, full.Length);
        state = state with { Inventory = state.Inventory! with { BagItems = full } };
        var result = HeldItemTransferRules.Take(c.EarlyShop, state.Inventory, state.Party[0], c.Machines);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("inventory_destination_full", result.Error);
        Assert.AreSame(state.Inventory, result.Inventory);
        Assert.AreSame(state.Party[0], result.Pokemon);
    }

    [TestMethod]
    public void FirstBerryWithNoPouchSpaceRollsBackTakeExchangeAndPcCursor()
    {
        var c = CurrentCampaignFixture.Catalog;
        var state = HeldFixture("ITEM_SPELON_BERRY");
        var inventory = state.Inventory! with { Berries = [],
            KeyItems = state.Inventory!.KeyItems!.Value.RemoveAll(s => s.Item == SsAnneItemRules.BerryPouchId) };
        var added = EarlyInventoryAddRules.Add(c.EarlyShop!, inventory, OpeningInventoryRules.PotionId, 1);
        Assert.IsTrue(added.Succeeded, added.Error);
        inventory = added.State;
        // Deliberately tighten this immutable test catalog to exercise the atomic capacity boundary.
        var keyLimits = c.EarlyShop!.QuestLimits with { KeyItemSlots = inventory.KeyItems!.Value.Length };
        var items = c.EarlyShop with { QuestLimits = keyLimits, Inventory = new(c.Inventory.Items.Values,
            c.Inventory.Limits, keyLimits, c.Inventory.MachineSlots, c.Inventory.Singletons) };
        var holder = state.Party[0];
        foreach (var result in new[]
        {
            HeldItemTransferRules.Take(items, inventory, holder, c.Machines),
            HeldItemTransferRules.Give(items, inventory, holder, OpeningInventoryRules.PotionId, false, true, c.Machines)
        })
        {
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("inventory_key_items_full", result.Error);
            Assert.AreSame(inventory, result.Inventory);
            Assert.AreSame(holder, result.Pokemon);
        }
        var cursor = new PokemonStorageItemCursor(new("pret:ITEM_SPELON_BERRY"));
        var party = state.Party.SetItem(0, holder with { HeldItem = "ITEM_NONE" });
        var returned = PokemonStorageItemRules.CursorToBag(c.PokemonContent, items, party, state.Storage!, inventory,
            cursor, cursor.Item, c.Machines);
        Assert.IsFalse(returned.Succeeded);
        Assert.AreEqual("inventory_key_items_full", returned.Diagnostic!.Code);
        Assert.AreSame(inventory, returned.Inventory);
        Assert.AreSame(cursor, returned.Cursor);
        Assert.AreEqual(party, returned.Party);
    }

    private static CampaignState HeldFixture(string symbol)
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        Assert.IsFalse(state.Party[0].IsEgg);
        Assert.AreEqual(255, state.Party[0].Mail);
        // A controlled already-owned holder; provenance is unchanged and only its mutable award varies.
        return state with { Party = state.Party.SetItem(0, state.Party[0] with { HeldItem = symbol }) };
    }
    private static int Count(InventoryState inventory, ItemId item) => OpeningInventoryRules.Quantity(
        CurrentCampaignFixture.Catalog.Inventory.Items[item].Pocket == "POCKET_BERRY_POUCH" ? inventory.Berries ?? [] : inventory.BagItems, item);
    private static void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, session.Revision));
    private static void Send(CampaignSession session, CampaignCommand command)
    {
        var outcome = session.Execute("held-boundary:" + session.Revision, session.Revision, command);
        Assert.AreEqual(CommandStatus.Accepted, outcome.Status, command.Kind + ": " + outcome.Error);
    }
}
