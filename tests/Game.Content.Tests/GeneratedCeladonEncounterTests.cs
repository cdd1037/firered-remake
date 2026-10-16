using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedCeladonEncounterTests
{
    private const string Relative = "content/generated/data/encounters/celadon-approach.json";

    private static JsonObject Current() => JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, Relative)))!.AsObject();

    private static PretCeladonApproachEncounters Load(JsonObject document)
    {
        using var output = TestAssets.TemporaryOutput();
        var target = Path.Combine(output.Path, Relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, document.ToJsonString());
        return PretCeladonApproachEncounters.Load(output.Path);
    }

    [TestMethod]
    public void CurrentSourceValuesMatchOriginalTablesWithoutProvenanceFields()
    {
        var document = Current();
        Assert.IsFalse(document.ContainsKey("excludedTables"));
        foreach (var row in document["heldItems"]!.AsArray())
            CollectionAssert.AreEquivalent(new[] { "species", "common", "rare" }, row!.AsObject().Select(p => p.Key).ToArray());
        var result = Load(document);
        Assert.AreEqual(2, result.Tables.Count);
        Assert.AreEqual(5, result.HeldItems.Length);
        foreach (var expected in CeladonApproachLandEncounterTables.All)
        {
            var actual = result.Tables[expected.Map];
            Assert.AreEqual(expected.RawEncounterRate, actual.RawEncounterRate);
            CollectionAssert.AreEqual(expected.Slots.ToArray(), actual.Slots.ToArray());
        }
        Assert.AreEqual("ITEM_RAWST_BERRY", result.HeldItems.Single(r => r.Species == "pret:SPECIES_GROWLITHE").Common);
        Assert.AreEqual("ITEM_NUGGET", result.HeldItems.Single(r => r.Species == "pret:SPECIES_MEOWTH").Rare);
    }

    [TestMethod]
    public void LoaderAcceptsLegalTableEditsWithoutHistoricalMapOrSpeciesCounts()
    {
        var document = Current();
        var table = document["tables"]![0]!.DeepClone();
        table["map"]!["value"] = "map.import-test";
        table["rawEncounterRate"] = 22;
        foreach (var slot in table["slots"]!.AsArray())
        {
            slot!["species"]!["value"] = "pret:SPECIES_PIDGEY";
            slot["minimumLevel"] = 5;
            slot["maximumLevel"] = 30;
        }
        document["tables"] = new JsonArray(table);
        document["heldItems"] = new JsonArray(document["heldItems"]!.AsArray().Single(r => r!["species"]!.GetValue<string>() == "pret:SPECIES_PIDGEY")!.DeepClone());
        var result = Load(document);
        Assert.AreEqual(1, result.Tables.Count);
        Assert.AreEqual(1, result.HeldItems.Length);
        Assert.AreEqual(22, result.Tables[new MapId("map.import-test")].RawEncounterRate);
        // This tests the content loader only. Domain admission remains a separate gate.
    }

    [TestMethod]
    public void LoaderRejectsDuplicateMapsAndIncompleteHeldItemReferences()
    {
        var document = Current();
        document["tables"]!.AsArray().Add(document["tables"]![0]!.DeepClone());
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
        document = Current();
        document["heldItems"]!.AsArray().RemoveAt(0);
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
        document = Current();
        document["heldItems"]!.AsArray().Add(document["heldItems"]![0]!.DeepClone());
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
    }

    [TestMethod]
    public void LoaderRejectsInvalidSlotsAndUnsupportedHeldItemPolicy()
    {
        var document = Current();
        document["tables"]![0]!["slots"]![0]!["minimumLevel"] = 101;
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
        document = Current();
        document["tables"]![0]!["slots"]![0]!["weight"] = 19;
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
        document = Current();
        document["heldItemDrawBound"] = 99;
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
        document = Current();
        document["heldItems"]![0]!["common"] = "ITEM_POTION";
        Assert.ThrowsExactly<InvalidDataException>(() => Load(document));
    }
}
