using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CurrentItemCatalogTests
{
    [TestMethod]
    public void CurrentItemsShareDefinitionsAndKeepUseAdmissionSeparate()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var current = CurrentItemCatalog.Load(TestAssets.Root, shared);
        Assert.IsFalse(current.Items.IsDefaultOrEmpty);
        Assert.IsFalse(current.Shops.IsDefaultOrEmpty);
        Assert.AreEqual(current.Shops.Length, current.Shops.Select(shop => shop.Id).Distinct().Count());
        Assert.AreEqual("ViridianCity_Mart_Items", current.Shops[0].Id);
        Assert.AreEqual("TrainerTower_Lobby_Mart_Items", current.Shops[^1].Id);
        foreach (var item in current.Items) Assert.AreSame(shared.Definition(item.Id), item.Item);
        var potion = current.Items.Single(item => item.Id.Value == "pret:ITEM_POTION");
        Assert.AreEqual(20, potion.HealHp);
        Assert.AreEqual(EarlyShopUseSupport.Potion, potion.UseSupport);
        var fullRestore = current.Items.Single(item => item.Id.Value == "pret:ITEM_FULL_RESTORE");
        Assert.AreEqual(EarlyShopUseSupport.NoFieldUse, fullRestore.UseSupport);
        Assert.IsTrue(fullRestore.EffectBytes.IsEmpty);
        var amulet = current.Items.Single(item => item.Id.Value == "pret:ITEM_AMULET_COIN");
        Assert.AreEqual(EarlyShopUseSupport.NoFieldUse, amulet.UseSupport);
        Assert.AreEqual("HOLD_EFFECT_DOUBLE_PRIZE", amulet.Item.HoldEffect);
        Assert.IsTrue(amulet.Item.CanGive);
        var icons = PretEarlyShop.LoadPresentation(TestAssets.Root, current).Icons;
        var icon = icons.Single(i => i.Item == amulet.Id);
        Assert.AreEqual(24, icon.Width);
        Assert.AreEqual(24, icon.Height);
        Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, icon.PngPath)));
        Assert.IsFalse(current.Items.Any(item => item.Id.Value == "pret:ITEM_HM08"));
        Assert.IsNotNull(shared.Definition(new("pret:ITEM_HM08")));
    }

    [TestMethod]
    public void CurrentCatalogUsesExplicitUniqueAdmissionRatherThanHistoricalCounts()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/available-items.json")))!;
        var potion = original["items"]!.AsArray().Single(row => row!["item"]!.GetValue<string>() == "pret:ITEM_POTION")!;
        original["items"] = new JsonArray(potion.DeepClone());
        original["shops"] = new JsonArray(new JsonObject
        {
            ["id"] = "PotionCounter",
            ["items"] = new JsonArray(new JsonObject { ["value"] = "pret:ITEM_POTION" })
        });
        var catalog = CurrentItemCatalog.Parse(original.ToJsonString(), shared);
        Assert.AreEqual(1, catalog.Items.Length);
        Assert.AreEqual(1, catalog.Shops.Length);
        Assert.AreSame(shared.Definition(OpeningInventoryRules.PotionId), catalog.Items[0].Item);
        original["items"]!.AsArray().Add(potion.DeepClone());
        Assert.ThrowsExactly<InvalidDataException>(() => CurrentItemCatalog.Parse(original.ToJsonString(), shared));
    }

    [TestMethod]
    public void CurrentCatalogRejectsUnknownStockAndInconsistentMedicineEffects()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/available-items.json"));
        var invalid = JsonNode.Parse(json)!;
        invalid["shops"]![0]!["items"]![0]!["value"] = "pret:ITEM_HM08";
        Assert.ThrowsExactly<InvalidDataException>(() => CurrentItemCatalog.Parse(invalid.ToJsonString(), shared));
        invalid = JsonNode.Parse(json)!;
        invalid["items"]!.AsArray().Single(row => row!["item"]!.GetValue<string>() == "pret:ITEM_POTION")!["healHp"] = 999;
        Assert.ThrowsExactly<InvalidDataException>(() => CurrentItemCatalog.Parse(invalid.ToJsonString(), shared));
        invalid = JsonNode.Parse(json)!;
        invalid["items"]![0]!["item"] = "pret:ITEM_NONE";
        Assert.ThrowsExactly<InvalidDataException>(() => CurrentItemCatalog.Parse(invalid.ToJsonString(), shared));
    }
}
