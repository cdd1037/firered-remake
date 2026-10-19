using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class QuestAndGymItemDisplayTextTests
{
    private const string ItemsPath = "content/generated/data/items.json";
    private const string QuestPath = "content/generated/data/items/quest/quest-items.json";
    private const string TownMapPath = "content/generated/ui/town-map/town-map.json";

    [TestMethod]
    public void QuestAndTownMapLoadersResolveEditedNamesByStableItemId()
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        var ids = new[] { QuestItemRules.ParcelId, QuestItemRules.PokeBallId, QuestItemRules.TeachyTvId, QuestItemRules.TownMapId };
        TestAssets.EditJson(output.Path, ItemsPath, data =>
        {
            foreach (var id in ids) SharedItem(data, id.Value)["name"] = "Same display name";
        });
        TestAssets.EditJson(output.Path, QuestPath, data =>
        {
            data["catalog"]!["items"]!.AsArray().Add(QuestItemRules.TeachyTvId.Value);
            data["catalog"]!["items"]!.AsArray().Add(QuestItemRules.TownMapId.Value);
        });
        var original = SharedItemCatalog.LoadProject(TestAssets.Root);
        var shared = SharedItemCatalog.LoadProject(output.Path);
        var quests = PretQuestItems.Load(File.ReadAllText(Path.Combine(output.Path, QuestPath)), shared);
        foreach (var id in ids)
        {
            var item = quests.Items.Single(value => value.Id == id);
            Assert.AreSame(shared.Definition(id), item);
            Assert.AreEqual(original.Definition(id) with { Name = "Same display name" }, item);
        }
        var map = PretTownMap.Load(output.Path, shared);
        Assert.AreSame(shared.Definition(QuestItemRules.TownMapId), map.Item);
        Assert.AreEqual("Same display name", map.Item.Name);
        Assert.AreEqual(QuestItemRules.SourceLimits, quests.Limits);
        Assert.AreEqual(new TownMapPoint(4, 11), map.PositionForMap("map.pallet-town", 0, 0));
        Assert.AreEqual(new TownMapPoint(21, 13), map.Layout.Cancel);
        Assert.IsFalse(map.SwitchAvailable);
        Assert.IsFalse(map.Item.CanToss || map.Item.CanSell || map.Item.CanGive);
    }

    [TestMethod]
    public void CeladonGymLoaderAcceptsEditedCanonicalNameWithoutChangingItsReward()
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        TestAssets.EditJson(output.Path, ItemsPath, data => SharedItem(data, CeladonGymItemRules.Tm19Id.Value)["name"] = "Draining Training Disc");
        var shared = SharedMachineCatalog.LoadProject(output.Path);
        var document = PretCeladonGymItems.LoadProject(output.Path, shared);
        var item = document.Items.Single();
        Assert.AreSame(shared.Items.Definition(CeladonGymItemRules.Tm19Id), item.Metadata.Item);
        Assert.AreEqual("Draining Training Disc", item.Metadata.Item.Name);
        Assert.AreEqual(SharedItemCatalog.LoadProject(TestAssets.Root).Definition(CeladonGymItemRules.Tm19Id)
            with { Name = "Draining Training Disc" }, item.Metadata.Item);
        Assert.IsTrue(item.EffectBytes.IsEmpty);
        Assert.AreEqual(CeladonGymItemRules.Tm19Id, document.Icons.Single().Item);
        Assert.AreEqual(item.Metadata.Item, PretCeladonGymItems.Definition(document, CeladonGymItemRules.Tm19Id)!.Item);
    }

    [TestMethod]
    [DataRow("pret:ITEM_TOWN_MAP")]
    [DataRow("pret:ITEM_TM19")]
    public void ContentLoadersStillRejectBlankCanonicalNames(string id)
    {
        foreach (var name in new[] { "", " \t\n" })
        {
            using var output = TestAssets.TemporaryOutput();
            Prepare(output.Path);
            TestAssets.EditJson(output.Path, ItemsPath, data => SharedItem(data, id)["name"] = name);
            Action load = id == QuestItemRules.TownMapId.Value
                ? () => PretTownMap.Load(output.Path)
                : () => PretCeladonGymItems.LoadProject(output.Path);
            Assert.AreEqual("item_catalog_definition", Assert.ThrowsExactly<InvalidDataException>(load).Message);
        }
    }

    [TestMethod]
    [DataRow("sourceItemId")]
    [DataRow("registrability")]
    [DataRow("pocket")]
    [DataRow("fieldUseFunction")]
    [DataRow("itemReference")]
    [DataRow("gift")]
    [DataRow("layout")]
    [DataRow("description")]
    public void TownMapKeepsItemGiftAndLayoutConstraintsAfterRenaming(string change)
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        TestAssets.EditJson(output.Path, ItemsPath, data =>
        {
            var item = SharedItem(data, QuestItemRules.TownMapId.Value);
            item["name"] = "Region Atlas";
            switch (change)
            {
                case "sourceItemId": item[change] = 999; break;
                case "registrability": item[change] = 0; break;
                case "pocket": item[change] = "POCKET_ITEMS"; break;
                case "fieldUseFunction": item[change] = "NULL"; break;
            }
        });
        TestAssets.EditJson(output.Path, TownMapPath, data =>
        {
            switch (change)
            {
                case "itemReference": data["itemDefinition"] = QuestItemRules.TeachyTvId.Value; break;
                case "gift": data["gift"]!["receivedScene"] = 3; break;
                case "layout": data["layout"]!["columns"] = 21; break;
                case "description": data["itemDescription"] = " \t"; break;
            }
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretTownMap.Load(output.Path));
        Assert.AreEqual(change switch {
            "gift" => "Town Map gift profile differs.",
            "layout" => "Town Map geometry profile differs.",
            _ => "Town Map Bag item profile differs."
        }, error.Message);
    }

    private static void Prepare(string root)
    {
        TestAssets.CopyFiles(root, new[] { ItemsPath, QuestPath, "content/generated/data/machines.json",
            "content/generated/data/items/celadon-gym/manifest.json",
            "content/generated/data/items/celadon-gym/celadon-gym-items.json", "content/generated/sprites/items.png" }
            .Concat(Directory.EnumerateFiles(Path.Combine(TestAssets.Root, "content/generated/ui/town-map"))
                .Select(path => Path.GetRelativePath(TestAssets.Root, path))));
    }


    private static JsonNode SharedItem(JsonNode data, string id) =>
        data["items"]!.AsArray().Single(item => item!["id"]!["value"]!.GetValue<string>() == id)!;
}
