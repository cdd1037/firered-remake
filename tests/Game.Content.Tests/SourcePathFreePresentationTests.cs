using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class SourcePathFreePresentationTests
{
    [TestMethod]
    [DataRow(typeof(TownMapAsset), "ui/town-map/town-map.json", "assets")]
    [DataRow(typeof(TownMapAsset), "ui/fly-map/fly-map.json", "assets")]
    [DataRow(typeof(TownMapPositionSource), "ui/town-map/town-map.json", "positions")]
    [DataRow(typeof(FirstSeviiTownMapPosition), "ui/first-sevii-town-map/town-map.json", "positions")]
    [DataRow(typeof(FirstSeviiPresentationAsset), "ui/first-sevii-presentation/presentation.json", "assets")]
    [DataRow(typeof(MailPresentationAsset), "ui/mail-presentation/catalog.json", "assets")]
    [DataRow(typeof(TrainerCardAsset), "ui/trainer-card/trainer-card.json", "assets")]
    [DataRow(typeof(NpcTradeMailAsset), "ui/npc-trade-mail/catalog.json", "assets")]
    [DataRow(typeof(NpcTradeItemIcon), "maps/regions/npc-trade-elyssa/items.json", "icons")]
    [DataRow(typeof(NpcTradeItemIcon), "ui/npc-trade-mail/items.json", "icons")]
    public void GeneratedPresentationRowsRetainTheirFieldsWithoutSourcePathInventories(Type type, string path, string property)
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", path)))!;
        var rows = document[property]!.AsArray();
        Assert.IsTrue(rows.Count > 0);
        Assert.IsNull(type.GetProperty("SourcePaths"));
        var options = ContentJson.CreateOptions();
        foreach (var row in rows)
        {
            var original = row!.AsObject();
            Assert.IsFalse(original.ContainsKey("sourcePaths"));
            var value = JsonSerializer.Deserialize(original.ToJsonString(), type, options);
            Assert.IsNotNull(value);
            var serialized = JsonSerializer.SerializeToNode(value, type, options)!.AsObject();
            foreach (var field in original)
                Assert.IsTrue(JsonNode.DeepEquals(field.Value, serialized[field.Key]),
                    $"{path}: {field.Key} must survive typed loading unchanged.");
            Assert.IsFalse(serialized.ContainsKey("sourcePaths"));
            var withInventory = original.DeepClone().AsObject();
            withInventory["sourcePaths"] = new JsonArray();
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize(withInventory.ToJsonString(), type, options));
        }
    }
}
