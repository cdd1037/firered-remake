using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class RegionResolverAdmissionTests
{
    private static TestAssets.TemporaryDirectory CopyInputs(string region)
    {
        var output = TestAssets.TemporaryOutput();
        string directory = "content/generated/maps/regions/" + region;
        TestAssets.CopyFiles(output.Path, CurrentWorldTestFixture.RelativeFiles().Select(p => "content/generated/" + p)
            .Concat(Directory.EnumerateFiles(Path.Combine(TestAssets.Root, directory), "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(TestAssets.Root, p)))
            .Append("content/generated/data/constants.json").Distinct());
        return output;
    }

    [TestMethod]
    [DataRow("MAP_ROUTE3")]
    [DataRow("MAP_SIX_ISLAND_POKEMON_CENTER_2F")]
    public void SixIslandEncounterIdentityCannotGrantRegionalAdmission(string symbol)
    {
        using var output = CopyInputs("six-island");
        var world = PretCurrentWorld.Load(output.Path);
        Assert.IsNotNull(PretSixIslandRegion.Load(world));
        _ = world.Resolve(symbol);
        TestAssets.EditJson(output.Path, "content/generated/maps/regions/six-island/encounters.json",
            d => d[0]!["map"] = symbol);
        Assert.ThrowsExactly<InvalidDataException>(() => PretSixIslandRegion.Load(world));
    }

    [TestMethod]
    [DataRow("pickup-foreign")]
    [DataRow("pickup-unselected")]
    [DataRow("pickup-index")]
    [DataRow("rescue-foreign")]
    [DataRow("rescue-unselected")]
    [DataRow("return-index")]
    public void TowerRowsRequireRealRegionalPickupAndRescueEndpoints(string damage)
    {
        using var output = CopyInputs("pokemon-tower");
        var world = PretCurrentWorld.Load(output.Path);
        var original = PretPokemonTowerRegion.Load(world);
        Assert.AreEqual(PretPokemonTowerRegion.HouseMapId, original.FujiRescue.Warp.DestinationMapId);
        Assert.IsFalse(original.NewMaps.ContainsKey(original.FujiRescue.ExteriorMapId));
        string file = damage.StartsWith("pickup", StringComparison.Ordinal) ? "dependencies.json" : "pokemon-tower.json";
        TestAssets.EditJson(output.Path, "content/generated/maps/regions/pokemon-tower/" + file, d =>
        {
            switch (damage)
            {
                case "pickup-foreign": d["pickups"]![0]!["map"] = "MAP_ROUTE3"; break;
                case "pickup-unselected": d["pickups"]![0]!["map"] = "MAP_PEWTER_CITY_POKEMON_CENTER_2F"; break;
                case "pickup-index": d["pickups"]![0]!["localId"] = 999; break;
                case "rescue-foreign": d["dynamicExit"]!["rescueWarp"]!["map"] = "MAP_ROUTE3"; break;
                case "rescue-unselected": d["dynamicExit"]!["rescueWarp"]!["map"] = "MAP_PEWTER_CITY_POKEMON_CENTER_2F"; break;
                case "return-index": d["dynamicExit"]!["houseExteriorReturn"]!["warpId"] = 999; break;
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => PretPokemonTowerRegion.Load(world));
    }

    [TestMethod]
    public void TowerEncounterCountCannotSubstituteForeignMaps()
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, Directory.EnumerateFiles(Path.Combine(TestAssets.Root, "content/generated"), "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(TestAssets.Root, p)));
        TestAssets.EditJson(output.Path, "content/generated/maps/regions/pokemon-tower/dependencies.json", d =>
            d["wildEncounters"]![0]!["map"] = "MAP_ROUTE3");
        Assert.ThrowsExactly<InvalidDataException>(() => PretCampaignCatalog.LoadCurrent(output.Path));
    }

    [TestMethod]
    [DataRow("MAP_ROCKET_HIDEOUT_ELEVATOR")]
    [DataRow("MAP_CELADON_CITY_DEPARTMENT_STORE_ELEVATOR")]
    [DataRow("MAP_SILPH_CO_ELEVATOR")]
    [DataRow("MAP_TRAINER_TOWER_ELEVATOR")]
    public void FourSupportedElevatorsKeepPairedDynamicWarps(string symbol)
    {
        var world = PretCurrentWorld.Load(TestAssets.Root);
        var map = world.Maps[world.Resolve(symbol)];
        Assert.IsTrue(map.FieldWarps.Any(w => w.DestinationMapId == PretRocketHideoutRegion.DynamicMapId &&
            w.DestinationWarpId == PretRocketHideoutRegion.DynamicWarpId));
    }

    [TestMethod]
    [DataRow("unpaired-map")]
    [DataRow("unpaired-index")]
    [DataRow("ordinary-dynamic")]
    [DataRow("negative")]
    [DataRow("overflow")]
    [DataRow("destination-index")]
    public void InvalidDynamicAndSelectedWarpEndpointsFail(string damage)
    {
        using var output = CopyInputs("rocket-hideout");
        string relative = damage.StartsWith("unpaired", StringComparison.Ordinal)
            ? "content/generated/maps/regions/rocket-hideout/rocket-hideout-elevator.json"
            : "content/generated/maps/regions/opening/pallet.json";
        TestAssets.EditJson(output.Path, relative, d =>
        {
            var warp = d["events"]!["warp_events"]![0]!;
            switch (damage)
            {
                case "unpaired-map": warp["dest_map"] = "MAP_PALLET_TOWN"; break;
                case "unpaired-index": warp["dest_warp_id"] = "0"; break;
                case "ordinary-dynamic": warp["dest_map"] = "MAP_DYNAMIC"; warp["dest_warp_id"] = "WARP_ID_DYNAMIC"; break;
                case "negative": warp["dest_warp_id"] = "-1"; break;
                case "overflow": warp["dest_warp_id"] = "256"; break;
                case "destination-index": warp["dest_warp_id"] = "254"; break;
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => PretCurrentWorld.Load(output.Path));
    }
}
