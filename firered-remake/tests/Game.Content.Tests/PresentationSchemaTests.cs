using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PresentationSchemaTests
{
    [TestMethod]
    [DataRow(typeof(ForestPokemonSprite), "forest-pokemon/battle-art.json", "sprites")]
    [DataRow(typeof(StorageArtIcon), "storage-art/storage-art.json", "icons")]
    [DataRow(typeof(PlayerGenderArtAsset), "player-gender-art/player-gender-art.json", "assets")]
    [DataRow(typeof(PretFishingArtAsset), "city-cycle-art/fishing-art.json", "assets")]
    [DataRow(typeof(OldManArtDocument), "old-man-art/old-man-art.json", "")]
    [DataRow(typeof(EarlyStarterStatusUiDocument), "early-starter-status/early-starter-status-ui.json", "")]
    [DataRow(typeof(ForestStatusUiDocument), "forest-status-ui/forest-status-ui.json", "")]
    [DataRow(typeof(PretMainMenuRules), "main-menu/main-menu.json", "rules")]
    public void PresentationRecordsRoundTripTheirRuntimeFieldsWithoutSourceReceipts(Type type, string path, string property)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", path)))!;
        if (property.Length > 0) node = node[property]!;
        if (node is JsonArray array) node = array[0]!;
        var options = ContentJson.CreateOptions();
        var original = node.ToJsonString();
        var value = JsonSerializer.Deserialize(original, type, options);
        Assert.IsNotNull(value);
        Assert.IsNull(type.GetProperty("SourceRefs"));
        Assert.IsTrue(JsonNode.DeepEquals(node, JsonNode.Parse(JsonSerializer.Serialize(value, type, options))),
            "Runtime asset values must survive typed loading without replacement source records.");
        node["sourceRefs"] = new JsonArray();
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize(node.ToJsonString(), type, options));
    }

    [TestMethod]
    [DataRow("width")]
    [DataRow("frameCount")]
    [DataRow("paletteRgb555")]
    public void TutorialArtStillRejectsInvalidRenderingMetadata(string field)
    {
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/old-man-art/old-man-art.json"));
        var original = PretOldManArt.Load(json);
        Assert.AreEqual(5, original.Assets.Length);
        var changed = JsonNode.Parse(json)!;
        var asset = changed["assets"]![0]!;
        asset[field] = field == "paletteRgb555" ? new JsonArray(32768) : JsonValue.Create(0);
        Assert.ThrowsExactly<InvalidDataException>(() => PretOldManArt.Load(changed.ToJsonString()));
    }
}
