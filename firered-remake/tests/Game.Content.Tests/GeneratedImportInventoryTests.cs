using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedImportInventoryTests
{
    [TestMethod]
    public void ContentApisDoNotExposeOriginalFileInventories()
    {
        var forbidden = new[] { "Inputs", "LocalInputs", "DependencyInputs", "SourceFiles" };
        foreach (var type in typeof(PretOpeningItems).Assembly.GetExportedTypes())
            foreach (var property in type.GetProperties())
                Assert.IsFalse(forbidden.Contains(property.Name), $"{type.Name}.{property.Name} is import bookkeeping.");
    }

    [TestMethod]
    [DataRow(typeof(PretOpeningItemsDocument), "data/items/opening/opening-items.json")]
    [DataRow(typeof(PretEarlyShopDocument), "data/services/early-shop/early-shop.json")]
    [DataRow(typeof(PretQuestItemsDocument), "data/items/quest/quest-items.json")]
    public void ItemDocumentsLoadWithoutInventoriesAndRejectTheirReintroduction(Type type, string relative)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", relative)))!;
        var options = SharedItemCatalog.LoadProject(TestAssets.Root).CreateJsonOptions();
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        Assert.IsFalse(node.AsObject().ContainsKey("inputs"));
        Assert.IsNotNull(JsonSerializer.Deserialize(node.ToJsonString(), type, options));
        node["inputs"] = new JsonArray();
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize(node.ToJsonString(), type, options));
    }
}
