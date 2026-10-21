using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class EarlyLandCompositionTests
{
    private const string EncounterPath = "content/generated/data/encounters/early-land.json";
    private static readonly string[] EarlySymbols =
    [
        "MAP_ROUTE2", "MAP_VIRIDIAN_FOREST", "MAP_ROUTE3", "MAP_ROUTE4", "MAP_ROUTE22",
        "MAP_MT_MOON_1F", "MAP_MT_MOON_B1F", "MAP_MT_MOON_B2F",
        "MAP_ROUTE24", "MAP_ROUTE25", "MAP_ROUTE5", "MAP_ROUTE6"
    ];

    private static TestAssets.TemporaryDirectory CopyInputs()
    {
        var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, CurrentWorldTestFixture.RelativeFiles()
            .Select(p => "content/generated/" + p).Append(EncounterPath));
        return output;
    }

    private static JsonNode Header(JsonNode document, string symbol) =>
        document.AsArray().Single(e => e!["map"]!.GetValue<string>() == symbol)!;

    [TestMethod]
    public void CompleteEarlySelectionPreservesProtectedTablesAndExcludesRoute1()
    {
        var world = PretCurrentWorld.Load(TestAssets.Root);
        var actual = PretEarlyLandEncounters.Load(world);
        CollectionAssert.AreEquivalent(EarlySymbols.Select(world.Resolve).ToArray(), actual.Keys.ToArray());
        Assert.IsFalse(actual.ContainsKey(EarlyLandEncounterRules.Route1Table.Map));
        foreach (var expected in MtMoonLandEncounterTables.All.Concat(BillLandEncounterTables.All)
            .Concat(VermilionLandEncounterTables.All).Append(EarlyLandEncounterRules.Route22Table))
        {
            Assert.AreEqual(expected.RawEncounterRate, actual[expected.Map].RawEncounterRate, expected.Map.Value);
            CollectionAssert.AreEqual(expected.Slots.ToArray(), actual[expected.Map].Slots.ToArray(), expected.Map.Value);
        }
    }

    [TestMethod]
    public void StandaloneRoute3DoesNotAdmitRoute4()
    {
        using var output = CopyInputs();
        var world = PretCurrentWorld.Load(output.Path);
        var all = PretEarlyLandEncounters.Load(world);
        Assert.IsTrue(all.ContainsKey(world.Resolve("MAP_ROUTE4")));
        var route3 = PretRoute3Encounters.Load(world);
        var standalone = PretRoute3Encounters.LoadProject(output.Path);
        Assert.AreEqual(world.Resolve("MAP_ROUTE3"), route3.Map);
        Assert.AreEqual(route3.Map, standalone.Map);
        Assert.AreEqual(all[route3.Map].RawEncounterRate, standalone.RawEncounterRate);
        CollectionAssert.AreEqual(all[route3.Map].Slots.ToArray(), standalone.Slots.ToArray());
    }

    [TestMethod]
    public void FullCampaignAddsRoute1AlongsideAllEarlyLandTables()
    {
        var world = PretCurrentWorld.Load(TestAssets.Root);
        var early = PretEarlyLandEncounters.Load(world);
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var route1 = EarlyLandEncounterRules.Route1Table;
        Assert.AreEqual(route1.RawEncounterRate, catalog.LandEncounters[route1.Map].RawEncounterRate);
        CollectionAssert.AreEqual(route1.Slots.ToArray(), catalog.LandEncounters[route1.Map].Slots.ToArray());
        foreach (var table in early.Values)
        {
            Assert.AreEqual(table.RawEncounterRate, catalog.LandEncounters[table.Map].RawEncounterRate, table.Map.Value);
            CollectionAssert.AreEqual(table.Slots.ToArray(), catalog.LandEncounters[table.Map].Slots.ToArray(), table.Map.Value);
        }
    }

    [TestMethod]
    [DataRow("MAP_ROUTE2")]
    [DataRow("MAP_VIRIDIAN_FOREST")]
    [DataRow("MAP_ROUTE3")]
    public void DataDrivenEarlyTablesAcceptValidRateSpeciesAndLevelEdits(string symbol)
    {
        using var output = CopyInputs();
        TestAssets.EditJson(output.Path, EncounterPath, document =>
        {
            var land = Header(document, symbol)["land_mons"]!;
            land["encounter_rate"] = 37;
            foreach (var mon in land["mons"]!.AsArray())
            {
                mon!["species"] = "SPECIES_PIDGEY";
                mon["min_level"] = 10;
                mon["max_level"] = 25;
            }
        });
        var world = PretCurrentWorld.Load(output.Path);
        var table = PretEarlyLandEncounters.Load(world)[world.Resolve(symbol)];
        Assert.AreEqual(37, table.RawEncounterRate);
        Assert.IsTrue(table.Slots.All(s => s.Species == new SpeciesId("pret:SPECIES_PIDGEY") &&
            s.MinimumLevel == 10 && s.MaximumLevel == 25));
        CollectionAssert.AreEqual(EarlyLandEncounterRules.SlotWeights.ToArray(), table.Slots.Select(s => s.Weight).ToArray());
        if (symbol == "MAP_ROUTE3")
        {
            var standalone = PretRoute3Encounters.LoadProject(output.Path);
            Assert.AreEqual(table.RawEncounterRate, standalone.RawEncounterRate);
            CollectionAssert.AreEqual(table.Slots.ToArray(), standalone.Slots.ToArray());
        }
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("duplicate")]
    [DataRow("empty")]
    [DataRow("unknown")]
    [DataRow("extra-known")]
    [DataRow("extra-header-without-land")]
    [DataRow("unselected-known")]
    [DataRow("missing-land")]
    [DataRow("empty-land")]
    [DataRow("empty-slots")]
    [DataRow("short-slots")]
    [DataRow("extra-slot")]
    [DataRow("missing-species")]
    [DataRow("zero-rate")]
    [DataRow("excessive-rate")]
    [DataRow("zero-level")]
    [DataRow("excessive-level")]
    [DataRow("reversed-levels")]
    public void InvalidHeadersAndLandTablesCannotEnterComposition(string mutation)
    {
        using var output = CopyInputs();
        TestAssets.EditJson(output.Path, EncounterPath, document =>
        {
            var entries = document.AsArray();
            var entry = Header(document, "MAP_ROUTE3");
            var land = entry["land_mons"]!;
            var mons = land["mons"]!.AsArray();
            switch (mutation)
            {
                case "missing": entries.Remove(entry); break;
                case "duplicate": entries.Add(entry.DeepClone()); break;
                case "empty": entries.Clear(); break;
                case "unknown": entry["map"] = "MAP_INVENTED_EARLY_LAND"; break;
                case "extra-known":
                    var extra = entry.DeepClone(); extra["map"] = "MAP_ROUTE1"; entries.Add(extra); break;
                case "extra-header-without-land": entries.Add(new JsonObject { ["map"] = "MAP_ROUTE1" }); break;
                case "unselected-known": entry["map"] = "MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F"; break;
                case "missing-land": entry.AsObject().Remove("land_mons"); break;
                case "empty-land": entry["land_mons"] = new JsonObject(); break;
                case "empty-slots": mons.Clear(); break;
                case "short-slots": mons.RemoveAt(0); break;
                case "extra-slot": mons.Add(mons[0]!.DeepClone()); break;
                case "missing-species": mons[0]!.AsObject().Remove("species"); break;
                case "zero-rate": land["encounter_rate"] = 0; break;
                case "excessive-rate": land["encounter_rate"] = 256; break;
                case "zero-level": mons[0]!["min_level"] = 0; break;
                case "excessive-level": mons[0]!["max_level"] = 101; break;
                case "reversed-levels": mons[0]!["min_level"] = 50; mons[0]!["max_level"] = 1; break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        });
        var world = PretCurrentWorld.Load(output.Path);
        Assert.ThrowsExactly<InvalidDataException>(() => PretEarlyLandEncounters.Load(world));
    }

    [TestMethod]
    [DataRow("MAP_ROUTE22")]
    [DataRow("MAP_MT_MOON_1F")]
    [DataRow("MAP_MT_MOON_B1F")]
    [DataRow("MAP_MT_MOON_B2F")]
    [DataRow("MAP_ROUTE4")]
    [DataRow("MAP_ROUTE24")]
    [DataRow("MAP_ROUTE25")]
    [DataRow("MAP_ROUTE5")]
    [DataRow("MAP_ROUTE6")]
    public void ProtectedTableParityStillRejectsOtherwiseValidEdits(string symbol)
    {
        using var output = CopyInputs();
        TestAssets.EditJson(output.Path, EncounterPath, document => Header(document, symbol)["land_mons"]!["encounter_rate"] = 37);
        Assert.ThrowsExactly<InvalidDataException>(() => PretEarlyLandEncounters.Load(PretCurrentWorld.Load(output.Path)));
    }
}
