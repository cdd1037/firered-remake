using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class RegionalWarpValidationTests
{
    [TestMethod]
    [DataRow("celadon", "dest_map")]
    [DataRow("celadon", "dest_warp_id")]
    [DataRow("fuchsia", "dest_map")]
    [DataRow("fuchsia", "dest_warp_id")]
    public void GymReturnWarpIsValidatedFromTheActualMap(string city, string field)
    {
        var root = TestAssets.Output();
        var generated = Path.Combine(root, "content/generated");
        var source = Path.Combine(TestAssets.Root, "content/generated");
        try
        {
            foreach (var path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(generated, Path.GetRelativePath(source, path));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(path, destination);
            }
            Action load = city == "celadon" ? () => PretCeladonGymRegion.Load(root) : () => PretFuchsiaGymRegion.Load(root);
            load();
            var map = Path.Combine(generated, city + "-gym-region", city + "-city-gym/runtime-map.json");
            var node = JsonNode.Parse(File.ReadAllText(map))!;
            node["events"]!["warp_events"]![1]![field] = field == "dest_map" ? "MAP_PALLET_TOWN" : "999";
            File.WriteAllText(map, node.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(load);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
