using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedRegionalEncounterTests
{
    private static string Relative(string family) => family == "safari"
        ? "content/generated/maps/regions/safari/encounters.json"
        : $"content/generated/data/encounters/{family}.json";
    private static JsonObject Current(string family) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, Relative(family))))!.AsObject();
    private static readonly HashSet<MapId> SafariMaps = SafariLandEncounterTables.All.Select(t => t.Map).ToHashSet();

    private static IReadOnlyDictionary<MapId, EarlyLandEncounterTable> Load(string family, JsonObject document)
    {
        if (family == "safari") return PretSafariEncounters.Parse(document.ToJsonString(), SafariMaps).Tables;
        using var output = TestAssets.TemporaryOutput();
        var target = Path.Combine(output.Path, Relative(family));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, document.ToJsonString());
        return family == "rocktunnel" ? PretRockTunnelEncounters.Load(output.Path).Tables : PretRoute11Encounters.Load(output.Path).Tables;
    }

    [TestMethod]
    [DataRow("rocktunnel")]
    [DataRow("route11")]
    [DataRow("safari")]
    public void PreservedResourcesMatchOriginalLandTablesWithoutProvenance(string family)
    {
        var document = Current(family);
        CollectionAssert.AreEquivalent(new[] { "schemaVersion", "tables", "heldItems", "heldItemDrawBound" }, document.Select(p => p.Key).ToArray());
        foreach (var row in document["heldItems"]!.AsArray())
            CollectionAssert.AreEquivalent(new[] { "species", "common", "rare" }, row!.AsObject().Select(p => p.Key).ToArray());
        var actual = Load(family, document);
        var expected = family switch {
            "rocktunnel" => RockTunnelLandEncounterTables.All,
            "route11" => Route11LandEncounterTables.All,
            _ => SafariLandEncounterTables.All
        };
        Assert.AreEqual(expected.Length, actual.Count);
        foreach (var table in expected)
        {
            Assert.AreEqual(table.RawEncounterRate, actual[table.Map].RawEncounterRate);
            CollectionAssert.AreEqual(table.Slots.ToArray(), actual[table.Map].Slots.ToArray());
        }
        if (family == "safari")
            Assert.AreEqual(actual.Count, PretSafariEncounters.Load(PretSafariRegion.Load(PretCurrentWorld.Load(TestAssets.Root))).Tables.Count);
    }

    [TestMethod]
    [DataRow("rocktunnel")]
    [DataRow("route11")]
    [DataRow("safari")]
    public void LegalEditsDoNotRequireHistoricalTableOrSpeciesCounts(string family)
    {
        var document = Current(family);
        var table = document["tables"]![0]!.DeepClone();
        var species = table["slots"]![0]!["species"]!["value"]!.GetValue<string>();
        table["rawEncounterRate"] = 22;
        if (family != "safari") table["map"]!["value"] = "map.import-test";
        foreach (var slot in table["slots"]!.AsArray())
        {
            slot!["species"]!["value"] = species;
            slot["minimumLevel"] = 5;
            slot["maximumLevel"] = 30;
        }
        document["tables"] = new JsonArray(table);
        document["heldItems"] = new JsonArray(document["heldItems"]!.AsArray().Single(r => r!["species"]!.GetValue<string>() == species)!.DeepClone());
        var result = Load(family, document);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(22, result.Single().Value.RawEncounterRate);
        // Domain admission remains separate; this only proves loader independence.
    }

    [TestMethod]
    [DataRow("rocktunnel")]
    [DataRow("route11")]
    [DataRow("safari")]
    public void InvalidDefinitionsStillFailBeforeAdmission(string family)
    {
        Action<JsonObject>[] mutations = [
            d => d["tables"]!.AsArray().Add(d["tables"]![0]!.DeepClone()),
            d => d["heldItems"]!.AsArray().RemoveAt(0),
            d => d["heldItems"]!.AsArray().Add(d["heldItems"]![0]!.DeepClone()),
            d => d["tables"] = new JsonArray(),
            d => d["tables"]![0]!["rawEncounterRate"] = 0,
            d => d["tables"]![0]!["slots"]![0]!["minimumLevel"] = 101,
            d => d["tables"]![0]!["slots"]![0]!["weight"] = 19,
            d => d["heldItemDrawBound"] = 99
        ];
        foreach (var mutate in mutations)
        {
            var document = Current(family);
            mutate(document);
            Assert.ThrowsExactly<InvalidDataException>(() => Load(family, document));
        }
        var legacyDocument = Current(family);
        legacyDocument["excludedTables"] = new JsonArray();
        Assert.ThrowsExactly<ContentValidationException>(() => Load(family, legacyDocument));
    }

    [TestMethod]
    public void SafariRequiresARealRegionMapAndKeepsChanseyHeldItemPolicy()
    {
        var document = Current("safari");
        document["tables"]![0]!["map"]!["value"] = "map.nonexistent";
        Assert.ThrowsExactly<InvalidDataException>(() => Load("safari", document));
        document = Current("safari");
        document["heldItems"]!.AsArray().Single(r => r!["species"]!.GetValue<string>() == "pret:SPECIES_CHANSEY")!["rare"] = "ITEM_NONE";
        Assert.ThrowsExactly<InvalidDataException>(() => Load("safari", document));
    }
}
