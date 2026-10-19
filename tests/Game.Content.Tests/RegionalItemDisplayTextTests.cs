using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class RegionalItemDisplayTextTests
{
    private const string ItemsPath = "content/generated/data/items.json";
    private const string SaffronPath = "content/generated/data/regions/saffron-gym/items.json";
    private const string FuchsiaPath = "content/generated/data/items/fuchsia-gym/fuchsia-gym-items.json";

    [TestMethod]
    public void RegionalItemsUseEditedSharedNamesAndAuthoredDescriptions()
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        (string Id, string Name, string Description)[] edits =
        [
            ("pret:ITEM_TM04", "Mind Training Disc", "Focuses the user to raise its special stats."),
            ("pret:ITEM_MAX_REPEL", "Long-lasting Repellent", "Keeps weaker wild Pokemon away for 250 steps."),
            ("pret:ITEM_TM06", "Toxic Training Disc", "Badly poisons the target, with damage growing each turn.")
        ];
        foreach (var edit in edits)
        {
            TestAssets.EditJson(output.Path, ItemsPath, data => SharedItem(data, edit.Id)["name"] = edit.Name);
            TestAssets.EditJson(output.Path, RegionalPath(edit.Id), data => RegionalItem(data, edit.Id)["metadata"]!["description"] = edit.Description);
        }

        var shared = SharedMachineCatalog.LoadProject(output.Path);
        var saffron = PretSaffronGymItems.LoadProject(output.Path);
        var fuchsia = PretFuchsiaGymItems.LoadProject(output.Path, shared);
        foreach (var edit in edits)
        {
            var rows = edit.Id == "pret:ITEM_TM06" ? fuchsia.Items : saffron.Items;
            var metadata = rows.Single(row => row.Metadata.Item.Id.Value == edit.Id).Metadata;
            Assert.AreSame(shared.Items.Definition(new(edit.Id)), metadata.Item);
            Assert.AreEqual(edit.Name, metadata.Item.Name);
            Assert.AreEqual(edit.Description, metadata.Description);
        }
        var repel = PretSaffronGymItems.Definition(saffron, PretSaffronGymItems.MaxRepelId);
        Assert.AreEqual(EarlyShopUseSupport.DeferredRepel, repel.UseSupport);
        Assert.AreEqual(250, repel.Item.HoldEffectParam);
        Assert.AreEqual("FieldUseFunc_Repel", repel.Item.FieldUseFunction);
        Assert.AreEqual("POCKET_TM_CASE", PretFuchsiaGymItems.Definition(fuchsia).Item.Pocket);
    }

    [TestMethod]
    [DataRow("pret:ITEM_TM04", "name")]
    [DataRow("pret:ITEM_TM04", "description")]
    [DataRow("pret:ITEM_MAX_REPEL", "name")]
    [DataRow("pret:ITEM_MAX_REPEL", "description")]
    [DataRow("pret:ITEM_TM06", "name")]
    [DataRow("pret:ITEM_TM06", "description")]
    public void RegionalItemsRejectMissingDisplayText(string id, string field)
    {
        foreach (var value in new[] { "", " \t\n" })
        {
            // Every input gets a distinct root; immutable catalog caches cannot hide an edit.
            using var output = TestAssets.TemporaryOutput();
            Prepare(output.Path);
            if (field == "name")
                TestAssets.EditJson(output.Path, ItemsPath, data => SharedItem(data, id)["name"] = value);
            else
                TestAssets.EditJson(output.Path, RegionalPath(id), data => RegionalItem(data, id)["metadata"]!["description"] = value);
            var error = Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, id));
            Assert.AreEqual(field == "name" ? "item_catalog_definition" : id == "pret:ITEM_TM06"
                ? "TM06 display text is missing." : "Saffron Gym item display text is missing.", error.Message);
            if (field == "name")
                Assert.AreEqual("item_catalog_definition", Assert.ThrowsExactly<InvalidDataException>(() =>
                    SharedItemCatalog.LoadProject(output.Path)).Message);
        }
    }

    [TestMethod]
    [DataRow("pret:ITEM_TM04", "sourceItemId", "999")]
    [DataRow("pret:ITEM_TM04", "price", "1")]
    [DataRow("pret:ITEM_TM04", "pocket", "POCKET_ITEMS")]
    [DataRow("pret:ITEM_TM04", "fieldUseFunction", "FieldUseFunc_Repel")]
    [DataRow("pret:ITEM_MAX_REPEL", "sourceItemId", "999")]
    [DataRow("pret:ITEM_MAX_REPEL", "price", "1")]
    [DataRow("pret:ITEM_MAX_REPEL", "pocket", "POCKET_KEY_ITEMS")]
    [DataRow("pret:ITEM_MAX_REPEL", "holdEffectParam", "249")]
    [DataRow("pret:ITEM_MAX_REPEL", "fieldUseFunction", "NULL")]
    [DataRow("pret:ITEM_TM06", "sourceItemId", "999")]
    [DataRow("pret:ITEM_TM06", "price", "1")]
    [DataRow("pret:ITEM_TM06", "pocket", "POCKET_ITEMS")]
    [DataRow("pret:ITEM_TM06", "fieldUseFunction", "FieldUseFunc_Repel")]
    public void EditedDisplayTextDoesNotRelaxItemRules(string id, string field, string value)
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        TestAssets.EditJson(output.Path, ItemsPath, data =>
        {
            var item = SharedItem(data, id);
            item["name"] = "Edited display name";
            item[field] = field is "sourceItemId" or "price" or "holdEffectParam"
                ? JsonValue.Create(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
                : JsonValue.Create(value);
        });
        TestAssets.EditJson(output.Path, RegionalPath(id), data => RegionalItem(data, id)["metadata"]!["description"] = "Edited item description.");
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, id));
        Assert.AreEqual(id == "pret:ITEM_TM06" ? "TM06 source item differs." :
            "Saffron Gym TM04/Max Repel source values differ.", error.Message);
    }

    [TestMethod]
    [DataRow("pret:ITEM_TM04", "identity")]
    [DataRow("pret:ITEM_MAX_REPEL", "identity")]
    [DataRow("pret:ITEM_TM06", "identity")]
    [DataRow("pret:ITEM_TM04", "effect")]
    [DataRow("pret:ITEM_MAX_REPEL", "effect")]
    [DataRow("pret:ITEM_TM06", "effect")]
    public void RegionalItemsRetainStableMembershipAndEffectConstraints(string id, string change)
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        TestAssets.EditJson(output.Path, RegionalPath(id), data =>
        {
            var item = RegionalItem(data, id);
            if (change == "identity") item["metadata"]!["item"] = "pret:ITEM_POTION";
            else item["effectBytes"] = new JsonArray(1);
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, id));
        Assert.AreEqual(id == "pret:ITEM_TM06" ? "TM06 source item differs." : change == "identity"
            ? "Saffron Gym item scope differs." : "Saffron Gym item declaration differs.", error.Message);
    }

    private static void Prepare(string root)
    {
        TestAssets.CopyFiles(root, new[]
        {
            ItemsPath, "content/generated/data/machines.json", SaffronPath, FuchsiaPath,
            "content/generated/data/items/fuchsia-gym/manifest.json", "content/generated/sprites/items.png"
        });
    }


    private static JsonNode SharedItem(JsonNode data, string id) =>
        data["items"]!.AsArray().Single(item => item!["id"]!["value"]!.GetValue<string>() == id)!;

    private static JsonNode RegionalItem(JsonNode data, string id) =>
        data["items"]!.AsArray().Single(item => item!["metadata"]!["item"]!.GetValue<string>() == id)!;

    private static string RegionalPath(string id) => id == "pret:ITEM_TM06" ? FuchsiaPath : SaffronPath;

    private static ImmutableArray<PretBillSourceItem> Load(string root, string id) => id == "pret:ITEM_TM06"
        ? PretFuchsiaGymItems.LoadProject(root).Items : PretSaffronGymItems.LoadProject(root).Items;
}
