using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Content;
[TestClass]
public abstract class ContentFixture
{
    private protected string root = default !;
    private protected string original = default !;
    private protected string sourceShape = default !;
    private protected global::Game.Content.SourceRef sourceRef = default !;
    [TestInitialize]
    public void SetUp()
    {
        root = Path.Combine(TestAssets.Root, "tests/Game.Content.Tests/Fixtures");
        original = File.ReadAllText(Path.Combine(root, "original-two-rooms.json"));
        sourceShape = File.ReadAllText(Path.Combine(root, "original-pret-map-shape.json"));
        sourceRef = new SourceRef(SourceKind.OriginalFixture, "tests/Game.Content.Tests/Fixtures/original-pret-map-shape.json", "MAP_ENGINEERING_ROOM");
    }

    private protected string Mutate(Action<JsonNode> action)
    {
        var node = JsonNode.Parse(original)!;
        action(node);
        return node.ToJsonString();
    }

    private protected string SourceMutate(Action<JsonNode> action)
    {
        var node = JsonNode.Parse(sourceShape)!;
        action(node);
        return node.ToJsonString();
    }
}
