using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class RegionFieldAdmissionTests
{
    private static void WithRegion(string region, Action<PretCurrentWorld, string, JsonObject> check)
    {
        using var output = TestAssets.TemporaryOutput();
        string source = Path.Combine(TestAssets.Root, "content/generated");
        string generated = Path.Combine(output.Path, "content/generated");
        string package = "maps/regions/" + region;
        var files = CurrentWorldTestFixture.RelativeFiles().Concat(["data/constants.json"])
            .Concat(Directory.EnumerateFiles(Path.Combine(source, package), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(source, path))).Distinct();
        foreach (string relative in files)
        {
            string destination = Path.Combine(generated, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(source, relative), destination);
        }
        string contract = Path.Combine(generated, package, "contracts.json");
        check(PretCurrentWorld.Load(output.Path), contract, JsonNode.Parse(File.ReadAllText(contract))!.AsObject());
    }

    [TestMethod]
    public void OriginalSevenIslandCrossMapActorsAndHealingRemainValid()
    {
        WithRegion("seven-island", (world, _, _) =>
        {
            var field = PretSevenIslandRegion.Load(world).Field;
            var clone = field.Clones.Single();
            Assert.AreEqual(-2, clone.Y);
            Assert.AreNotEqual(clone.Map, clone.TargetMap);
            var target = world.Maps[clone.TargetMap].Events.GetProperty("object_events")[clone.TargetLocalId - 1];
            Assert.AreEqual(clone.TargetLocalSymbol, target.GetProperty("local_id").GetString());
            Assert.IsNotNull(world.Maps[field.HealLocation.RespawnMap].GetSourceCell(field.HealLocation.BlackoutX, field.HealLocation.BlackoutY));
        });
        WithRegion("event-island", (world, _, _) => Assert.IsTrue(PretEventIslandRegion.Load(world).Field.MapHeaders.Length > 0));
    }

    [TestMethod]
    [DataRow("MAP_PALLET_TOWN")]
    [DataRow("MAP_SEVEN_ISLAND_POKEMON_CENTER_2F")]
    public void SevenIslandRejectsForeignAndUnselectedSourceMaps(string symbol)
    {
        WithRegion("seven-island", (world, path, original) =>
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(world.Resolve(symbol).Value));
            Action<JsonObject>[] mutations = [
                d => d["itemPickups"]![0]!["map"] = symbol,
                d => d["tanobyKey"]!["map"] = symbol,
                d => d["ferryLanding"]!["map"] = symbol,
                d => d["healLocation"]!["declaration"]!["map"] = symbol,
                d => d["clones"]![0]!["map"] = symbol
            ];
            RejectMutations(path, original, mutations, () => PretSevenIslandRegion.Load(world));
        });
    }

    [TestMethod]
    public void SevenIslandRejectsInvalidCrossMapActorsAndCoordinates()
    {
        WithRegion("seven-island", (world, path, original) =>
        {
            Action<JsonObject>[] mutations = [
                d => d["clones"]![0]!["targetMap"] = "MAP_SEVEN_ISLAND_POKEMON_CENTER_2F",
                d => d["clones"]![0]!["targetMap"] = "MAP_PALLET_TOWN",
                d => d["clones"]![0]!["targetLocalId"] = 999,
                d => d["clones"]![0]!["targetLocalId"] = 2,
                d => d["clones"]![0]!["declaration"]!["target_local_id"] = "LOCALID_MISSING",
                d => d["clones"]![0]!["localId"] = 999,
                d => d["healLocation"]!["declaration"]!["respawn_map"] = "MAP_SEVEN_ISLAND_POKEMON_CENTER_2F",
                d => d["healLocation"]!["declaration"]!["respawn_npc"] = "LOCALID_MISSING",
                d => d["healLocation"]!["blackoutX"] = 999,
                d => d["ferryLanding"]!["x"] = 999,
                d => d["tanobyKey"]!["boulders"]![0]!["solvedX"] = 999,
                d => d["tanobyKey"]!["switches"]![0]!["event"]!["x"] = 999
            ];
            RejectMutations(path, original, mutations, () => PretSevenIslandRegion.Load(world));
        });
    }

    [TestMethod]
    [DataRow("MAP_PALLET_TOWN")]
    [DataRow("MAP_SEVEN_ISLAND_POKEMON_CENTER_2F")]
    public void EventIslandRejectsForeignAndUnselectedMaps(string symbol)
    {
        WithRegion("event-island", (world, path, original) =>
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(world.Resolve(symbol).Value));
            Action<JsonObject>[] mutations = [
                d => d["mapHeaders"]![0]!["map"] = symbol,
                d => d["hiddenItems"]![0]!["map"] = symbol,
                d => d["coordinateTriggers"]![0]!["map"] = symbol,
                d => d["ferryLandings"]![0]!["map"] = symbol,
                d => d["dynamicGraphics"]![0]!["map"] = symbol
            ];
            RejectMutations(path, original, mutations, () => PretEventIslandRegion.Load(world));
        });
    }

    [TestMethod]
    public void EventIslandRejectsInvalidEventAndActorEndpoints()
    {
        WithRegion("event-island", (world, path, original) =>
        {
            Action<JsonObject>[] mutations = [
                d => d["hiddenItems"]![0]!["event"]!["x"] = 999,
                d => d["coordinateTriggers"]![0]!["event"]!["script"] = "MissingScript",
                d => d["ferryLandings"]![0]!["y"] = 999,
                d => d["dynamicGraphics"]![0]!["localId"] = 999,
                d => d["dynamicGraphics"]![0]!["sourceGraphics"] = "OBJ_EVENT_GFX_NURSE"
            ];
            RejectMutations(path, original, mutations, () => PretEventIslandRegion.Load(world));
        });
    }

    private static void RejectMutations(string path, JsonObject original, IEnumerable<Action<JsonObject>> mutations, Action load)
    {
        foreach (var mutate in mutations)
        {
            var changed = original.DeepClone().AsObject();
            mutate(changed);
            File.WriteAllText(path, changed.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(load);
        }
    }
}
