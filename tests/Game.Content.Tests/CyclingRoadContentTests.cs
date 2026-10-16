using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CyclingRoadContentTests
{
    private const string Package = "content/generated/maps/regions/cycling-road";

    [TestMethod]
    public void CurrentCatalogConnectsRoute17AndKeepsTheNineteenInitialSingleBattles()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var region = catalog.CyclingRoadRegion;
        var id = PretCyclingRoadRegion.Route17MapId;
        Assert.IsNotNull(catalog.CyclingRoadRegion);
        CollectionAssert.AreEquivalent(new[] { id, new MapId("map.route-16-north-entrance-2f") }, region.NewMaps.Keys.ToArray());
        Assert.AreSame(region.NewMaps[id], catalog.SourceMaps[id]);
        Assert.AreEqual(24, catalog.SourceMaps[id].Width);
        Assert.AreEqual(160, catalog.SourceMaps[id].Height);
        var connected = catalog.ConnectedFieldMaps![id];
        CollectionAssert.AreEqual(new[] {
            new ConnectedFieldConnection(Direction.North, 0, new("map.route-16")),
            new ConnectedFieldConnection(Direction.South, 0, new("map.route-18"))
        }, connected.Connections.ToArray());
        Assert.IsTrue(catalog.ConnectedFieldMaps[new("map.route-16")].Connections.Any(c => c.DestinationMapId == id));
        Assert.IsTrue(catalog.ConnectedFieldMaps[new("map.route-18")].Connections.Any(c => c.DestinationMapId == id));
        Assert.AreEqual(19, catalog.CyclingRoadTrainers.Trainers.Count);
        string[] expected = [
            "Route16_EventScript_Lao", "Route16_EventScript_Koji", "Route16_EventScript_Luke",
            "Route16_EventScript_Ruben", "Route16_EventScript_Hideo", "Route16_EventScript_Camron",
            "Route17_EventScript_Virgil", "Route17_EventScript_Isaiah", "Route17_EventScript_Raul",
            "Route17_EventScript_Nikolas", "Route17_EventScript_Billy", "Route17_EventScript_Jamal",
            "Route17_EventScript_Zeek", "Route17_EventScript_Corey", "Route17_EventScript_William", "Route17_EventScript_Jaxon",
            "Route18_EventScript_Jacob", "Route18_EventScript_Ramiro", "Route18_EventScript_Wilton"];
        CollectionAssert.AreEquivalent(expected, catalog.CyclingRoadTrainers.BattlesByScript.Keys.ToArray());
        foreach (var battle in catalog.CyclingRoadTrainers.BattlesByScript.Values)
        {
            Assert.AreEqual(0, battle.Mode);
            Assert.AreEqual(8u, battle.BattleTypeFlags);
            Assert.IsNotNull(catalog.FindProgram(battle.ScriptEntry));
        }
        Assert.AreEqual(10, catalog.SourceMaps[id].Events.GetProperty("object_events").GetArrayLength());
        var signs = catalog.SourceMaps[id].Events.GetProperty("bg_events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "sign").ToArray();
        Assert.AreEqual(6, signs.Length);
        foreach (var sign in signs)
            Assert.IsNotNull(catalog.FindProgram(sign.GetProperty("script").GetString()!));
    }

    [TestMethod]
    public void UpstairsHasSourceGeometryActorsBinocularsAndReciprocalStairs()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var id = new MapId("map.route-16-north-entrance-2f");
        var map = catalog.SourceMaps[id];
        Assert.AreEqual(13, map.Width);
        Assert.AreEqual(11, map.Height);
        var definition = catalog.MapDefinition(id);
        Assert.AreEqual(3, definition.Actors.Length);
        var aideActor = definition.Actors.Single(a => a.ScriptId.EndsWith("_EventScript_Aide", StringComparison.Ordinal));
        var aide = catalog.PokedexAides.ByActor[(id, aideActor.LocalId)];
        Assert.AreEqual(40, aide.RequiredCaught);
        Assert.AreEqual("pret:ITEM_AMULET_COIN", aide.Item.Id.Value);
        Assert.AreEqual("FLAG_GOT_AMULET_COIN_FROM_OAKS_AIDE", aide.RewardFlag);
        Assert.AreEqual(2, definition.BackgroundTriggers.Length);
        Assert.IsFalse(map.Events.GetProperty("allow_cycling").GetBoolean());
        Assert.IsTrue(catalog.ConnectedFieldMaps![id].Connections.IsEmpty);
        foreach (var script in definition.Actors.Select(a => a.ScriptId).Concat(definition.BackgroundTriggers.Select(b => b.ScriptId)))
        {
            var program = catalog.FindProgram(script);
            Assert.IsNotNull(program);
            Assert.IsFalse(program.Instructions.Any(i => i.Op == Game.Domain.Events.StoryOp.Unsupported));
        }
        var stair = map.FieldWarps.Single();
        Assert.AreEqual(new MapId("map.route-16-north-entrance-1f"), stair.DestinationMapId);
        Assert.AreEqual(4, stair.DestinationWarpId);
        Assert.AreEqual(id, catalog.SourceMaps[stair.DestinationMapId].FieldWarps[4].DestinationMapId);
        Assert.IsTrue(catalog.CyclingRoadSceneInteriors.Contains(id));
        Assert.IsFalse(catalog.CyclingRoadSceneInteriors.Contains(new("map.cerulean-city-bike-shop")));
    }

    [TestMethod]
    public void CurrentCatalogBindsAllFiveHiddenItemsAtTheirOriginalCoordinates()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var pickups = catalog.FieldPickups.Where(p => p.MapSymbol == "MAP_ROUTE17").OrderBy(p => p.SourceFlagId).ToArray();
        var expected = new[] {
            ("ITEM_RARE_CANDY", 18, 83, 1017), ("ITEM_FULL_RESTORE", 10, 53, 1018),
            ("ITEM_PP_UP", 4, 68, 1019), ("ITEM_MAX_REVIVE", 4, 125, 1020), ("ITEM_MAX_ELIXIR", 14, 157, 1021)
        };
        Assert.AreEqual(expected.Length, pickups.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            var actual = pickups[index];
            var (item, x, y, flag) = expected[index];
            Assert.AreEqual(item, actual.ItemSymbol);
            Assert.AreEqual("pret:" + item, actual.Item.Value);
            Assert.AreEqual(x, actual.X);
            Assert.AreEqual(y, actual.Y);
            Assert.AreEqual(3, actual.Elevation);
            Assert.AreEqual(flag, actual.SourceFlagId);
            Assert.AreEqual("FLAG_HIDDEN_ITEM_ROUTE17_" + item[5..], actual.Flag);
            Assert.AreEqual(1, actual.Quantity);
            Assert.IsTrue(actual.Hidden);
            Assert.IsNull(actual.SourceLocalId);
        }
    }

    [TestMethod]
    public void GeneratedLandTablesMatchIndependentFireRedSlotVectors()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var region = catalog.CyclingRoadRegion;
        Assert.AreEqual(2, region.LandEncounters.Length);
        AssertTable("map.route-17", [
            ("SPEAROW", 20), ("DODUO", 24), ("SPEAROW", 22), ("DODUO", 26),
            ("RATICATE", 25), ("RATICATE", 27), ("DODUO", 28), ("RATICATE", 29),
            ("RATTATA", 22), ("FEAROW", 25), ("RATTATA", 22), ("FEAROW", 27)]);
        AssertTable("map.route-18", [
            ("SPEAROW", 20), ("DODUO", 24), ("SPEAROW", 22), ("DODUO", 26),
            ("RATICATE", 25), ("FEAROW", 25), ("DODUO", 28), ("RATICATE", 29),
            ("RATTATA", 22), ("FEAROW", 27), ("RATTATA", 22), ("FEAROW", 29)]);

        void AssertTable(string map, (string Species, byte Level)[] slots)
        {
            var table = region.LandEncounters.Single(t => t.Map.Value == map);
            Assert.AreEqual(21, table.RawEncounterRate);
            Assert.AreEqual(12, table.Slots.Length);
            int[] weights = [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1];
            for (int index = 0; index < slots.Length; index++)
                Assert.AreEqual(new EarlyLandEncounterSlot(index, new("pret:SPECIES_" + slots[index].Species),
                    slots[index].Level, slots[index].Level, weights[index]), table.Slots[index]);
            Assert.AreSame(table, catalog.LandEncounters![new MapId(map)]);
        }
    }

    [TestMethod]
    public void LegalGeneratedTableChangesReachCandidateGenerationWithoutStaticFallback()
    {
        using var output = TestAssets.TemporaryOutput();
        foreach (var relative in new[] { Package + "/route-17.json", Package + "/route-16-north-entrance-2f.json", Package + "/trainers.json",
            Package + "/land-encounters.json", "content/generated/data/trainers.json", "content/generated/data/constants.json" }
            .Concat(CurrentWorldTestFixture.RelativeFiles().Select(path => "content/generated/" + path)).Distinct())
        {
            string target = Path.Combine(output.Path, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(TestAssets.Root, relative), target);
        }
        string encounters = Path.Combine(output.Path, Package, "land-encounters.json");
        var document = JsonNode.Parse(File.ReadAllText(encounters))!.AsArray();
        var altered = document.Single(t => t!["map"]!["value"]!.GetValue<string>() == "map.route-17")!;
        altered["rawEncounterRate"] = 255;
        foreach (var slot in altered["slots"]!.AsArray())
        {
            slot!["species"]!["value"] = "pret:SPECIES_PIDGEY";
            slot["minimumLevel"] = 5;
            slot["maximumLevel"] = 30;
        }
        File.WriteAllText(encounters, document.ToJsonString());
        var region = PretCyclingRoadRegion.Load(PretCurrentWorld.Load(output.Path));
        var table = region.LandEncounters.Single(t => t.Map.Value == "map.route-17");
        Assert.AreEqual(255, table.RawEncounterRate);
        Assert.AreEqual(5, table.Slots[0].MinimumLevel);
        Assert.AreEqual(30, table.Slots[0].MaximumLevel);
        var state = new EarlyLandEncounterState(new(0), 0xd1, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var pokemon = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!.Catalog;
        var definition = new OrdinaryLandEncounterCatalog(new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table },
            region.NewMaps.ToDictionary(p => p.Key, p => (IFieldMap)p.Value), pokemon)[table.Map];
        var result = EarlyLandEncounterRules.Check(state, definition,
            new(table.Map, EncounterCheckTrigger.WalkingStepCompleted, 0xd1, EncounterTileType.Land),
            new(RulesVersion: state.RulesVersion, Movement: EncounterMovementMode.Bicycle), new(0, 19));
        Assert.AreEqual(new EarlyLandEncounterCandidate(table.Map, 0, new("pret:SPECIES_PIDGEY"), 5), result.Candidate);
        Assert.AreEqual(1600, result.RateThreshold);
        Assert.AreEqual((ulong)21, result.GlobalRng.Cursor);
        definition.ValidateState(result.State, table);
    }
}
