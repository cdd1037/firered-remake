using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedItemCatalogTests
{
    [TestMethod]
    public void InventoryMachineAndBattleViewsResolveTheSameBusinessItem()
    {
        var machines = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var items = machines.Items;
        var potion = items.Definition(new("pret:ITEM_POTION"));
        var early = PretEarlyShop.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/services/early-shop/early-shop.json")), items);
        var opening = PretOpeningItems.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/items/opening/opening-items.json")), items);
        var battle = SourceBattleItemCatalogLoader.LoadAuthenticated(TestAssets.Root, items);
        Assert.AreSame(potion, early.Catalog.Items.Single(i => i.Id == potion.Id).Item);
        Assert.AreSame(potion, opening.Potion.Item);
        Assert.AreSame(potion, battle.Items[potion.Id].Item);
        Assert.AreSame(items.Definition(new("pret:ITEM_TM39")), machines.Definition(new("pret:ITEM_TM39")).Machine.Item);
        Assert.AreEqual(20, opening.Potion.HealHp);
        Assert.AreEqual(300, potion.Price);
        Assert.IsTrue(potion.CanGive && potion.CanSell && potion.CanToss);
        Assert.AreEqual(1, items.Definition(new("pret:ITEM_GOOD_ROD")).SecondaryId);
    }

    [TestMethod]
    public void SelectionDoesNotAdmitOtherKnownItemsOrUnsupportedMachines()
    {
        var items = SharedItemCatalog.LoadProject(TestAssets.Root);
        var selected = items.Select([new("pret:ITEM_POTION"), new("pret:ITEM_POTION")]);
        Assert.AreEqual(1, selected.Count);
        Assert.IsFalse(selected.ContainsKey(new("pret:ITEM_MASTER_BALL")));
        Assert.IsNotNull(items.Definition(new("pret:ITEM_HM08")));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedMachineCatalog.LoadProject(TestAssets.Root).Definition(new("pret:ITEM_HM08")));
        Assert.ThrowsExactly<InvalidDataException>(() => items.Definition(new("pret:ITEM_NONE")));
    }

    [TestMethod]
    public void EveryBusinessItemHasAContentOwnedDescriptionWithoutInventoryAdmission()
    {
        var items = SharedItemCatalog.LoadProject(TestAssets.Root, SourceTextPack.Load(null));
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/items.json")))!;
        var descriptions = document["descriptions"]!.AsObject();
        Assert.AreEqual(document["items"]!.AsArray().Count, descriptions.Count);
        foreach (var row in document["items"]!.AsArray())
        {
            var id = new ItemId(row!["id"]!["value"]!.GetValue<string>());
            Assert.AreEqual(descriptions[id.Value]!.GetValue<string>(), items.Description(id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(items.Description(id)));
        }
        Assert.IsFalse(string.IsNullOrWhiteSpace(items.Description(new("pret:ITEM_TEA"))));
        Assert.IsFalse(string.IsNullOrWhiteSpace(items.Description(new("pret:ITEM_HM08"))));
        Assert.ThrowsExactly<InvalidDataException>(() => items.Description(new("pret:ITEM_NONE")));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DescriptionMapIsRequiredAndCannotBeNull(bool remove)
    {
        using var temp = TestAssets.TemporaryOutput();
        const string path = "content/generated/data/items.json";
        TestAssets.CopyFiles(temp.Path, [path]);
        TestAssets.EditJson(temp.Path, path, document =>
        {
            if (remove) document.AsObject().Remove("descriptions");
            else document["descriptions"] = null;
        });
        Assert.ThrowsExactly<ContentValidationException>(() => SharedItemCatalog.LoadProject(temp.Path));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("unknown")]
    [DataRow("extra")]
    [DataRow("empty")]
    [DataRow("whitespace")]
    [DataRow("null")]
    [DataRow("old-schema")]
    public void DescriptionMapRejectsIncompleteUnknownOrEmptyEntries(string change)
    {
        using var temp = TestAssets.TemporaryOutput();
        const string path = "content/generated/data/items.json";
        TestAssets.CopyFiles(temp.Path, [path]);
        TestAssets.EditJson(temp.Path, path, document =>
        {
            var descriptions = document["descriptions"]!.AsObject();
            switch (change)
            {
                case "missing": descriptions.Remove("pret:ITEM_POTION"); break;
                case "unknown":
                    descriptions.Remove("pret:ITEM_POTION");
                    descriptions["pret:ITEM_MISSING"] = "Unknown item.";
                    break;
                case "extra": descriptions["pret:ITEM_MISSING"] = "Unknown item."; break;
                case "empty": descriptions["pret:ITEM_POTION"] = ""; break;
                case "whitespace": descriptions["pret:ITEM_POTION"] = " \t\n"; break;
                case "null": descriptions["pret:ITEM_POTION"] = null; break;
                case "old-schema": document["schemaVersion"] = 1; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => SharedItemCatalog.LoadProject(temp.Path));
    }

    [TestMethod]
    public void TypedReferencesRejectUnknownIdsAndExpandedBusinessObjects()
    {
        var items = SharedItemCatalog.LoadProject(TestAssets.Root);
        var options = items.CreateJsonOptions();
        var item = JsonSerializer.Deserialize<QuestItemDefinition>("\"pret:ITEM_TOWN_MAP\"", options)!;
        Assert.AreSame(items.Definition(item.Id), item);
        Assert.ThrowsExactly<InvalidDataException>(() => JsonSerializer.Deserialize<QuestItemDefinition>("\"pret:ITEM_MISSING\"", options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<QuestItemDefinition>(JsonSerializer.Serialize(item), options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<QuestItemDefinition>("{\"item\":\"pret:ITEM_TOWN_MAP\",\"source\":{}}", options));
    }
}
