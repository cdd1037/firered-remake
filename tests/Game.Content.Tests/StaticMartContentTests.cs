using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class StaticMartContentTests
{
    [TestMethod]
    [DataRow("map.celadon-city-department-store-2f", "CeladonCity_DepartmentStore_2F_Items", "GREAT_BALL SUPER_POTION REVIVE ANTIDOTE PARALYZE_HEAL AWAKENING BURN_HEAL ICE_HEAL SUPER_REPEL")]
    [DataRow("map.celadon-city-department-store-2f", "CeladonCity_DepartmentStore_2F_TMs", "TM05 TM15 TM28 TM31 TM43 TM45")]
    [DataRow("map.celadon-city-department-store-4f", "CeladonCity_DepartmentStore_4F_Items", "POKE_DOLL RETRO_MAIL FIRE_STONE THUNDER_STONE WATER_STONE LEAF_STONE")]
    [DataRow("map.celadon-city-department-store-5f", "CeladonCity_DepartmentStore_5F_XItems", "X_ATTACK X_DEFEND X_SPEED X_SPECIAL X_ACCURACY GUARD_SPEC DIRE_HIT")]
    [DataRow("map.celadon-city-department-store-5f", "CeladonCity_DepartmentStore_5F_Vitamins", "HP_UP PROTEIN IRON CALCIUM ZINC CARBOS")]
    [DataRow("map.indigo-plateau-pokemon-center-1f", "IndigoPlateau_PokemonCenter_1F_Items", "ULTRA_BALL GREAT_BALL FULL_RESTORE MAX_POTION REVIVE FULL_HEAL MAX_REPEL")]
    public void StaticShelvesBindExactActorsAndInstructionsToCanonicalItems(string map, string stock, string orderedItems)
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var bound = catalog.OrdinaryMarts.ByActor.Single(pair => pair.Key.Map == new MapId(map) && pair.Value.Stock.Id == stock).Value;
        Assert.AreSame(catalog.EarlyShop!.Shops.Single(shop => shop.Id == stock), bound.Stock);
        Assert.AreEqual(bound.Actor.ScriptId, bound.Instruction.Source.Symbol);
        Assert.AreEqual("pokemart", bound.Instruction.Opcode);
        CollectionAssert.AreEqual(new[] { stock }, bound.Instruction.Args.ToArray());
        CollectionAssert.AreEqual(orderedItems.Split(' '), bound.Stock.Items.Select(item => item.Value[10..]).ToArray());
        Assert.IsNull(catalog.DeferredRegionalServiceReason(new(map), bound.Actor.ScriptId));
        foreach (var id in bound.Stock.Items)
        {
            var item = catalog.EarlyShop.Items.Single(item => item.Id == id);
            Assert.AreSame(catalog.Inventory.Items[id], item.Item);
            Assert.IsTrue(item.Price > 0);
            if (item.Pocket == "POCKET_TM_CASE")
                Assert.AreSame(MachineRules.Definition(catalog.Machines!, id)!.Machine.Item, item.Item);
        }
    }

    [TestMethod]
    public void ConditionalTwoIslandClerkIsNotMistakenForDirectStaticStock()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        Assert.IsFalse(catalog.OrdinaryMarts.ByActor.Keys.Any(key => key.Map.Value == "map.two-island"));
        foreach (string id in new[] { "TwoIsland_Items_ShopInitial", "TwoIsland_Items_ShopExpanded1" })
            Assert.IsTrue(catalog.EarlyShop!.Shops.Any(shop => shop.Id == id));
    }
}
