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
        Assert.AreEqual(151, current.Items.Length);
        Assert.AreEqual(14, current.Shops.Length);
        Assert.AreEqual("ViridianCity_Mart_Items", current.Shops[0].Id);
        Assert.AreEqual("TrainerTower_Lobby_Mart_Items", current.Shops[^1].Id);
        foreach (var item in current.Items) Assert.AreSame(shared.Definition(item.Id), item.Item);
        var potion = current.Items.Single(item => item.Id.Value == "pret:ITEM_POTION");
        Assert.AreEqual(20, potion.HealHp);
        Assert.AreEqual(EarlyShopUseSupport.Potion, potion.UseSupport);
        var fullRestore = current.Items.Single(item => item.Id.Value == "pret:ITEM_FULL_RESTORE");
        Assert.AreEqual(EarlyShopUseSupport.NoFieldUse, fullRestore.UseSupport);
        Assert.IsTrue(fullRestore.EffectBytes.IsEmpty);
        Assert.IsFalse(current.Items.Any(item => item.Id.Value == "pret:ITEM_HM08"));
        Assert.IsNotNull(shared.Definition(new("pret:ITEM_HM08")));
    }

    [TestMethod]
    public void CurrentCatalogRejectsUnknownStockAndInconsistentMedicineEffects()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/current-items/catalog.json"));
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
