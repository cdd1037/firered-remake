using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedConnectedMapProjectionTests
{
    [TestMethod]
    public void EveryPublishedMapProjectsTheSameOrderedMovementCells()
    {
        int maps = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, "content/generated/maps/regions"),
            "*.json", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(path);
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("blocks", out _)) continue;
            var data = PretRuntimeMapData.Decode(bytes, document.RootElement.GetProperty("stableId").GetString()!);
            var map = PretWorldMap.FromRuntime(data, new("", "", "", "", ""), path);
            var expected = map.Cells.Select(cell => new ConnectedFieldCell(cell.BehaviorId, cell.Collision, cell.Elevation)).ToImmutableArray();
            Assert.AreEqual(map.Width * map.Height, expected.Length, path);
            Assert.IsTrue(expected.SequenceEqual(data.CreateConnectedCells()), path);
            Assert.IsTrue(expected.SequenceEqual(map.CreateConnectedCells()), path);
            Assert.IsTrue(expected.All(cell => !cell.Occupied), path);
            maps++;
        }
        Assert.IsTrue(maps > 0, "No published maps were checked.");
    }

    [TestMethod]
    public void CurrentMapsAndTowerLayoutsKeepIdentityDimensionsCellsAndConnections()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        Assert.AreEqual(catalog.SourceMaps.Count, catalog.ConnectedFieldMaps!.Count);
        foreach (var (id, source) in catalog.SourceMaps)
            Check(source, catalog.ConnectedFieldMaps[id]);
        Assert.IsTrue(catalog.TrainerTowerRegion.Layouts.Count > 0);
        foreach (var source in catalog.TrainerTowerRegion.Layouts.Values)
            Check(source, PretTrainerTowerRegion.ConnectedMap(source));

        void Check(PretWorldMap source, ConnectedFieldMap connected)
        {
            Assert.AreEqual(source.Id, connected.Id);
            Assert.AreEqual(source.Width, connected.Width);
            Assert.AreEqual(source.Height, connected.Height);
            Assert.IsTrue(source.Cells.Select(cell => new ConnectedFieldCell(cell.BehaviorId, cell.Collision, cell.Elevation))
                .SequenceEqual(connected.Cells), source.Id.Value);
            var edges = source.Events.GetProperty("connections");
            Assert.AreEqual(edges.ValueKind == JsonValueKind.Null ? 0 : edges.GetArrayLength(), connected.Connections.Length);
            for (int index = 0; index < connected.Connections.Length; index++)
            {
                var original = edges[index];
                var edge = connected.Connections[index];
                var direction = original.GetProperty("direction").GetString() switch
                {
                    "up" => Direction.North, "down" => Direction.South, "left" => Direction.West, "right" => Direction.East,
                    _ => throw new InvalidDataException("Unsupported connection direction.")
                };
                Assert.AreEqual(direction, edge.Direction);
                Assert.AreEqual(original.GetProperty("offset").GetInt32(), edge.Offset);
                if (catalog.SourceMaps.TryGetValue(edge.DestinationMapId, out var destination))
                    Assert.AreEqual(original.GetProperty("map").GetString(), destination.SourceMapSymbol);
            }
        }
    }

    [TestMethod]
    [DataRow("dimensions")]
    [DataRow("blocks")]
    [DataRow("metatile")]
    [DataRow("behavior")]
    [DataRow("layer")]
    [DataRow("identity")]
    public void InvalidCompactMapsAreRejectedBeforeMovementProjection(string damage)
    {
        var path = Path.Combine(TestAssets.Root, "content/generated/maps/regions/opening/pallet.json");
        var document = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        string stableId = document["stableId"]!.GetValue<string>();
        switch (damage)
        {
            case "dimensions": document["width"] = 0; break;
            case "blocks": document["blocks"] = ""; break;
            case "metatile": document["metatiles"]!.AsArray().RemoveAt(0); break;
            case "behavior": document["behaviors"]!.AsArray().RemoveAt(0); break;
            case "layer":
                var metatile = document["metatiles"]![0]!.AsArray();
                metatile[1] = (metatile[1]!.GetValue<uint>() & ~(3u << 29)) | (3u << 29);
                break;
            case "identity": document["stableId"] = "map.invalid-projection"; break;
            default: throw new ArgumentOutOfRangeException(nameof(damage));
        }
        Assert.Throws<InvalidDataException>(() =>
            PretRuntimeMapData.Decode(Encoding.UTF8.GetBytes(document.ToJsonString()), stableId).CreateConnectedCells());
    }
}
